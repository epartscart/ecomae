namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_wht_settle</c> twin. Schema ensure, record, and certificate minting stay PHP.
/// </summary>
public interface IErpWhtSettleWriteService
{
    Task<ErpSimpleWriteResult> SettleAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class ErpWhtSettleWriteService : IErpWhtSettleWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpWhtSettleWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SettleAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A withholding transaction id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_wht_txn` SET `status` = 'settled' WHERE `id` = ?"),
            cancellationToken,
            id);
        return ErpSimpleWriteResult.Ok("Withholding settled to authority", id);
    }
}
