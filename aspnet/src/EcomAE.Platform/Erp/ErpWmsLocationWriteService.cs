namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_wms_location_delete</c> twin. Schema ensure stays PHP.
/// </summary>
public interface IErpWmsLocationWriteService
{
    Task<ErpSimpleWriteResult> DeleteAsync(long locationId, CancellationToken cancellationToken = default);
}

public sealed class ErpWmsLocationWriteService : IErpWmsLocationWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpWmsLocationWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(
        long locationId,
        CancellationToken cancellationToken = default)
    {
        if (locationId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A warehouse location id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var holding = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM `epc_erp_wms_lp` WHERE `location_id` = ? AND `status` = 'active' AND `qty` > 0"),
            cancellationToken,
            locationId);
        if (holding > 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Location holds stock — move or close license plates first");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `epc_erp_wms_locations` WHERE `id` = ?"),
            cancellationToken,
            locationId);
        return ErpSimpleWriteResult.Ok("Warehouse location deleted.", locationId);
    }
}
