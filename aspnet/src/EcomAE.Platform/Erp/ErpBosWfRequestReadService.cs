using System.Data.Common;

namespace EcomAE.Platform.Erp;


// Legacy PHP references twinned by this file (scripts/php_reference_gap_inventory.py):
//   content/shop/finance/epc_bos_workflow.php
/// <summary>
/// Read twin of PHP <c>epc_bos_wf_summary</c>, <c>epc_bos_wf_requests</c> and
/// <c>epc_bos_wf_request_log</c> used by <c>erp_tabs_approvals.php</c> (queue and
/// history panels). Read-only; never creates the PHP-owned tables.
/// </summary>
public interface IErpBosWfRequestReadService
{
    Task<ErpBosWfRequestReadResult> ReadAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ErpBosWfRuleRow>> ReadRulesAsync(CancellationToken cancellationToken = default);
}

public sealed record ErpBosWfRuleRow(
    long Id,
    string Name,
    string EntityType,
    string Operator,
    decimal Threshold,
    int Priority,
    bool Active,
    string Gate,
    int Level,
    string Role);

public sealed record ErpBosWfLogRow(
    long Id,
    int StepIndex,
    string Action,
    string ActorName,
    string Comment,
    long Time);

public sealed record ErpBosWfRequestRow(
    long Id,
    string EntityType,
    string EntityRef,
    decimal Amount,
    string Title,
    int CurrentStep,
    int StepCount,
    string CurrentStepLabel,
    string Status,
    long CreatedAt,
    long DecidedAt,
    IReadOnlyList<ErpBosWfLogRow> Log);

public sealed record ErpBosWfSummary(int Pending, int Approved, int Rejected, int Rules);

public sealed record ErpBosWfRequestReadResult(
    ErpBosWfSummary Summary,
    IReadOnlyList<ErpBosWfRequestRow> Pending,
    IReadOnlyList<ErpBosWfRequestRow> History,
    string Source,
    string Message)
{
    public static ErpBosWfRequestReadResult Empty(string source, string message)
        => new(new(0, 0, 0, 0), [], [], source, message);
}

public sealed class ErpBosWfRequestReadService : IErpBosWfRequestReadService
{
    public const int PendingLimit = 100;
    public const int HistoryLimit = 80;

    public static readonly IReadOnlyDictionary<string, string> EntityTypeLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["purchase_order"] = "Purchase order",
        ["sales_order"] = "Sales order",
        ["purchase_invoice"] = "Purchase invoice / bill",
        ["sales_invoice"] = "Sales invoice",
        ["payment_voucher"] = "Payment voucher",
        ["receipt_voucher"] = "Receipt voucher",
        ["gl_journal"] = "GL journal",
        ["expense"] = "Expense claim",
        ["rfq"] = "RFQ",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpBosWfRequestReadService(IErpWriteConnectionFactory connections) => _connections = connections;

    public static string EntityTypeLabel(string entityType)
        => EntityTypeLabels.TryGetValue(entityType, out var label) ? label : entityType;

    public async Task<ErpBosWfRequestReadResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpBosWfRequestReadResult.Empty("migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, "epc_bos_approval_requests", cancellationToken).ConfigureAwait(false))
            {
                return ErpBosWfRequestReadResult.Empty("not-provisioned", "Approval request table is not provisioned.");
            }

            var hasLog = await TableExistsAsync(connection, "epc_bos_approval_log", cancellationToken).ConfigureAwait(false);
            var hasRules = await TableExistsAsync(connection, "epc_bos_approval_rules", cancellationToken).ConfigureAwait(false);

            int pending = 0, approved = 0, rejected = 0, rules = 0;
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT `status`, COUNT(*) FROM `epc_bos_approval_requests` GROUP BY `status`";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var count = Convert.ToInt32(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture);
                    switch (Text(reader, 0))
                    {
                        case "pending": pending = count; break;
                        case "approved": approved = count; break;
                        case "rejected": rejected = count; break;
                    }
                }
            }

            if (hasRules)
            {
                rules = (int)await ErpDb.LongAsync(
                    connection,
                    null,
                    "SELECT COUNT(*) FROM `epc_bos_approval_rules` WHERE `active` = 1",
                    cancellationToken).ConfigureAwait(false);
            }

            var pendingRows = await ReadRequestsAsync(connection, "pending", PendingLimit, false, cancellationToken).ConfigureAwait(false);
            var historyRows = await ReadRequestsAsync(connection, string.Empty, HistoryLimit, hasLog, cancellationToken).ConfigureAwait(false);
            return new(new(pending, approved, rejected, rules), pendingRows, historyRows, "database", string.Empty);
        }
        catch (DbException exception)
        {
            return ErpBosWfRequestReadResult.Empty("database-error", exception.Message);
        }
    }

    public async Task<IReadOnlyList<ErpBosWfRuleRow>> ReadRulesAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, "epc_bos_approval_rules", cancellationToken).ConfigureAwait(false))
            {
                return [];
            }

            var rows = new List<ErpBosWfRuleRow>();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `id`,IFNULL(`name`,''),IFNULL(`entity_type`,''),IFNULL(`operator`,''),IFNULL(`threshold_amount`,0),IFNULL(`steps_json`,''),IFNULL(`priority`,100),IFNULL(`active`,0) FROM `epc_bos_approval_rules` ORDER BY `entity_type`, `priority`, `threshold_amount`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var steps = Text(reader, 5);
                rows.Add(new(
                    Convert.ToInt64(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture),
                    Text(reader, 1),
                    Text(reader, 2),
                    Text(reader, 3),
                    reader.IsDBNull(4) ? 0m : Convert.ToDecimal(reader.GetValue(4), System.Globalization.CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader.GetValue(6), System.Globalization.CultureInfo.InvariantCulture),
                    Convert.ToInt32(reader.GetValue(7), System.Globalization.CultureInfo.InvariantCulture) == 1,
                    GateOf(steps),
                    LevelOf(steps),
                    RoleOf(steps)));
            }

            return rows;
        }
        catch (DbException)
        {
            return [];
        }
    }

    public static string GateOf(string? stepsJson)
    {
        var gate = FirstString(stepsJson, "gate").ToLowerInvariant();
        if (gate is "save" or "post")
        {
            return gate;
        }

        var label = FirstString(stepsJson, "label");
        if (label.StartsWith("save", StringComparison.OrdinalIgnoreCase))
        {
            return "save";
        }

        return "post";
    }

    public static int LevelOf(string? stepsJson)
    {
        if (!TryFirst(stepsJson, out var step) || !step.TryGetProperty("level", out var level))
        {
            return 0;
        }

        return level.TryGetInt32(out var value) ? Math.Clamp(value, 0, 4) : 0;
    }

    public static string RoleOf(string? stepsJson)
        => FirstString(stepsJson, "role");

    private static async Task<IReadOnlyList<ErpBosWfRequestRow>> ReadRequestsAsync(
        DbConnection connection,
        string status,
        int limit,
        bool includeLog,
        CancellationToken cancellationToken)
    {
        var rows = new List<ErpBosWfRequestRow>();
        await using (var command = connection.CreateCommand())
        {
            var where = status.Length > 0 ? "WHERE `status` = ? " : string.Empty;
            command.CommandText = ErpDb.Positional(
                "SELECT `id`,`entity_type`,`entity_ref`,`amount`,IFNULL(`title`,''),`steps_json`,`current_step`,`status`,`created_at`,`decided_at` " +
                "FROM `epc_bos_approval_requests` " + where +
                "ORDER BY `created_at` DESC LIMIT " + limit.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (status.Length > 0)
            {
                ErpDb.AddParameters(command, status);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var steps = ErpBosWfStepLabels(Text(reader, 5));
                var current = Convert.ToInt32(reader.GetValue(6), System.Globalization.CultureInfo.InvariantCulture);
                rows.Add(new(
                    Convert.ToInt64(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture),
                    Text(reader, 1),
                    Text(reader, 2),
                    reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3), System.Globalization.CultureInfo.InvariantCulture),
                    Text(reader, 4),
                    current,
                    steps.Count,
                    current >= 0 && current < steps.Count ? steps[current] : "Approval",
                    Text(reader, 7),
                    Convert.ToInt64(reader.GetValue(8), System.Globalization.CultureInfo.InvariantCulture),
                    Convert.ToInt64(reader.GetValue(9), System.Globalization.CultureInfo.InvariantCulture),
                    []));
            }
        }

        if (!includeLog || rows.Count == 0)
        {
            return rows;
        }

        var withLog = new List<ErpBosWfRequestRow>(rows.Count);
        foreach (var row in rows)
        {
            withLog.Add(row with { Log = await ReadLogAsync(connection, row.Id, cancellationToken).ConfigureAwait(false) });
        }

        return withLog;
    }

    private static async Task<IReadOnlyList<ErpBosWfLogRow>> ReadLogAsync(
        DbConnection connection,
        long requestId,
        CancellationToken cancellationToken)
    {
        var log = new List<ErpBosWfLogRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `id`,`step_index`,`action`,IFNULL(`actor_name`,''),IFNULL(`comment`,''),`time` FROM `epc_bos_approval_log` WHERE `request_id` = ? ORDER BY `id` ASC");
        ErpDb.AddParameters(command, requestId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            log.Add(new(
                Convert.ToInt64(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture),
                Convert.ToInt32(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture),
                Text(reader, 2),
                Text(reader, 3),
                Text(reader, 4),
                Convert.ToInt64(reader.GetValue(5), System.Globalization.CultureInfo.InvariantCulture)));
        }

        return log;
    }

    /// <summary>Mirrors PHP <c>epc_bos_wf_decode_steps</c>: empty or invalid JSON yields one "Approval" step; a step without a label renders blank.</summary>
    public static IReadOnlyList<string> ErpBosWfStepLabels(string? stepsJson)
    {
        var labels = new List<string>();
        if (!string.IsNullOrWhiteSpace(stepsJson))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(stepsJson);
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var step in doc.RootElement.EnumerateArray())
                    {
                        var label = step.ValueKind == System.Text.Json.JsonValueKind.Object
                            && step.TryGetProperty("label", out var l)
                            && l.ValueKind == System.Text.Json.JsonValueKind.String
                                ? l.GetString() ?? string.Empty
                                : string.Empty;
                        labels.Add(label);
                    }
                }
            }
            catch (System.Text.Json.JsonException)
            {
                labels.Clear();
            }
        }

        if (labels.Count == 0)
        {
            labels.Add("Approval");
        }

        return labels;
    }

    private static string FirstString(string? stepsJson, string property)
    {
        if (!TryFirst(stepsJson, out var step) || !step.TryGetProperty(property, out var value) || value.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            return string.Empty;
        }

        return (value.GetString() ?? string.Empty).Trim();
    }

    private static bool TryFirst(string? stepsJson, out System.Text.Json.JsonElement step)
    {
        step = default;
        if (string.IsNullOrWhiteSpace(stepsJson))
        {
            return false;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(stepsJson);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return false;
            }

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    step = item.Clone();
                    return true;
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }

        return false;
    }

    private static Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
        => ErpDb.TableKnownAsync(connection, table, cancellationToken);

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
}
