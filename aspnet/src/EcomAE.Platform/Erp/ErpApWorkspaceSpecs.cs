using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Accounts-payable workspace tabs over the real AP ledger tables:
/// epc_erp_supplier_accounting (invoice/payment entries), epc_erp_purchases
/// (posted invoices) and epc_erp_suppliers. Entry rows: is_credit=1 invoice,
/// is_credit=0 payment, entry_kind carries the detail.
/// </summary>
public static class ErpApWorkspaceSpecs
{
    private const string Purchases = "/erp/purchases-app";
    private const string Suppliers = "/erp/suppliers-app";

    private const string AcctJoin =
        " FROM `epc_erp_supplier_accounting` a" +
        " LEFT JOIN `epc_erp_suppliers` s ON s.`id` = a.`supplier_id`";

    private const string AcctCols =
        "SELECT IFNULL(s.`name`,'(none)'), IFNULL(a.`entry_kind`,'invoice')," +
        " IFNULL(a.`reference`,''), a.`amount`, FROM_UNIXTIME(a.`time`)," +
        " LEFT(IFNULL(a.`note`,''),80)";

    public static readonly IReadOnlyDictionary<string, ErpWorkspaceSpec> Tabs =
        new Dictionary<string, ErpWorkspaceSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["ap_vendor_balance"] = new(
                ["Vendor", "Invoiced", "Paid", "Balance"],
                "SELECT s.`name`," +
                " IFNULL(SUM(IF(a.`is_credit`=1,a.`amount`,0)),0)," +
                " IFNULL(SUM(IF(a.`is_credit`=0,a.`amount`,0)),0)," +
                " IFNULL(SUM(IF(a.`is_credit`=1,a.`amount`,-a.`amount`)),0)" +
                AcctJoin +
                " WHERE IFNULL(a.`active`,1) = 1 GROUP BY s.`id` ORDER BY 4 DESC LIMIT 300",
                "No vendor balances yet.",
                Suppliers),

            ["ap_vendor_transactions"] = new(
                ["Vendor", "Kind", "Reference", "Amount", "When", "Note"],
                AcctCols + AcctJoin +
                " WHERE IFNULL(a.`active`,1) = 1" +
                " ORDER BY a.`time` DESC, a.`id` DESC LIMIT 400",
                "No vendor transactions yet.",
                Purchases),

            ["ap_vendor_statement"] = new(
                ["Vendor", "Kind", "Reference", "Dr", "Cr", "When"],
                "SELECT IFNULL(s.`name`,'(none)'), IFNULL(a.`entry_kind`,'invoice')," +
                " IFNULL(a.`reference`,''), IF(a.`is_credit`=0,a.`amount`,0)," +
                " IF(a.`is_credit`=1,a.`amount`,0), FROM_UNIXTIME(a.`time`)" +
                AcctJoin +
                " WHERE IFNULL(a.`active`,1) = 1" +
                " ORDER BY a.`time` DESC, a.`id` DESC LIMIT 400",
                "No vendor statement lines yet.",
                Purchases),

            ["ap_global_invoice_journal"] = new(
                ["Invoice", "Vendor", "Date", "Ex VAT", "VAT", "Total", "Status"],
                "SELECT IFNULL(b.`invoice_number`, CONCAT('PI-',b.`id`)), IFNULL(s.`name`,'(none)')," +
                " FROM_UNIXTIME(b.`purchase_date`), b.`amount_ex_vat`, b.`vat_amount`," +
                " b.`total_amount`, IFNULL(b.`status`,'confirmed')" +
                " FROM `epc_erp_purchases` b LEFT JOIN `epc_erp_suppliers` s ON s.`id` = b.`supplier_id`" +
                " WHERE IFNULL(b.`active`,1) = 1" +
                " ORDER BY b.`purchase_date` DESC, b.`id` DESC LIMIT 400",
                "No invoices posted yet.",
                Purchases),

            ["ap_invoice_journal_report"] = new(
                ["Invoice", "Voucher", "Vendor", "Total", "GL", "Status"],
                "SELECT IFNULL(b.`invoice_number`, CONCAT('PI-',b.`id`)), IFNULL(b.`voucher_no`,'')," +
                " IFNULL(s.`name`,'(none)'), b.`total_amount`, IFNULL(b.`gl_journal_id`,0)," +
                " IFNULL(b.`status`,'confirmed')" +
                " FROM `epc_erp_purchases` b LEFT JOIN `epc_erp_suppliers` s ON s.`id` = b.`supplier_id`" +
                " WHERE IFNULL(b.`active`,1) = 1 ORDER BY b.`id` DESC LIMIT 300",
                "No invoice journal lines yet.",
                Purchases),

