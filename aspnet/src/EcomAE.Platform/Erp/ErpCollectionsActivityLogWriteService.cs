using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_coll_activity_log</c> twin. Schema ensure, dunning run,
/// and credit hold stay PHP. Does not CREATE tables.
/// </summary>
public interface IErpCollectionsActivityLogWriteService
{
    Task<ErpSimpleWriteResult> LogAsync(
        ErpCollectionsActivityLogWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpCollectionsActivityLogWriteRequest(
    long CaseId = 0,
    string? Type = null,
    string? Outcome = null,
    decimal Amount = 0,
    string? FollowUpDate = null);

public sealed class ErpCollectionsActivityLogWriteService : IErpCollectionsActivityLogWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpCollectionsActivityLogWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> LogAsync(
        ErpCollectionsActivityLogWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CaseId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "id must be positive.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var type = Clip(string.IsNullOrWhiteSpace(request.Type) ? "note" : request.Type, 20);
        var outcome = Clip(request.Outcome, 200);
        var amount = decimal.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        var followUp = ErpCollectionsCaseSaveWriteService.ResolvePromiseUnix(request.FollowUpDate);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_coll_activity", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_coll_activity", "outcome", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Collections activity table is not provisioned");
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_coll_activity` (`case_id`,`type`,`outcome`,`amount`,`follow_up_date`,`time_created`) VALUES (?,?,?,?,?,?)"),
                cancellationToken,
                request.CaseId,
                type,
                outcome,
                amount,
                followUp,
                now).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Activity logged", id);
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

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
