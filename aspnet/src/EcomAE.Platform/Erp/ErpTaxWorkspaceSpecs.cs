using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Tax workspace tabs — UAE VAT views over the real posted documents:
/// epc_einvoice_documents (output VAT: total_vat per invoice, buyer_json) and
/// epc_erp_purchases (input VAT: vat_amount per purchase invoice).
/// </summary>
public static class ErpTaxWorkspaceSpecs
{
    private const string Invoices = "/erp/invoices-app";
    private const string Purchases = "/erp/purchases-app";

    private const string OutVat =
        " FROM `epc_einvoice_documents` d WHERE IFNULL(d.`active`,1) = 1";
    private const string InVat =
        " FROM `epc_erp_purchases` b LEFT JOIN `epc_erp_suppliers` s ON s.`id` = b.`supplier_id`" +
        " WHERE IFNULL(b.`active`,1) = 1";

    public static readonly IReadOnlyDictionary<string, ErpWorkspaceSpec> Tabs =
        new Dictionary<string, ErpWorkspaceSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["tax_posted_hist"] = new(
                ["Invoice", "Customer", "Issued", "Ex VAT", "VAT", "Total"],
                "SELECT d.`invoice_number`," +
                " IFNULL(JSON_UNQUOTE(JSON_EXTRACT(d.`buyer_json`,'$.name')),'(none)')," +
                " IFNULL(d.`issue_date`,''), d.`subtotal_ex_vat`, d.`total_vat`, d.`total_incl_vat`" +
                OutVat + " AND d.`total_vat` > 0 ORDER BY d.`id` DESC LIMIT 400",
                "No posted tax yet — issue a tax invoice.",
                Invoices),

            ["tax_by_customer"] = new(
                ["Customer", "Invoices", "Ex VAT", "VAT", "Total"],
                "SELECT IFNULL(JSON_UNQUOTE(JSON_EXTRACT(d.`buyer_json`,'$.name')),'(none)')," +
                " COUNT(d.`id`), IFNULL(SUM(d.`subtotal_ex_vat`),0), IFNULL(SUM(d.`total_vat`),0)," +
                " IFNULL(SUM(d.`total_incl_vat`),0)" +
                OutVat + " GROUP BY 1 ORDER BY 4 DESC LIMIT 300",
                "No output VAT by customer.",
                Invoices),

            ["tax_by_vendor"] = new(
                ["Vendor", "Invoices", "Ex VAT", "Input VAT", "Total"],
                "SELECT IFNULL(s.`name`,'(none)'), COUNT(b.`id`)," +
                " IFNULL(SUM(b.`amount_ex_vat`),0), IFNULL(SUM(b.`vat_amount`),0)," +
                " IFNULL(SUM(b.`total_amount`),0)" +
                InVat + " AND b.`vat_amount` > 0" +
                " GROUP BY s.`id` ORDER BY 4 DESC LIMIT 300",
                "No input VAT by vendor.",
                Purchases),

            ["tax_by_code"] = new(
                ["Doc type", "Lines", "Ex VAT", "VAT", "Total"],
                "SELECT IFNULL(d.`invoice_type_code`,'standard'), COUNT(d.`id`)," +
                " IFNULL(SUM(d.`subtotal_ex_vat`),0), IFNULL(SUM(d.`total_vat`),0)," +
                " IFNULL(SUM(d.`total_incl_vat`),0)" +
                OutVat + " GROUP BY 1 ORDER BY 4 DESC LIMIT 100",
                "No tax codes on documents yet.",
                Invoices),

            ["tax_payments"] = new(
                ["Invoice", "Customer", "VAT", "Paid", "Status"],
                "SELECT d.`invoice_number`," +
                " IFNULL(JSON_UNQUOTE(JSON_EXTRACT(d.`buyer_json`,'$.name')),'(none)')," +
                " d.`total_vat`, d.`paid_amount`, d.`status`" +
                OutVat + " AND d.`total_vat` > 0 ORDER BY d.`id` DESC LIMIT 300",
                "No VAT-collecting payments yet.",
                Invoices),

            ["tax_summary"] = new(
                ["Month", "Output VAT", "Invoices", "Input VAT", "Purchases", "Net due"],
                "SELECT m.`ym`, m.`out_vat`, m.`out_lines`, p.`in_vat`, p.`in_lines`," +
                " m.`out_vat` - p.`in_vat`" +
                " FROM (SELECT DATE_FORMAT(d.`issue_date`,'%Y-%m') ym," +
                "  SUM(d.`total_vat`) out_vat, COUNT(d.`id`) out_lines" +
                "  FROM `epc_einvoice_documents` d WHERE IFNULL(d.`active`,1) = 1" +
                "  GROUP BY 1) m" +
                " LEFT JOIN (SELECT DATE_FORMAT(FROM_UNIXTIME(b.`purchase_date`),'%Y-%m') ym," +
                "  SUM(b.`vat_amount`) in_vat, COUNT(b.`id`) in_lines" +
                "  FROM `epc_erp_purchases` b WHERE IFNULL(b.`active`,1) = 1" +
                "  GROUP BY 1) p ON p.`ym` = m.`ym`" +
                " ORDER BY m.`ym` DESC LIMIT 24",
                "No VAT periods yet — post sales and purchase invoices.",
                Invoices),

