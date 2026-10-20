using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Sales workspace tabs over epc_erp_sales_orders (+lines), epc_einvoice_documents
/// and epc_rma_requests (legacy returns register where present).
/// </summary>
public static class ErpSalesWorkspaceSpecs
{
    private const string Orders = "/erp/sales-orders-app";
    private const string Invoices = "/erp/invoices-app";
    private const string Returns = "/erp/returns-rma-app";

    private const string SoJoin = " FROM `epc_erp_sales_orders` o";
    private const string SoCols =
        "SELECT o.`so_no`, IFNULL(o.`title`,''), o.`amount_ex_vat`, o.`vat_amount`," +
        " o.`total_amount`, o.`status`, FROM_UNIXTIME(o.`time_created`)";

    public static readonly IReadOnlyDictionary<string, ErpWorkspaceSpec> Tabs =
        new Dictionary<string, ErpWorkspaceSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["sm_open_orders"] = new(
                ["SO", "Title", "Ex VAT", "VAT", "Total", "Status", "Raised"],
                SoCols + SoJoin +
                " WHERE o.`status` IN ('draft','confirmed')" +
                " ORDER BY o.`id` DESC LIMIT 300",
                "No open sales orders.",
                Orders),

            ["sm_ic_orders"] = new(
                ["SO", "Title", "Total", "Status", "Invoice", "Raised"],
                "SELECT o.`so_no`, IFNULL(o.`title`,''), o.`total_amount`, o.`status`," +
                " IFNULL(o.`sales_invoice_id`,0), FROM_UNIXTIME(o.`time_created`)" +
                SoJoin + " ORDER BY o.`id` DESC LIMIT 300",
                "No sales orders yet.",
                Orders),

            ["sm_backorder"] = new(
                ["SO", "Title", "Total", "Status", "Raised"],
                "SELECT o.`so_no`, IFNULL(o.`title`,''), o.`total_amount`, o.`status`," +
                " FROM_UNIXTIME(o.`time_created`)" +
                SoJoin + " WHERE o.`status` = 'confirmed' AND o.`sales_invoice_id` = 0" +
                " ORDER BY o.`id` DESC LIMIT 300",
                "No backorders — every confirmed order is invoiced.",
                Orders),

            ["sm_confirm"] = new(
                ["SO", "Title", "Total", "Status", "Raised"],
                "SELECT o.`so_no`, IFNULL(o.`title`,''), o.`total_amount`, o.`status`," +
                " FROM_UNIXTIME(o.`time_created`)" +
                SoJoin + " WHERE o.`status` = 'draft' ORDER BY o.`id` DESC LIMIT 300",
                "No orders awaiting confirmation.",
                Orders),

            ["sm_holds"] = new(
                ["SO", "Title", "Total", "Status", "Raised"],
                "SELECT o.`so_no`, IFNULL(o.`title`,''), o.`total_amount`, o.`status`," +
                " FROM_UNIXTIME(o.`time_created`)" +
                SoJoin + " WHERE o.`status` = 'cancelled' ORDER BY o.`id` DESC LIMIT 200",
                "No cancelled/held orders.",
                Orders),

            ["sm_picking"] = new(
                ["SO", "Line", "Item", "Qty", "Price"],
                "SELECT o.`so_no`, l.`line_no`, LEFT(IFNULL(l.`item_code`,''),60)," +
                " l.`qty`, l.`unit_price_ex_vat`" +
                " FROM `epc_erp_sales_order_lines` l" +
                " JOIN `epc_erp_sales_orders` o ON o.`id` = l.`sales_order_id`" +
                " WHERE o.`status` = 'confirmed'" +
                " ORDER BY o.`id` DESC, l.`line_no` LIMIT 400",
                "Nothing to pick — no confirmed order lines.",
                Orders),

            ["sm_turnover"] = new(
                ["Month", "Orders", "Ex VAT", "VAT", "Total"],
                "SELECT DATE_FORMAT(FROM_UNIXTIME(o.`time_created`),'%Y-%m')," +
                " COUNT(o.`id`), IFNULL(SUM(o.`amount_ex_vat`),0)," +
                " IFNULL(SUM(o.`vat_amount`),0), IFNULL(SUM(o.`total_amount`),0)" +
                SoJoin + " WHERE o.`status` <> 'cancelled'" +
                " GROUP BY 1 ORDER BY 1 DESC LIMIT 24",
                "No turnover yet.",
                Orders),

            ["sm_sales_stats"] = new(
                ["Status", "Orders", "Ex VAT", "VAT", "Total"],
                "SELECT o.`status`, COUNT(o.`id`), IFNULL(SUM(o.`amount_ex_vat`),0)," +
                " IFNULL(SUM(o.`vat_amount`),0), IFNULL(SUM(o.`total_amount`),0)" +
                SoJoin + " GROUP BY o.`status` ORDER BY 5 DESC",
                "No sales statistics yet.",
                Orders),

