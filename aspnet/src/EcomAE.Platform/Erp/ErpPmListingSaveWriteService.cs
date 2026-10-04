using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_pm_listing_save</c> / ajax <c>pm_listing_save</c> twin
/// (<c>epc_erp_pdf_modules.php</c>). UPDATE by id or INSERT with
/// <c>LST-{year}-{seq:00000}</c> (seq = COUNT(*) for the current year + 1,
/// matching PHP <c>epc_erp_pm_next_listing_seq</c>). amount = qty x rate.
/// Dimension save is applied by the caller via <see cref="IErpDimensionWriteService"/>
/// with entity type <c>listing</c> (PHP <c>epc_erp_dim_save_from_post</c>).
/// Fails closed when the listing table is not provisioned.
/// </summary>
public interface IErpPmListingSaveWriteService
{
    Task<ErpPmListingSaveResult> SaveAsync(
        ErpPmListingSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPmListingSaveRequest(
    long Id = 0,
    string? ResourceType = null,
    string? Title = null,
    string? Description = null,
    double Qty = 0,
    double Rate = 0,
    string? VoucherRef = null,
    string? Status = null);

public sealed record ErpPmListingSaveResult(bool Ok, string Message, long Id, string RefNo, int Writes)
{
    public static ErpPmListingSaveResult FailMsg(string message) => new(false, message, 0, string.Empty, 0);
}

public sealed class ErpPmListingSaveWriteService : IErpPmListingSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpPmListingSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpPmListingSaveResult> SaveAsync(
        ErpPmListingSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_connections.IsConfigured)
        {
            return ErpPmListingSaveResult.FailMsg("TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var amount = request.Qty * request.Rate;
        var resourceType = (request.ResourceType ?? string.Empty).Trim();
        var title = (request.Title ?? string.Empty).Trim();
        var description = request.Description ?? string.Empty;
        var voucherRef = (request.VoucherRef ?? string.Empty).Trim();
        var status = (request.Status ?? string.Empty).Trim();
        if (status.Length == 0) status = "draft";

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_pm_listings", "ref_no", cancellationToken).ConfigureAwait(false))
        {
            return ErpPmListingSaveResult.FailMsg("Listing table is not provisioned");
        }

        if (request.Id > 0)
        {
            var writes = await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_erp_pm_listings` SET `resource_type`=?,`title`=?,`description`=?,`qty`=?,`rate`=?,`amount`=?,`voucher_ref`=?,`status`=?,`time_updated`=? WHERE `id`=?"),
                cancellationToken,
                resourceType,
                title,
                description,
                request.Qty,
                request.Rate,
                amount,
                voucherRef,
                status,
                now,
                request.Id).ConfigureAwait(false);
            return new(true, "Listing saved", request.Id, string.Empty, writes);
        }

        var year = DateTimeOffset.UtcNow.ToString("yyyy", CultureInfo.InvariantCulture);
        var yearCount = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_pm_listings` WHERE YEAR(FROM_UNIXTIME(`time_created`)) = ?"),
            cancellationToken,
            int.Parse(year, CultureInfo.InvariantCulture)).ConfigureAwait(false);
        var refNo = $"LST-{year}-{(yearCount + 1).ToString("00000", CultureInfo.InvariantCulture)}";

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_pm_listings` (`ref_no`,`resource_type`,`title`,`description`,`qty`,`rate`,`amount`,`voucher_ref`,`status`,`active`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,1,?,?)"),
            cancellationToken,
            refNo,
            resourceType,
            title,
            description,
            request.Qty,
            request.Rate,
            amount,
            voucherRef,
            status,
            now,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return new(true, "Listing saved", id, refNo, 1);
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
