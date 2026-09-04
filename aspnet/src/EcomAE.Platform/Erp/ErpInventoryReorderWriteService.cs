using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>Live PHP <c>inv_set_reorder_level</c> twin. Schema ensure, movements, and transfers stay PHP.</summary>
public interface IErpInventoryReorderWriteService
{
    Task<ErpSimpleWriteResult> SetReorderLevelAsync(long itemId, decimal level, CancellationToken cancellationToken = default);
}

public sealed class ErpInventoryReorderWriteService : IErpInventoryReorderWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpInventoryReorderWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetReorderLevelAsync(
        long itemId,
        decimal level,
        CancellationToken cancellationToken = default)
    {
        if (itemId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Item is required");
        }

        if (level < 0)
        {
            level = 0;
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var qty = decimal.Round(level, 3, MidpointRounding.AwayFromZero);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_inv_items` SET `reorder_level` = ? WHERE `id` = ? AND `active` = 1"),
            cancellationToken,
            qty.ToString(CultureInfo.InvariantCulture), itemId);
        return ErpSimpleWriteResult.Ok("Reorder level updated", itemId);
    }
}