            ["ap_invoice_pool"] = new(
                ["Vendor", "Invoice ref", "Amount", "When"],
                "SELECT IFNULL(s.`name`,'(none)'), IFNULL(a.`reference`,''), a.`amount`," +
                " FROM_UNIXTIME(a.`time`)" +
                AcctJoin +
                " WHERE IFNULL(a.`active`,1) = 1 AND a.`is_credit` = 1" +
                " ORDER BY a.`time` DESC LIMIT 300",
                "No open invoice entries.",
                Purchases),

            ["ap_payment_history"] = new(
                ["Vendor", "Payment ref", "Amount", "When", "Note"],
                "SELECT IFNULL(s.`name`,'(none)'), IFNULL(a.`reference`,''), a.`amount`," +
                " FROM_UNIXTIME(a.`time`), LEFT(IFNULL(a.`note`,''),80)" +
                AcctJoin +
                " WHERE IFNULL(a.`active`,1) = 1 AND a.`is_credit` = 0" +
                " ORDER BY a.`time` DESC LIMIT 300",
                "No payments posted yet.",
                Purchases),

            ["ap_payments_assigned"] = new(
                ["Vendor", "Payment ref", "Amount", "When"],
                "SELECT IFNULL(s.`name`,'(none)'), IFNULL(a.`reference`,''), a.`amount`," +
                " FROM_UNIXTIME(a.`time`)" +
                AcctJoin +
                " WHERE IFNULL(a.`active`,1) = 1 AND a.`is_credit` = 0" +
                " ORDER BY a.`time` DESC LIMIT 300",
                "No payment journal lines yet.",
                Purchases),

            ["ap_unposted_invoices"] = new(
                ["PO", "Vendor", "Total", "Status", "Raised"],
                "SELECT p.`po_no`, IFNULL(s.`name`,'(none)'), p.`total_amount`, p.`status`," +
                " FROM_UNIXTIME(p.`time_created`)" +
                " FROM `epc_erp_purchase_orders` p LEFT JOIN `epc_erp_suppliers` s ON s.`id` = p.`supplier_id`" +
                " WHERE p.`status` IN ('draft','approved')" +
                " ORDER BY p.`id` DESC LIMIT 300",
                "No unposted invoices — approved POs waiting for invoice appear here.",
                Purchases),

            ["ap_invoices_due_today"] = new(
                ["Vendor", "Invoice ref", "Amount", "Age days"],
                "SELECT IFNULL(s.`name`,'(none)'), IFNULL(a.`reference`,''), a.`amount`," +
                " DATEDIFF(NOW(), FROM_UNIXTIME(a.`time`))" +
                AcctJoin +
                " WHERE IFNULL(a.`active`,1) = 1 AND a.`is_credit` = 1" +
                " AND DATEDIFF(NOW(), FROM_UNIXTIME(a.`time`)) >= 0" +
                " ORDER BY 4 DESC LIMIT 200",
                "Nothing due today.",
                Purchases),

            ["ap_payment_proposal"] = new(
                ["Vendor", "Invoice ref", "Amount", "Days overdue"],
                "SELECT IFNULL(s.`name`,'(none)'), IFNULL(a.`reference`,''), a.`amount`," +
                " DATEDIFF(NOW(), FROM_UNIXTIME(a.`time`)) - 30" +
                AcctJoin +
                " WHERE IFNULL(a.`active`,1) = 1 AND a.`is_credit` = 1" +
                " AND DATEDIFF(NOW(), FROM_UNIXTIME(a.`time`)) > 30" +
                " ORDER BY 4 DESC LIMIT 200",
                "Nothing past due.",
                Purchases),

