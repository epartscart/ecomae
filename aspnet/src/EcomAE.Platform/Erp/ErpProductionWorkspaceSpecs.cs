using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Production-control workspace tabs backed by the real manufacturing tables
/// (epc_mfg_work_orders, epc_mfg_bom, epc_mfg_bom_lines joined to inv_items).
/// Statuses: planned → in_progress → completed.
/// </summary>
public static class ErpProductionWorkspaceSpecs
{
    private const string Overview = "/erp/production-overview-app";

    private const string WoJoin =
        " FROM `epc_mfg_work_orders` wo" +
        " LEFT JOIN `epc_erp_inv_items` i ON i.`id` = wo.`product_item_id`" +
        " LEFT JOIN `epc_erp_inv_warehouses` w ON w.`id` = wo.`warehouse_id`";

    private const string WoCols =
        "SELECT wo.`wo_no`, IFNULL(i.`sku`,''), IFNULL(i.`name`,''), wo.`qty_planned`," +
        " wo.`qty_produced`, IFNULL(w.`name`,'(none)'), wo.`status`," +
        " FROM_UNIXTIME(wo.`time_created`)";

    public static readonly IReadOnlyDictionary<string, ErpWorkspaceSpec> Tabs =
        new Dictionary<string, ErpWorkspaceSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["prd_jobs_activities"] = new(
                ["WO", "SKU", "Product", "Planned", "Produced", "Warehouse", "Status", "Created"],
                WoCols + WoJoin + " ORDER BY wo.`id` DESC LIMIT 300",
                "No production jobs — create a work order on the production floor.",
                Overview),

            ["prd_planned"] = new(
                ["WO", "SKU", "Product", "Planned", "Produced", "Warehouse", "Status", "Created"],
                WoCols + WoJoin + " WHERE wo.`status` = 'planned'" +
                " ORDER BY wo.`id` DESC LIMIT 300",
                "No planned orders — firm a planned production order first.",
                Overview),

            ["prd_started"] = new(
                ["WO", "SKU", "Product", "Planned", "Produced", "Warehouse", "Status", "Created"],
                WoCols + WoJoin + " WHERE wo.`status` = 'in_progress'" +
                " ORDER BY wo.`id` DESC LIMIT 300",
                "Nothing started on the floor.",
                Overview),

            ["prd_released"] = new(
                ["WO", "SKU", "Product", "Planned", "Produced", "Warehouse", "Status", "Created"],
                WoCols + WoJoin + " WHERE wo.`status` IN ('planned','in_progress')" +
                " ORDER BY wo.`id` DESC LIMIT 300",
                "No released orders.",
                Overview),

            ["prd_scheduled"] = new(
                ["WO", "SKU", "Product", "Planned", "Warehouse", "Status", "Updated"],
                "SELECT wo.`wo_no`, IFNULL(i.`sku`,''), IFNULL(i.`name`,''), wo.`qty_planned`," +
                " IFNULL(w.`name`,'(none)'), wo.`status`, FROM_UNIXTIME(wo.`time_updated`)" +
                WoJoin + " WHERE wo.`status` <> 'completed'" +
                " ORDER BY wo.`time_updated` DESC LIMIT 300",
                "No scheduled orders.",
                Overview),

            ["prd_delayed"] = new(
                ["WO", "SKU", "Product", "Planned", "Produced", "Status", "Days open"],
                "SELECT wo.`wo_no`, IFNULL(i.`sku`,''), IFNULL(i.`name`,''), wo.`qty_planned`," +
                " wo.`qty_produced`, wo.`status`," +
                " DATEDIFF(NOW(), FROM_UNIXTIME(wo.`time_created`))" +
                WoJoin + " WHERE wo.`status` <> 'completed'" +
                " ORDER BY wo.`time_created` LIMIT 300",
                "No open orders — nothing can be delayed.",
                Overview),

            ["prd_deferred"] = new(
                ["WO", "SKU", "Product", "Planned", "Produced", "Status", "Created"],
                "SELECT wo.`wo_no`, IFNULL(i.`sku`,''), IFNULL(i.`name`,''), wo.`qty_planned`," +
                " wo.`qty_produced`, wo.`status`, FROM_UNIXTIME(wo.`time_created`)" +
                WoJoin + " WHERE wo.`status` = 'planned' AND wo.`qty_produced` = 0" +
                " ORDER BY wo.`id` LIMIT 300",
                "No orders waiting to post.",
                Overview),

