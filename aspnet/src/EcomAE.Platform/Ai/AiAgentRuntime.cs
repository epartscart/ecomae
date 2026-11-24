using System.Data.Common;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using EcomAE.Platform.Data;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Ai;

/// <summary>Model-provider abstraction — OpenAI today, others later via config.</summary>
public interface IAiModelClient
{
    bool Configured { get; }
    Task<AiModelResult> CompleteAsync(string model, string system, string user, CancellationToken cancellationToken = default);
}

public sealed record AiModelResult(string Text, long TokensIn, long TokensOut, decimal CostUsd, string Model, string? Error);

/// <summary>OpenAI Chat Completions client; prices resolved from AiModelPricing.</summary>
public sealed class OpenAiModelClient : IAiModelClient
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;

    public OpenAiModelClient(IConfiguration config, IHttpClientFactory http)
    {
        _config = config;
        _http = http;
    }

    public bool Configured => !string.IsNullOrEmpty(Key);
    private string? Key => _config["OPENAI_API_KEY"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

    public async Task<AiModelResult> CompleteAsync(string model, string system, string user, CancellationToken cancellationToken = default)
    {
        if (!Configured)
        {
            return new AiModelResult("", 0, 0, 0, model, "openai_key_missing");
        }
        try
        {
            var http = _http.CreateClient();
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Key);
            req.Content = JsonContent.Create(new
            {
                model,
                messages = new[] { new { role = "system", content = system }, new { role = "user", content = user } },
                temperature = 0.3,
            });
            using var res = await http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            var body = await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                return new AiModelResult("", 0, 0, 0, model, "http_" + (int)res.StatusCode);
            }
            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
            var usage = doc.RootElement.GetProperty("usage");
            var tin = usage.GetProperty("prompt_tokens").GetInt64();
            var tout = usage.GetProperty("completion_tokens").GetInt64();
            return new AiModelResult(text, tin, tout, AiModelPricing.Cost(model, tin, tout), model, null);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new AiModelResult("", 0, 0, 0, model, e.GetType().Name);
        }
    }
}

/// <summary>Per-1M-token USD pricing; configurable via env override later.</summary>
public static class AiModelPricing
{
    public static decimal Cost(string model, long tin, long tout)
    {
        var (pin, pout) = model.StartsWith("gpt-4o-mini", StringComparison.OrdinalIgnoreCase) ? (0.15m, 0.60m)
            : model.StartsWith("gpt-4o", StringComparison.OrdinalIgnoreCase) ? (2.50m, 10.00m)
            : (1.00m, 4.00m); // safe default mid-tier
        return (tin * pin + tout * pout) / 1_000_000m;
    }
}

public interface IAiAgentRuntime
{
    /// <summary>Run one agent once; the tenant is resolved server-side — never from model input.</summary>
    Task<AiExecutionRow> RunAsync(string tenantKey, string? tenantDbName, long agentId, string trigger, string taskText, CancellationToken cancellationToken = default);
}

public sealed class AiAgentRuntime : IAiAgentRuntime
{
    private readonly ITenantDbConnectionFactory _connections;
    private readonly IAiModelClient _model;
    private readonly IConfiguration _config;
    private readonly IAiPlatform _platform;

    public AiAgentRuntime(ITenantDbConnectionFactory connections, IAiModelClient model, IConfiguration config, IAiPlatform platform)
    {
        _connections = connections;
        _model = model;
        _config = config;
        _platform = platform;
    }

    private string DefaultModel => _config["AI_MODEL_DEFAULT"] ?? "gpt-4o-mini";

