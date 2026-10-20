using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Accounts-receivable workspace tabs over the real AR tables:
/// epc_einvoice_documents (issued sales invoices, amount_due/paid_amount) and
/// epc_erp_sales_orders. Buyer display name comes from buyer_json.
/// </summary>
public static class ErpArWorkspaceSpecs
{
    private const string Orders = "/erp/sales-orders-app";
    private const string Invoices = "/erp/invoices-app";

    private const string Buyer =
        "IFNULL(JSON_UNQUOTE(JSON_EXTRACT(d.`buyer_json`,'$.name')),'(none)')";

    private const string DocJoin = " FROM `epc_einvoice_documents` d";

    public static readonly IReadOnlyDictionary<string, ErpWorkspaceSpec> Tabs =
        new Dictionary<string, ErpWorkspaceSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["ar_customer_balance"] = new(
                ["Customer", "Invoiced", "Paid", "Balance"],
                "SELECT " + Buyer + ", IFNULL(SUM(d.`total_incl_vat`),0)," +
                " IFNULL(SUM(d.`paid_amount`),0), IFNULL(SUM(d.`amount_due`),0)" +
                DocJoin + " WHERE IFNULL(d.`active`,1) = 1" +
                " GROUP BY 1 ORDER BY 4 DESC LIMIT 300",
                "No customer balances yet.",
                Invoices),

            ["ar_customer_ledger_recon"] = new(
                ["Customer", "Invoices", "Invoiced", "Paid", "Open"],
                "SELECT " + Buyer + ", COUNT(d.`id`), IFNULL(SUM(d.`total_incl_vat`),0)," +
                " IFNULL(SUM(d.`paid_amount`),0), IFNULL(SUM(d.`amount_due`),0)" +
                DocJoin + " WHERE IFNULL(d.`active`,1) = 1" +
                " GROUP BY 1 ORDER BY 5 DESC LIMIT 300",
                "Nothing to reconcile — the AR ledger is empty.",
                Invoices),

            ["ar_customer_statement"] = new(
                ["Customer", "Invoice", "Issued", "Total", "Paid", "Due", "Status"],
                "SELECT " + Buyer + ", d.`invoice_number`, IFNULL(d.`issue_date`,'')," +
                " d.`total_incl_vat`, d.`paid_amount`, d.`amount_due`, d.`status`" +
                DocJoin + " WHERE IFNULL(d.`active`,1) = 1" +
                " ORDER BY d.`id` DESC LIMIT 400",
                "No customer statements yet.",
                Invoices),

            ["ar_customer_transactions"] = new(
                ["Customer", "Invoice", "Issued", "Total", "Due", "Status"],
                "SELECT " + Buyer + ", d.`invoice_number`, IFNULL(d.`issue_date`,'')," +
                " d.`total_incl_vat`, d.`amount_due`, d.`status`" +
                DocJoin + " WHERE IFNULL(d.`active`,1) = 1" +
                " ORDER BY d.`id` DESC LIMIT 400",
                "No customer transactions yet.",
                Invoices),

            ["ar_payment_proposal"] = new(
                ["Customer", "Invoice", "Due date", "Amount due", "Days overdue"],
                "SELECT " + Buyer + ", d.`invoice_number`, IFNULL(d.`payment_due_date`,'')," +
                " d.`amount_due`, DATEDIFF(NOW(), d.`payment_due_date`)" +
                DocJoin + " WHERE IFNULL(d.`active`,1) = 1 AND d.`amount_due` > 0" +
                " AND d.`payment_due_date` < CURDATE()" +
                " ORDER BY 5 DESC LIMIT 300",
                "No customers past due.",
                Invoices),

            ["ar_customers_underage"] = new(
                ["Customer", "Invoice", "Due date", "Amount due", "Days left"],
                "SELECT " + Buyer + ", d.`invoice_number`, IFNULL(d.`payment_due_date`,'')," +
                " d.`amount_due`, DATEDIFF(d.`payment_due_date`, NOW())" +
                DocJoin + " WHERE IFNULL(d.`active`,1) = 1 AND d.`amount_due` > 0" +
                " AND d.`payment_due_date` >= CURDATE()" +
                " ORDER BY 5 LIMIT 300",
                "Nothing due but not yet overdue.",
                Invoices),

            ["ar_free_text_assigned"] = new(
                ["Invoice", "Customer", "Issued", "Total", "Due", "Status"],
                "SELECT d.`invoice_number`, " + Buyer + ", IFNULL(d.`issue_date`,'')," +
                " d.`total_incl_vat`, d.`amount_due`, d.`status`" +
                DocJoin + " WHERE IFNULL(d.`active`,1) = 1" +
                " ORDER BY d.`id` DESC LIMIT 400",
                "No free-text invoices yet.",
                Invoices),

