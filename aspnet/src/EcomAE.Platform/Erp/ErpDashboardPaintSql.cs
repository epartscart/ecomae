namespace EcomAE.Platform.Erp;

/// <summary>
/// Same dashboard columns as <c>SelectErpDashboardSummaryBatch</c>, but each
/// subquery is omitted when that table — or any column it touches — is not in
/// the tenant. A missing table or a diverged column otherwise fails the whole
/// statement and the page then walks dozens of failing scalars.
/// </summary>
public static class ErpDashboardPaintSql
{
    /// <summary>Tables the batch can touch; probed once via <see cref="ErpTableCatalog.LoadColumnsAsync"/>.</summary>
    public static readonly IReadOnlyCollection<string> BatchTables = new[]
    {
        "epc_erp_cash_bank_accounts",
        "epc_erp_cash_bank_entries",
        "epc_erp_supplier_accounting",
        "epc_erp_suppliers",
        "epc_erp_purchases",
        "epc_invoices",
        "epc_bills",
        "epc_erp_inv_stock",
        "epc_inventory_stock",
        "shop_orders",
        "shop_users_accounting",
        "epc_erp_inv_items",
        "epc_erp_periods",
        "epc_erp_sales_orders",
        "epc_erp_purchase_orders",
        "epc_erp_gl_journals",
        "epc_erp_sales_invoices",
        "epc_einvoice_documents",
        "epc_pf_cases",
    };

