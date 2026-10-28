namespace EcomAE.Platform.Erp;

/// <summary>
/// Same dashboard columns as <c>SelectErpDashboardSummaryBatch</c>, but each
/// subquery is omitted when that table is not in the tenant. One missing
/// shop or invoice table otherwise fails the whole statement and the page
/// then walks dozens of failing scalars.
/// </summary>
public static class ErpDashboardPaintSql
{
    public static string Batch(IReadOnlySet<string> tables, bool includeShop, bool includePeriod)
    {
        bool Has(string name) => tables.Contains(name);
        var shop = includeShop && Has("shop_orders");

        var cash = Has("epc_erp_cash_bank_accounts") && Has("epc_erp_cash_bank_entries")
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
            : Has("epc_erp_cash_bank_accounts")
                ? "(SELECT IFNULL(SUM(a.`opening_balance`), 0) FROM `epc_erp_cash_bank_accounts` a WHERE a.`active` = 1)"
                : "0";

        var stock = Has("epc_erp_inv_stock")
            ? "(SELECT COALESCE(SUM(`qty_on_hand` * `avg_unit_cost`), 0) FROM `epc_erp_inv_stock`)"
            : Has("epc_inventory_stock")
                ? "(SELECT COALESCE(SUM(`quantity` * `avg_cost`), 0) FROM `epc_inventory_stock`)"
                : "0";

        var lowStock = Has("epc_erp_inv_stock") && Has("epc_erp_inv_items")
            ? """
                (
                    SELECT COUNT(*) FROM `epc_erp_inv_stock` s
                    INNER JOIN `epc_erp_inv_items` i ON i.`id` = s.`item_id` AND i.`active` = 1
                    WHERE i.`reorder_level` > 0 AND s.`qty_on_hand` > 0 AND s.`qty_on_hand` <= i.`reorder_level`
                )
                """
            : "0";

        var period = includePeriod && Has("epc_erp_periods")
            ? "IFNULL((SELECT `status` FROM `epc_erp_periods` WHERE `year_month` = @periodKey LIMIT 1), 'open')"
            : "'open'";

        return "SELECT "
            + cash + " AS cash_position, "
            + ScalarSum("epc_erp_supplier_accounting", Has("epc_erp_supplier_accounting"), "`active` = 1 AND `is_credit` = 1") + " AS supplier_credit, "
            + ScalarSum("epc_erp_supplier_accounting", Has("epc_erp_supplier_accounting"), "`active` = 1 AND `is_credit` = 0") + " AS supplier_debit, "
            + ScalarCount("epc_erp_cash_bank_accounts", Has("epc_erp_cash_bank_accounts"), "`active` = 1") + " AS cash_accounts, "
            + ScalarCount("epc_erp_suppliers", Has("epc_erp_suppliers"), "`active` = 1") + " AS active_suppliers, "
            + ScalarCount("epc_erp_purchases", Has("epc_erp_purchases"), "`active` = 1") + " AS active_purchases, "
            + (Has("epc_invoices")
                ? "(SELECT COALESCE(SUM(`total_amount` - `paid_amount`), 0) FROM `epc_invoices` WHERE `status` <> 'paid')"
                : "0") + " AS receivables, "
            + (Has("epc_bills")
                ? "(SELECT COALESCE(SUM(`total_amount` - `paid_amount`), 0) FROM `epc_bills` WHERE `status` <> 'paid')"
                : "0") + " AS payables, "
            + stock + " AS stock_value, "
            + (shop
                ? "(SELECT IFNULL(SUM(CASE WHEN `successfully_created` = 1 THEN `price_total_wt` - `price_total_wt_vat` ELSE 0 END), 0) FROM `shop_orders` WHERE `time` >= @dateFrom AND `time` <= @dateTo)"
                : "0") + " AS revenue_ex_vat, "
            + (shop
                ? "(SELECT COUNT(*) FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= @dateFrom AND `time` <= @dateTo)"
                : "0") + " AS orders_count, "
            + (Has("shop_users_accounting")
                ? "(SELECT IFNULL(SUM(CASE WHEN `income` = 1 THEN `amount` ELSE -`amount` END), 0) FROM `shop_users_accounting` WHERE `active` = 1)"
                : "0") + " AS ar_balance, "
            + (Has("epc_erp_suppliers")
                ? "(SELECT IFNULL(SUM(`balance`), 0) FROM `epc_erp_suppliers` WHERE `active` = 1)"
                : "0") + " AS ap_balance, "
            + (shop
                ? "(SELECT IFNULL(SUM(`price_total_wt_vat`), 0) FROM `shop_orders` WHERE `successfully_created` = 1 AND `time` >= @dateFrom AND `time` <= @dateTo)"
                : "0") + " AS vat_out, "
            + (Has("epc_erp_purchases")
                ? "(SELECT IFNULL(SUM(`vat_amount`), 0) FROM `epc_erp_purchases` WHERE `active` = 1 AND `purchase_date` >= @dateFrom AND `purchase_date` <= @dateTo)"
                : "0") + " AS vat_in, "
            + ScalarCount("epc_erp_inv_items", Has("epc_erp_inv_items"), "`active` = 1") + " AS inventory_items, "
            + period + " AS period_status, "
            + (Has("epc_erp_sales_orders")
                ? "(SELECT COUNT(*) FROM `epc_erp_sales_orders` WHERE `status` = 'draft')"
                : "0") + " AS draft_sales_orders, "
            + (Has("epc_erp_purchase_orders")
                ? "(SELECT COUNT(*) FROM `epc_erp_purchase_orders` WHERE `status` IN ('draft', 'pending'))"
                : "0") + " AS pending_purchase_orders, "
            + (Has("epc_erp_gl_journals")
                ? "(SELECT COUNT(*) FROM `epc_erp_gl_journals` WHERE `status` = 'draft' AND `active` = 1)"
                : "0") + " AS unposted_gl_journals, "
            + (Has("epc_erp_sales_invoices")
                ? "(SELECT COUNT(*) FROM `epc_erp_sales_invoices` WHERE `status` = 'unpaid' AND `due_date` < @overdueBefore)"
                : "0") + " AS overdue_invoices, "
            + lowStock + " AS low_stock_items, "
            + (Has("epc_einvoice_documents")
                ? "(SELECT COUNT(*) FROM `epc_einvoice_documents` WHERE `status` IN ('draft', 'queued'))"
                : "0") + " AS pending_einvoices, "
            + (Has("epc_pf_cases")
                ? "(SELECT COUNT(*) FROM `epc_pf_cases` WHERE `status` = 'open')"
                : "0") + " AS process_open, "
            + (Has("epc_pf_cases")
                ? "(SELECT COUNT(*) FROM `epc_pf_cases` WHERE `status` = 'done')"
                : "0") + " AS process_done, "
            + (Has("epc_pf_cases")
                ? "(SELECT COUNT(*) FROM `epc_pf_cases` WHERE `status` = 'open' AND `due_at` > 0 AND `due_at` < UNIX_TIMESTAMP())"
                : "0") + " AS process_overdue";
    }

    private static string ScalarCount(string table, bool present, string where)
        => present ? "(SELECT COUNT(*) FROM `" + table + "` WHERE " + where + ")" : "0";

    private static string ScalarSum(string table, bool present, string where)
        => present ? "(SELECT IFNULL(SUM(`amount`), 0) FROM `" + table + "` WHERE " + where + ")" : "0";
}
