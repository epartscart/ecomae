using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_coll_case_promise</c> twin. Updates promise amount/date,
/// sets status to <c>promise_to_pay</c>, and inserts the promise activity.
/// Schema ensure stays PHP. Dedicated activity / dunning / hold stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpCollectionsCasePromiseWriteService
{
    Task<ErpSimpleWriteResult> PromiseAsync(
        ErpCollectionsCasePromiseWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpCollectionsCasePromiseWriteRequest(
    long Id = 0,
    decimal Amount = 0,
    string? PromiseDate = null);

public sealed class ErpCollectionsCasePromiseWriteService : IErpCollectionsCasePromiseWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpCollectionsCasePromiseWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> PromiseAsync(
        ErpCollectionsCasePromiseWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "id must be positive.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        var promiseUnix = ErpCollectionsCaseSaveWriteService.ResolvePromiseUnix(request.PromiseDate);
        var outcome = "Promise to pay " + amount.ToString("N2", CultureInfo.InvariantCulture);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_coll_cases", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_coll_cases", "promise_amount", cancellationToken).ConfigureAwait(false)
            || !await TableExistsAsync(connection, "epc_coll_activity", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_coll_activity", "outcome", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Collections case tables are not provisioned");
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var updated = await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "UPDATE `epc_coll_cases` SET `promise_amount`=?, `promise_date`=?, `status`='promise_to_pay', `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                amount,
                promiseUnix,
                now,
                request.Id).ConfigureAwait(false);
            if (updated <= 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("invalid", "Case not found");
            }

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_coll_activity` (`case_id`,`type`,`outcome`,`amount`,`follow_up_date`,`time_created`) VALUES (?,?,?,?,?,?)"),
                cancellationToken,
                request.Id,
                "promise",
                outcome,
                amount,
                promiseUnix,
                now).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Promise to pay recorded", request.Id);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

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