            ["prd_job_card"] = new(
                ["WO", "SKU", "Product", "Planned", "Produced", "Material", "Labour", "Overhead"],
                "SELECT wo.`wo_no`, IFNULL(i.`sku`,''), IFNULL(i.`name`,''), wo.`qty_planned`," +
                " wo.`qty_produced`, wo.`material_cost`, wo.`labour_cost`, wo.`overhead_cost`" +
                WoJoin + " ORDER BY wo.`id` DESC LIMIT 300",
                "No job cards — issue a work order to generate one.",
                Overview),

            ["prd_wip"] = new(
                ["WO", "SKU", "Product", "Planned", "Produced", "WIP value"],
                "SELECT wo.`wo_no`, IFNULL(i.`sku`,''), IFNULL(i.`name`,''), wo.`qty_planned`," +
                " wo.`qty_produced`," +
                " wo.`material_cost` + wo.`labour_cost` + wo.`overhead_cost`" +
                WoJoin + " WHERE wo.`status` = 'in_progress'" +
                " ORDER BY 6 DESC LIMIT 300",
                "No work in progress.",
                Overview),

            ["prd_batch_cost"] = new(
                ["WO", "SKU", "Product", "Produced", "Total cost", "Unit cost"],
                "SELECT wo.`wo_no`, IFNULL(i.`sku`,''), IFNULL(i.`name`,''), wo.`qty_produced`," +
                " wo.`material_cost` + wo.`labour_cost` + wo.`overhead_cost`," +
                " IF(wo.`qty_produced` > 0," +
                "   (wo.`material_cost` + wo.`labour_cost` + wo.`overhead_cost`) / wo.`qty_produced`, 0)" +
                WoJoin + " WHERE wo.`status` = 'completed'" +
                " ORDER BY wo.`id` DESC LIMIT 300",
                "No completed orders to cost yet.",
                Overview),

            ["prd_estimate"] = new(
                ["BOM", "Product", "Output qty", "Lines", "Labour", "Overhead"],
                "SELECT b.`name`, IFNULL(i.`sku`,''), b.`output_qty`, COUNT(l.`id`)," +
                " b.`labour_cost`, b.`overhead_cost`" +
                " FROM `epc_mfg_bom` b" +
                " LEFT JOIN `epc_erp_inv_items` i ON i.`id` = b.`product_item_id`" +
                " LEFT JOIN `epc_mfg_bom_lines` l ON l.`bom_id` = b.`id`" +
                " WHERE b.`active` = 1 GROUP BY b.`id` ORDER BY b.`id` DESC LIMIT 300",
                "No BOMs — create a bill of materials to estimate costs.",
                Overview),

            ["prd_picking_list"] = new(
                ["BOM", "Component", "Qty per", "Scrap %"],
                "SELECT b.`name`, IFNULL(c.`sku`,''), l.`qty_per`, l.`scrap_percent`" +
                " FROM `epc_mfg_bom_lines` l" +
                " JOIN `epc_mfg_bom` b ON b.`id` = l.`bom_id`" +
                " LEFT JOIN `epc_erp_inv_items` c ON c.`id` = l.`component_item_id`" +
                " WHERE b.`active` = 1 ORDER BY b.`id`, l.`id` LIMIT 400",
                "No picking lines — add components to a BOM.",
                Overview),

            ["prd_kanbans"] = new(
                ["WO", "SKU", "Product", "Planned", "Produced", "Status"],
                "SELECT wo.`wo_no`, IFNULL(i.`sku`,''), IFNULL(i.`name`,'')," +
                " wo.`qty_planned`, wo.`qty_produced`, wo.`status`" +
                WoJoin + " ORDER BY wo.`status`, wo.`id` DESC LIMIT 300",
                "No kanban cards — work orders appear here once created.",
                Overview),

