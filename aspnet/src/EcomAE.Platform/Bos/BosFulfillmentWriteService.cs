using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Bos;

/// <summary>
/// Live PHP <c>ajax_epc_bos.php</c> <c>fulfillment_queue</c> <c>pick_item</c> / <c>epc_fulfillment_pick_item</c>.
/// Queue, transition, wave, and schema-ensure stay Classic. This service does not invent a send.
/// It does not emit CREATE/ALTER.
/// </summary>
public interface IBosFulfillmentWriteService
{
    Task<ErpSimpleWriteResult> PickItemAsync(
        long itemId,
        int qtyPicked,
        CancellationToken cancellationToken = default);
}

public sealed class BosFulfillmentWriteService : IBosFulfillmentWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public BosFulfillmentWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> PickItemAsync(
        long itemId,
        int qtyPicked,
        CancellationToken cancellationToken = default)
    {
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
                    UPDATE `epc_fulfillment_items` SET `qty_picked` = ?, `pick_status` = ?
                    WHERE `id` = ?
                    """),
                cancellationToken, qtyPicked, "picked", itemId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Line pick saved", itemId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Fulfillment items table is missing — schema-ensure stays Classic.");
        }
    }
}
