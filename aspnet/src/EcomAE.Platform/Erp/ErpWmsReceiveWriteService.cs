using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_wms_receive</c> twin. Creates/merges an LP and raises put-away work.
/// Schema ensure, wave create, pick add, and work complete stay PHP.
/// </summary>
public interface IErpWmsReceiveWriteService
{
    Task<ErpSimpleWriteResult> ReceiveAsync(
        string? item,
        decimal qty,
        long receiveLocationId,
        long putawayLocationId,
        string? reference,
        string? lpCode,
        long companyId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpWmsReceiveWriteService : IErpWmsReceiveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpWmsReceiveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> ReceiveAsync(
        string? item,
        decimal qty,
        long receiveLocationId,
        long putawayLocationId,
        string? reference,
        string? lpCode,
        long companyId,
        CancellationToken cancellationToken = default)
    {
        var sku = (item ?? string.Empty).Trim();
        if (sku.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Item is required");
        }

        if (qty <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "qty must be positive.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var resolvedCompany = companyId > 0
            ? companyId
            : await ResolveCompanyAsync(connection, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var lpId = await UpsertLpAsync(connection, resolvedCompany, lpCode, receiveLocationId, sku, qty, now, cancellationToken)
            .ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_wms_work` (`company_id`,`work_type`,`reference`,`wave_id`,`item`,`qty`,`from_location_id`,`to_location_id`,`lp_id`,`status`,`assigned_to`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            resolvedCompany, "putaway", reference ?? string.Empty, 0L, sku, qty,
            receiveLocationId, putawayLocationId, lpId, "open", string.Empty, now, now);
        var workId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Received — put-away work raised", workId);
    }

    public static string FormatAutoLpCode(long seq)
        => "LP" + seq.ToString("000000", CultureInfo.InvariantCulture);

    private static async Task<long> UpsertLpAsync(
        System.Data.Common.DbConnection connection,
        long companyId,
        string? lpCode,
        long locationId,
        string item,
        decimal qty,
        long now,
        CancellationToken cancellationToken)
    {
        var code = (lpCode ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length == 0)
        {
            var seq = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_wms_lp` WHERE `company_id`=?"),
                cancellationToken,
                companyId).ConfigureAwait(false) + 1;
            code = FormatAutoLpCode(seq);
        }

        var existingId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_wms_lp` WHERE `company_id`=? AND `lp_code`=? LIMIT 1"),
            cancellationToken,
            companyId, code).ConfigureAwait(false);
        if (existingId > 0)
        {
            var existingQty = await ErpDb.DecimalAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `qty` FROM `epc_erp_wms_lp` WHERE `id`=?"),
                cancellationToken,
                existingId).ConfigureAwait(false);
            var existingItem = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `item` FROM `epc_erp_wms_lp` WHERE `id`=?"),
                cancellationToken,
                existingId).ConfigureAwait(false);
            var newQty = existingQty + qty;
            var storedItem = item.Length > 0 ? item : (existingItem ?? string.Empty);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_erp_wms_lp` SET `location_id`=?, `item`=?, `qty`=?, `status`=?, `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                locationId, storedItem, newQty, newQty > 0 ? "active" : "closed", now, existingId);
            return existingId;
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_wms_lp` (`company_id`,`lp_code`,`location_id`,`item`,`qty`,`status`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?)"),
            cancellationToken,
            companyId, code, locationId, item, qty, qty > 0 ? "active" : "closed", now, now);
        return await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> ResolveCompanyAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            return await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `active`=1 ORDER BY `id` LIMIT 1"),
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return 0;
        }
    }
}
