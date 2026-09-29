namespace EcomAE.Platform.Erp;

/// <summary>Live PHP <c>epc_mfgr_planned_firm</c> twin. MRP generation remains PHP-owned.</summary>
public interface IErpMasterPlannedOrderFirmWriteService
{
    Task<ErpSimpleWriteResult> FirmAsync(long plannedOrderId, CancellationToken cancellationToken = default);
}

public sealed class ErpMasterPlannedOrderFirmWriteService : IErpMasterPlannedOrderFirmWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpMasterPlannedOrderFirmWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> FirmAsync(long plannedOrderId, CancellationToken cancellationToken = default)
    {
        if (plannedOrderId <= 0)
            return ErpSimpleWriteResult.Fail("invalid", "A planned order id is required.");
        if (!_connections.IsConfigured)
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var writes = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_mfg_planned` SET `status`='firmed' WHERE `id`=? AND `source`='mrp' AND `status`='planned'"),
            cancellationToken,
            plannedOrderId).ConfigureAwait(false);
        return writes == 0
            ? ErpSimpleWriteResult.Fail("invalid", "MRP planned order was not found or is not in planned status.")
            : ErpSimpleWriteResult.Ok("MRP planned order firmed", plannedOrderId);
    }
}
