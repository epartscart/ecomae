namespace EcomAE.Platform.Erp;

/// <summary>
/// Tab → real-data specs for the "Procurement and sourcing" workspace app.
/// Reads run against epc_erp_suppliers / epc_erp_purchase_orders / epc_erp_po_lines /
/// epc_erp_purchases / epc_erp_supplier_accounting / epc_erp_gl_journals.
/// </summary>
public static class ErpPurchasingWorkspaceSpecs
{
    private const string Suppliers = "/erp/suppliers-app";
    private const string Purchases = "/erp/purchases-app";
    private const string Orders = "/erp/purchase-orders-app";

    private const string SupplierJoin =
        " FROM `epc_erp_suppliers` s";

    private const string PoJoin =
        " FROM `epc_erp_purchase_orders` p LEFT JOIN `epc_erp_suppliers` s ON s.`id` = p.`supplier_id`";

    private const string PurchaseJoin =
        " FROM `epc_erp_purchases` b LEFT JOIN `epc_erp_suppliers` s ON s.`id` = b.`supplier_id`";

    public static readonly IReadOnlyDictionary<string, ErpWorkspaceSpec> Tabs =
        new Dictionary<string, ErpWorkspaceSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["ps_vendor_performance"] = new(
                ["Vendor", "Group", "POs", "Received", "Spend", "Currency"],
                "SELECT IFNULL(s.`name`,'(none)'), IFNULL(NULLIF(s.`vendor_group`,''),'(none)')," +
                " COUNT(DISTINCT p.`id`)," +
                " SUM(p.`status` = 'received')," +
                " IFNULL(SUM(p.`total_amount`),0), IFNULL(s.`currency_code`,'AED')" +
                SupplierJoin +
                " LEFT JOIN `epc_erp_purchase_orders` p ON p.`supplier_id` = s.`id`" +
                " WHERE IFNULL(s.`active`,1) = 1" +
                " GROUP BY s.`id` ORDER BY SUM(p.`total_amount`) DESC LIMIT 200",
                "No vendors yet — create suppliers in the Vendors page.",
                Suppliers,
                EnsurePurchasingAsync),

            ["ps_top100"] = new(
                ["#", "Vendor", "Purchases", "Spend", "Last purchase"],
                "SELECT @r := @r + 1," +
                " IFNULL(s.`name`,'(none)'), COUNT(b.`id`), IFNULL(SUM(b.`total_amount`),0)," +
                " FROM_UNIXTIME(IFNULL(MAX(b.`purchase_date`),0))" +
                PurchaseJoin +
                " CROSS JOIN (SELECT @r := 0) r" +
                " WHERE IFNULL(b.`active`,1) = 1" +
                " GROUP BY s.`id` ORDER BY SUM(b.`total_amount`) DESC LIMIT 100",
                "No purchase invoices yet — post one in Purchases.",
                Purchases,
                EnsurePurchasingAsync),

            ["ps_vendor_item_stats"] = new(
                ["Item", "PO lines", "Qty ordered", "Qty received", "Spend"],
                "SELECT IFNULL(NULLIF(l.`item_code`,''), NULLIF(l.`description`,''), '(unlisted)') AS item," +
                " COUNT(l.`id`), IFNULL(SUM(l.`qty`),0), IFNULL(SUM(l.`qty_received`),0)," +
                " IFNULL(SUM(l.`line_ex_vat`),0)" +
                " FROM `epc_erp_po_lines` l" +
                " GROUP BY item ORDER BY SUM(l.`line_ex_vat`) DESC LIMIT 200",
                "No purchase order lines yet — raise a PO with lines first.",
                Orders,
                EnsurePurchasingAsync),

            ["ps_req_stats"] = new(
                ["Month", "POs", "Amount", "Received"],
                "SELECT DATE_FORMAT(FROM_UNIXTIME(p.`time_created`),'%Y-%m')," +
                " COUNT(p.`id`), IFNULL(SUM(p.`total_amount`),0)," +
                " SUM(p.`status` = 'received')" +
                PoJoin +
                " GROUP BY 1 ORDER BY 1 DESC LIMIT 24",
                "No purchase orders yet — raise your first PO.",
                Orders,
                EnsurePurchasingAsync),

