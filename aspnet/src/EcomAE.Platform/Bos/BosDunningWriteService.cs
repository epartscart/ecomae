using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>collections_dunning</c> <c>update_status</c> / <c>epc_dunning_update_status</c>,
/// <c>record_payment</c> / <c>epc_dunning_record_payment</c>, and <c>profile_create</c> / <c>epc_dunning_profile_create</c>.
/// Process, add-invoice, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER. The CP twin writes the tenant shop DB; this write uses the platform operator PDO.
/// </summary>
public interface IBosDunningWriteService
{
    Task<ErpSimpleWriteResult> UpdateStatusAsync(
        long queueId,
        string? status,
        string? notes,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> RecordPaymentAsync(
        long queueId,
        decimal amount,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> CreateProfileAsync(
        string? siteKey,
        string? name,
        string? stepsJson,
        CancellationToken cancellationToken = default);
}

public sealed class BosDunningWriteService : IBosDunningWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public BosDunningWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> UpdateStatusAsync(
        long queueId,
        string? status,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var next = status ?? string.Empty;
        var noteText = notes ?? string.Empty;
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_dunning_queue` SET `status` = ?, `notes` = ? WHERE `id` = ?
                    """),
                cancellationToken, next, noteText, queueId).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_dunning_log` (`queue_id`, `action_type`, `details`, `performed_by`) VALUES (?, 'note', ?, ?)
                    """),
                cancellationToken, queueId, "Status → " + next + ": " + noteText, 0).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Dunning status updated", queueId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Dunning tables are missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> RecordPaymentAsync(
        long queueId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var due = await ErpDb.DecimalAsync(
                connection, null,
                ErpDb.Positional("SELECT `amount_due` FROM `epc_dunning_queue` WHERE `id` = ?"),
                cancellationToken, queueId).ConfigureAwait(false);
            var remaining = due - amount;
            if (remaining < 0)
            {
                remaining = 0;
            }

            var next = remaining <= 0 ? "paid" : "partial";
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_dunning_queue` SET `amount_due` = ?, `status` = ? WHERE `id` = ?
                    """),
                cancellationToken, remaining, next, queueId).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_dunning_log` (`queue_id`, `action_type`, `details`, `performed_by`) VALUES (?, 'payment', ?, ?)
                    """),
                cancellationToken, queueId, "Payment received: " + amount.ToString("N2", CultureInfo.InvariantCulture), 0).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Dunning payment recorded", queueId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Dunning tables are missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> CreateProfileAsync(
        string? siteKey,
        string? name,
        string? stepsJson,
        CancellationToken cancellationToken = default)
    {
        var key = PhpBosSiteKey(siteKey);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Missing site_key");
        }

        var profileName = name ?? "Default";
        var steps = SerializeSteps(ParseSteps(stepsJson));
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `epc_dunning_profiles` (`site_key`, `name`, `steps`) VALUES (?, ?, ?)"),
                cancellationToken, key, profileName, steps).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Dunning profile created", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Dunning profiles table is missing — schema-ensure stays Classic.");
        }
    }

    /// <summary>PHP ajax <c>preg_replace('/[^a-z0-9_]/', '', strtolower(...))</c>.</summary>
    public static string PhpBosSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? "").ToLowerInvariant(), "");

    public static IReadOnlyList<(int Day, string Action, string Template, string Subject)> DefaultSteps { get; } =
    [
        (1, "email", "friendly_reminder", "Friendly Payment Reminder"),
        (7, "email", "first_notice", "Payment Notice — Invoice Overdue"),
        (14, "email", "second_notice", "Second Payment Notice — Urgent"),
        (21, "call", "phone_followup", "Phone Follow-Up Required"),
        (30, "letter", "formal_demand", "Formal Demand for Payment"),
        (45, "escalation", "escalation", "Account Escalated to Collections"),
        (60, "letter", "final_notice", "Final Notice Before Legal Action"),
    ];

    public static IReadOnlyList<(int Day, string Action, string Template, string Subject)> ParseSteps(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return DefaultSteps;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            {
                return DefaultSteps;
            }

            var list = new List<(int Day, string Action, string Template, string Subject)>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                list.Add((
                    ReadInt(el, "day"),
                    ReadString(el, "action"),
                    ReadString(el, "template"),
                    ReadString(el, "subject")));
            }

            return list.Count == 0 ? DefaultSteps : list;
        }
        catch (JsonException)
        {
            return DefaultSteps;
        }
    }

    public static string SerializeSteps(IReadOnlyList<(int Day, string Action, string Template, string Subject)> steps)
    {
        var payload = new List<Dictionary<string, object?>>(steps.Count);
        foreach (var step in steps)
        {
            payload.Add(new Dictionary<string, object?>
            {
                ["day"] = step.Day,
                ["action"] = step.Action,
                ["template"] = step.Template,
                ["subject"] = step.Subject,
            });
        }

        return JsonSerializer.Serialize(payload);
    }

    private static int ReadInt(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop))
        {
            return 0;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.Number when prop.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
            _ => 0,
        };
    }

    private static string ReadString(JsonElement el, string name)
        => el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? ""
            : "";
}