            ["prd_performance"] = new(
                ["Month", "Orders", "Completed", "Qty produced", "Total cost"],
                "SELECT DATE_FORMAT(FROM_UNIXTIME(wo.`time_created`),'%Y-%m')," +
                " COUNT(wo.`id`), SUM(wo.`status` = 'completed')," +
                " IFNULL(SUM(wo.`qty_produced`),0)," +
                " IFNULL(SUM(wo.`material_cost` + wo.`labour_cost` + wo.`overhead_cost`),0)" +
                WoJoin + " GROUP BY 1 ORDER BY 1 DESC LIMIT 24",
                "No production history yet.",
                Overview),

            ["prd_stockout"] = new(
                ["Component", "SKU", "Required by BOM", "On hand", "Shortfall"],
                "SELECT IFNULL(c.`name`,''), IFNULL(c.`sku`,''), IFNULL(SUM(l.`qty_per`),0)," +
                " IFNULL((SELECT SUM(st.`qty_on_hand`) FROM `epc_erp_inv_stock` st" +
                "  WHERE st.`item_id` = c.`id`),0)," +
                " GREATEST(IFNULL(SUM(l.`qty_per`),0) -" +
                "  IFNULL((SELECT SUM(st.`qty_on_hand`) FROM `epc_erp_inv_stock` st" +
                "   WHERE st.`item_id` = c.`id`),0), 0)" +
                " FROM `epc_mfg_bom_lines` l" +
                " JOIN `epc_mfg_bom` b ON b.`id` = l.`bom_id` AND b.`active` = 1" +
                " LEFT JOIN `epc_erp_inv_items` c ON c.`id` = l.`component_item_id`" +
                " GROUP BY c.`id` ORDER BY 5 DESC LIMIT 300",
                "No BOM demand — all components covered or no BOMs defined.",
                Overview),
        };

    /// <summary>Lazy-creates the manufacturing tables the specs read (idempotent).</summary>
    internal static async Task EnsureProductionAsync(DbConnection c, CancellationToken ct)
    {
        await ErpLazySchema.EnsureInventoryAsync(c, ct).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_mfg_bom` (`id` int(11) NOT NULL AUTO_INCREMENT," +
            " `product_item_id` int(11) NOT NULL, `name` varchar(128) NOT NULL DEFAULT ''," +
            " `output_qty` decimal(14,3) NOT NULL DEFAULT 1.000, `labour_cost` decimal(14,2) NOT NULL DEFAULT 0.00," +
            " `overhead_cost` decimal(14,2) NOT NULL DEFAULT 0.00, `active` tinyint(1) NOT NULL DEFAULT 1," +
            " `time_created` int(11) NOT NULL DEFAULT 0, PRIMARY KEY (`id`)," +
            " KEY `x_product` (`product_item_id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8", ct).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_mfg_bom_lines` (`id` int(11) NOT NULL AUTO_INCREMENT," +
            " `bom_id` int(11) NOT NULL, `component_item_id` int(11) NOT NULL," +
            " `qty_per` decimal(14,4) NOT NULL DEFAULT 0.0000, `scrap_percent` decimal(6,2) NOT NULL DEFAULT 0.00," +
            " PRIMARY KEY (`id`), KEY `x_bom` (`bom_id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8", ct).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_mfg_work_orders` (`id` int(11) NOT NULL AUTO_INCREMENT," +
            " `wo_no` varchar(32) NOT NULL, `bom_id` int(11) NOT NULL DEFAULT 0," +
            " `product_item_id` int(11) NOT NULL, `qty_planned` decimal(14,3) NOT NULL DEFAULT 0.000," +
            " `qty_produced` decimal(14,3) NOT NULL DEFAULT 0.000, `warehouse_id` int(11) NOT NULL DEFAULT 0," +
            " `status` varchar(16) NOT NULL DEFAULT 'planned'," +
            " `material_cost` decimal(14,2) NOT NULL DEFAULT 0.00, `labour_cost` decimal(14,2) NOT NULL DEFAULT 0.00," +
            " `overhead_cost` decimal(14,2) NOT NULL DEFAULT 0.00," +
            " `time_created` int(11) NOT NULL DEFAULT 0, `time_updated` int(11) NOT NULL DEFAULT 0," +
            " PRIMARY KEY (`id`), KEY `x_status` (`status`)) ENGINE=InnoDB DEFAULT CHARSET=utf8", ct).ConfigureAwait(false);
    }
}
