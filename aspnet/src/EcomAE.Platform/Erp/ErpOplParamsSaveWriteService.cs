using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_opl_params_save</c> / ajax <c>opl_params_save</c> twin.
/// UPSERT <c>epc_erp_planning_params</c>. Does not call <c>epc_opl_compute</c>.
/// Confirm/create-PO/autoplan and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpOplParamsSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpOplParamsSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpOplParamsSaveWriteRequest(
    long ItemId = 0,
    long WarehouseId = 0,
    int? LeadTimeDays = null,
    decimal? TargetServiceLevel = null,
    int? ReviewPeriodDays = null,
    decimal? MinOrderQty = null,
    decimal? OrderMultiple = null,
    decimal? ManualBuffer = null,
    string? Supplier = null,
    int? Stocked = null);

public sealed class ErpOplParamsSaveWriteService : IErpOplParamsSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpOplParamsSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpOplParamsSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ItemId <= 0 || request.WarehouseId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Item and warehouse required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var lead = request.LeadTimeDays ?? 30;
        if (lead < 1)
        {
            lead = 1;
        }

        var service = request.TargetServiceLevel ?? 90m;
        if (service < 0)
        {
            service = 0;
        }
        if (service > 99.9m)
        {
            service = 99.9m;
        }

        var review = request.ReviewPeriodDays ?? 30;
        if (review < 1)
        {
            review = 1;
        }

        var minQty = request.MinOrderQty ?? 0;
        if (minQty < 0)
        {
            minQty = 0;
        }

        var multiple = request.OrderMultiple ?? 0;
        if (multiple < 0)
        {
            multiple = 0;
        }

        var buffer = request.ManualBuffer ?? 0;
        if (buffer < 0)
        {
            buffer = 0;
        }

        var supplier = request.Supplier ?? string.Empty;
        var stocked = request.Stocked is > 0 ? 1 : 0;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_planning_params", "lead_time_days", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Planning parameters table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                """
                INSERT INTO `epc_erp_planning_params`
                    (`item_id`,`warehouse_id`,`lead_time_days`,`target_service_level`,`review_period_days`,`min_order_qty`,`order_multiple`,`manual_buffer`,`supplier`,`stocked`,`time_updated`)
                    VALUES (?,?,?,?,?,?,?,?,?,?,?)
                    ON DUPLICATE KEY UPDATE `lead_time_days`=VALUES(`lead_time_days`), `target_service_level`=VALUES(`target_service_level`),
                    `review_period_days`=VALUES(`review_period_days`), `min_order_qty`=VALUES(`min_order_qty`), `order_multiple`=VALUES(`order_multiple`),
                    `manual_buffer`=VALUES(`manual_buffer`), `supplier`=VALUES(`supplier`), `stocked`=VALUES(`stocked`), `time_updated`=VALUES(`time_updated`)
                """),
            cancellationToken,
            request.ItemId,
            request.WarehouseId,
            lead,
            service,
            review,
            minQty,
            multiple,
            buffer,
            supplier,
            stocked,
            now).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Parameters saved — recalculated", request.ItemId);
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
