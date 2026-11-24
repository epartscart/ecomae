using EcomAE.Platform.Data;
using EcomAE.Platform.Erp;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Ai;

/// <summary>Configuration for the 24/7 agent scheduler (Ai:Scheduler section).</summary>
public sealed class AiAgentSchedulerOptions
{
    public const string SectionName = "Ai:Scheduler";

    /// <summary>Set false to pause autonomous runs (manual chat/Run still works).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often the worker checks for due agents, in minutes.</summary>
    public int TickMinutes { get; set; } = 15;
}

/// <summary>
/// Keeps the agent fleet working without humans: runs each agent on its cadence
/// (operations + IT-monitoring hourly, AI Manager daily, others daily)
/// through <see cref="IAiAgentRuntime"/> — all runs are audited, metered and
/// bounded by the workspace daily token cap.
/// </summary>
public sealed class AiAgentScheduler : BackgroundService
{
    /// <summary>Cadence per agent template, in minutes.</summary>
    private static readonly IReadOnlyDictionary<string, int> Cadences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["operations_agent"] = 60,
        ["it_monitor_agent"] = 60,
        ["storefront_agent"] = 240,
        ["ai_manager"] = 24 * 60,
        ["platform_ai_manager"] = 24 * 60,
        ["platform_tenant_agent"] = 24 * 60,
        ["platform_infra_agent"] = 24 * 60,
        ["platform_bi_agent"] = 24 * 60,
    };
    private const int DefaultCadenceMinutes = 24 * 60;

    private static readonly IReadOnlyDictionary<string, string> Tasks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["operations_agent"] = "Order lifecycle sweep: check payments with finance, supplier fulfilment with purchase, inbound material, order confirmation, shipment and tracking. Flag every stuck order with its stage and responsible agent.",
        ["it_monitor_agent"] = "Health sweep: scheduled jobs, infrastructure signals, error patterns. Report any incident immediately as URGENT with the responsible party.",
        ["ai_manager"] = "Daily tenant report: summarize sales/operations/risks across all department agents, list improvement advice and anything needing approval.",
        ["platform_ai_manager"] = "Daily fleet audit: tenant escalations waiting in the platform inbox, cross-tenant health, cost/usage anomalies, prioritized actions.",
        ["platform_tenant_agent"] = "Daily tenant onboarding check: new or idle workspaces, provisioning gaps, tenants needing outreach.",
        ["platform_infra_agent"] = "Daily infrastructure check: deploy health, service and job failures, capacity risks. Report incidents as URGENT.",
        ["platform_bi_agent"] = "Daily BI digest: platform KPIs, tenant AI usage/cost trend, anomalies worth surfacing.",
        ["storefront_agent"] = "Visitor engagement sweep: review the warm visitor sessions attached, identify leads to convert, draft a short personalized outreach message per lead (NEEDS-APPROVAL), and note which sources/pages bring engaged visitors.",
    };

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<AiAgentSchedulerOptions> _options;
    private readonly ILogger<AiAgentScheduler> _logger;

    public AiAgentScheduler(IServiceScopeFactory scopes, IOptions<AiAgentSchedulerOptions> options, ILogger<AiAgentScheduler> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled)
        {
            _logger.LogInformation("AI agent scheduler disabled ({Section}:Enabled=false).", AiAgentSchedulerOptions.SectionName);
            return;
        }
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(5, _options.Value.TickMinutes)));
        await RunDueAsync(stoppingToken).ConfigureAwait(false);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await RunDueAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunDueAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var connections = scope.ServiceProvider.GetRequiredService<ITenantDbConnectionFactory>();
            var runtime = scope.ServiceProvider.GetRequiredService<IAiAgentRuntime>();
            var ai = scope.ServiceProvider.GetRequiredService<IAiPlatform>();

            await using var reg = await connections.OpenRegistryAsync(ct).ConfigureAwait(false);
            // Join the tenant registry for the real tenant DB name and to drop workspaces
            // whose tenant was deleted in Super CP (they'd keep spending tokens otherwise).
            var workspaces = new List<(long Id, string TenantKey, string? DbName)>();
            await using (var cmd = reg.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT w.`id`, w.`tenant_key`, t.`db_name` FROM `ai_tenant_workspaces` w"
                    + " LEFT JOIN `epc_portal_tenants` t ON t.`site_key` = w.`tenant_key`"
                    + " WHERE w.`tenant_key` = 'platform' OR (t.`id` IS NOT NULL AND t.`is_active` = 1)";
                await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await r.ReadAsync(ct).ConfigureAwait(false))
                {
                    workspaces.Add((r.GetInt64(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2)));
                }
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var (wsId, tenantKey, dbName) in workspaces)
            {
                // Top-up provisioning: adds newly introduced agent templates to existing workspaces
                // so scheduled sweeps cover every agent without anyone visiting the AI page.
                await ai.WorkspaceForAsync(tenantKey, tenantKey, ct).ConfigureAwait(false);

                var agents = new List<(long Id, string TemplateKey, string Status)>();
                await using (var cmd = reg.CreateCommand())
                {
                    cmd.CommandText = ErpDb.Positional("SELECT `id`, `template_key`, `status` FROM `ai_agents` WHERE `workspace_id` = ?");
                    ErpDb.AddParameters(cmd, wsId);
                    await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
                    while (await r.ReadAsync(ct).ConfigureAwait(false))
                    {
                        agents.Add((r.GetInt64(0), r.GetString(1), r.GetString(2)));
                    }
                }

                foreach (var (agentId, templateKey, status) in agents)
                {
                    if (status != "active" || ct.IsCancellationRequested)
                    {
                        continue;
                    }
                    var cadence = Cadences.TryGetValue(templateKey, out var c) ? c : DefaultCadenceMinutes;
                    var last = await ErpDb.LongAsync(reg, null, ErpDb.Positional(
                        "SELECT COALESCE(MAX(`started_at`),0) FROM `ai_agent_executions` WHERE `agent_id` = ? AND `trigger` = 'schedule'"),
                        ct, agentId).ConfigureAwait(false);
                    if (now - last < cadence * 60L)
                    {
                        continue;
                    }
                    var task = Tasks.TryGetValue(templateKey, out var t) ? t : "Scheduled run: health check and improvement advice for your area.";
                    try
                    {
                        await runtime.RunAsync(tenantKey, dbName, agentId, "schedule", task, ct).ConfigureAwait(false);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        _logger.LogWarning(e, "AI scheduler run failed for agent {AgentId} in workspace {Tenant}", agentId, tenantKey);
                    }
                }
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning(e, "AI agent scheduler tick failed.");
        }
    }
}
