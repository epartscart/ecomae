using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_wht_record</c> twin. INSERT <c>epc_wht_txn</c> status
/// <c>accrued</c>. Schema ensure and certificate minting stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpWhtRecordWriteService
{
    Task<ErpSimpleWriteResult> RecordAsync(
        ErpWhtRecordWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpWhtRecordWriteRequest(
    long CodeId = 0,
    long CompanyId = 0,
    string? Vendor = null,
    string? DocRef = null,
    string? TxnDate = null,
    decimal BaseAmount = 0);

public sealed class ErpWhtRecordWriteService : IErpWhtRecordWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpWhtRecordWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> RecordAsync(
        ErpWhtRecordWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invalid = Validate(request.CodeId, request.BaseAmount);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var vendor = Clip((request.Vendor ?? string.Empty).Trim(), 180);
        var docRef = Clip((request.DocRef ?? string.Empty).Trim(), 80);
        var txnDate = Clip((request.TxnDate ?? string.Empty).Trim(), 16);
        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_wht_code", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_wht_code", "rate", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Withholding code table is not provisioned");
        }

        if (!await TableExistsAsync(connection, "epc_wht_txn", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_wht_txn", "base_amount", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Withholding transaction table is not provisioned");
        }

        var found = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_wht_code` WHERE `id`=?"),
            cancellationToken,
            request.CodeId).ConfigureAwait(false);
        if (found <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Withholding code not found");
        }

        var rate = await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `rate` FROM `epc_wht_code` WHERE `id`=?"),
            cancellationToken,
            request.CodeId).ConfigureAwait(false);
        var withheld = Calc(request.BaseAmount, rate);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_wht_txn` (`company_id`,`code_id`,`vendor`,`doc_ref`,`txn_date`,`base_amount`,`wht_amount`,`rate`,`status`,`time_created`) VALUES (?,?,?,?,?,?,?,?,'accrued',?)"),
            cancellationToken,
            companyId,
            request.CodeId,
            vendor,
            docRef,
            txnDate,
            request.BaseAmount,
            withheld,
            rate,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Withholding applied", id);
    }

    public static string? Validate(long codeId, decimal baseAmount)
    {
        if (codeId <= 0)
        {
            return "Withholding code not found";
        }

        if (baseAmount <= 0)
        {
            return "Base amount must be positive";
        }

        return null;
    }

    public static decimal Calc(decimal baseAmount, decimal rate)
        => decimal.Round(baseAmount * (rate / 100m), 2, MidpointRounding.AwayFromZero);

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }

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