            ["ar_invoice_journal_report"] = new(
                ["Invoice", "Customer", "Issued", "Ex VAT", "VAT", "Total", "Status"],
                "SELECT d.`invoice_number`, " + Buyer + ", IFNULL(d.`issue_date`,'')," +
                " d.`subtotal_ex_vat`, d.`total_vat`, d.`total_incl_vat`, d.`status`" +
                DocJoin + " WHERE IFNULL(d.`active`,1) = 1" +
                " ORDER BY d.`id` DESC LIMIT 400",
                "No posted invoices yet.",
                Invoices),

            ["ar_invoices_due_today"] = new(
                ["Customer", "Invoice", "Total", "Due", "Due date"],
                "SELECT " + Buyer + ", d.`invoice_number`, d.`total_incl_vat`," +
                " d.`amount_due`, IFNULL(d.`payment_due_date`,'')" +
                DocJoin + " WHERE IFNULL(d.`active`,1) = 1 AND d.`amount_due` > 0" +
                " AND d.`payment_due_date` = CURDATE()" +
                " ORDER BY d.`id` DESC LIMIT 200",
                "Nothing due today.",
                Invoices),

            ["ar_bill_of_exchange"] = new(
                ["Customer", "Invoice", "Due date", "Due", "Days overdue"],
                "SELECT " + Buyer + ", d.`invoice_number`, IFNULL(d.`payment_due_date`,'')," +
                " d.`amount_due`, DATEDIFF(NOW(), d.`payment_due_date`)" +
                DocJoin + " WHERE IFNULL(d.`active`,1) = 1 AND d.`amount_due` > 0" +
                " AND d.`payment_due_date` < CURDATE()" +
                " ORDER BY 5 DESC LIMIT 300",
                "No invoices past due.",
                Invoices),

            ["ar_invoice_pool"] = new(
                ["Customer", "Invoice", "Issued", "Total", "Due"],
                "SELECT " + Buyer + ", d.`invoice_number`, IFNULL(d.`issue_date`,'')," +
                " d.`total_incl_vat`, d.`amount_due`" +
                DocJoin + " WHERE IFNULL(d.`active`,1) = 1 AND d.`amount_due` > 0" +
                " ORDER BY d.`id` DESC LIMIT 300",
                "No open invoices.",
                Invoices),

            ["ar_orders_not_invoiced"] = new(
                ["SO", "Title", "Total", "Status", "Raised"],
                "SELECT o.`so_no`, IFNULL(o.`title`,''), o.`total_amount`, o.`status`," +
                " FROM_UNIXTIME(o.`time_created`)" +
                " FROM `epc_erp_sales_orders` o" +
                " WHERE o.`status` = 'confirmed' AND o.`sales_invoice_id` = 0" +
                " ORDER BY o.`id` DESC LIMIT 300",
                "All confirmed orders are invoiced.",
                Orders),

            ["ar_payment_history"] = new(
                ["Customer", "Invoice", "Paid", "Invoice total", "Status"],
                "SELECT " + Buyer + ", d.`invoice_number`, d.`paid_amount`," +
                " d.`total_incl_vat`, d.`status`" +
                DocJoin + " WHERE IFNULL(d.`active`,1) = 1 AND d.`paid_amount` > 0" +
                " ORDER BY d.`id` DESC LIMIT 300",
                "No payments recorded yet.",
                Invoices),

            ["ar_ic_sales_orders"] = new(
                ["SO", "Title", "Ex VAT", "VAT", "Total", "Status", "Raised"],
                "SELECT o.`so_no`, IFNULL(o.`title`,''), o.`amount_ex_vat`, o.`vat_amount`," +
                " o.`total_amount`, o.`status`, FROM_UNIXTIME(o.`time_created`)" +
                " FROM `epc_erp_sales_orders` o ORDER BY o.`id` DESC LIMIT 400",
                "No sales orders — create one from the Orders page.",
                Orders),

            ["ar_settled_invoices"] = new(
                ["Customer", "Invoice", "Total", "Paid", "Status"],
                "SELECT " + Buyer + ", d.`invoice_number`, d.`total_incl_vat`," +
                " d.`paid_amount`, d.`status`" +
                DocJoin + " WHERE IFNULL(d.`active`,1) = 1" +
                " AND d.`amount_due` <= 0 AND d.`total_incl_vat` > 0" +
                " ORDER BY d.`id` DESC LIMIT 300",
                "No settled invoices yet.",
                Invoices),
        };

    /// <summary>Lazy-creates the AR tables the specs read.</summary>
    internal static async Task EnsureArAsync(DbConnection c, CancellationToken ct)
    {
        await ErpSalesInvoiceWriteService.EnsureSchemaAsync(c, ct).ConfigureAwait(false);
        await ErpSalesOrderWriteService.EnsureSchemaAsync(c, ct).ConfigureAwait(false);
    }
}
