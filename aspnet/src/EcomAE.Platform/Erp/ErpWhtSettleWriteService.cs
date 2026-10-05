using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_wht_settle</c> twin. Schema ensure, record, and certificate minting stay PHP.
/// Code save is a separate live twin.
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

        await ErpLazySchema.EnsureWithholdingAsync(connection, cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_wht_txn", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_wht_txn", "status", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Withholding transaction table is not provisioned");
        }

        var status = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `status` FROM `epc_wht_txn` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            id).ConfigureAwait(false);
        if (status is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Transaction not found");
        }

        if (string.Equals(status, "settled", StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Already settled");
        }

        var updated = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_wht_txn` SET `status` = 'settled' WHERE `id` = ?"),
            cancellationToken,
            id);
        if (updated != 1)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Transaction not found");
        }

        return ErpSimpleWriteResult.Ok("Withholding settled to authority", id);
    }

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return count > 0;
    }

    private static async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        var count = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return count > 0;
    }
}