            ["ps_grn_summary"] = new(
                ["Vendor", "Invoices", "Ex VAT", "VAT", "Total", "Last"],
                "SELECT IFNULL(s.`name`,'(none)'), COUNT(b.`id`)," +
                " IFNULL(SUM(b.`amount_ex_vat`),0), IFNULL(SUM(b.`vat_amount`),0)," +
                " IFNULL(SUM(b.`total_amount`),0), FROM_UNIXTIME(IFNULL(MAX(b.`purchase_date`),0))" +
                PurchaseJoin +
                " WHERE IFNULL(b.`active`,1) = 1" +
                " GROUP BY s.`id` ORDER BY SUM(b.`total_amount`) DESC LIMIT 200",
                "No goods received yet — post a purchase invoice.",
                Purchases,
                EnsurePurchasingAsync),

            ["ps_grn_detail"] = new(
                ["Invoice", "Vendor", "Date", "Ex VAT", "VAT", "Total", "Status"],
                "SELECT IFNULL(b.`invoice_number`, CONCAT('PI-',b.`id`)), IFNULL(s.`name`,'(none)')," +
                " FROM_UNIXTIME(b.`purchase_date`), b.`amount_ex_vat`, b.`vat_amount`, b.`total_amount`," +
                " IFNULL(b.`status`,'confirmed')" +
                PurchaseJoin +
                " WHERE IFNULL(b.`active`,1) = 1" +
                " ORDER BY b.`purchase_date` DESC, b.`id` DESC LIMIT 200",
                "No goods received yet — post a purchase invoice.",
                Purchases,
                EnsurePurchasingAsync),

            ["ps_confirmation"] = new(
                ["PO", "Vendor", "Title", "Total", "Status", "Raised"],
                "SELECT p.`po_no`, IFNULL(s.`name`,'(none)'), IFNULL(p.`title`,'')," +
                " p.`total_amount`, p.`status`, FROM_UNIXTIME(p.`time_created`)" +
                PoJoin +
                " WHERE p.`status` IN ('draft','approved')" +
                " ORDER BY p.`id` DESC LIMIT 200",
                "No purchase orders awaiting confirmation.",
                Orders,
                EnsurePurchasingAsync),

            ["ps_receipt_list"] = new(
                ["PO", "Vendor", "Total", "Status", "Received at"],
                "SELECT p.`po_no`, IFNULL(s.`name`,'(none)'), p.`total_amount`, p.`status`," +
                " FROM_UNIXTIME(IFNULL(NULLIF(p.`received_at`,0), p.`time_updated`))" +
                PoJoin +
                " WHERE p.`status` IN ('partial','received')" +
                " ORDER BY p.`id` DESC LIMIT 200",
                "No received purchase orders yet.",
                Orders,
                EnsurePurchasingAsync),

            ["ps_txn_reports"] = new(
                ["Vendor", "Kind", "Reference", "Debit/Credit", "Amount", "When", "Note"],
                "SELECT IFNULL(s.`name`,'(none)'), IFNULL(a.`entry_kind`,'invoice')," +
                " IFNULL(a.`reference`,''), IF(a.`is_credit`=1,'debit','credit'), a.`amount`," +
                " FROM_UNIXTIME(a.`time`), LEFT(IFNULL(a.`note`,''),100)" +
                " FROM `epc_erp_supplier_accounting` a LEFT JOIN `epc_erp_suppliers` s ON s.`id` = a.`supplier_id`" +
                " WHERE IFNULL(a.`active`,1) = 1" +
                " ORDER BY a.`time` DESC, a.`id` DESC LIMIT 300",
                "No supplier transactions yet — purchases and payments post here.",
                Purchases,
                EnsurePurchasingAsync),