            ["sm_sales_analysis"] = new(
                ["Item", "Lines", "Qty", "Revenue"],
                "SELECT LEFT(IFNULL(l.`item_code`,''),60), COUNT(l.`id`)," +
                " IFNULL(SUM(l.`qty`),0), IFNULL(SUM(l.`line_ex_vat`),0)" +
                " FROM `epc_erp_sales_order_lines` l" +
                " GROUP BY 1 ORDER BY 4 DESC LIMIT 300",
                "No line sales to analyse.",
                Orders),

            ["sm_performance"] = new(
                ["Customer", "Orders", "Invoiced", "Paid", "Open"],
                "SELECT IFNULL(JSON_UNQUOTE(JSON_EXTRACT(d.`buyer_json`,'$.name')),'(none)')," +
                " COUNT(d.`id`), IFNULL(SUM(d.`total_incl_vat`),0)," +
                " IFNULL(SUM(d.`paid_amount`),0), IFNULL(SUM(d.`amount_due`),0)" +
                " FROM `epc_einvoice_documents` d WHERE IFNULL(d.`active`,1) = 1" +
                " GROUP BY 1 ORDER BY 3 DESC LIMIT 300",
                "No customer sales performance yet.",
                Invoices),

            ["sm_cases"] = new(
                ["RMA", "Customer", "Product", "Reason", "Status", "Opened"],
                "SELECT IFNULL(r.`rma_number`, CONCAT('RMA-',r.`id`))," +
                " IFNULL(r.`customer_name`,''), LEFT(IFNULL((SELECT ri.`product_name` FROM `epc_rma_items` ri WHERE ri.`rma_id`=r.`id` ORDER BY ri.`id` LIMIT 1),''),50)," +
                " LEFT(IFNULL(r.`reason`,''),60), r.`status`, IFNULL(r.`created_at`,'')" +
                " FROM `epc_rma_requests` r ORDER BY r.`id` DESC LIMIT 300",
                "No return cases — RMA requests appear here.",
                Returns),

            ["sm_return_reasons"] = new(
                ["Reason", "Cases", "Refunded"],
                "SELECT IFNULL(NULLIF(r.`reason`,''),'(unspecified)'), COUNT(r.`id`)," +
                " SUM(r.`status` = 'refund')" +
                " FROM `epc_rma_requests` r GROUP BY 1 ORDER BY 2 DESC LIMIT 100",
                "No return reasons yet.",
                Returns),

            ["sm_daily_cash"] = new(
                ["Date", "Invoices", "Invoiced", "Paid"],
                "SELECT d.`issue_date`, COUNT(d.`id`)," +
                " IFNULL(SUM(d.`total_incl_vat`),0), IFNULL(SUM(d.`paid_amount`),0)" +
                " FROM `epc_einvoice_documents` d WHERE IFNULL(d.`active`,1) = 1" +
                " GROUP BY d.`issue_date` ORDER BY d.`issue_date` DESC LIMIT 60",
                "No daily takings yet.",
                Invoices),

            ["sm_open_quotations"] = new(
                ["SO", "Title", "Total", "Status", "Raised"],
                "SELECT o.`so_no`, IFNULL(o.`title`,''), o.`total_amount`, o.`status`," +
                " FROM_UNIXTIME(o.`time_created`)" +
                SoJoin + " WHERE o.`status` = 'draft' ORDER BY o.`id` DESC LIMIT 200",
                "No open quotations.",
                Orders),

            ["sm_my_quotations"] = new(
                ["SO", "Title", "Total", "Status", "Owner", "Raised"],
                "SELECT o.`so_no`, IFNULL(o.`title`,''), o.`total_amount`, o.`status`," +
                " o.`admin_id`, FROM_UNIXTIME(o.`time_created`)" +
                SoJoin + " ORDER BY o.`id` DESC LIMIT 200",
                "No quotations yet.",
                Orders),

            ["sm_recalc_credit"] = new(
                ["Customer", "Invoices", "Open balance", "Oldest"],
                "SELECT IFNULL(JSON_UNQUOTE(JSON_EXTRACT(d.`buyer_json`,'$.name')),'(none)')," +
                " COUNT(d.`id`), IFNULL(SUM(d.`amount_due`),0)," +
                " MIN(d.`issue_date`)" +
                " FROM `epc_einvoice_documents` d" +
                " WHERE IFNULL(d.`active`,1) = 1 AND d.`amount_due` > 0" +
                " GROUP BY 1 ORDER BY 3 DESC LIMIT 300",
                "No customer credit exposure.",
                Invoices),
        };

    /// <summary>Lazy-creates the sales tables the specs read.</summary>
    internal static async Task EnsureSalesAsync(DbConnection c, CancellationToken ct)
    {
        await ErpSalesOrderWriteService.EnsureSchemaAsync(c, ct).ConfigureAwait(false);
        await ErpSalesInvoiceWriteService.EnsureSchemaAsync(c, ct).ConfigureAwait(false);
        await ErpLazySchema.EnsureReturnAndVariantTablesAsync(c, ct).ConfigureAwait(false);
    }
}
