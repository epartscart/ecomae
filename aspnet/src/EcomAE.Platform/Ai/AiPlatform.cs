using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Data;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Ai;

/// <summary>Shared registry of agent templates + tenant workspaces + agent instances (registry DB).</summary>
public interface IAiPlatform
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
    Task<AiWorkspace> WorkspaceForAsync(string tenantKey, string displayName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiAgentRow>> AgentsAsync(long workspaceId, CancellationToken cancellationToken = default);
    Task<long> ProvisionTenantAsync(string tenantKey, string displayName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiExecutionRow>> RecentExecutionsAsync(long workspaceId, int take, CancellationToken cancellationToken = default);
    Task<AiUsageSummary> UsageSummaryAsync(long workspaceId, CancellationToken cancellationToken = default);
    Task SetAgentStatusAsync(long agentId, string status, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiApprovalRow>> PendingApprovalsAsync(long workspaceId, CancellationToken cancellationToken = default);
    Task DecideApprovalAsync(long approvalId, string decision, string actor, CancellationToken cancellationToken = default);
    /// <summary>Speak layer: post a message (user->agent, agent->user, agent->agent, tenant->platform).</summary>
    Task<long> PostMessageAsync(AiMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiMessage>> MessagesAsync(long workspaceId, int take, CancellationToken cancellationToken = default);
}

public sealed record AiMessage(long Id, long WorkspaceId, string Direction, long? FromAgentId, string FromActor, long? ToAgentId, string Body, long? ReplyToId, string Status, string CorrelationId, long CreatedAt);

public sealed record AiWorkspace(long Id, string TenantKey, string DisplayName, string Plan, string Status, long DailyTokenCap, decimal MonthlyCostCapUsd);
public sealed record AiAgentRow(long Id, long WorkspaceId, string TemplateKey, string Name, string Status, bool IsManager, string? ScheduleCron);
public sealed record AiExecutionRow(long Id, long AgentId, string AgentName, string Trigger, string Status, string Model, long TokensIn, long TokensOut, decimal CostUsd, long StartedAt, long FinishedAt, string? Error, string? OutputSummary);
public sealed record AiApprovalRow(long Id, long AgentId, string AgentName, string Action, string PayloadJson, string Risk, string? Reason, long CreatedAt, string Status);
public sealed record AiUsageSummary(long TodayTokens, long MonthTokens, decimal TodayCostUsd, decimal MonthCostUsd, long ExecutionsToday, long PendingApprovals, long ActiveAgents);

public sealed class AiPlatform : IAiPlatform
{
    private readonly ITenantDbConnectionFactory _connections;

    public AiPlatform(ITenantDbConnectionFactory connections) => _connections = connections;

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var c = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
        await AiPlatformSchema.EnsureAsync(c, cancellationToken).ConfigureAwait(false);
        await SeedTemplatesAsync(c, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AiWorkspace> WorkspaceForAsync(string tenantKey, string displayName, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var c = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
        var id = await ErpDb.LongAsync(c, null, ErpDb.Positional(
            "SELECT `id` FROM `ai_tenant_workspaces` WHERE `tenant_key` = ? LIMIT 1"), cancellationToken, tenantKey).ConfigureAwait(false);
        if (id <= 0)
        {
            await ProvisionTenantAsync(tenantKey, displayName, cancellationToken).ConfigureAwait(false);
            id = await ErpDb.LongAsync(c, null, ErpDb.Positional(
                "SELECT `id` FROM `ai_tenant_workspaces` WHERE `tenant_key` = ? LIMIT 1"), cancellationToken, tenantKey).ConfigureAwait(false);
        }
        return await LoadWorkspaceAsync(c, id, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<AiWorkspace> LoadWorkspaceAsync(DbConnection c, long id, CancellationToken ct)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = ErpDb.Positional(
            "SELECT `id`,`tenant_key`,`display_name`,`plan`,`status`,`daily_token_cap`,`monthly_cost_cap_usd` FROM `ai_tenant_workspaces` WHERE `id` = ? LIMIT 1");
        ErpDb.AddParameters(cmd, id);
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await r.ReadAsync(ct).ConfigureAwait(false))
        {
            return new AiWorkspace(0, "", "", "starter", "missing", 0, 0);
        }
        return new AiWorkspace(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetInt64(5), r.GetDecimal(6));
    }

    /// <summary>Provision a tenant workspace + its AI Manager + all eligible specialist agents.</summary>
    public async Task<long> ProvisionTenantAsync(string tenantKey, string displayName, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        await using var c = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
        var id = await ErpDb.LongAsync(c, null, ErpDb.Positional(
            "SELECT `id` FROM `ai_tenant_workspaces` WHERE `tenant_key` = ? LIMIT 1"), cancellationToken, tenantKey).ConfigureAwait(false);
        if (id <= 0)
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandText = ErpDb.Positional(
                "INSERT INTO `ai_tenant_workspaces` (`tenant_key`,`display_name`,`created_at`,`updated_at`) VALUES (?,?,?,?)");
            ErpDb.AddParameters(cmd, tenantKey, displayName, Now(), Now());
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            id = await ErpDb.LongAsync(c, null, ErpDb.Positional(
                "SELECT `id` FROM `ai_tenant_workspaces` WHERE `tenant_key` = ? LIMIT 1"), cancellationToken, tenantKey).ConfigureAwait(false);
        }

        // Idempotent seeding: safe to re-run after an interrupted first provision.
        string[] specs = tenantKey == "platform"
            ? ["platform_ai_manager","platform_tenant_agent","platform_infra_agent","platform_bi_agent"]
            : ["ai_manager","website_agent","cp_agent","erp_agent","support_agent","sales_agent",
               "purchase_agent","inventory_agent","accounts_agent","finance_agent","marketing_agent",
               "it_monitor_agent","bi_agent"];
        foreach (var key in specs)
        {
            var exists = await ErpDb.LongAsync(c, null, ErpDb.Positional(
                "SELECT `id` FROM `ai_agents` WHERE `workspace_id` = ? AND `template_key` = ? LIMIT 1"), cancellationToken, id, key).ConfigureAwait(false);
            if (exists > 0) continue;
            await using var ins = c.CreateCommand();
            ins.CommandText = ErpDb.Positional(
                "INSERT INTO `ai_agents` (`workspace_id`,`template_key`,`name`,`is_manager`,`created_at`,`updated_at`)"
                + " VALUES (?,?,?,?,?,?)");
            ErpDb.AddParameters(ins, id, key, TemplateName(key), key is "ai_manager" or "platform_ai_manager" ? 1 : 0, Now(), Now());
            await ins.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        return id;
    }

    private static string TemplateName(string key) => key switch
    {
        "ai_manager" => "AI Manager",
        "website_agent" => "Website Management Agent",
        "cp_agent" => "Customer Portal / CP Agent",
        "erp_agent" => "ERP Management Agent",
        "support_agent" => "Customer Support Agent",
        "sales_agent" => "Sales Management Agent",
        "purchase_agent" => "Purchase Management Agent",
        "inventory_agent" => "Inventory Management Agent",
        "accounts_agent" => "Accounts Management Agent",
        "finance_agent" => "Finance Management Agent",
        "marketing_agent" => "Marketing Agent",
        "it_monitor_agent" => "IT & Technical Monitoring Agent",
        "bi_agent" => "Business Intelligence & Reporting Agent",
        "platform_ai_manager" => "Ecomae AI Manager",
        "platform_tenant_agent" => "Tenant Management Agent",
        "platform_infra_agent" => "IT & Infrastructure Agent",
        "platform_bi_agent" => "Reporting & BI Agent",
        _ => key,
    };

    public async Task<IReadOnlyList<AiAgentRow>> AgentsAsync(long workspaceId, CancellationToken cancellationToken = default)
    {
        await using var c = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<AiAgentRow>();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = ErpDb.Positional(
            "SELECT `id`,`workspace_id`,`template_key`,`name`,`status`,`is_manager`,`schedule_cron` FROM `ai_agents` WHERE `workspace_id` = ? ORDER BY `is_manager` DESC, `id` ASC");
        ErpDb.AddParameters(cmd, workspaceId);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new AiAgentRow(r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetInt32(5) == 1, r.IsDBNull(6) ? null : r.GetString(6)));
        }
        return list;
    }

    public async Task<IReadOnlyList<AiExecutionRow>> RecentExecutionsAsync(long workspaceId, int take, CancellationToken cancellationToken = default)
    {
        await using var c = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<AiExecutionRow>();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = ErpDb.Positional(
            "SELECT e.`id`, e.`agent_id`, a.`name`, e.`trigger`, e.`status`, e.`model`, e.`tokens_in`, e.`tokens_out`, e.`cost_usd`, e.`started_at`, e.`finished_at`, e.`error`, e.`output_summary`"
            + " FROM `ai_agent_executions` e LEFT JOIN `ai_agents` a ON a.`id` = e.`agent_id`"
            + " WHERE e.`workspace_id` = ? ORDER BY e.`id` DESC LIMIT " + Math.Clamp(take, 1, 200).ToString(CultureInfo.InvariantCulture));
        ErpDb.AddParameters(cmd, workspaceId);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new AiExecutionRow(r.GetInt64(0), r.GetInt64(1), r.IsDBNull(2) ? "" : r.GetString(2), r.GetString(3), r.GetString(4),
                r.GetString(5), r.GetInt64(6), r.GetInt64(7), r.GetDecimal(8), r.GetInt64(9), r.GetInt64(10),
                r.IsDBNull(11) ? null : r.GetString(11), r.IsDBNull(12) ? null : r.GetString(12)));
        }
        return list;
    }

    public async Task<AiUsageSummary> UsageSummaryAsync(long workspaceId, CancellationToken cancellationToken = default)
    {
        await using var c = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
        var dayStart = UnixDayStart();
        var monthStart = UnixMonthStart();
        var today = await LoadUsageAsync(c, workspaceId, dayStart, cancellationToken).ConfigureAwait(false);
        var month = await LoadUsageAsync(c, workspaceId, monthStart, cancellationToken).ConfigureAwait(false);
        var pending = await ErpDb.LongAsync(c, null, ErpDb.Positional(
            "SELECT COUNT(*) FROM `ai_approvals` WHERE `workspace_id` = ? AND `status` = 'pending'"), cancellationToken, workspaceId).ConfigureAwait(false);
        var active = await ErpDb.LongAsync(c, null, ErpDb.Positional(
            "SELECT COUNT(*) FROM `ai_agents` WHERE `workspace_id` = ? AND `status` = 'active'"), cancellationToken, workspaceId).ConfigureAwait(false);
        return new AiUsageSummary(today.Tokens, month.Tokens, today.Cost, month.Cost, today.Execs, pending, active);
    }

    private static async Task<(long Tokens, decimal Cost, long Execs)> LoadUsageAsync(DbConnection c, long ws, long since, CancellationToken ct)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = ErpDb.Positional(
            "SELECT COALESCE(SUM(`tokens_in`+`tokens_out`),0), COALESCE(SUM(`cost_usd`),0), COUNT(*) FROM `ai_agent_executions` WHERE `workspace_id` = ? AND `started_at` >= ?");
        ErpDb.AddParameters(cmd, ws, since);
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await r.ReadAsync(ct).ConfigureAwait(false)
            ? (r.GetInt64(0), r.GetDecimal(1), r.GetInt64(2))
            : (0, 0, 0);
    }

    public async Task SetAgentStatusAsync(long agentId, string status, string actor, CancellationToken cancellationToken = default)
    {
        await using var c = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = ErpDb.Positional("UPDATE `ai_agents` SET `status` = ?, `updated_at` = ? WHERE `id` = ?");
        ErpDb.AddParameters(cmd, status, Now(), agentId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await AuditAsync(c, 0, agentId, "agent_status_" + status, "agent " + agentId + " -> " + status, actor, "", cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AiApprovalRow>> PendingApprovalsAsync(long workspaceId, CancellationToken cancellationToken = default)
    {
        await using var c = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<AiApprovalRow>();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = ErpDb.Positional(
            "SELECT p.`id`, p.`agent_id`, a.`name`, p.`action`, p.`payload_json`, p.`risk`, p.`reason`, p.`created_at`, p.`status`"
            + " FROM `ai_approvals` p LEFT JOIN `ai_agents` a ON a.`id` = p.`agent_id`"
            + " WHERE p.`workspace_id` = ? AND p.`status` = 'pending' ORDER BY p.`id` DESC LIMIT 200");
        ErpDb.AddParameters(cmd, workspaceId);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new AiApprovalRow(r.GetInt64(0), r.GetInt64(1), r.IsDBNull(2) ? "" : r.GetString(2), r.GetString(3),
                r.GetString(4), r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), r.GetInt64(7), r.GetString(8)));
        }
        return list;
    }

    public async Task DecideApprovalAsync(long approvalId, string decision, string actor, CancellationToken cancellationToken = default)
    {
        await using var c = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
        var status = decision == "approve" ? "approved" : "rejected";
        await using var cmd = c.CreateCommand();
        cmd.CommandText = ErpDb.Positional("UPDATE `ai_approvals` SET `status` = ?, `decided_by` = ?, `decided_at` = ? WHERE `id` = ? AND `status` = 'pending'");
        ErpDb.AddParameters(cmd, status, actor, Now(), approvalId);
        var n = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (n == 1)
        {
            await AuditAsync(c, 0, null, "approval_" + status, "approval " + approvalId, actor, "", cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<long> PostMessageAsync(AiMessage m, CancellationToken cancellationToken = default)
    {
        await using var c = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = ErpDb.Positional(
            "INSERT INTO `ai_agent_messages` (`workspace_id`,`direction`,`from_agent_id`,`from_actor`,`to_agent_id`,`body`,`reply_to_id`,`status`,`correlation_id`,`created_at`)"
            + " VALUES (?,?,?,?,?,?,?,?,?,?)");
        var corr = string.IsNullOrEmpty(m.CorrelationId) ? Guid.NewGuid().ToString("N")[..24] : m.CorrelationId;
        ErpDb.AddParameters(cmd, m.WorkspaceId, m.Direction, m.FromAgentId, m.FromActor, m.ToAgentId, m.Body, m.ReplyToId, m.Status, corr, Now());
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        var id = await ErpDb.LongAsync(c, null, "SELECT LAST_INSERT_ID()", cancellationToken).ConfigureAwait(false);
        await AuditAsync(c, m.WorkspaceId, m.FromAgentId, "message_" + m.Direction, m.Body.Length > 200 ? m.Body[..200] : m.Body, m.FromActor, corr, cancellationToken).ConfigureAwait(false);
        return id;
    }

    public async Task<IReadOnlyList<AiMessage>> MessagesAsync(long workspaceId, int take, CancellationToken cancellationToken = default)
    {
        await using var c = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
        var list = new List<AiMessage>();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = ErpDb.Positional(
            "SELECT `id`,`workspace_id`,`direction`,`from_agent_id`,`from_actor`,`to_agent_id`,`body`,`reply_to_id`,`status`,`correlation_id`,`created_at`"
            + " FROM `ai_agent_messages` WHERE `workspace_id` = ? ORDER BY `id` DESC LIMIT " + Math.Clamp(take, 1, 300).ToString(CultureInfo.InvariantCulture));
        ErpDb.AddParameters(cmd, workspaceId);
        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new AiMessage(r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetInt64(3), r.GetString(4),
                r.IsDBNull(5) ? null : r.GetInt64(5), r.GetString(6), r.IsDBNull(7) ? null : r.GetInt64(7), r.GetString(8), r.GetString(9), r.GetInt64(10)));
        }
        return list;
    }

    internal static async Task AuditAsync(DbConnection c, long workspaceId, long? agentId, string evt, string detail, string actor, string corr, CancellationToken ct)
    {
        await using var cmd = c.CreateCommand();
        cmd.CommandText = ErpDb.Positional(
            "INSERT INTO `ai_audit_logs` (`workspace_id`,`agent_id`,`event`,`detail`,`actor`,`correlation_id`,`created_at`) VALUES (?,?,?,?,?,?,?)");
        ErpDb.AddParameters(cmd, workspaceId, agentId, evt, detail, actor, corr, Now());
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    private static long UnixDayStart() => new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).ToUnixTimeSeconds();
    private static long UnixMonthStart() => new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    /// <summary>Platform-approved agent templates — instructions are advisory/read-only for now.</summary>
    private static async Task SeedTemplatesAsync(DbConnection c, CancellationToken ct)
    {
        var rows = new (string Key, string Name, string Scope, string Dept, string Instructions, string Risk)[]
        {
            ("ai_manager", "AI Manager", "tenant", "management", "You coordinate this tenant's specialist AI agents. Summarize their findings, prioritize issues, recommend actions, and prepare the daily management report. You never execute write actions — you recommend and escalate.", "read"),
            ("website_agent", "Website Management Agent", "tenant", "frontend", "Monitor the tenant storefront: availability, errors, slow pages, broken flows. Recommend fixes; never deploy.", "read"),
            ("cp_agent", "Customer Portal Agent", "tenant", "cp", "Monitor CP health, ticket volume, SLA, customer complaints. Draft responses for approval.", "read"),
            ("erp_agent", "ERP Management Agent", "tenant", "erp", "Monitor ERP sync, vouchers, stock discrepancies, failed jobs. Prepare remediation for approval.", "read"),
            ("support_agent", "Customer Support Agent", "tenant", "support", "Review complaints and inquiries, draft replies and resolutions for human approval.", "write"),
            ("sales_agent", "Sales Management Agent", "tenant", "sales", "Analyze sales, quotes, conversions, abandoned carts. Recommend actions; quote/order changes need approval.", "write"),
            ("purchase_agent", "Purchase Management Agent", "tenant", "purchase", "Analyze purchasing, suppliers, low stock. Prepare purchase recommendations for approval.", "write"),
            ("inventory_agent", "Inventory Management Agent", "tenant", "inventory", "Detect low stock, dead stock, discrepancies. Recommend adjustments for approval.", "write"),
            ("accounts_agent", "Accounts Management Agent", "tenant", "accounts", "Review receivables/payables, anomalies, reconciliations. Flag unusual transactions.", "read"),
            ("finance_agent", "Finance Management Agent", "tenant", "finance", "Summarize financial position, flag anomalies. Never move funds or post entries.", "read"),
            ("marketing_agent", "Marketing Agent", "tenant", "marketing", "SEO/marketing monitoring and recommendations for the tenant. Publishing requires approval.", "write"),
            ("it_monitor_agent", "IT Monitoring Agent", "tenant", "it", "Monitor tenant infrastructure signals and scheduled jobs. Report incidents.", "read"),
            ("bi_agent", "BI & Reporting Agent", "tenant", "reporting", "Produce daily/weekly/monthly tenant business summaries and improvement advice.", "read"),
            // Platform scope
            ("platform_ai_manager", "Ecomae AI Manager", "platform", "management", "Coordinate all platform agents and tenant AI health across the fleet. Report to management daily.", "read"),
            ("platform_tenant_agent", "Tenant Management Agent", "platform", "tenants", "Monitor tenant onboarding, health, lifecycle and provisioning.", "read"),
            ("platform_infra_agent", "IT & Infrastructure Agent", "platform", "it", "Monitor shared infrastructure, deploys, jobs, capacity.", "read"),
            ("platform_bi_agent", "Reporting & BI Agent", "platform", "reporting", "Fleet-level usage, cost and performance reporting.", "read"),
        };
        foreach (var t in rows)
        {
            var exists = await ErpDb.LongAsync(c, null, ErpDb.Positional(
                "SELECT `id` FROM `ai_agent_templates` WHERE `key` = ? LIMIT 1"), ct, t.Key).ConfigureAwait(false);
            if (exists > 0) continue;
            await using var cmd = c.CreateCommand();
            cmd.CommandText = ErpDb.Positional(
                "INSERT INTO `ai_agent_templates` (`key`,`name`,`scope`,`department`,`instructions`,`risk_level`,`created_at`,`updated_at`) VALUES (?,?,?,?,?,?,?,?)");
            ErpDb.AddParameters(cmd, t.Key, t.Name, t.Scope, t.Dept, t.Instructions, t.Risk, Now(), Now());
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }
}
