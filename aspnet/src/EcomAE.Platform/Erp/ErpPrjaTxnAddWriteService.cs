using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_prja_txn_add</c> / ajax <c>prja_txn_add</c> twin.
/// INSERT <c>epc_prja_txn</c>. Budget save, recognition, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpPrjaTxnAddWriteService
{
    Task<ErpSimpleWriteResult> AddAsync(
        ErpPrjaTxnAddWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPrjaTxnAddWriteRequest(
    long CompanyId = 0,
    long ProjectId = 0,
    string? TxnType = null,
    string? Category = null,
    string? Description = null,
    decimal Amount = 0,
    long TxnDate = 0);

public sealed class ErpPrjaTxnAddWriteService : IErpPrjaTxnAddWriteService
{
    public static readonly HashSet<string> TxnTypes = new(StringComparer.Ordinal)
    {
        "cost", "revenue", "billing",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpPrjaTxnAddWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddAsync(
        ErpPrjaTxnAddWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var type = request.TxnType ?? "cost";
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
        var category = request.Category is null ? "general" : Clip(request.Category, 60);
        var description = Clip(request.Description ?? string.Empty, 200);
        var amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var txnDate = request.TxnDate > 0 ? request.TxnDate : now;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_prja_txn", "txn_type", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_prja_txn", "amount", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Project transaction table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_prja_txn` (`company_id`,`project_id`,`txn_type`,`category`,`description`,`amount`,`txn_date`,`time_created`) VALUES (?,?,?,?,?,?,?,?)"),
            cancellationToken,
            companyId,
            request.ProjectId,
            type,
            category,
            description,
            amount,
            txnDate,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Transaction posted", id);
    }

    public static string? Validate(string txnType)
        => TxnTypes.Contains(txnType) ? null : "Invalid project transaction type";

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];

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
