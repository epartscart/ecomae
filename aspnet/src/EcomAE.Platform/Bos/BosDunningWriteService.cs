using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>collections_dunning</c> <c>update_status</c> / <c>epc_dunning_update_status</c>,
/// <c>record_payment</c> / <c>epc_dunning_record_payment</c>, <c>profile_create</c> / <c>epc_dunning_profile_create</c>,
/// and <c>add_invoice</c> / <c>epc_dunning_add_invoice</c>.
/// Process and schema-ensure stay Classic. This service does not invent a send.
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

    Task<ErpSimpleWriteResult> AddInvoiceAsync(
        string? siteKey,
        long customerId,
        string? customerName,
        string? invoiceRef,
        decimal invoiceAmount,
        decimal? amountDue,
        string? dueDate,
        long profileId,
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

    public async Task<ErpSimpleWriteResult> AddInvoiceAsync(
        string? siteKey,
        long customerId,
        string? customerName,
        string? invoiceRef,
        decimal invoiceAmount,
        decimal? amountDue,
        string? dueDate,
        long profileId,
        CancellationToken cancellationToken = default)
    {
        var key = PhpBosSiteKey(siteKey);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Missing site_key");
        }

        var due = string.IsNullOrWhiteSpace(dueDate)
            ? DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : dueDate.Trim();
        var days = DaysOverdue(due, DateTime.Now);
        var dueAmt = amountDue ?? invoiceAmount;
        object? profile = profileId == 0 ? null : profileId;
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
                    INSERT INTO `epc_dunning_queue`
                        (`site_key`, `customer_id`, `customer_name`, `invoice_ref`, `invoice_amount`,
                         `amount_due`, `due_date`, `days_overdue`, `profile_id`)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
                    """),
                cancellationToken,
                key,
                customerId,
                customerName ?? "",
                invoiceRef ?? "",
                invoiceAmount,
                dueAmt,
                due,
                days,
                profile).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Dunning invoice queued", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Dunning queue table is missing — schema-ensure stays Classic.");
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

    /// <summary>PHP <c>(int)</c> on a token (leading digits; trailing junk ignored).</summary>
    public static long PhpIntval(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        var text = raw.Trim();
        var i = 0;
        if (text[0] is '+' or '-')
        {
            i = 1;
        }

        while (i < text.Length && char.IsDigit(text[i]))
        {
            i++;
        }

        if (i == 0 || (i == 1 && text[0] is '+' or '-'))
        {
            return 0;
        }

        return long.TryParse(text[..i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    /// <summary>PHP <c>(float)</c> on a token (leading numeric / optional exponent; trailing junk ignored).</summary>
    public static decimal PhpFloat(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return 0;
        }

        var text = raw.TrimStart();
        if (text.Length == 0)
        {
            return 0;
        }

        var i = 0;
        if (text[0] is '+' or '-')
        {
            i = 1;
        }

        var sawDigit = false;
        while (i < text.Length && char.IsDigit(text[i]))
        {
            sawDigit = true;
            i++;
        }

        if (i < text.Length && text[i] == '.')
        {
            i++;
            while (i < text.Length && char.IsDigit(text[i]))
            {
                sawDigit = true;
                i++;
            }
        }

        if (!sawDigit)
        {
            return 0;
        }

        if (i < text.Length && text[i] is 'e' or 'E')
        {
            var exp = i + 1;
            if (exp < text.Length && text[exp] is '+' or '-')
            {
                exp++;
            }

            var expDigits = exp;
            while (expDigits < text.Length && char.IsDigit(text[expDigits]))
            {
                expDigits++;
            }

            if (expDigits > exp)
            {
                i = expDigits;
            }
        }

        return decimal.TryParse(text[..i], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    /// <summary>
    /// PHP <c>json_decode((string)($_POST['invoice'] ?? '{}'), true) ?: array()</c>
    /// then <c>due_date ?? date('Y-m-d')</c>, <c>amount_due ?? invoice_amount ?? 0</c>,
    /// and <c>(int)(profile_id ?? 0) ?: null</c>.
    /// </summary>
    public static (
        long CustomerId,
        string CustomerName,
        string InvoiceRef,
        decimal InvoiceAmount,
        decimal AmountDue,
        string? DueDate,
        long ProfileId) ParseInvoice(string? json)
    {
        JsonElement root = default;
        var hasObject = false;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    root = doc.RootElement.Clone();
                    hasObject = true;
                }
            }
            catch (JsonException)
            {
                hasObject = false;
            }
        }

        var invoiceAmount = hasObject ? ReadNumber(root, "invoice_amount") : 0m;
        var amountDue = hasObject && root.TryGetProperty("amount_due", out _)
            ? ReadNumber(root, "amount_due")
            : invoiceAmount;
        string? dueDate = null;
        if (hasObject && root.TryGetProperty("due_date", out var dueProp) && dueProp.ValueKind == JsonValueKind.String)
        {
            dueDate = dueProp.GetString();
        }

        return (
            hasObject ? ReadLong(root, "customer_id") : 0,
            hasObject ? ReadLooseString(root, "customer_name") : "",
            hasObject ? ReadLooseString(root, "invoice_ref") : "",
            invoiceAmount,
            amountDue,
            dueDate,
            hasObject ? ReadLong(root, "profile_id") : 0);
    }

    /// <summary>PHP <c>max(0, (int)((time()-strtotime($dueDate))/86400))</c> for parseable dates.</summary>
    public static int DaysOverdue(string? dueDate, DateTime now)
    {
        if (!DateTime.TryParse(dueDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var due)
            && !DateTime.TryParse(dueDate, out due))
        {
            return 0;
        }

        return Math.Max(0, (int)((now - due).TotalSeconds / 86400));
    }

    private static long ReadLong(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop))
        {
            return 0;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.Number when prop.TryGetInt64(out var n) => n,
            JsonValueKind.String => PhpIntval(prop.GetString()),
            _ => 0,
        };
    }

    private static decimal ReadNumber(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop))
        {
            return 0;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.Number when prop.TryGetDecimal(out var n) => n,
            JsonValueKind.String => PhpFloat(prop.GetString()),
            _ => 0,
        };
    }

    private static string ReadLooseString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop))
        {
            return "";
        }

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString() ?? "",
            JsonValueKind.Number => prop.ToString(),
            _ => "",
        };
    }
}
