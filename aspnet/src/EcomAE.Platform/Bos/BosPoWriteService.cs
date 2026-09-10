using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>po_approval</c> <c>cancel</c> / <c>epc_po_cancel</c>.
/// Create, approve, reject, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER.
/// </summary>
public interface IBosPoWriteService
{
    Task<ErpSimpleWriteResult> CancelAsync(
        long poId,
        CancellationToken cancellationToken = default);
}

public sealed class BosPoWriteService : IBosPoWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public BosPoWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CancelAsync(
        long poId,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Database unavailable");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var status = await ErpDb.StringAsync(
                connection, null,
                ErpDb.Positional("SELECT `status` FROM `epc_po_requests` WHERE `id` = ?"),
                cancellationToken, poId).ConfigureAwait(false);
            if (string.IsNullOrEmpty(status))
            {
                return ErpSimpleWriteResult.Fail("invalid", "PO not found");
            }

            if (string.Equals(status, "approved", StringComparison.Ordinal))
            {
                return ErpSimpleWriteResult.Fail("invalid", "Cannot cancel approved PO");
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_po_requests` SET `status` = 'cancelled' WHERE `id` = ?
                    """),
                cancellationToken, poId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("PO cancelled", poId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "PO requests table is missing — schema-ensure stays Classic.");
        }
    }
}
