namespace EcomAE.Platform.Erp;

/// <summary>
/// Warehouse-management workspace tabs backed by the real inventory tables
/// (inv_warehouses, inv_stock, inv_movements, inv_serials, inv_closing, inv_items).
/// EnsureWarehouseAsync lazy-creates the schema before every read.
/// </summary>
public static class ErpWarehouseWorkspaceSpecs
{
    private const string Stock = "/erp/inventory-stock-app";
    private const string Report = "/erp/inventory-report-app";
    private const string Forecast = "/erp/inventory-forecast-app";

    private const string WhJoin = " FROM `epc_erp_inv_warehouses` w";
    private const string StockJoin =
        " FROM `epc_erp_inv_stock` st" +
        " LEFT JOIN `epc_erp_inv_items` i ON i.`id` = st.`item_id`" +
        " LEFT JOIN `epc_erp_inv_warehouses` w ON w.`id` = st.`warehouse_id`";
    private const string MoveJoin =
        " FROM `epc_erp_inv_movements` m" +
        " LEFT JOIN `epc_erp_inv_items` i ON i.`id` = m.`item_id`" +
        " LEFT JOIN `epc_erp_inv_warehouses` w ON w.`id` = m.`warehouse_id`";

    public static readonly IReadOnlyDictionary<string, ErpWorkspaceSpec> Tabs =
        new Dictionary<string, ErpWorkspaceSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["wh_all_loads"] = new(
                ["Date", "Lines", "Qty", "Value"],
                "SELECT DATE_FORMAT(FROM_UNIXTIME(m.`movement_date`),'%Y-%m-%d')," +
                " COUNT(m.`id`), IFNULL(SUM(m.`qty`),0), IFNULL(SUM(m.`total_cost`),0)" +
                MoveJoin +
                " WHERE m.`active` = 1 GROUP BY 1 ORDER BY 1 DESC LIMIT 120",
                "No loads yet — stock movements group into daily loads here.",
                Report),

            ["wh_all_waves"] = new(
                ["SKU", "Item", "Warehouse", "On hand", "Avg cost", "Batch", "Expiry"],
                "SELECT i.`sku`, i.`name`, IFNULL(w.`name`,'(none)'), st.`qty_on_hand`," +
                " st.`avg_unit_cost`, IFNULL(st.`batch_no`,''), IFNULL(st.`expiry_date`,'')" +
                StockJoin +
                " WHERE st.`qty_on_hand` <> 0 ORDER BY i.`sku` LIMIT 400",
                "Nothing in stock — post an opening balance or receive a PO.",
                Stock),

            ["wh_physical"] = new(
                ["SKU", "Item", "Warehouse", "Qty", "Value", "Batch"],
                "SELECT i.`sku`, i.`name`, IFNULL(w.`name`,'(none)'), st.`qty_on_hand`," +
                " ROUND(st.`qty_on_hand` * st.`avg_unit_cost`, 2), IFNULL(st.`batch_no`,'')" +
                StockJoin +
                " WHERE st.`qty_on_hand` <> 0 ORDER BY i.`sku`, w.`name` LIMIT 400",
                "No physical inventory yet — receive or open stock first.",
                Stock),

            ["wh_recv"] = new(
                ["SKU", "Item", "Warehouse", "Qty", "Unit cost", "Reference", "Date"],
                "SELECT i.`sku`, i.`name`, IFNULL(w.`name`,'(none)'), m.`qty`," +
                " m.`unit_cost`, IFNULL(m.`reference`,''), FROM_UNIXTIME(m.`movement_date`)" +
                MoveJoin +
                " WHERE m.`active` = 1 AND m.`movement_type` IN ('purchase_in','return_in','opening')" +
                " ORDER BY m.`movement_date` DESC, m.`id` DESC LIMIT 400",
                "No receipts — post a purchase invoice or an opening balance.",
                Stock),

            ["wh_all_ship"] = new(
                ["SKU", "Item", "Warehouse", "Qty", "Unit cost", "Reference", "Date"],
                "SELECT i.`sku`, i.`name`, IFNULL(w.`name`,'(none)'), m.`qty`," +
                " m.`unit_cost`, IFNULL(m.`reference`,''), FROM_UNIXTIME(m.`movement_date`)" +
                MoveJoin +
                " WHERE m.`active` = 1 AND m.`movement_type` = 'sale_out'" +
                " ORDER BY m.`movement_date` DESC, m.`id` DESC LIMIT 400",
                "No shipments — post a sale to see outbound work.",
                Stock),

