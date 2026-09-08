using System.Data.Common;
using System.Globalization;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_bos_wf_raise</c> / ajax <c>bos_wf_raise_test</c> twin.
/// INSERT <c>epc_bos_approval_requests</c> + <c>epc_bos_approval_log</c> when
/// a rule matches. Does not CREATE tables. Disable is already ASP.NET-live.
/// Save, decide, seed, and schema ensure stay PHP.
/// </summary>
public interface IErpBosWfRaiseWriteService
{
    Task<ErpSimpleWriteResult> RaiseAsync(
        ErpBosWfRaiseWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpBosWfRaiseWriteRequest(
    string? EntityType = null,
    long EntityId = 0,
    string? EntityRef = null,
    decimal Amount = 0,
    string? Title = null,
    long AdminId = 0,
    string? ActorName = null);

public sealed class ErpBosWfRaiseWriteService : IErpBosWfRaiseWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpBosWfRaiseWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> RaiseAsync(
        ErpBosWfRaiseWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var entityType = (request.EntityType ?? string.Empty).Trim();
        if (entityType.Length == 0)
        {
            entityType = "purchase_order";
        }

        var entityRef = Clip((request.EntityRef ?? string.Empty).Trim(), 96);
        if (entityRef.Length == 0)
        {
            entityRef = "TEST";
        }

        var title = Clip((request.Title ?? string.Empty).Trim(), 200);
        if (title.Length == 0)
        {
            title = entityRef;
        }

        var entityId = request.EntityId;
        if (entityId <= 0)
        {
            entityId = DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 1_000_000;
        }

        var adminId = request.AdminId < 0 ? 0 : request.AdminId;
        var actor = Clip((request.ActorName ?? string.Empty).Trim(), 120);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_bos_approval_rules", "entity_type", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_bos_approval_rules", "operator", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_bos_approval_rules", "threshold_amount", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_bos_approval_rules", "active", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_bos_approval_rules", "name", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_bos_approval_rules", "steps_json", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Approval rule table is not provisioned");
        }

        if (!await ColumnExistsAsync(connection, "epc_bos_approval_requests", "entity_type", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_bos_approval_requests", "entity_id", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_bos_approval_requests", "status", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_bos_approval_log", "request_id", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Approval request table is not provisioned");
        }

        var rule = await MatchRuleAsync(connection, entityType, request.Amount, cancellationToken).ConfigureAwait(false);
        if (rule is null)
        {
            return new ErpSimpleWriteResult(true, "ok", "No rule matched — no approval needed for this amount", 0, 0);
        }

        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT `id` FROM `epc_bos_approval_requests` WHERE `entity_type` = ? AND `entity_id` = ? AND `status` = 'pending' LIMIT 1"),
            cancellationToken,
            entityType,
            entityId).ConfigureAwait(false);
        if (existing > 0)
        {
            return ErpSimpleWriteResult.Ok("Approval request raised (#" + existing.ToString(CultureInfo.InvariantCulture) + ")", existing);
        }

        var stepsJson = EncodeSteps(rule.StepsJson);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_bos_approval_requests` (`rule_id`,`entity_type`,`entity_ref`,`entity_id`,`amount`,`title`,`steps_json`,`current_step`,`status`,`requested_by`,`created_at`) VALUES (?,?,?,?,?,?,?,0,'pending',?,?)"),
            cancellationToken,
            rule.Id, entityType, entityRef, entityId, request.Amount, title, stepsJson, adminId, now).ConfigureAwait(false);
        var reqId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        var comment = "Auto-raised by rule \"" + rule.Name + "\" (" + rule.Operator + " "
            + rule.Threshold.ToString("N2", CultureInfo.InvariantCulture) + ")";
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_bos_approval_log` (`request_id`,`step_index`,`action`,`actor_id`,`actor_name`,`comment`,`time`) VALUES (?,?,?,?,?,?,?)"),
            cancellationToken,
            reqId, 0, "raised", adminId, actor, comment, now).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Approval request raised (#" + reqId.ToString(CultureInfo.InvariantCulture) + ")", reqId);
    }

    public static string EncodeSteps(string? json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var steps = new List<Dictionary<string, string>>();
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var role = item.TryGetProperty("role", out var roleEl) ? roleEl.GetString() ?? "" : "";
                    var label = item.TryGetProperty("label", out var labelEl) ? labelEl.GetString() ?? "" : "";
                    if (role.Length == 0)
                    {
                        continue;
                    }

                    steps.Add(new Dictionary<string, string>
                    {
                        ["role"] = role,
                        ["label"] = label.Length == 0 ? "Approval" : label,
                    });
                }

                if (steps.Count > 0)
                {
                    return JsonSerializer.Serialize(steps);
                }
            }
        }
        catch (JsonException)
        {
            // PHP falls back to a single default step.
        }

        return JsonSerializer.Serialize(new[]
        {
            new Dictionary<string, string> { ["role"] = "Manager", ["label"] = "Approval" },
        });
    }

    private static async Task<MatchedRule?> MatchRuleAsync(
        DbConnection connection,
        string entityType,
        decimal amount,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `id`,`name`,`operator`,`threshold_amount`,`steps_json` FROM `epc_bos_approval_rules` WHERE `active` = 1 AND `entity_type` = ? ORDER BY `priority`, `threshold_amount` DESC");
        ErpDb.AddParameters(command, entityType);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = reader.IsDBNull(0) ? 0L : Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
            var name = reader.IsDBNull(1) ? "" : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? "";
            var op = reader.IsDBNull(2) ? ">=" : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? ">=";
            var threshold = reader.IsDBNull(3) ? 0m : Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture);
            var stepsJson = reader.IsDBNull(4) ? null : Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture);
            var hit = op switch
            {
                "any" => true,
                ">" => amount > threshold,
                "<=" => amount <= threshold,
                _ => amount >= threshold,
            };
            if (hit)
            {
                return new MatchedRule(id, name, op, threshold, stepsJson);
            }
        }

        return null;
    }

    private sealed record MatchedRule(long Id, string Name, string Operator, decimal Threshold, string? StepsJson);

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];

    private static async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
