using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>collections_dunning</c> <c>update_status</c> / <c>epc_dunning_update_status</c>.
/// Record-payment, process, profile-create, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER. The CP twin writes the tenant shop DB; this write uses the platform operator PDO.
/// </summary>
public interface IBosDunningWriteService
{
    Task<ErpSimpleWriteResult> UpdateStatusAsync(
        long queueId,
        string? status,
        string? notes,
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
}