    public static string Batch(
        IReadOnlySet<string> tables,
        bool includeShop,
        bool includePeriod,
        IReadOnlySet<string>? columns = null)
    {
        bool Has(string name) => tables.Contains(name);
        bool Cols(string table, params string[] cols)
            => columns is null || cols.All(c => columns.Contains(table + "." + c));

        var shopRevenue = includeShop && Has("shop_orders")
            && Cols("shop_orders", "successfully_created", "price_total_wt", "price_total_wt_vat", "time");
        var shopCount = includeShop && Has("shop_orders")
            && Cols("shop_orders", "successfully_created", "time");
        var shopVat = includeShop && Has("shop_orders")
            && Cols("shop_orders", "successfully_created", "price_total_wt_vat", "time");

        var cash = Has("epc_erp_cash_bank_accounts") && Cols("epc_erp_cash_bank_accounts", "id", "opening_balance", "active")
            ? Has("epc_erp_cash_bank_entries") && Cols("epc_erp_cash_bank_entries", "account_id", "direction", "amount", "active")
                ? """
                    (
                        SELECT IFNULL(SUM(a.`opening_balance`
                                + IFNULL(x.in_amt, 0) - IFNULL(x.out_amt, 0)), 0)
                        FROM `epc_erp_cash_bank_accounts` a
                        LEFT JOIN (
                            SELECT `account_id`,
                                SUM(CASE WHEN `direction` = 1 THEN `amount` ELSE 0 END) AS in_amt,
                                SUM(CASE WHEN `direction` = 0 THEN `amount` ELSE 0 END) AS out_amt
                            FROM `epc_erp_cash_bank_entries`
                            WHERE `active` = 1
                            GROUP BY `account_id`
                        ) x ON x.`account_id` = a.`id`
                        WHERE a.`active` = 1
                    )
                    """
                : "(SELECT IFNULL(SUM(a.`opening_balance`), 0) FROM `epc_erp_cash_bank_accounts` a WHERE a.`active` = 1)"
            : "0";

        var erpStock = Has("epc_erp_inv_stock") && Cols("epc_erp_inv_stock", "qty_on_hand", "avg_unit_cost");
        var legacyStock = Has("epc_inventory_stock") && Cols("epc_inventory_stock", "quantity", "avg_cost");
        var stock = erpStock && legacyStock
            // Match the legacy batch: populated ERP stock wins, else fall back to
            // the legacy table (an empty ERP table must not hide legacy rows).
            ? """
                (
                    SELECT IF(
                        EXISTS(SELECT 1 FROM `epc_erp_inv_stock` LIMIT 1),
                        (SELECT COALESCE(SUM(`qty_on_hand` * `avg_unit_cost`), 0) FROM `epc_erp_inv_stock`),
                        (SELECT COALESCE(SUM(`quantity` * `avg_cost`), 0) FROM `epc_inventory_stock`)
                    )
                )
                """
            : erpStock
                ? "(SELECT COALESCE(SUM(`qty_on_hand` * `avg_unit_cost`), 0) FROM `epc_erp_inv_stock`)"
                : legacyStock
                    ? "(SELECT COALESCE(SUM(`quantity` * `avg_cost`), 0) FROM `epc_inventory_stock`)"
                    : "0";

        var lowStock = Has("epc_erp_inv_stock") && Has("epc_erp_inv_items")
            && Cols("epc_erp_inv_stock", "qty_on_hand", "item_id")
            && Cols("epc_erp_inv_items", "id", "active", "reorder_level")
            ? """
                (
                    SELECT COUNT(*) FROM `epc_erp_inv_stock` s
                    INNER JOIN `epc_erp_inv_items` i ON i.`id` = s.`item_id` AND i.`active` = 1
                    WHERE i.`reorder_level` > 0 AND s.`qty_on_hand` > 0 AND s.`qty_on_hand` <= i.`reorder_level`
                )
                """
            : "0";

        var period = includePeriod && Has("epc_erp_periods") && Cols("epc_erp_periods", "year_month", "status")
            ? "IFNULL((SELECT `status` FROM `epc_erp_periods` WHERE `year_month` = @periodKey LIMIT 1), 'open')"
            : "'open'";

        var unpostedGl = Has("epc_erp_gl_journals") && Cols("epc_erp_gl_journals", "status", "active")
            ? "(SELECT COUNT(*) FROM `epc_erp_gl_journals` WHERE `status` = 'draft' AND `active` = 1)"
            : "0";

        return "SELECT "
            + cash + " AS cash_position, "
            + ScalarSum("epc_erp_supplier_accounting",
                Has("epc_erp_supplier_accounting") && Cols("epc_erp_supplier_accounting", "amount", "active", "is_credit"),
                "`active` = 1 AND `is_credit` = 1") + " AS supplier_credit, "
            + ScalarSum("epc_erp_supplier_accounting",
                Has("epc_erp_supplier_accounting") && Cols("epc_erp_supplier_accounting", "amount", "active", "is_credit"),
                "`active` = 1 AND `is_credit` = 0") + " AS supplier_debit, "
            + ScalarCount("epc_erp_cash_bank_accounts", Has("epc_erp_cash_bank_accounts") && Cols("epc_erp_cash_bank_accounts", "active"), "`active` = 1") + " AS cash_accounts, "
            + ScalarCount("epc_erp_suppliers", Has("epc_erp_suppliers") && Cols("epc_erp_suppliers", "active"), "`active` = 1") + " AS active_suppliers, "
            + ScalarCount("epc_erp_purchases", Has("epc_erp_purchases") && Cols("epc_erp_purchases", "active"), "`active` = 1") + " AS active_purchases, "
            + (Has("epc_invoices") && Cols("epc_invoices", "total_amount", "paid_amount", "status")
                ? "(SELECT COALESCE(SUM(`total_amount` - `paid_amount`), 0) FROM `epc_invoices` WHERE `status` <> 'paid')"
                : "0") + " AS receivables, "
            + (Has("epc_bills") && Cols("epc_bills", "total_amount", "paid_amount", "status")
                ? "(SELECT COALESCE(SUM(`total_amount` - `paid_amount`), 0) FROM `epc_bills` WHERE `status` <> 'paid')"
                : "0") + " AS payables, "
            + stock + " AS stock_value, "
            + (shopRevenue
                ? "(SELECT IFNULL(SUM(CASE WHEN `successfully_created` = 1 THEN `price_total_wt` - `price_total_wt_vat` ELSE 0 END), 0) FROM `shop_orders` WHERE `time` >= @dateFrom AND `time` <= @dateTo)"
                : "0") + " AS revenue_ex_vat, "
            + (shopCount
                ? "(SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= @dateFrom AND `time` <= @dateTo)"
                : "0") + " AS orders_count, "
            + (Has("shop_users_accounting") && Cols("shop_users_accounting", "income", "amount", "active")
                ? "(SELECT IFNULL(SUM(CASE WHEN `income` = 1 THEN `amount` ELSE -`amount` END), 0) FROM `shop_users_accounting` WHERE `active` = 1)"
                : "0") + " AS ar_balance, "
            + (Has("epc_erp_suppliers") && Cols("epc_erp_suppliers", "balance", "active")
                ? "(SELECT IFNULL(SUM(`balance`), 0) FROM `epc_erp_suppliers` WHERE `active` = 1)"
                : "0") + " AS ap_balance, "
            + (shopVat
                ? "(SELECT IFNULL(SUM(`price_total_wt_vat`), 0) FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= @dateFrom AND `time` <= @dateTo)"
                : "0") + " AS vat_out, "
            + (Has("epc_erp_purchases") && Cols("epc_erp_purchases", "vat_amount", "active", "purchase_date")
                ? "(SELECT IFNULL(SUM(`vat_amount`), 0) FROM `epc_erp_purchases` WHERE `active` = 1 AND `purchase_date` >= @dateFrom AND `purchase_date` <= @dateTo)"
                : "0") + " AS vat_in, "
            + ScalarCount("epc_erp_inv_items", Has("epc_erp_inv_items") && Cols("epc_erp_inv_items", "active"), "`active` = 1") + " AS inventory_items, "
            + period + " AS period_status, "
            + (Has("epc_erp_sales_orders") && Cols("epc_erp_sales_orders", "status")
                ? "(SELECT COUNT(*) FROM `epc_erp_sales_orders` WHERE `status` = 'draft')"
                : "0") + " AS draft_sales_orders, "
            + (Has("epc_erp_purchase_orders") && Cols("epc_erp_purchase_orders", "status")
                ? "(SELECT COUNT(*) FROM `epc_erp_purchase_orders` WHERE `status` IN ('draft', 'pending'))"
                : "0") + " AS pending_purchase_orders, "
            + unpostedGl + " AS unposted_gl_journals, "
            + (Has("epc_erp_sales_invoices") && Cols("epc_erp_sales_invoices", "status", "due_date")
                ? "(SELECT COUNT(*) FROM `epc_erp_sales_invoices` WHERE `status` = 'unpaid' AND `due_date` < @overdueBefore)"
                : "0") + " AS overdue_invoices, "
            + lowStock + " AS low_stock_items, "
            + (Has("epc_einvoice_documents") && Cols("epc_einvoice_documents", "status")
                ? "(SELECT COUNT(*) FROM `epc_einvoice_documents` WHERE `status` IN ('draft', 'queued'))"
                : "0") + " AS pending_einvoices, "
            + (Has("epc_pf_cases") && Cols("epc_pf_cases", "status")
                ? "(SELECT COUNT(*) FROM `epc_pf_cases` WHERE `status` = 'open')"
                : "0") + " AS process_open, "
            + (Has("epc_pf_cases") && Cols("epc_pf_cases", "status")
                ? "(SELECT COUNT(*) FROM `epc_pf_cases` WHERE `status` = 'done')"
                : "0") + " AS process_done, "
            + (Has("epc_pf_cases") && Cols("epc_pf_cases", "status", "due_at")
                ? "(SELECT COUNT(*) FROM `epc_pf_cases` WHERE `status` = 'open' AND `due_at` > 0 AND `due_at` < UNIX_TIMESTAMP())"
                : "0") + " AS process_overdue";
    }

    private static string ScalarCount(string table, bool present, string where)
        => present ? "(SELECT COUNT(*) FROM `" + table + "` WHERE " + where + ")" : "0";

    private static string ScalarSum(string table, bool present, string where)
        => present ? "(SELECT IFNULL(SUM(`amount`), 0) FROM `" + table + "` WHERE " + where + ")" : "0";
}