            ["ps_posting_log"] = new(
                ["Invoice", "Voucher", "Vendor", "Total", "GL journal", "Status"],
                "SELECT IFNULL(b.`invoice_number`, CONCAT('PI-',b.`id`)), IFNULL(b.`voucher_no`,'')," +
                " IFNULL(s.`name`,'(none)'), b.`total_amount`, IFNULL(b.`gl_journal_id`,0)," +
                " IFNULL(b.`status`,'confirmed')" +
                PurchaseJoin +
                " WHERE IFNULL(b.`active`,1) = 1" +
                " ORDER BY b.`id` DESC LIMIT 200",
                "No posted purchases yet.",
                Purchases,
                EnsurePurchasingAsync),

            ["ps_ledger_posting"] = new(
                ["Journal", "Reference", "Amount", "Posted", "Summary"],
                "SELECT j.`journal_no`, IFNULL(j.`reference`,'')," +
                " IFNULL((SELECT SUM(l.`debit`) FROM `epc_erp_gl_lines` l" +
                "  WHERE l.`journal_id` = j.`id`),0)," +
                " FROM_UNIXTIME(j.`time_created`), LEFT(IFNULL(j.`description`,''),120)" +
                " FROM `epc_erp_gl_journals` j WHERE j.`source_type` = 'purchase'" +
                " ORDER BY j.`id` DESC LIMIT 200",
                "No purchase GL postings yet — purchases post journals automatically.",
                Purchases,
                EnsurePurchasingAsync),

            ["ps_correct_po_lines"] = new(
                ["PO", "Line", "Item", "Description", "Qty", "Received", "Unit cost", "Status"],
                "SELECT p.`po_no`, l.`line_no`, IFNULL(l.`item_code`,''), LEFT(IFNULL(l.`description`,''),80)," +
                " l.`qty`, l.`qty_received`, l.`unit_cost_ex_vat`, p.`status`" +
                " FROM `epc_erp_po_lines` l JOIN `epc_erp_purchase_orders` p ON p.`id` = l.`po_id`" +
                " ORDER BY p.`id` DESC, l.`line_no` LIMIT 300",
                "No purchase order lines yet.",
                Orders,
                EnsurePurchasingAsync),

            ["ps_finalize_pos"] = new(
                ["PO", "Vendor", "Total", "Status", "Raised"],
                "SELECT p.`po_no`, IFNULL(s.`name`,'(none)'), p.`total_amount`, p.`status`," +
                " FROM_UNIXTIME(p.`time_created`)" +
                PoJoin +
                " WHERE p.`status` <> 'received' AND p.`status` <> 'cancelled'" +
                " ORDER BY p.`id` DESC LIMIT 200",
                "No open purchase orders to finalize.",
                Orders,
                EnsurePurchasingAsync),

            ["ps_vendor_groups"] = new(
                ["Vendor group", "Vendors", "Spend"],
                "SELECT IFNULL(NULLIF(s.`vendor_group`,''),'(ungrouped)'), COUNT(s.`id`)," +
                " (SELECT IFNULL(SUM(b.`total_amount`),0) FROM `epc_erp_purchases` b" +
                "  WHERE b.`supplier_id` IN (SELECT id FROM `epc_erp_suppliers` x WHERE IFNULL(NULLIF(x.`vendor_group`,''),'(ungrouped)') = IFNULL(NULLIF(s.`vendor_group`,''),'(ungrouped)')))" +
                SupplierJoin +
                " WHERE IFNULL(s.`active`,1) = 1" +
                " GROUP BY 1 ORDER BY 1 LIMIT 100",
                "No vendor groups — set the group on supplier records.",
                Suppliers,
                EnsurePurchasingAsync),