            ["wh_return_work"] = new(
                ["Type", "SKU", "Item", "Qty", "Reference", "Date"],
                "SELECT m.`movement_type`, i.`sku`, i.`name`, m.`qty`," +
                " IFNULL(m.`reference`,''), FROM_UNIXTIME(m.`movement_date`)" +
                MoveJoin +
                " WHERE m.`active` = 1 AND m.`movement_type` IN ('return_in','return_out')" +
                " ORDER BY m.`movement_date` DESC LIMIT 200",
                "No returns on record.",
                Stock),

            ["wh_xfer_work"] = new(
                ["SKU", "Item", "From", "To", "Qty", "Date"],
                "SELECT i.`sku`, i.`name`, IFNULL(w.`name`,'(none)')," +
                " IFNULL(tw.`name`,'(none)'), m.`qty`, FROM_UNIXTIME(m.`movement_date`)" +
                MoveJoin +
                " LEFT JOIN `epc_erp_inv_warehouses` tw ON tw.`id` = m.`transfer_warehouse_id`" +
                " WHERE m.`active` = 1 AND m.`movement_type` IN ('transfer_in','transfer_out')" +
                " ORDER BY m.`movement_date` DESC, m.`id` DESC LIMIT 300",
                "No transfers — move stock between warehouses to see work here.",
                Stock),

            ["wh_pending_waves"] = new(
                ["Type", "SKU", "Item", "Warehouse", "Qty", "Cost", "Date"],
                "SELECT m.`movement_type`, i.`sku`, i.`name`, IFNULL(w.`name`,'(none)')," +
                " m.`qty`, m.`total_cost`, FROM_UNIXTIME(m.`movement_date`)" +
                MoveJoin +
                " WHERE m.`active` = 1 ORDER BY m.`movement_date` DESC, m.`id` DESC LIMIT 400",
                "No warehouse work yet — post stock movements first.",
                Stock),

            ["wh_open_waves"] = new(
                ["Type", "Lines", "Qty", "Value", "Last"],
                "SELECT m.`movement_type`, COUNT(m.`id`), IFNULL(SUM(m.`qty`),0)," +
                " IFNULL(SUM(m.`total_cost`),0), FROM_UNIXTIME(IFNULL(MAX(m.`movement_date`),0))" +
                MoveJoin +
                " WHERE m.`active` = 1 GROUP BY m.`movement_type` ORDER BY 4 DESC",
                "No work lines yet.",
                Stock),

            ["wh_containers"] = new(
                ["Batch", "SKU", "Item", "Warehouse", "Qty", "Expiry"],
                "SELECT st.`batch_no`, i.`sku`, i.`name`, IFNULL(w.`name`,'(none)')," +
                " st.`qty_on_hand`, IFNULL(st.`expiry_date`,'')" +
                StockJoin +
                " WHERE st.`batch_no` IS NOT NULL AND st.`batch_no` <> ''" +
                " ORDER BY st.`batch_no`, i.`sku` LIMIT 300",
                "No batch/lp containers in stock — receive against a batch to see them.",
                Stock),

            ["wh_mobile_log"] = new(
                ["Worker", "Type", "SKU", "Qty", "Reference", "Date"],
                "SELECT m.`admin_id`, m.`movement_type`, i.`sku`, m.`qty`," +
                " IFNULL(m.`reference`,''), FROM_UNIXTIME(m.`movement_date`)" +
                MoveJoin +
                " WHERE m.`active` = 1 ORDER BY m.`id` DESC LIMIT 300",
                "No device transactions logged yet.",
                Stock),

            ["wh_cc_plans"] = new(
                ["Period end", "Warehouse", "SKU", "Qty", "Avg cost", "Value"],
                "SELECT c.`period_end`, IFNULL(w.`name`,'(none)'), i.`sku`," +
                " c.`qty_closing`, c.`avg_unit_cost`, c.`value_closing`" +
                " FROM `epc_erp_inv_closing` c" +
                " LEFT JOIN `epc_erp_inv_items` i ON i.`id` = c.`item_id`" +
                " LEFT JOIN `epc_erp_inv_warehouses` w ON w.`id` = c.`warehouse_id`" +
                " ORDER BY c.`period_end` DESC LIMIT 300",
                "No count plans — run an inventory close to snapshot counts.",
                Stock),

