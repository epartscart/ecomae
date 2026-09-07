using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_dunning_update_status</c> / <c>epc_dunning_record_payment</c> twins.
/// Profile CRUD, add-invoice, letter/email process, and assign stay PHP.
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

    private static string Normalize(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
