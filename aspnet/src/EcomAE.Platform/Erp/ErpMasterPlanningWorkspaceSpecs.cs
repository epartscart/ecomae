using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Master-planning workspace tabs — coverage, demand/supply and master data
/// views over the real inventory, reservation, customer and supplier tables.
/// </summary>
public static class ErpMasterPlanningWorkspaceSpecs
{
    private const string Forecast = "/erp/inventory-forecast-app";
    private const string Customers = "/erp/customers-app";
    private const string Suppliers = "/erp/suppliers-app";

    public static readonly IReadOnlyDictionary<string, ErpWorkspaceSpec> Tabs =
        new Dictionary<string, ErpWorkspaceSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["mp_items"] = new(
                ["SKU", "Item", "Type", "Unit", "On hand", "Reorder"],
                "SELECT i.`sku`, i.`name`, i.`item_type`, i.`unit`," +
                " IFNULL((SELECT SUM(st.`qty_on_hand`) FROM `epc_erp_inv_stock` st" +
                "  WHERE st.`item_id` = i.`id`),0), i.`reorder_level`" +
                " FROM `epc_erp_inv_items` i WHERE IFNULL(i.`active`,1) = 1" +
                " ORDER BY i.`sku` LIMIT 300",
                "No items — upload products in PIM first.",
                Forecast),

            ["mp_vendors"] = new(
                ["Vendor", "Group", "Currency", "Country", "Terms"],
                "SELECT s.`name`, IFNULL(NULLIF(s.`vendor_group`,''),'(none)')," +
                " 'AED', IFNULL(s.`country_code`,'')," +
                " IFNULL(s.`payment_terms`,'')" +
                " FROM `epc_erp_suppliers` s WHERE IFNULL(s.`active`,1) = 1" +
                " ORDER BY s.`name` LIMIT 300",
                "No vendors in the supply plan.",
                Suppliers),

            ["mp_customers"] = new(
                ["Customer", "Email", "Created"],
                "SELECT c.`name`, IFNULL(c.`email`,''), IFNULL(c.`created_at_utc`,'')" +
                " FROM `epc_erp_customers` c ORDER BY c.`name` LIMIT 300",
                "No customers in the demand plan.",
                Customers),

            ["mp_run"] = new(
                ["SKU", "Item", "On hand", "Reserved", "Net", "Reorder", "Action"],
                "SELECT i.`sku`, i.`name`," +
                " IFNULL((SELECT SUM(st.`qty_on_hand`) FROM `epc_erp_inv_stock` st" +
                "  WHERE st.`item_id` = i.`id`),0)," +
                " IFNULL((SELECT SUM(r.`qty`) FROM `epc_erp_inv_reservations` r" +
                "  WHERE r.`item_id` = i.`id` AND r.`status` = 'held'),0)," +
                " IFNULL((SELECT SUM(st.`qty_on_hand`) FROM `epc_erp_inv_stock` st" +
                "  WHERE st.`item_id` = i.`id`),0)" +
                "  - IFNULL((SELECT SUM(r.`qty`) FROM `epc_erp_inv_reservations` r" +
                "  WHERE r.`item_id` = i.`id` AND r.`status` = 'held'),0)," +
                " i.`reorder_level`," +
                " IF(IFNULL((SELECT SUM(st.`qty_on_hand`) FROM `epc_erp_inv_stock` st" +
                "  WHERE st.`item_id` = i.`id`),0)" +
                "  - IFNULL((SELECT SUM(r.`qty`) FROM `epc_erp_inv_reservations` r" +
                "  WHERE r.`item_id` = i.`id` AND r.`status` = 'held'),0)" +
                "  <= i.`reorder_level` AND i.`reorder_level` > 0, 'replenish', 'ok')" +
                " FROM `epc_erp_inv_items` i WHERE IFNULL(i.`active`,1) = 1" +
                " ORDER BY 7 DESC, i.`sku` LIMIT 300",
                "No plan to run — add items and stock first.",
                Forecast),

            ["mp_vendor_groups"] = new(
                ["Group", "Vendors", "Spend"],
                "SELECT IFNULL(NULLIF(s.`vendor_group`,''),'(none)'), COUNT(s.`id`)," +
                " IFNULL((SELECT SUM(p.`total_amount`) FROM `epc_erp_purchase_orders` p" +
                "  JOIN `epc_erp_suppliers` s2 ON s2.`id` = p.`supplier_id`" +
                "  WHERE IFNULL(NULLIF(s2.`vendor_group`,''),'(none)')" +
                "    = IFNULL(NULLIF(s.`vendor_group`,''),'(none)')),0)" +
                " FROM `epc_erp_suppliers` s WHERE IFNULL(s.`active`,1) = 1" +
                " GROUP BY 1 ORDER BY 3 DESC LIMIT 100",
                "No vendor groups.",
                Suppliers),

            ["mp_customer_groups"] = new(
                ["Party type", "Contacts"],
                "SELECT ct.`party_type`, COUNT(ct.`id`)" +
                " FROM `epc_erp_contacts` ct GROUP BY ct.`party_type` LIMIT 50",
                "No contacts classified yet.",
                Customers),

            ["mp_sequences"] = new(
                ["Document", "Next number", "Latest"],
                "SELECT 'purchase_order', IFNULL(MAX(p.`id`),0) + 1," +
                " IFNULL(MAX(p.`po_no`),'')" +
                " FROM `epc_erp_purchase_orders` p" +
                " UNION ALL SELECT 'work_order', IFNULL(MAX(w.`id`),0) + 1," +
                " IFNULL(MAX(w.`wo_no`),'') FROM `epc_mfg_work_orders` w",
                "No number sequences — documents number themselves.",
                null),
        };

    /// <summary>Lazy-creates every table the master-planning specs read.</summary>
    internal static async Task EnsurePlanningAsync(DbConnection c, CancellationToken ct)
    {
        await ErpLazySchema.EnsureInventoryAsync(c, ct).ConfigureAwait(false);
        await ErpLazySchema.EnsureSuppliersAsync(c, ct).ConfigureAwait(false);
        await ErpInventoryReservationWriteService.EnsureSchemaAsync(c, ct).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_erp_customers` (`id` int(11) NOT NULL AUTO_INCREMENT," +
            " `name` varchar(160) NOT NULL DEFAULT '', `email` varchar(160) NOT NULL DEFAULT ''," +
            " `created_at_utc` datetime DEFAULT NULL, PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8", ct)
            .ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(c,
            "CREATE TABLE IF NOT EXISTS `epc_erp_contacts` (`id` int(11) NOT NULL AUTO_INCREMENT," +
            " `party_type` varchar(16) NOT NULL DEFAULT 'customer', `name` varchar(160) NOT NULL DEFAULT ''," +
            " PRIMARY KEY (`id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8", ct).ConfigureAwait(false);
    }
}
