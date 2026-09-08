using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_costm_txn_add</c> / ajax <c>costm_txn_add</c> twin.
/// INSERT <c>epc_costm_txn</c>. Item-set, closing run, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpCostmTxnAddWriteService
{
    Task<ErpSimpleWriteResult> AddAsync(
        ErpCostmTxnAddWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpCostmTxnAddWriteRequest(
    long CompanyId = 0,
    long ItemId = 0,
    string? TxnType = null,
    decimal Qty = 0,
    decimal UnitCost = 0,
    long TxnDate = 0);

public sealed class ErpCostmTxnAddWriteService : IErpCostmTxnAddWriteService
{
    public static readonly HashSet<string> TxnTypes = new(StringComparer.Ordinal)
    {
        "receipt", "issue",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpCostmTxnAddWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddAsync(
        ErpCostmTxnAddWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var type = request.TxnType ?? "receipt";
        var invalid = Validate(type);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var qty = decimal.Round(request.Qty, 4, MidpointRounding.AwayFromZero);
        var unitCost = decimal.Round(request.UnitCost, 4, MidpointRounding.AwayFromZero);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var txnDate = request.TxnDate > 0 ? request.TxnDate : now;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_costm_txn", "txn_type", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_costm_txn", "qty", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Costing transaction table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_costm_txn` (`company_id`,`item_id`,`txn_type`,`qty`,`unit_cost`,`txn_date`,`time_created`) VALUES (?,?,?,?,?,?,?)"),
            cancellationToken,
            companyId,
            request.ItemId,
            type,
            qty,
            unitCost,
            txnDate,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Transaction added", id);
    }

    public static string? Validate(string txnType)
        => TxnTypes.Contains(txnType) ? null : "Invalid transaction type";

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
