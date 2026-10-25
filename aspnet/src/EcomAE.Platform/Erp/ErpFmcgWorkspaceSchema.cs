using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Layer B tables for the FMCG industry workspace. Only industry-specific
/// extensions — products, COA, GL, warehouses, customers, suppliers, legal
/// entities, business units and cost centres stay in the existing core
/// (epc_erp_pm_* / epc_erp_gl_*) tables.
/// </summary>
public static class ErpFmcgWorkspaceSchema
{
    public const string Items = "epc_erp_fmcg_items";
    public const string Batches = "epc_erp_fmcg_batches";
    public const string Channels = "epc_erp_fmcg_channels";
    public const string Promotions = "epc_erp_fmcg_promotions";
    public const string Routes = "epc_erp_fmcg_routes";
    public const string DemoRegistry = "ecomae_fmcg_demo_registry";

    public static async Task EnsureAsync(DbConnection connection, CancellationToken cancellationToken = default)
    {
        foreach (var sql in Statements)
        {
            await ErpDb.TryExecuteAsync(connection, sql, cancellationToken).ConfigureAwait(false);
        }
    }

    private static readonly string[] Statements =
    [
        // SKU-level FMCG extension: GTIN/barcode, brand/category hierarchy,
        // pack-size conversions (case/carton/pallet), shelf-life + FEFO flags,
        // excise category for GCC excise, COA link for sales/COGS/inventory.
        "CREATE TABLE IF NOT EXISTS `epc_erp_fmcg_items` ("
        + " `id` bigint(20) NOT NULL AUTO_INCREMENT,"
        + " `sku` varchar(60) NOT NULL,"
        + " `name` varchar(255) NOT NULL DEFAULT '',"
        + " `brand` varchar(120) NOT NULL DEFAULT '',"
        + " `category` varchar(120) NOT NULL DEFAULT '',"
        + " `subcategory` varchar(120) NOT NULL DEFAULT '',"
        + " `gtin` varchar(20) NOT NULL DEFAULT '',"
        + " `base_uom` varchar(20) NOT NULL DEFAULT 'EA',"
        + " `units_per_case` int(11) NOT NULL DEFAULT 0,"
        + " `cases_per_pallet` int(11) NOT NULL DEFAULT 0,"
        + " `shelf_life_days` int(11) NOT NULL DEFAULT 0,"
        + " `batch_tracked` tinyint(1) NOT NULL DEFAULT 1,"
        + " `expiry_tracked` tinyint(1) NOT NULL DEFAULT 1,"
        + " `fefo` tinyint(1) NOT NULL DEFAULT 1,"
        + " `min_shelf_life_pct` int(11) NOT NULL DEFAULT 0,"
        + " `excise_category` varchar(40) NOT NULL DEFAULT '',"
        + " `coa_sales_id` bigint(20) NOT NULL DEFAULT 0,"
        + " `coa_cogs_id` bigint(20) NOT NULL DEFAULT 0,"
        + " `coa_inventory_id` bigint(20) NOT NULL DEFAULT 0,"
        + " `cost` decimal(16,4) NOT NULL DEFAULT 0,"
        + " `price` decimal(16,4) NOT NULL DEFAULT 0,"
        + " `channel` varchar(40) NOT NULL DEFAULT '',"
        + " `active` tinyint(1) NOT NULL DEFAULT 1,"
        + " `time_created` bigint(20) NOT NULL DEFAULT 0,"
        + " `time_modified` bigint(20) NOT NULL DEFAULT 0,"
        + " PRIMARY KEY (`id`), UNIQUE KEY `uq_fmcg_items_sku` (`sku`),"
        + " KEY `ix_fmcg_items_category` (`category`),"
        + " KEY `ix_fmcg_items_gtin` (`gtin`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        // Batch / lot genealogy with manufacturing + expiry dates.
        "CREATE TABLE IF NOT EXISTS `epc_erp_fmcg_batches` ("
        + " `id` bigint(20) NOT NULL AUTO_INCREMENT,"
        + " `item_id` bigint(20) NOT NULL,"
        + " `batch_no` varchar(60) NOT NULL DEFAULT '',"
        + " `mfg_date` bigint(20) NOT NULL DEFAULT 0,"
        + " `expiry_date` bigint(20) NOT NULL DEFAULT 0,"
        + " `qty_on_hand` decimal(16,4) NOT NULL DEFAULT 0,"
        + " `warehouse` varchar(80) NOT NULL DEFAULT '',"
        + " `status` varchar(20) NOT NULL DEFAULT 'available',"
        + " `recall_flag` tinyint(1) NOT NULL DEFAULT 0,"
        + " `time_created` bigint(20) NOT NULL DEFAULT 0,"
        + " `time_modified` bigint(20) NOT NULL DEFAULT 0,"
        + " PRIMARY KEY (`id`),"
        + " KEY `ix_fmcg_batches_item` (`item_id`),"
        + " KEY `ix_fmcg_batches_expiry` (`expiry_date`),"
        + " KEY `ix_fmcg_batches_recall` (`recall_flag`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        // FMCG channels: modern trade, HORECA, wholesale, van/route sales, retail.
        "CREATE TABLE IF NOT EXISTS `epc_erp_fmcg_channels` ("
        + " `id` bigint(20) NOT NULL AUTO_INCREMENT,"
        + " `code` varchar(40) NOT NULL,"
        + " `name` varchar(160) NOT NULL DEFAULT '',"
        + " `kind` varchar(40) NOT NULL DEFAULT '',"
        + " `sales_account_id` bigint(20) NOT NULL DEFAULT 0,"
        + " `active` tinyint(1) NOT NULL DEFAULT 1,"
        + " `time_created` bigint(20) NOT NULL DEFAULT 0,"
        + " `time_modified` bigint(20) NOT NULL DEFAULT 0,"
        + " PRIMARY KEY (`id`), UNIQUE KEY `uq_fmcg_channels_code` (`code`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        // Trade promotions / rebates: free goods, bonus qty, off-invoice, rebates.
        "CREATE TABLE IF NOT EXISTS `epc_erp_fmcg_promotions` ("
        + " `id` bigint(20) NOT NULL AUTO_INCREMENT,"
        + " `code` varchar(40) NOT NULL,"
        + " `name` varchar(160) NOT NULL DEFAULT '',"
        + " `kind` varchar(40) NOT NULL DEFAULT '',"
        + " `item_id` bigint(20) NOT NULL DEFAULT 0,"
        + " `channel_id` bigint(20) NOT NULL DEFAULT 0,"
        + " `buy_qty` decimal(16,4) NOT NULL DEFAULT 0,"
        + " `free_qty` decimal(16,4) NOT NULL DEFAULT 0,"
        + " `discount_pct` decimal(8,4) NOT NULL DEFAULT 0,"
        + " `accrual_account_id` bigint(20) NOT NULL DEFAULT 0,"
        + " `start_date` bigint(20) NOT NULL DEFAULT 0,"
        + " `end_date` bigint(20) NOT NULL DEFAULT 0,"
        + " `active` tinyint(1) NOT NULL DEFAULT 1,"
        + " `time_created` bigint(20) NOT NULL DEFAULT 0,"
        + " `time_modified` bigint(20) NOT NULL DEFAULT 0,"
        + " PRIMARY KEY (`id`), UNIQUE KEY `uq_fmcg_promotions_code` (`code`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        // Van-sales / route distribution: route, salesperson, vehicle stock.
        "CREATE TABLE IF NOT EXISTS `epc_erp_fmcg_routes` ("
        + " `id` bigint(20) NOT NULL AUTO_INCREMENT,"
        + " `code` varchar(40) NOT NULL,"
        + " `name` varchar(160) NOT NULL DEFAULT '',"
        + " `salesperson` varchar(160) NOT NULL DEFAULT '',"
        + " `vehicle` varchar(80) NOT NULL DEFAULT '',"
        + " `warehouse` varchar(80) NOT NULL DEFAULT '',"
        + " `active` tinyint(1) NOT NULL DEFAULT 1,"
        + " `time_created` bigint(20) NOT NULL DEFAULT 0,"
        + " `time_modified` bigint(20) NOT NULL DEFAULT 0,"
        + " PRIMARY KEY (`id`), UNIQUE KEY `uq_fmcg_routes_code` (`code`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        // Demo registry so sample data can be removed row-for-row (sandbox only).
        "CREATE TABLE IF NOT EXISTS `ecomae_fmcg_demo_registry` ("
        + " `id` bigint(20) NOT NULL AUTO_INCREMENT,"
        + " `table_name` varchar(80) NOT NULL DEFAULT '',"
        + " `row_id` bigint(20) NOT NULL DEFAULT 0,"
        + " `time_created` bigint(20) NOT NULL DEFAULT 0,"
        + " PRIMARY KEY (`id`),"
        + " KEY `ix_fmcg_demo_registry_table` (`table_name`,`row_id`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
    ];
}
