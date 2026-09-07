using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Serialization;
using EcomAE.Platform.Migration;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_bos_wf_save_rule</c> / ajax <c>bos_wf_save_rule</c> twin.
/// INSERT/UPDATE <c>epc_bos_approval_rules</c>. Does not CREATE tables.
/// Disable is already ASP.NET-live. Decide, raise, seed, and schema ensure stay PHP.
/// </summary>
public interface IErpBosWfSaveRuleWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpBosWfSaveRuleWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpBosWfSaveStep(string Role, string Label);

public sealed record ErpBosWfSaveRuleWriteRequest(
    long Id = 0,
    string? Name = null,
    string? EntityType = null,
    string? Operator = null,
    decimal ThresholdAmount = 0,
    int Priority = 100,
    bool Disable = false,
    IReadOnlyList<ErpBosWfSaveStep>? Steps = null,
    long AdminId = 0);

public sealed class ErpBosWfSaveRuleWriteService : IErpBosWfSaveRuleWriteService
{
    internal static readonly HashSet<string> EntityTypes = new(StringComparer.Ordinal)
    {
        "purchase_order", "sales_order", "purchase_invoice", "sales_invoice",
        "payment_voucher", "receipt_voucher", "gl_journal", "expense", "rfq",
    };

    internal static readonly HashSet<string> Operators = new(StringComparer.Ordinal)
    {
        ">=", ">", "<=", "any",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpBosWfSaveRuleWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpBosWfSaveRuleWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Id < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A rule id must be >= 0.");
        }

        var name = Clip((request.Name ?? string.Empty).Trim(), 160);
        var entity = (request.EntityType ?? string.Empty).Trim();
        if (name.Length == 0 || !EntityTypes.Contains(entity))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Name and document type required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var op = (request.Operator ?? string.Empty).Trim();
        if (!Operators.Contains(op))
        {
            op = ">=";
        }

        var threshold = decimal.Round(request.ThresholdAmount, 2, MidpointRounding.AwayFromZero);
        var priority = request.Priority;
        var stepsJson = JsonSerializer.Serialize(NormalizeSteps(request.Steps));
        var active = request.Disable ? 0 : 1;
        var adminId = request.AdminId < 0 ? 0 : request.AdminId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_bos_approval_rules", "name", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_bos_approval_rules", "entity_type", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_bos_approval_rules", "steps_json", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Approval rule table is not provisioned");
        }

        if (request.Id > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_bos_approval_rules` SET `name` = ?, `entity_type` = ?, `operator` = ?, `threshold_amount` = ?, `steps_json` = ?, `priority` = ?, `active` = ? WHERE `id` = ?"),
                cancellationToken,
                name, entity, op, threshold, stepsJson, priority, active, request.Id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Approval rule saved", request.Id);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_bos_approval_rules` (`name`,`entity_type`,`operator`,`threshold_amount`,`steps_json`,`priority`,`active`,`admin_id`,`time`) VALUES (?,?,?,?,?,?,1,?,?)"),
            cancellationToken,
            name, entity, op, threshold, stepsJson, priority, adminId, now).ConfigureAwait(false);
        var inserted = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Approval rule saved", inserted);
    }

    public static IReadOnlyList<ErpBosWfSaveStep> ParseSteps(
        IReadOnlyList<ErpBosWfSaveStep>? steps,
        string? stepsJson,
        IFormCollection? form)
    {
        var parsed = new List<ErpBosWfSaveStep>();
        if (steps is not null)
        {
            parsed.AddRange(steps);
        }

        if (parsed.Count == 0 && !string.IsNullOrWhiteSpace(stepsJson))
        {
            try
            {
                var fromJson = JsonSerializer.Deserialize<List<ErpBosWfSaveStepDto>>(stepsJson);
                if (fromJson is not null)
                {
                    parsed.AddRange(fromJson.Select(item => new ErpBosWfSaveStep(item.Role ?? "", item.Label ?? "")));
                }
            }
            catch (JsonException)
            {
                // fall through to form / approver_role
            }
        }

        if (form is not null)
        {
            var roles = form["step_role"].Count > 0 ? form["step_role"] : form["stepRole"];
            var labels = form["step_label"].Count > 0 ? form["step_label"] : form["stepLabel"];
            var count = Math.Max(roles.Count, labels.Count);
            for (var i = 0; i < count; i++)
            {
                var role = i < roles.Count ? (roles[i] ?? string.Empty) : string.Empty;
                var label = i < labels.Count ? (labels[i] ?? string.Empty) : string.Empty;
                parsed.Add(new ErpBosWfSaveStep(role, label));
            }

            var singleRole = LiveWriteFormBinder.Text(form, "approver_role", "approverRole");
            var singleLabel = LiveWriteFormBinder.Text(form, "approver_label", "approverLabel");
            if (parsed.Count == 0 && singleRole.Length > 0)
            {
                parsed.Add(new ErpBosWfSaveStep(singleRole, singleLabel));
            }
        }

        return NormalizeSteps(parsed);
    }

    public static IReadOnlyList<ErpBosWfSaveStep> NormalizeSteps(IReadOnlyList<ErpBosWfSaveStep>? steps)
    {
        var kept = new List<ErpBosWfSaveStep>();
        if (steps is not null)
        {
            foreach (var step in steps)
            {
                var role = Clip((step.Role ?? string.Empty).Trim(), 80);
                if (role.Length == 0)
                {
                    continue;
                }

                var label = Clip((step.Label ?? string.Empty).Trim(), 160);
                if (label.Length == 0)
                {
                    label = role + " approval";
                }

                kept.Add(new ErpBosWfSaveStep(role, label));
            }
        }

        if (kept.Count == 0)
        {
            kept.Add(new ErpBosWfSaveStep("Manager", "Approval"));
        }

        return kept;
    }

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

    private sealed class ErpBosWfSaveStepDto
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("label")]
        public string? Label { get; set; }
    }
}
