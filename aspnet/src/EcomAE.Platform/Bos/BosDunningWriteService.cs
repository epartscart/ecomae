using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>collections_dunning</c> <c>update_status</c> / <c>epc_dunning_update_status</c>
/// and <c>record_payment</c> / <c>epc_dunning_record_payment</c>.
/// Process, profile-create, and schema-ensure stay Classic. This service does not invent a send.
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
}

public sealed class BosDunningWriteService : IBosDunningWriteService
{
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
}
