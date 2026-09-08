using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_rtl_assortment_set</c> / ajax <c>rtl_assortment_set</c> twin.
/// UPSERT <c>epc_rtl_assortment</c> on <c>channel_id</c>+<c>item_id</c>.
/// Channel save, discount, POS, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpRtlAssortmentSetWriteService
{
    Task<ErpSimpleWriteResult> SetAsync(
        ErpRtlAssortmentSetWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpRtlAssortmentSetWriteRequest(
    long CompanyId = 0,
    long ChannelId = 0,
    long ItemId = 0,
    int? Active = null);

public sealed class ErpRtlAssortmentSetWriteService : IErpRtlAssortmentSetWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpRtlAssortmentSetWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetAsync(
        ErpRtlAssortmentSetWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        var active = request.Active is null || request.Active != 0 ? 1 : 0;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_rtl_assortment", "channel_id", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_rtl_assortment", "item_id", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Assortment table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_rtl_assortment` (`company_id`,`channel_id`,`item_id`,`active`) VALUES (?,?,?,?) "
                + "ON DUPLICATE KEY UPDATE `active`=VALUES(`active`)"),
            cancellationToken,
            companyId,
            request.ChannelId,
            request.ItemId,
            active).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            id = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_rtl_assortment` WHERE `channel_id`=? AND `item_id`=? LIMIT 1"),
                cancellationToken,
                request.ChannelId,
                request.ItemId).ConfigureAwait(false);
        }

        return ErpSimpleWriteResult.Ok("Assortment updated", id);
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