            ["wh_open_cc"] = new(
                ["Period end", "Warehouse", "Lines", "Qty", "Value"],
                "SELECT c.`period_end`, IFNULL(w.`name`,'(none)'), COUNT(c.`id`)," +
                " IFNULL(SUM(c.`qty_closing`),0), IFNULL(SUM(c.`value_closing`),0)" +
                " FROM `epc_erp_inv_closing` c" +
                " LEFT JOIN `epc_erp_inv_warehouses` w ON w.`id` = c.`warehouse_id`" +
                " GROUP BY c.`period_end`, w.`name` ORDER BY c.`period_end` DESC LIMIT 200",
                "No open counts.",
                Stock),

            ["wh_closed_cc"] = new(
                ["Period end", "Warehouse", "Lines", "Qty", "Value"],
                "SELECT c.`period_end`, IFNULL(w.`name`,'(none)'), COUNT(c.`id`)," +
                " IFNULL(SUM(c.`qty_closing`),0), IFNULL(SUM(c.`value_closing`),0)" +
                " FROM `epc_erp_inv_closing` c" +
                " LEFT JOIN `epc_erp_inv_warehouses` w ON w.`id` = c.`warehouse_id`" +
                " GROUP BY c.`period_end`, w.`name` ORDER BY c.`period_end` DESC LIMIT 200",
                "No closed counts.",
                Stock),

            ["wh_gs1"] = new(
                ["SKU", "Item", "Barcode", "Unit", "Type"],
                "SELECT i.`sku`, i.`name`, i.`barcode`, i.`unit`, i.`item_type`" +
                " FROM `epc_erp_inv_items` i" +
                " WHERE IFNULL(i.`active`,1) = 1 AND i.`barcode` <> ''" +
                " ORDER BY i.`sku` LIMIT 300",
                "No barcoded items — set barcodes on items to print GS1 labels.",
                Stock),

            ["wh_replenish"] = new(
                ["SKU", "Item", "On hand", "Reorder level", "Suggested", "Unit"],
                "SELECT i.`sku`, i.`name`, IFNULL(SUM(st.`qty_on_hand`),0)," +
                " i.`reorder_level`," +
                " GREATEST(i.`reorder_level` * 2 - IFNULL(SUM(st.`qty_on_hand`),0), 0)," +
                " i.`unit`" +
                " FROM `epc_erp_inv_items` i" +
                " LEFT JOIN `epc_erp_inv_stock` st ON st.`item_id` = i.`id`" +
                " WHERE IFNULL(i.`active`,1) = 1 AND i.`reorder_level` > 0" +
                " GROUP BY i.`id` HAVING IFNULL(SUM(st.`qty_on_hand`),0) <= i.`reorder_level`" +
                " ORDER BY i.`sku` LIMIT 200",
                "Nothing below reorder level — or no reorder levels set.",
                Forecast),

            ["wh_zones"] = new(
                ["Warehouse", "On-hand lines", "Qty", "Value"],
                "SELECT w.`name`, COUNT(st.`id`), IFNULL(SUM(st.`qty_on_hand`),0)," +
                " ROUND(IFNULL(SUM(st.`qty_on_hand` * st.`avg_unit_cost`),0), 2)" +
                StockJoin +
                " GROUP BY w.`id` ORDER BY w.`name` LIMIT 200",
                "No stock zones — receive stock to see per-warehouse totals.",
                Report),

            ["wh_loc_profiles"] = new(
                ["Code", "Warehouse", "Stocked lines", "On hand"],
                "SELECT w.`code`, w.`name`, COUNT(st.`id`), IFNULL(SUM(st.`qty_on_hand`),0)" +
                WhJoin +
                " LEFT JOIN `epc_erp_inv_stock` st ON st.`warehouse_id` = w.`id`" +
                " GROUP BY w.`id` ORDER BY w.`code` LIMIT 200",
                "No location profiles — warehouses carry the default location set.",
                Stock),

            ["wh_load_inq"] = new(
                ["Date", "Lines", "Qty", "Value", "Warehouses"],
                "SELECT DATE_FORMAT(FROM_UNIXTIME(m.`movement_date`),'%Y-%m-%d')," +
                " COUNT(m.`id`), IFNULL(SUM(m.`qty`),0), IFNULL(SUM(m.`total_cost`),0)," +
                " COUNT(DISTINCT m.`warehouse_id`)" +
                MoveJoin +
                " WHERE m.`active` = 1 GROUP BY 1 ORDER BY 1 DESC LIMIT 120",
                "No loads — movements group into daily loads here.",
                Report),
        };

    /// <summary>Lazy-creates the warehouse schema the specs read (idempotent).</summary>
    internal static Task EnsureWarehouseAsync(
        System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
        => ErpLazySchema.EnsureInventoryAsync(connection, cancellationToken);
}