    public async Task<AiExecutionRow> RunAsync(string tenantKey, string? tenantDbName, long agentId, string trigger, string taskText, CancellationToken cancellationToken = default)
    {
        await using var reg = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
        var wsId = await ErpDb.LongAsync(reg, null, ErpDb.Positional(
            "SELECT `id` FROM `ai_tenant_workspaces` WHERE `tenant_key` = ? LIMIT 1"), cancellationToken, tenantKey).ConfigureAwait(false);
        var corr = Guid.NewGuid().ToString("N")[..24];
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Server-side context: agent + template resolved from trusted registry rows only.
        string agentName = "", instructions = "", risk = "read";
        await using (var cmd = reg.CreateCommand())
        {
            cmd.CommandText = ErpDb.Positional(
                "SELECT a.`name`, IFNULL(t.`instructions`,''), IFNULL(t.`risk_level`,'read'), a.`status`"
                + " FROM `ai_agents` a LEFT JOIN `ai_agent_templates` t ON t.`key` = a.`template_key`"
                + " WHERE a.`id` = ? AND a.`workspace_id` = ? LIMIT 1");
            ErpDb.AddParameters(cmd, agentId, wsId);
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                agentName = r.GetString(0);
                instructions = r.GetString(1);
                risk = r.GetString(2);
                if (r.GetString(3) != "active")
                {
                    return Finish(reg, wsId, agentId, agentName, trigger, "suspended", "", 0, 0, 0, started, "agent_suspended", corr, cancellationToken);
                }
            }
            else
            {
                return Finish(reg, wsId, agentId, agentName, trigger, "failed", "", 0, 0, 0, started, "agent_not_found", corr, cancellationToken);
            }
        }