            ["ps_vendor_category"] = new(
                ["Category", "Vendors", "Countries"],
                "SELECT IFNULL(NULLIF(s.`vendor_group`,''),'(uncategorized)'), COUNT(s.`id`)," +
                " GROUP_CONCAT(DISTINCT NULLIF(s.`country_code`,'') SEPARATOR ', ')" +
                SupplierJoin +
                " WHERE IFNULL(s.`active`,1) = 1" +
                " GROUP BY 1 ORDER BY 1 LIMIT 100",
                "No vendor categories yet.",
                Suppliers,
                EnsurePurchasingAsync),

            ["ps_vendor_price_groups"] = new(
                ["Terms", "Vendors"],
                "SELECT IFNULL(NULLIF(s.`payment_terms`,''),'(none)'), COUNT(s.`id`)" +
                SupplierJoin +
                " WHERE IFNULL(s.`active`,1) = 1" +
                " GROUP BY 1 ORDER BY 1 LIMIT 100",
                "No payment terms set on suppliers yet.",
                Suppliers,
                EnsurePurchasingAsync),

            ["ps_price_disc_groups"] = new(
                ["Currency", "Vendors", "Avg spend"],
                "SELECT IFNULL(s.`currency_code`,'AED'), COUNT(s.`id`)," +
                " (SELECT IFNULL(AVG(b.`total_amount`),0) FROM `epc_erp_purchases` b" +
                "  WHERE b.`supplier_id` = s.`id`)" +
                SupplierJoin +
                " WHERE IFNULL(s.`active`,1) = 1" +
                " GROUP BY 1 ORDER BY 1 LIMIT 50",
                "No vendor currencies yet.",
                Suppliers,
                EnsurePurchasingAsync),

            ["ps_vendor_catalogs"] = new(
                ["Vendor", "Items supplied"],
                "SELECT IFNULL(s.`name`,'(none)'), COUNT(i.`id`)" +
                SupplierJoin +
                " LEFT JOIN `epc_erp_inv_items` i ON i.`default_vendor_id` = s.`id`" +
                " WHERE IFNULL(s.`active`,1) = 1" +
                " GROUP BY s.`id` ORDER BY COUNT(i.`id`) DESC, s.`name` LIMIT 200",
                "No vendor catalogs — set a default vendor on items.",
                Suppliers,
                EnsurePurchasingAsync),

            ["ps_catalog_vendors"] = new(
                ["Vendor", "Items supplied", "Preferred on items"],
                "SELECT IFNULL(s.`name`,'(none)'), COUNT(i.`id`)," +
                " SUM(NULLIF(i.`preferred_vendor_name`,'') IS NOT NULL)" +
                SupplierJoin +
                " LEFT JOIN `epc_erp_inv_items` i ON i.`default_vendor_id` = s.`id`" +
                " WHERE IFNULL(s.`active`,1) = 1" +
                " GROUP BY s.`id` ORDER BY COUNT(i.`id`) DESC LIMIT 200",
                "No vendor catalogs — link items to a default vendor.",
                Suppliers,
                EnsurePurchasingAsync),

            ["ps_multisource"] = new(
                ["SKU", "Item", "Preferred vendor", "Manufacturer part"],
                "SELECT IFNULL(i.`sku`,''), IFNULL(i.`name`,'')," +
                " IFNULL(NULLIF(i.`preferred_vendor_name`,''),'(none)'), IFNULL(i.`manufacturer_part`,'')" +
                " FROM `epc_erp_inv_items` i" +
                " WHERE IFNULL(i.`active`,1) = 1" +
                " ORDER BY i.`sku` LIMIT 200",
                "No sourcing assignments — set preferred vendors on items.",
                Suppliers,
                EnsurePurchasingAsync),
        };

    /// <summary>Lazy-creates every table the purchasing specs read (idempotent).</summary>
    internal static async Task EnsurePurchasingAsync(
        System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpLazySchema.EnsureInventoryAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpPurchaseOrderWriteService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpPurchaseInvoiceWriteService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpCashWriteService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpLazySchema.EnsureSuppliersAsync(connection, cancellationToken).ConfigureAwait(false);
    }
}
