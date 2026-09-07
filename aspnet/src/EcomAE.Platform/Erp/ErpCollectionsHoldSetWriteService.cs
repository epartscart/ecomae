using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_coll_hold_set</c> twin. Flips <c>epc_credit_profiles.on_hold</c>
/// and inserts the hold action log. Schema ensure and dunning run stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpCollectionsHoldSetWriteService
{
    Task<ErpSimpleWriteResult> SetHoldAsync(
        ErpCollectionsHoldSetWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpCollectionsHoldSetWriteRequest(
    long CustomerId = 0,
    bool Place = true,
    string? Reason = null,
    string? Actor = null,
    long CompanyId = 0);

public sealed class ErpCollectionsHoldSetWriteService : IErpCollectionsHoldSetWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpCollectionsHoldSetWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetHoldAsync(
        ErpCollectionsHoldSetWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CustomerId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "customerId must be positive.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var reason = Clip(request.Reason, 255);
        var actor = Clip(request.Actor, 120);
        var onHold = request.Place ? 1 : 0;
        var action = request.Place ? "place" : "release";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_coll_hold", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_coll_hold", "action", cancellationToken).ConfigureAwait(false)
            || !await TableExistsAsync(connection, "epc_credit_profiles", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_credit_profiles", "on_hold", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Collections hold tables are not provisioned");
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existingId = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `id` FROM `epc_credit_profiles` WHERE `customer_id`=? LIMIT 1"),
                cancellationToken,
                request.CustomerId).ConfigureAwait(false);
            if (existingId > 0)
            {
                var limit = await ErpDb.DecimalAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT `credit_limit` FROM `epc_credit_profiles` WHERE `id`=?"),
                    cancellationToken,
                    existingId).ConfigureAwait(false);
                var terms = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT `terms_days` FROM `epc_credit_profiles` WHERE `id`=?"),
                    cancellationToken,
                    existingId).ConfigureAwait(false);
                var band = await ErpDb.StringAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT `risk_band` FROM `epc_credit_profiles` WHERE `id`=?"),
                    cancellationToken,
                    existingId).ConfigureAwait(false);
                var notes = await ErpDb.StringAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT `notes` FROM `epc_credit_profiles` WHERE `id`=?"),
                    cancellationToken,
                    existingId).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "UPDATE `epc_credit_profiles` SET `credit_limit`=?, `terms_days`=?, `on_hold`=?, `risk_band`=?, `notes`=?, `time_updated`=? WHERE `id`=?"),
                    cancellationToken,
                    limit,
                    terms <= 0 ? 30 : terms,
                    onHold,
                    string.IsNullOrWhiteSpace(band) ? "normal" : band,
                    notes ?? "",
                    now,
                    existingId).ConfigureAwait(false);
            }
            else
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_credit_profiles` (`customer_id`,`credit_limit`,`terms_days`,`on_hold`,`risk_band`,`notes`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?)"),
                    cancellationToken,
                    request.CustomerId,
                    0m,
                    30,
                    onHold,
                    "normal",
                    "",
                    now,
                    now).ConfigureAwait(false);
            }

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_coll_hold` (`company_id`,`customer_id`,`action`,`reason`,`actor`,`time_created`) VALUES (?,?,?,?,?,?)"),
                cancellationToken,
                companyId,
                request.CustomerId,
                action,
                reason,
                actor,
                now).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Credit hold updated", id);
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