            ["ap_vendor_ledger_recon"] = new(
                ["Vendor", "0-30", "31-60", "61-90", "90+", "Balance"],
                "SELECT IFNULL(s.`name`,'(none)')," +
                " IFNULL(SUM(IF(DATEDIFF(NOW(),FROM_UNIXTIME(a.`time`)) <= 30," +
                "  IF(a.`is_credit`=1,a.`amount`,-a.`amount`),0)),0)," +
                " IFNULL(SUM(IF(DATEDIFF(NOW(),FROM_UNIXTIME(a.`time`)) BETWEEN 31 AND 60," +
                "  IF(a.`is_credit`=1,a.`amount`,-a.`amount`),0)),0)," +
                " IFNULL(SUM(IF(DATEDIFF(NOW(),FROM_UNIXTIME(a.`time`)) BETWEEN 61 AND 90," +
                "  IF(a.`is_credit`=1,a.`amount`,-a.`amount`),0)),0)," +
                " IFNULL(SUM(IF(DATEDIFF(NOW(),FROM_UNIXTIME(a.`time`)) > 90," +
                "  IF(a.`is_credit`=1,a.`amount`,-a.`amount`),0)),0)," +
                " IFNULL(SUM(IF(a.`is_credit`=1,a.`amount`,-a.`amount`)),0)" +
                AcctJoin +
                " WHERE IFNULL(a.`active`,1) = 1 GROUP BY s.`id` ORDER BY 6 DESC LIMIT 300",
                "No aging buckets — vendor ledger is empty.",
                Suppliers),

            ["ap_vendor_groups"] = new(
                ["Group", "Vendors", "Balance"],
                "SELECT IFNULL(NULLIF(s.`vendor_group`,''),'(none)'), COUNT(s.`id`)," +
                " IFNULL((SELECT SUM(IF(a.`is_credit`=1,a.`amount`,-a.`amount`))" +
                "  FROM `epc_erp_supplier_accounting` a JOIN `epc_erp_suppliers` s2" +
                "  ON s2.`id` = a.`supplier_id`" +
                "  WHERE IFNULL(NULLIF(s2.`vendor_group`,''),'(none)')" +
                "   = IFNULL(NULLIF(s.`vendor_group`,''),'(none)') AND IFNULL(a.`active`,1)=1),0)" +
                " FROM `epc_erp_suppliers` s WHERE IFNULL(s.`active`,1) = 1" +
                " GROUP BY 1 ORDER BY 3 DESC LIMIT 100",
                "No vendor groups.",
                Suppliers),

            ["ap_terms_payment"] = new(
                ["Terms", "Vendors", "Balance"],
                "SELECT IFNULL(NULLIF(s.`payment_terms`,''),'(none)'), COUNT(s.`id`)," +
                " IFNULL((SELECT SUM(IF(a.`is_credit`=1,a.`amount`,-a.`amount`))" +
                "  FROM `epc_erp_supplier_accounting` a JOIN `epc_erp_suppliers` s2" +
                "  ON s2.`id` = a.`supplier_id`" +
                "  WHERE IFNULL(NULLIF(s2.`payment_terms`,''),'(none)')" +
                "   = IFNULL(NULLIF(s.`payment_terms`,''),'(none)') AND IFNULL(a.`active`,1)=1),0)" +
                " FROM `epc_erp_suppliers` s WHERE IFNULL(s.`active`,1) = 1" +
                " GROUP BY 1 ORDER BY 3 DESC LIMIT 100",
                "No payment terms set.",
                Suppliers),

            ["ap_settled_invoices"] = new(
                ["Vendor", "Invoice ref", "Invoiced", "Paid", "Settled"],
                "SELECT IFNULL(s.`name`,'(none)'), IFNULL(a.`reference`,'')," +
                " SUM(IF(a.`is_credit`=1,a.`amount`,0))," +
                " SUM(IF(a.`is_credit`=0,a.`amount`,0))," +
                " IF(SUM(IF(a.`is_credit`=0,a.`amount`,0)) >= SUM(IF(a.`is_credit`=1,a.`amount`,0))" +
                "  AND SUM(IF(a.`is_credit`=1,a.`amount`,0)) > 0, 'yes','no')" +
                AcctJoin +
                " WHERE IFNULL(a.`active`,1) = 1 AND a.`reference` <> ''" +
                " GROUP BY s.`id`, a.`reference` HAVING SUM(IF(a.`is_credit`=0,a.`amount`,0)) > 0" +
                " ORDER BY 2 LIMIT 300",
                "No settled invoices yet.",
                Purchases),
        };

    /// <summary>Lazy-creates the AP tables the specs read.</summary>
    internal static Task EnsureApAsync(DbConnection c, CancellationToken ct)
        => ErpPurchasingWorkspaceSpecs.EnsurePurchasingAsync(c, ct);
}
