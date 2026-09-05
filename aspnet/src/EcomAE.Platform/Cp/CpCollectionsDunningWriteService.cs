using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_dunning_update_status</c> / <c>epc_dunning_record_payment</c> /
/// <c>epc_dunning_profile_create</c> / <c>epc_dunning_add_invoice</c> /
/// <c>epc_dunning_process</c> twins. SMTP/letter delivery is not in the PHP file
/// (process only logs the step action). Schema-ensure stays PHP.
/// </summary>
public interface ICpCollectionsDunningWriteService
{
    Task<ErpSimpleWriteResult> UpdateStatusAsync(
        long queueId,
        string? status,
        string? notes,
        int userId,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> RecordPaymentAsync(
        long queueId,
        decimal amount,
        int userId,
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

    Task<ErpSimpleWriteResult> ProcessAsync(
        string? siteKey,
        CancellationToken cancellationToken = default);
}

public sealed class CpCollectionsDunningWriteService : ICpCollectionsDunningWriteService
{
    public static readonly string[] AllowedStatuses =
        ["open", "in_progress", "promised", "partial", "paid", "written_off", "disputed"];

    private readonly IErpWriteConnectionFactory _connections;

    public CpCollectionsDunningWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> UpdateStatusAsync(
        long queueId,
        string? status,
        string? notes,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (queueId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A dunning queue id is required.");
        }

        var next = Normalize(status);
        if (!AllowedStatuses.Contains(next, StringComparer.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid dunning queue status");
        }

        if (userId < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "performed_by cannot be negative.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var noteText = Clip(notes, 4000);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_dunning_queue` WHERE `id` = ?"),
            cancellationToken,
            queueId);
        if (exists <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Dunning queue item not found");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_dunning_queue` SET `status` = ?, `notes` = ? WHERE `id` = ?"),
            cancellationToken,
            next, noteText, queueId);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_dunning_log` (`queue_id`, `action_type`, `details`, `performed_by`) VALUES (?, 'note', ?, ?)"),
            cancellationToken,
            queueId, "Status → " + next + ": " + noteText, userId);
        return ErpSimpleWriteResult.Ok("Dunning queue status set to " + next + ".", queueId);
    }

    public async Task<ErpSimpleWriteResult> RecordPaymentAsync(
        long queueId,
        decimal amount,
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (queueId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A dunning queue id is required.");
        }

        if (amount <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Payment amount must be greater than zero.");
        }

        if (userId < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "performed_by cannot be negative.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var dueRaw = await ErpDb.ScalarAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `amount_due` FROM `epc_dunning_queue` WHERE `id` = ?"),
            cancellationToken,
            queueId);
        if (dueRaw is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Dunning queue item not found");
        }

        var due = Convert.ToDecimal(dueRaw, CultureInfo.InvariantCulture);
        var remaining = due - amount;
        if (remaining < 0)
        {
            remaining = 0;
        }

        var next = remaining <= 0 ? "paid" : "partial";
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_dunning_queue` SET `amount_due` = ?, `status` = ? WHERE `id` = ?"),
            cancellationToken,
            remaining, next, queueId);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_dunning_log` (`queue_id`, `action_type`, `details`, `performed_by`) VALUES (?, 'payment', ?, ?)"),
            cancellationToken,
            queueId, "Payment received: " + amount.ToString("N2", CultureInfo.InvariantCulture), userId);
        return ErpSimpleWriteResult.Ok(
            "Payment recorded. Remaining " + remaining.ToString("N2", CultureInfo.InvariantCulture) + " (" + next + ").",
            queueId);
    }

    public async Task<ErpSimpleWriteResult> CreateProfileAsync(
        string? siteKey,
        string? name,
        string? stepsJson,
        CancellationToken cancellationToken = default)
    {
        var key = Clip(siteKey, 64);
        var profileName = Clip(name, 128);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A site key is required.");
        }

        if (profileName.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A dunning profile name is required.");
        }

        var steps = ParseSteps(stepsJson);
        if (steps.Count == 0)
        {
            steps = DefaultSteps;
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_dunning_profiles` (`site_key`, `name`, `steps`) VALUES (?, ?, ?)"),
            cancellationToken,
            key, profileName, SerializeSteps(steps));
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Dunning profile created.", id);
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
        var key = Clip(siteKey, 64);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A site key is required.");
        }

        var due = Clip(dueDate, 32);
        if (due.Length == 0)
        {
            due = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        var days = DaysOverdue(due, DateTime.Now);
        var dueAmt = amountDue ?? invoiceAmount;
        object? profile = profileId > 0 ? profileId : null;

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                INSERT INTO `epc_dunning_queue`
                    (`site_key`, `customer_id`, `customer_name`, `invoice_ref`, `invoice_amount`,
                     `amount_due`, `due_date`, `days_overdue`, `profile_id`)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
                """),
            cancellationToken,
            key,
            customerId < 0 ? 0 : customerId,
            Clip(customerName, 128),
            Clip(invoiceRef, 64),
            invoiceAmount,
            dueAmt,
            due,
            days,
            profile);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok(
            "Invoice queued (" + days.ToString(CultureInfo.InvariantCulture) + " days overdue).",
            id);
    }

    public async Task<ErpSimpleWriteResult> ProcessAsync(
        string? siteKey,
        CancellationToken cancellationToken = default)
    {
        var key = Clip(siteKey, 64);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A site key is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            """
            SELECT q.`id`, q.`due_date`, q.`dunning_step`, p.`steps`
            FROM `epc_dunning_queue` q
            LEFT JOIN `epc_dunning_profiles` p ON q.`profile_id` = p.`id`
            WHERE q.`site_key` = ? AND q.`status` IN ('open', 'in_progress')
            """);
        ErpDb.AddParameters(command, key);
        var items = new List<(long Id, string DueDate, int Step, string? StepsJson)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                items.Add((
                    reader.GetInt64(0),
                    reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? "",
                    reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                    reader.IsDBNull(3) ? null : reader.GetValue(3)?.ToString()));
            }
        }

        var now = DateTime.Now;
        var actioned = 0;
        foreach (var item in items)
        {
            var steps = ParseSteps(item.StepsJson);
            if (steps.Count == 0)
            {
                steps = DefaultSteps;
            }

            var itemDays = DaysOverdue(item.DueDate, now);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_dunning_queue` SET `days_overdue` = ? WHERE `id` = ?"),
                cancellationToken,
                itemDays, item.Id);

            if (!ShouldAdvance(item.Step, itemDays, steps))
            {
                continue;
            }

            var step = steps[item.Step];
            var action = NormalizeLogAction(step.Action);
            var details = "Step " + (item.Step + 1).ToString(CultureInfo.InvariantCulture) + ": " + Clip(step.Subject, 400);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_dunning_queue` SET `dunning_step` = ?, `status` = 'in_progress' WHERE `id` = ?"),
                cancellationToken,
                item.Step + 1, item.Id);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `epc_dunning_log` (`queue_id`, `action_type`, `details`) VALUES (?, ?, ?)"),
                cancellationToken,
                item.Id, action, details);
            actioned++;
        }

        return new ErpSimpleWriteResult(
            true,
            "ok",
            "Processed " + items.Count.ToString(CultureInfo.InvariantCulture)
                + " queue items, actioned " + actioned.ToString(CultureInfo.InvariantCulture) + ".",
            0,
            1);
    }

    public static readonly IReadOnlyList<CpDunningStep> DefaultSteps =
    [
        new(1, "email", "friendly_reminder", "Friendly Payment Reminder"),
        new(7, "email", "first_notice", "Payment Notice — Invoice Overdue"),
        new(14, "email", "second_notice", "Second Payment Notice — Urgent"),
        new(21, "call", "phone_followup", "Phone Follow-Up Required"),
        new(30, "letter", "formal_demand", "Formal Demand for Payment"),
        new(45, "escalation", "escalation", "Account Escalated to Collections"),
        new(60, "letter", "final_notice", "Final Notice Before Legal Action"),
    ];

    private static readonly HashSet<string> LogActions = new(StringComparer.Ordinal)
    {
        "email", "sms", "call", "letter", "escalation", "note", "payment", "write_off",
    };

    public static int DaysOverdue(string? dueDate, DateTime now)
    {
        if (!DateTime.TryParse(dueDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var due)
            && !DateTime.TryParse(dueDate, out due))
        {
            return 0;
        }

        return Math.Max(0, (int)((now - due).TotalSeconds / 86400));
    }

    public static bool ShouldAdvance(int currentStep, int itemDays, IReadOnlyList<CpDunningStep> steps)
        => steps is { Count: > 0 }
           && currentStep >= 0
           && currentStep < steps.Count
           && itemDays >= steps[currentStep].Day;

    public static string SerializeSteps(IReadOnlyList<CpDunningStep> steps)
        => JsonSerializer.Serialize(steps, StepJson);

    public static IReadOnlyList<CpDunningStep> ParseSteps(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var list = new List<CpDunningStep>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                list.Add(new CpDunningStep(
                    ReadInt(el, "day"),
                    ReadString(el, "action"),
                    ReadString(el, "template"),
                    ReadString(el, "subject")));
            }

            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string NormalizeLogAction(string? action)
    {
        var next = Normalize(action);
        return LogActions.Contains(next) ? next : "note";
    }

    private static readonly JsonSerializerOptions StepJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

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

    private static string Normalize(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}

public sealed record CpDunningStep(int Day, string Action, string Template, string Subject);