            ["tax_txn"] = new(
                ["Source", "Doc", "Party", "Ex VAT", "VAT", "Date"],
                "SELECT 'sales', d.`invoice_number`," +
                " IFNULL(JSON_UNQUOTE(JSON_EXTRACT(d.`buyer_json`,'$.name')),'(none)')," +
                " d.`subtotal_ex_vat`, d.`total_vat`, IFNULL(d.`issue_date`,'')" +
                OutVat +
                " UNION ALL" +
                " SELECT 'purchase', IFNULL(b.`invoice_number`, CONCAT('PI-',b.`id`))," +
                " IFNULL(s.`name`,'(none)'), b.`amount_ex_vat`, b.`vat_amount`," +
                " FROM_UNIXTIME(b.`purchase_date`)" + InVat +
                " ORDER BY 6 DESC LIMIT 400",
                "No tax transactions yet.",
                Invoices),

            ["tax_txn_detail"] = new(
                ["Invoice", "Line", "Item", "VAT line total"],
                "SELECT d.`invoice_number`, l.`line_no`," +
                " LEFT(IFNULL(l.`item_name`,''),60), l.`gross_amount`" +
                " FROM `epc_einvoice_lines` l" +
                " JOIN `epc_einvoice_documents` d ON d.`id` = l.`document_id`" +
                " WHERE IFNULL(d.`active`,1) = 1" +
                " ORDER BY d.`id` DESC, l.`line_no` LIMIT 400",
                "No tax line detail yet.",
                Invoices),

            ["tax_vouchers_no_tax"] = new(
                ["Invoice", "Customer", "Issued", "Total", "Status"],
                "SELECT d.`invoice_number`," +
                " IFNULL(JSON_UNQUOTE(JSON_EXTRACT(d.`buyer_json`,'$.name')),'(none)')," +
                " IFNULL(d.`issue_date`,''), d.`total_incl_vat`, d.`status`" +
                OutVat + " AND IFNULL(d.`total_vat`,0) = 0" +
                " ORDER BY d.`id` DESC LIMIT 300",
                "No zero-VAT vouchers.",
                Invoices),

            ["tax_list"] = new(
                ["Code", "Rate", "Source"],
                "SELECT DISTINCT 'standard'," +
                " IF(d.`subtotal_ex_vat` > 0, ROUND(d.`total_vat` / d.`subtotal_ex_vat` * 100, 2), 0)," +
                " 'inferred from invoices'" +
                OutVat + " AND d.`subtotal_ex_vat` > 0" +
                " GROUP BY 2 ORDER BY 2 DESC LIMIT 20",
                "No VAT rates inferred yet — post an invoice with VAT.",
                Invoices),

            ["tax_gl_recon"] = new(
                ["Month", "Output VAT", "Input VAT", "Net", "Check"],
                "SELECT m.`ym`, m.`out_vat`, p.`in_vat`, m.`out_vat` - p.`in_vat`," +
                " IF(m.`out_vat` - p.`in_vat` >= 0,'payable','refundable')" +
                " FROM (SELECT DATE_FORMAT(d.`issue_date`,'%Y-%m') ym," +
                "  SUM(d.`total_vat`) out_vat" +
                "  FROM `epc_einvoice_documents` d WHERE IFNULL(d.`active`,1) = 1" +
                "  GROUP BY 1) m" +
                " LEFT JOIN (SELECT DATE_FORMAT(FROM_UNIXTIME(b.`purchase_date`),'%Y-%m') ym," +
                "  SUM(b.`vat_amount`) in_vat" +
                "  FROM `epc_erp_purchases` b WHERE IFNULL(b.`active`,1) = 1" +
                "  GROUP BY 1) p ON p.`ym` = m.`ym`" +
                " ORDER BY m.`ym` DESC LIMIT 24",
                "No VAT to reconcile.",
                Invoices),

            ["tax_e_items"] = new(
                ["Invoice", "Line", "Item", "Qty", "Total"],
                "SELECT d.`invoice_number`, l.`line_no`," +
                " LEFT(IFNULL(l.`item_name`,''),60), l.`quantity`, l.`gross_amount`" +
                " FROM `epc_einvoice_lines` l" +
                " JOIN `epc_einvoice_documents` d ON d.`id` = l.`document_id`" +
                " WHERE IFNULL(d.`active`,1) = 1" +
                " ORDER BY d.`id` DESC, l.`line_no` LIMIT 400",
                "No e-invoice item lines.",
                Invoices),

            ["tax_e_messages"] = new(
                ["Document", "Event", "Status", "Message", "When"],
                "SELECT d.`invoice_number`, e.`event_type`, e.`status`," +
                " LEFT(IFNULL(e.`message`,''),80), FROM_UNIXTIME(e.`time_created`)" +
                " FROM `epc_einvoice_events` e" +
                " JOIN `epc_einvoice_documents` d ON d.`id` = e.`document_id`" +
                " ORDER BY e.`id` DESC LIMIT 300",
                "No e-invoice messages yet.",
                Invoices),
        };

    /// <summary>Lazy-creates the tax tables the specs read.</summary>
    internal static async Task EnsureTaxAsync(DbConnection c, CancellationToken ct)
    {
        await ErpSalesInvoiceWriteService.EnsureSchemaAsync(c, ct).ConfigureAwait(false);
        await ErpPurchaseInvoiceWriteService.EnsureSchemaAsync(c, ct).ConfigureAwait(false);
        await ErpLazySchema.EnsureSuppliersAsync(c, ct).ConfigureAwait(false);
    }
}