        // Daily token cap enforcement server-side.
        var dayStart = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).ToUnixTimeSeconds();
        var usedToday = await ErpDb.LongAsync(reg, null, ErpDb.Positional(
            "SELECT COALESCE(SUM(`tokens_in`+`tokens_out`),0) FROM `ai_agent_executions` WHERE `workspace_id` = ? AND `started_at` >= ?"),
            cancellationToken, wsId, dayStart).ConfigureAwait(false);
        var cap = await ErpDb.LongAsync(reg, null, ErpDb.Positional(
            "SELECT `daily_token_cap` FROM `ai_tenant_workspaces` WHERE `id` = ?"), cancellationToken, wsId).ConfigureAwait(false);
        if (cap > 0 && usedToday >= cap)
        {
            return Finish(reg, wsId, agentId, agentName, trigger, "quota_blocked", "", 0, 0, 0, started, "daily_token_cap", corr, cancellationToken);
        }

        // Gather authorized context (read-only business snapshot through tenant DB).
        var context = await GatherTenantContextAsync(tenantDbName ?? tenantKey, cancellationToken).ConfigureAwait(false);
        var system = instructions + "\n\nYou operate inside tenant '" + tenantKey + "'. "
            + "Never request or fabricate another tenant's data. Reply concise, structured: findings, risks, recommended actions (each marked AUTO or NEEDS-APPROVAL). "
            + "If the task is a request for a new module, feature or platform-level improvement that you cannot fulfil inside the tenant, "
            + "append one line starting 'ESCALATE:' with a one-sentence hand-off to the platform AI Manager.";
        var result = await _model.CompleteAsync(DefaultModel, system, "Task: " + taskText + "\n\nTenant snapshot (authorized):\n" + context, cancellationToken).ConfigureAwait(false);

        var status = result.Error is null ? "completed" : "failed";

        // Auto-escalation: agent flagged a platform-level request -> platform workspace inbox (audited).
        var escalate = string.IsNullOrEmpty(result.Text) ? null : result.Text.Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.StartsWith("ESCALATE:", StringComparison.OrdinalIgnoreCase));
        if (escalate is not null && !string.Equals(tenantKey, "platform", StringComparison.OrdinalIgnoreCase))
        {
            var platformWs = await _platform.WorkspaceForAsync("platform", "Ecomae Platform", cancellationToken).ConfigureAwait(false);
            var handoff = "[" + tenantKey + "] " + escalate["ESCALATE:".Length..].Trim();
            await _platform.PostMessageAsync(new AiMessage(0, platformWs.Id, "escalate", agentId, "agent:" + agentName + "@" + tenantKey, null, handoff, null, "open", corr, 0), cancellationToken).ConfigureAwait(false);
            await _platform.PostMessageAsync(new AiMessage(0, wsId, "escalate", agentId, agentName, null, handoff, null, "open", corr, 0), cancellationToken).ConfigureAwait(false);
        }

        var row = Finish(reg, wsId, agentId, agentName, trigger, status, result.Model,
            result.TokensIn, result.TokensOut, result.CostUsd, started, result.Error ?? Trim(result.Text, 3000), corr, cancellationToken);

        // Read-only agents never create approvals; write-capable agents get an approval entry when they propose actions.
        if (result.Error is null && risk == "write" && result.Text.Contains("NEEDS-APPROVAL", StringComparison.OrdinalIgnoreCase))
        {
            await using var cmd = reg.CreateCommand();
            cmd.CommandText = ErpDb.Positional(
                "INSERT INTO `ai_approvals` (`workspace_id`,`agent_id`,`action`,`payload_json`,`payload_hash`,`risk`,`reason`,`created_at`)"
                + " VALUES (?,?,?,?,?,?,?,?)");
            var payload = JsonSerializer.Serialize(new { task = taskText, proposal = result.Text });
            ErpDb.AddParameters(cmd, wsId, agentId, "agent_proposal", payload,
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload))).ToLowerInvariant(),
                "medium", agentName + " proposed actions", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        return row;
    }

    /// <summary>Read-only tenant snapshot via tenant DB — cheap counts only.</summary>
    private async Task<string> GatherTenantContextAsync(string tenantKey, CancellationToken ct)
    {
        try
        {
            var tenant = new EcomAE.Platform.Services.TenantContext("", "/", EcomAE.Platform.Services.TenantSurface.Storefront, EcomAE.Platform.Services.TenantMode.LiveTenant, DatabaseName: tenantKey);
            await using var c = await _connections.OpenForTenantAsync(tenant, ct).ConfigureAwait(false);
            var orders = await Scalar(c, "SELECT COUNT(*) FROM `orders`", ct);
            var users = await Scalar(c, "SELECT COUNT(*) FROM `users`", ct);
            var products = await Scalar(c, "SELECT COUNT(*) FROM `products`", ct);
            return "orders=" + orders + " users=" + users + " products=" + products;
        }
        catch (DbException)
        {
            return "tenant_db_unavailable";
        }
    }

    private static async Task<long> Scalar(DbConnection c, string sql, CancellationToken ct)
    {
        try
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            var v = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return Convert.ToInt64(v ?? 0, CultureInfo.InvariantCulture);
        }
        catch (DbException)
        {
            return -1;
        }
    }

    private static AiExecutionRow Finish(DbConnection reg, long wsId, long agentId, string agentName, string trigger,
        string status, string model, long tin, long tout, decimal cost, long started, string? outcome, string corr, CancellationToken ct)
    {
        var finished = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var isErr = outcome is not null && status is "failed" or "suspended" or "quota_blocked";
        var ins = reg.CreateCommand();
        ins.CommandText = ErpDb.Positional(
            "INSERT INTO `ai_agent_executions` (`agent_id`,`workspace_id`,`trigger`,`status`,`output_summary`,`model`,`tokens_in`,`tokens_out`,`cost_usd`,`correlation_id`,`started_at`,`finished_at`,`error`)"
            + " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)");
        ErpDb.AddParameters(ins, agentId, wsId, trigger, status, isErr ? null : Trim(outcome, 3000), model, tin, tout, cost, corr, started, finished, isErr ? outcome : null);
        ins.ExecuteNonQueryAsync(ct).GetAwaiter().GetResult();
        AiPlatform.AuditAsync(reg, wsId, agentId, "execution_" + status, outcome ?? "", "agent", corr, ct).GetAwaiter().GetResult();
        var newId = ErpDb.LongAsync(reg, null, "SELECT LAST_INSERT_ID()", ct).GetAwaiter().GetResult();
        return new AiExecutionRow(newId, agentId, agentName, trigger, status, model, tin, tout, cost, started, finished, isErr ? outcome : null, isErr ? null : outcome);
    }

    private static string? Trim(string? s, int n) => s is null ? null : s.Length <= n ? s : s[..n];
}
