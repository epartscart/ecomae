using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public interface IErpPaymentBatchSaveWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpPaymentBatchSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPaymentBatchSaveWriteRequest(
    long AccountId = 0,
    string? BatchType = null,
    decimal TotalAmount = 0,
    int LineCount = 1,
    string? ExecutionDate = null,
    string? Notes = null,
    int AdminId = 0);

public sealed class ErpPaymentBatchSaveWriteService : IErpPaymentBatchSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPaymentBatchSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        ErpPaymentBatchSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var type = request.BatchType is "sepa" or "local" or "cheque" ? request.BatchType : "sepa";
        var amount = Math.Round(request.TotalAmount, 2, MidpointRounding.AwayFromZero);
        if (amount < 0 || request.LineCount < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Total and line count must not be negative.");
        }

        var execution = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (DateTime.TryParseExact(request.ExecutionDate?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            execution = new DateTimeOffset(day, TimeSpan.Zero).AddHours(12).ToUnixTimeSeconds();
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var batchNo = await NextBatchNumberAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                INSERT INTO `epc_erp_payment_batches`
                    (`batch_no`,`batch_type`,`account_id`,`total_amount`,`line_count`,`status`,
                     `execution_date`,`notes`,`admin_id`,`time_created`,`time_updated`)
                VALUES (?,?,?,?,?,'draft',?,?,?,?,?)
                """),
            cancellationToken,
            batchNo,
            type,
            request.AccountId,
            amount,
            request.LineCount,
            execution,
            (request.Notes ?? string.Empty).Trim(),
            request.AdminId,
            now,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Payment batch draft created", id);
    }

    private static async Task<string> NextBatchNumberAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var next = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COALESCE(MAX(`id`), 0) + 1 FROM `epc_erp_payment_batches`"),
            cancellationToken).ConfigureAwait(false);
        return "PAY-" + next.ToString(CultureInfo.InvariantCulture);
    }
}
