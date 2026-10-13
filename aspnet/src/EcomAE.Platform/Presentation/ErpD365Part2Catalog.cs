namespace EcomAE.Platform.Presentation;

/// <summary>
/// Dynamics-style workspace pages taken from the Procurement, Product information,
/// Production, Project, Questionnaire, Rebate, Retail, Sales, Service, System
/// administration, Tax, Time and attendance, Transportation, and Warehouse module menus.
/// Existing dedicated apps stay linked; missing pages land on the workspace writer.
/// </summary>
public static class ErpD365Part2Catalog
{
    public const string ProcurementApp = "/erp/procurement-workspace-app";
    public const string PimApp = "/erp/pim-workspace-app";
    public const string ProductionApp = "/erp/production-workspace-app";
    public const string ProjectsApp = "/erp/projects-workspace-app";
    public const string QuestionnaireApp = "/erp/questionnaire-workspace-app";
    public const string RebateApp = "/erp/rebate-workspace-app";
    public const string RetailApp = "/erp/retail-workspace-app";
    public const string SalesApp = "/erp/sales-workspace-app";
    public const string ServiceApp = "/erp/service-workspace-app";
    public const string SystemAdminApp = "/erp/system-admin-workspace-app";
    public const string TaxApp = "/erp/tax-workspace-app";
    public const string TimeAttApp = "/erp/time-attendance-app";
    public const string TransportationApp = "/erp/transportation-workspace-app";
    public const string WarehouseApp = "/erp/warehouse-workspace-app";

    public static readonly IReadOnlyList<ErpD365Part3Catalog.Page> All = Build();

    public static IReadOnlyList<ErpD365Part3Catalog.Page> ForArea(string? area)
    {
        var key = (area ?? string.Empty).Trim();
        if (key.Length == 0)
        {
            return All;
        }

        return All.Where(p => p.Area.Equals(key, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public static bool TryGet(string? tab, out ErpD365Part3Catalog.Page page)
    {
        page = All.FirstOrDefault(p => p.Tab.Equals((tab ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))!;
        return page is not null;
    }

    public static bool TryMapTab(string? tab, out string href)
    {
        href = string.Empty;
        if (!TryGet(tab, out var page))
        {
            return false;
        }

        href = page.ExistingHref ?? (page.AppPath + "?tab=" + Uri.EscapeDataString(page.Tab));
        return true;
    }

    public static string AppPathForArea(string? area)
        => (area ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "purchasing" => ProcurementApp,
            "pim" => PimApp,
            "production" => ProductionApp,
            "projects" => ProjectsApp,
            "questionnaire" => QuestionnaireApp,
            "rebate_mgmt" => RebateApp,
            "retail" => RetailApp,
            "sales" => SalesApp,
            "service_mgmt" => ServiceApp,
            "setup" => SystemAdminApp,
            "tax" => TaxApp,
            "time_att" => TimeAttApp,
            "transportation" or "logistics" => TransportationApp,
            "warehouse" => WarehouseApp,
            _ => PimApp,
        };

    public static IReadOnlyList<PhpModuleCatalog.ModuleLink> AsModuleLinks()
        => All
            .Where(p => p.ExistingHref is null)
            .Select(p => new PhpModuleCatalog.ModuleLink(
                p.Area + "/" + p.Tab,
                p.Label,
                "/ERP/?epc_erp_shell=1&area=" + p.Area + "&tab=" + p.Tab,
                p.Icon,
                p.Area,
                p.Purpose,
                false,
                false,
                p.Group))
            .ToList();

    public static IReadOnlyList<string> RequiredLabels(string area)
        => ForArea(area).Select(p => p.Label).ToList();

    private static IReadOnlyList<ErpD365Part3Catalog.Page> Build()
    {
        var rows = new List<ErpD365Part3Catalog.Page>(900);
        rows.AddRange(ProcurementPages());
        rows.AddRange(PimPages());
        rows.AddRange(ProductionPages());
        rows.AddRange(ProjectPages());
        rows.AddRange(QuestionnairePages());
        rows.AddRange(RebatePages());
        rows.AddRange(RetailPages());
        rows.AddRange(SalesPages());
        rows.AddRange(ServicePages());
        rows.AddRange(SystemPages());
        rows.AddRange(TaxPages());
        rows.AddRange(TimeAttPages());
        rows.AddRange(TransportationPages());
        rows.AddRange(WarehousePages());
        return rows;
    }

    private static ErpD365Part3Catalog.Page P(
        string area,
        string tab,
        string label,
        string group,
        string purpose,
        ErpD365Part3Catalog.FormKind kind,
        string app,
        string? existing = null,
        string icon = "fa-th-large")
        => new(area, tab, label, group, purpose, kind, app, existing, icon);

    private static IEnumerable<ErpD365Part3Catalog.Page> ProcurementPages()
    {
        const string a = "purchasing";
        const string app = ProcurementApp;
        yield return P(a, "ps_vendor_catalogs", "Vendor catalogs", "Catalogs", "Vendor-published catalogs used on requisitions and purchase orders.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-book");
        yield return P(a, "ps_external_catalogs", "External catalogs", "Catalogs", "Punch-out catalogs hosted by a vendor.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-external-link");
        yield return P(a, "ps_catalog_vendors", "Vendor catalog items", "Catalogs", "Items offered from a vendor catalog.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-cube");
        yield return P(a, "ps_price_disc_groups", "Price/discount groups", "Prices and discounts", "Price and discount groups used on purchase trade agreements.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "ps_vendor_price_groups", "Vendor price/discount groups", "Prices and discounts", "Vendor groups that share purchase prices and discounts.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-users");
        yield return P(a, "ps_trade_agreements", "Trade agreements", "Prices and discounts", "Purchase price, discount, and charge agreements.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-file-text-o");
        yield return P(a, "ps_trade_journals", "Trade agreement journals", "Prices and discounts", "Journals that activate purchase trade agreements.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-book");
        yield return P(a, "ps_trade_journal_names", "Trade agreement journal names", "Prices and discounts", "Named journals for purchase trade agreements.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "ps_activate_price", "Activate price/discount", "Prices and discounts", "Activate posted purchase trade agreements.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-check");
        yield return P(a, "ps_attr_pricing", "Attribute-based pricing", "Prices and discounts", "Price rules driven by product attributes.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-sliders");
        yield return P(a, "ps_multisource", "Multi-source policy assignments", "Prices and discounts", "Which vendors may supply a category or item.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-random");
        yield return P(a, "ps_agreement_fulfill", "Purchase agreement fulfillment report", "Purchase agreements", "Quantity or value consumed against a purchase agreement.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-bar-chart");
        yield return P(a, "ps_rebate_agreements", "Rebate agreements", "Purchase agreements", "Vendor rebate agreement headers.", ErpD365Part3Catalog.FormKind.Procurement, app, icon: "fa-percent");
        yield return P(a, "ps_rebate_claims", "Rebate claims", "Purchase agreements", "Claims generated from rebate agreements.", ErpD365Part3Catalog.FormKind.Procurement, app, icon: "fa-ticket");
        yield return P(a, "ps_cumulate_rebate", "Cumulate rebate", "Purchase agreements", "Accumulate rebate quantities before a claim.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-plus");
        yield return P(a, "ps_process_rebates", "Process rebates", "Purchase agreements", "Process due rebate claims.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-cogs");
        yield return P(a, "ps_calculate_rebate", "Calculate rebate", "Purchase agreements", "Calculate rebate amounts from purchase history.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-calculator");
        yield return P(a, "ps_purge_rebate", "Purge rebate", "Purchase agreements", "Remove obsolete rebate calculation rows.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-trash");
        yield return P(a, "ps_rebate_types", "Rebate program types", "Purchase agreements", "Named rebate program types.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "ps_rebate_groups", "Rebate groups", "Purchase agreements", "Item or vendor groups used by rebate programs.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-object-group");
        yield return P(a, "ps_vendor_rebate_groups", "Vendor rebate groups", "Purchase agreements", "Vendor groups that share a rebate program.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-users");
        yield return P(a, "ps_consignment", "Consignment", "Purchase agreements", "Consignment inventory owned by the vendor.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-inbox");
        yield return P(a, "ps_consign_replenish", "Consignment replenishment orders", "Purchase agreements", "Replenish consignment stock at a warehouse.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-refresh");
        yield return P(a, "ps_ownership_change", "Inventory ownership change", "Purchase agreements", "Take ownership of consignment stock.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-exchange");
        yield return P(a, "ps_confirmation", "Confirmation", "Vendor", "Purchase-order confirmations sent to or received from a vendor.", ErpD365Part3Catalog.FormKind.Procurement, app, icon: "fa-check");
        yield return P(a, "ps_receipt_list", "Receipt list", "Vendor", "Expected receipt list for a purchase order.", ErpD365Part3Catalog.FormKind.Procurement, app, icon: "fa-list");
        yield return P(a, "ps_txn_reports", "Transaction reports", "Vendor", "Posted purchase transactions for a vendor.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list-alt");
        yield return P(a, "ps_posting_log", "Purchase posting log", "Vendor", "Log of purchase-order posting runs.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-history");
        yield return P(a, "ps_ledger_posting", "Ledger posting for purchase orders", "Vendor", "Ledger accounts used when a purchase order posts.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-book");
        yield return P(a, "ps_rfq_journals", "Request for quotation journals", "Request for quotation", "Journals that issue or award an RFQ.", ErpD365Part3Catalog.FormKind.Procurement, app, "/erp/rfq-app", "fa-book");
        yield return P(a, "ps_rfq_item_vendor", "Request for quotation item/vendor", "Request for quotation", "RFQ lines by item and invited vendor.", ErpD365Part3Catalog.FormKind.Procurement, app, "/erp/rfq-app", "fa-exchange");
        yield return P(a, "ps_correct_po_lines", "Correct purchase order lines manually", "Request for quotation", "Correct a purchase-order line quantity or price.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-pencil");
        yield return P(a, "ps_disconnect_ic", "Disconnect intercompany orders", "Request for quotation", "Break the link between intercompany purchase and sales orders.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-unlink");
        yield return P(a, "ps_vendor_performance", "Vendor performance", "Inquiries and reports", "On-time receipt, quality, and price performance.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-line-chart");
        yield return P(a, "ps_vendor_item_stats", "Vendor/item statistics", "Inquiries and reports", "Spend and receipt statistics by vendor and item.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-bar-chart");
        yield return P(a, "ps_top100", "Top 100 vendors", "Inquiries and reports", "Highest-spend vendors in the selected period.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-trophy");
        yield return P(a, "ps_req_stats", "Purchase requisition statistics", "Inquiries and reports", "Requisition counts and amounts by status.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-pie-chart");
        yield return P(a, "ps_vendor_aging", "Vendor aging", "Inquiries and reports", "Open purchase and invoice aging for vendors.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/aging-app?tab=ap", "fa-hourglass");
        yield return P(a, "ps_grn_detail", "Purchase GRN detail report", "Inquiries and reports", "Goods-received note lines against purchase orders.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-file-text-o");
        yield return P(a, "ps_grn_summary", "Purchase GRN summary report", "Inquiries and reports", "Goods-received totals by vendor or period.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-files-o");
        yield return P(a, "ps_barcode_print", "Barcode print", "Inquiries and reports", "Print item barcodes for received stock.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-barcode");
        yield return P(a, "ps_finalize_pos", "Finalize purchase orders", "Periodic tasks", "Close remaining quantity on completed purchase orders.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-flag-checkered");
        yield return P(a, "ps_cleanup_history", "Purchase update history cleanup", "Cleanup", "Remove stale purchase update history.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-trash");
        yield return P(a, "ps_cleanup_unconfirmed", "Unconfirmed delivery date cleanup", "Cleanup", "Clear delivery dates that were never confirmed.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-eraser");
        yield return P(a, "ps_delete_rfq", "Delete request for quotations", "Cleanup", "Delete RFQs that were never awarded.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-minus-circle");
        yield return P(a, "ps_parameters", "Procurement and sourcing parameters", "Setup", "Company defaults for purchase orders, RFQ, and requisitions.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "ps_workflows", "Purchase and sourcing workflows", "Setup", "Approval workflows for requisitions, RFQ, and purchase orders.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/workflows-app", "fa-sitemap");
        yield return P(a, "ps_number_seq", "Number sequences", "Setup", "Number sequences for purchase documents.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list-ol");
        yield return P(a, "ps_vendors", "Vendors", "Setup", "Vendor master used on purchase documents.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/suppliers-app", "fa-truck");
        yield return P(a, "ps_vendor_groups", "Vendor groups", "Setup", "Posting and terms defaults shared by a vendor group.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-users");
        yield return P(a, "ps_vendor_reasons", "Vendor reasons", "Setup", "Reason codes for vendor hold or change.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-comment");
        yield return P(a, "ps_eval_criteria", "Vendor evaluation criteria", "Setup", "Criteria used when scoring vendor performance.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-check-square-o");
        yield return P(a, "ps_vendor_category", "Vendor category", "Setup", "Category a vendor belongs to.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sitemap");
        yield return P(a, "ps_catalog_setup", "Catalog", "Setup", "Internal catalog headers used on requisitions.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-book");
        yield return P(a, "ps_certification", "Certification", "Setup", "Vendor certifications required for a category.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-certificate");
        yield return P(a, "ps_line_of_business", "Line of business", "Setup", "Vendor line-of-business codes.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-briefcase");
        yield return P(a, "ps_vendor_collab", "Vendor collaboration", "Setup", "What a vendor may see and confirm in collaboration.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/suppliers-app?tab=supplier_portal", "fa-handshake-o");
        yield return P(a, "ps_info_for_vendors", "Information for vendors responding to POs", "Setup", "Text sent with a purchase order to the vendor.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-info-circle");
        yield return P(a, "ps_policies", "Policies", "Setup", "Spending and catalog policies for requisitions.", ErpD365Part3Catalog.FormKind.Plan, app, "/erp/procurement-categories-app", "fa-gavel");
        yield return P(a, "ps_justifications", "Business justifications", "Setup", "Justification codes required on a requisition.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-commenting");
        yield return P(a, "ps_catalog_perm", "Catalog permissions", "Setup", "Who may browse or request from a catalog.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-lock");
        yield return P(a, "ps_form_notes", "Form notes", "Setup", "Print notes on purchase documents.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sticky-note-o");
        yield return P(a, "ps_form_setup", "Form setup", "Setup", "Print layout for purchase documents.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-print");
        yield return P(a, "ps_form_sorting", "Form sorting parameters", "Setup", "Line sort order on printed purchase documents.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sort");
        yield return P(a, "ps_product_rel", "Product relationship types", "Setup", "How related products appear on a purchase catalog.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-link");
        yield return P(a, "ps_solicitation", "Solicitation type", "Setup", "RFQ solicitation types.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-bullhorn");
        yield return P(a, "ps_scoring", "Scoring method", "Setup", "How vendor RFQ replies are scored.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-star");
        yield return P(a, "ps_bidding_reply", "Bidding reply type", "Setup", "Reply types a vendor may send on an RFQ.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-reply");
        yield return P(a, "ps_purchase_class", "Purchase classification", "Setup", "Classify purchase orders for reporting.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "ps_purchase_pools", "Purchase pools", "Setup", "Pools that group purchase orders for processing.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-object-group");
        yield return P(a, "ps_return_action", "Return action", "Setup", "Actions taken when a purchase return is posted.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-undo");
        yield return P(a, "ps_confirming_po", "Confirming PO codes", "Setup", "Codes that mark a confirming purchase order.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-check-circle");
        yield return P(a, "ps_cxml", "cXML parameters", "Setup", "cXML punch-out and order transmission.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-code");
        yield return P(a, "ps_po_request", "Purchase order request", "Setup", "Request a purchase order from a catalog or requisition.", ErpD365Part3Catalog.FormKind.Procurement, app, "/erp/purchase-requests-app", "fa-plus");
        yield return P(a, "ps_fixed_assets", "Fixed assets", "Setup", "Fixed-asset identifiers created from a purchase order.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/fixed-assets-app", "fa-building");
        yield return P(a, "ps_fa_identifiers", "Organization-wide fixed asset identifiers", "Setup", "Shared asset identifier rules across legal entities.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-barcode");
        yield return P(a, "ps_fa_rules", "Business rules for fixed asset determination", "Setup", "When a purchased item becomes a fixed asset.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-gavel");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> PimPages()
    {
        const string a = "pim";
        const string app = PimApp;
        yield return P(a, "pim_released_maint", "Released product maintenance", "Workspaces", "Maintain released products that need a change.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/product-info-app", "fa-wrench");
        yield return P(a, "pim_variant_model", "Product variant model definition", "Workspaces", "Define how variants are generated from a product master.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-sitemap");
        yield return P(a, "pim_readiness_discrete", "Product readiness for discrete manufacturing", "Workspaces", "Check that a released product is ready for discrete production.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-check-square-o");
        yield return P(a, "pim_readiness_process", "Product readiness for process manufacturing", "Workspaces", "Check that a released product is ready for process production.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-flask");
        yield return P(a, "pim_all_products", "All products and product masters", "Products", "Every product and product master in the shared catalog.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/product-info-app", "fa-cubes");
        yield return P(a, "pim_product_masters", "Product masters", "Products", "Masters that generate variants from dimensions.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/product-info-app", "fa-cube");
        yield return P(a, "pim_variants", "Product variants", "Products", "Variants generated from a product master.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/product-info-app", "fa-th");
        yield return P(a, "pim_by_category", "Products by category", "Products", "Products in a procurement or sales category.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sitemap");
        yield return P(a, "pim_released", "Released products", "Products", "Products released to this legal entity.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/product-info-app", "fa-check");
        yield return P(a, "pim_released_by_cat", "Released products by category", "Products", "Released products in a category.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sitemap");
        yield return P(a, "pim_config_models", "Product configuration models", "Products", "Constraint-based configuration models.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-cogs");
        yield return P(a, "pim_part_number", "Item part number", "Products", "External part numbers stored on an item.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-barcode");
        yield return P(a, "pim_cross_part", "Item cross part number", "Products", "Cross-reference part numbers between items.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-exchange");
        yield return P(a, "pim_item_csv", "Item details CSV view", "Products", "Export or review item details as a table.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-table");
        yield return P(a, "pim_item_update", "Item master update", "Products", "Batch-update released product fields.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-refresh");
        yield return P(a, "pim_track_storage", "Tracking and storage dimension", "Products", "Assign tracking and storage dimension groups.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-th-large");
        yield return P(a, "pim_cross_ref", "Item cross reference", "Products", "Customer or vendor item-number cross references.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-link");
        yield return P(a, "pim_boms", "Bills of materials", "Bills of materials and formulas", "Discrete bills of materials.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list");
        yield return P(a, "pim_formulas", "Formulas", "Bills of materials and formulas", "Process formulas and co-products.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-flask");
        yield return P(a, "pim_bom_versions", "BOM versions", "Bills of materials and formulas", "Effective BOM versions for a product.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-code-fork");
        yield return P(a, "pim_all_cases", "All cases", "Cases", "Product-information cases.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-briefcase");
        yield return P(a, "pim_my_cases", "My cases", "Cases", "Cases assigned to this worker.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-user");
        yield return P(a, "pim_open_cases", "Open cases", "Cases", "Cases that are not closed.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-folder-open-o");
        yield return P(a, "pim_lean_schedule", "Lean schedule groups", "Lean manufacturing", "Schedule groups used by lean production.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-calendar");
        yield return P(a, "pim_kanban_rules", "Kanban rules", "Lean manufacturing", "Kanban replenishment rules on a product.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-refresh");
        yield return P(a, "pim_circulating", "Circulating cards", "Lean manufacturing", "Circulating kanban cards.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-id-card-o");
        yield return P(a, "pim_costing_versions", "Costing versions", "Costing", "Costing versions used when calculating a product.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-calculator");
        yield return P(a, "pim_cost_calc", "Item price calculation", "Costing", "Calculate a product cost from BOM and route.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-money");
        yield return P(a, "pim_no_active_cost", "Released products with no active cost", "Inquiries and reports", "Released products missing an active cost.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-exclamation-triangle");
        yield return P(a, "pim_to_configure", "All released products to configure", "Inquiries and reports", "Released products waiting for configuration.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-cogs");
        yield return P(a, "pim_bom_lines", "Bill of materials lines", "Inquiries and reports", "BOM component lines across products.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list-ul");
        yield return P(a, "pim_calc_details", "Calculation details of cost and price", "Inquiries and reports", "Cost-calculation breakdown for a product.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-table");
        yield return P(a, "pim_lifecycle_hist", "Product lifecycle state maintenance history", "Inquiries and reports", "History of lifecycle-state changes.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-history");
        yield return P(a, "pim_inv_maker", "Inventory maker", "Inquiries and reports", "Where a product is manufactured or stored.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-industry");
        yield return P(a, "pim_haz_limits", "Hazardous materials stock limits", "Inquiries and reports", "Stock limits for hazardous products.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-warning");
        yield return P(a, "pim_retail_label", "Retail label print", "Inquiries and reports", "Print retail shelf or product labels.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-tag");
        yield return P(a, "pim_paper_setup", "All paper setup", "Inquiries and reports", "Label paper sizes and layouts.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-file-o");
        yield return P(a, "pim_open_releases", "Open product releases", "Periodic tasks", "Product releases that are not finished.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-unlock");
        yield return P(a, "pim_recalc_bom", "Recalculate BOM levels", "Periodic tasks", "Recalculate low-level codes on BOMs.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-sort-amount-asc");
        yield return P(a, "pim_obsolete_state", "Change lifecycle state for obsolete products", "Periodic tasks", "Move unused products to an obsolete lifecycle state.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-archive");
        yield return P(a, "pim_change_bom_item", "Change BOM item", "Periodic tasks", "Replace a component across BOMs.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-exchange");
        yield return P(a, "pim_check_bom", "Check BOM", "Periodic tasks", "Validate BOM structure and quantities.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-check");
        yield return P(a, "pim_check_formula", "Check formula", "Periodic tasks", "Validate formula structure and yield.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-check-circle");
        yield return P(a, "pim_not_stocked", "Convert products to not stocked", "Periodic tasks", "Mark products that should not be stocked.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-ban");
        yield return P(a, "pim_cleanup_conv", "Cleanup conversion log to products not stocked", "Periodic tasks", "Clear the not-stocked conversion log.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-trash");
        yield return P(a, "pim_cost_conv", "Cost conversions", "Periodic tasks", "Convert costs between costing versions.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-refresh");
        yield return P(a, "pim_std_cost_conv", "Standard cost conversions", "Periodic tasks", "Convert standard costs to a new version.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-money");
        yield return P(a, "pim_wh_hier", "Storage dimension hierarchy for items", "Periodic tasks", "Storage-dimension hierarchy on items.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sitemap");
        yield return P(a, "pim_track_group", "Tracking dimension group for items", "Periodic tasks", "Assign tracking dimension groups in batch.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-barcode");
        yield return P(a, "pim_populate_distinct", "Populate distinct product", "Periodic tasks", "Create distinct products from variants.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-plus");
        yield return P(a, "pim_cleanup_attr", "Cleanup product attribute values", "Periodic tasks", "Remove unused product attribute values.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-eraser");
        yield return P(a, "pim_populate_attr", "Populate product attribute values", "Periodic tasks", "Fill attribute values from a source.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-download");
        yield return P(a, "pim_price_to_date", "Price to date batch task", "Periodic tasks", "Activate prices that become effective today.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-calendar");
        yield return P(a, "pim_parameters", "Product information management parameters", "Setup", "Company defaults for products, variants, and releases.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "pim_compliance", "Product compliance", "Setup", "Restricted-product and safety-data compliance.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-shield");
        yield return P(a, "pim_restricted", "Restricted products regional lists", "Setup", "Products restricted in a country or region.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-ban");
        yield return P(a, "pim_sds_validity", "Product safety data sheet validity", "Setup", "How long a safety data sheet stays valid.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-clock-o");
        yield return P(a, "pim_coo", "Country of origin", "Setup", "Country or region of origin on a product.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-globe");
        yield return P(a, "pim_coo_vendor", "Country of origin vendor certificates", "Setup", "Vendor certificates that prove country of origin.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-certificate");
        yield return P(a, "pim_dual_use", "Dual use products", "Setup", "Products that need a dual-use export check.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-exchange");
        yield return P(a, "pim_approved_cust", "Approved customer list", "Setup", "Customers approved to buy a restricted product.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-user");
        yield return P(a, "pim_approved_item", "Approved item list", "Setup", "Items a customer is approved to buy.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-cube");
        yield return P(a, "pim_categories", "Category hierarchy", "Categories and attributes", "Product category trees.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sitemap");
        yield return P(a, "pim_attr_groups", "Attribute groups", "Categories and attributes", "Groups of product attributes.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-object-group");
        yield return P(a, "pim_attr_types", "Attribute types", "Categories and attributes", "Data types used by product attributes.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "pim_attributes", "Attributes", "Categories and attributes", "Product attributes and default values.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list");
        yield return P(a, "pim_haz_class", "Hazardous material classification", "Hazardous materials", "UN class and packing group.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-warning");
        yield return P(a, "pim_haz_label", "Hazardous material label", "Hazardous materials", "Labels printed for hazardous products.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tag");
        yield return P(a, "pim_haz_packing", "Hazardous material packing instruction", "Hazardous materials", "Packing instructions for hazardous goods.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-archive");
        yield return P(a, "pim_warranty", "Warranty groups", "Warranty", "Warranty groups assigned to a product.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-shield");
        yield return P(a, "pim_product_upload", "Product upload", "Setup", "Upload products from a workbook.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-upload");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> ProductionPages()
    {
        const string a = "production";
        const string app = ProductionApp;
        yield return P(a, "prd_floor", "Production floor management", "Workspaces", "Floor view of jobs, feedback, and exceptions.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/production-overview-app", "fa-industry");
        yield return P(a, "prd_jobs_activities", "Jobs and activities", "Workspaces", "Production jobs and shop-floor activities.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-tasks");
        yield return P(a, "prd_all_orders", "All production orders", "Production orders", "Every production order regardless of status.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/production-overview-app", "fa-list");
        yield return P(a, "prd_planned", "Planned production orders", "Production orders", "Planned production orders not yet firmed.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-lightbulb-o");
        yield return P(a, "prd_delayed", "Delayed production orders", "Production orders", "Orders behind the scheduled finish.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-clock-o");
        yield return P(a, "prd_job_card", "Job card", "Production orders", "Job cards issued to the shop floor.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-id-card-o");
        yield return P(a, "prd_scheduled", "Productions scheduled", "Production orders", "Orders that have been scheduled.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-calendar");
        yield return P(a, "prd_started", "Productions started", "Production orders", "Orders that have been started.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-play");
        yield return P(a, "prd_released", "Productions released", "Production orders", "Orders released to the floor.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-unlock");
        yield return P(a, "prd_deferred", "Deferred batch order posting", "Production orders", "Batch orders waiting to post.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-pause");
        yield return P(a, "prd_current_ops", "Current operations", "Operations", "Operations running now.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-cogs");
        yield return P(a, "prd_all_routes", "All routes", "Operations", "Routes used by production orders.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-random");
        yield return P(a, "prd_route_card", "Route card", "Operations", "Route cards for a production order.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-list-alt");
        yield return P(a, "prd_batch_orders", "Batch orders", "Operations", "Process batch orders.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-flask");
        yield return P(a, "prd_kanbans", "Kanbans", "Kanban", "Active kanban cards.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-refresh");
        yield return P(a, "prd_kanban_board", "Kanban board for process jobs", "Kanban", "Process-job kanban board.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-th");
        yield return P(a, "prd_kanban_xfer", "Kanban board for transfer jobs", "Kanban", "Transfer-job kanban board.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-exchange");
        yield return P(a, "prd_kanban_schedule", "Kanban schedule board", "Kanban", "Schedule board for kanban jobs.", ErpD365Part3Catalog.FormKind.Calendar, app, icon: "fa-calendar");
        yield return P(a, "prd_picking_list", "Picking list", "Adjustments", "Picking lists for production material.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-hand-paper-o");
        yield return P(a, "prd_route_adj", "Route card journal", "Adjustments", "Post route-card time and quantity.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-clock-o");
        yield return P(a, "prd_raf", "Report as finished", "Adjustments", "Report a production order as finished.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-flag-checkered");
        yield return P(a, "prd_dispense", "Dispensing pick journal", "Adjustments", "Dispense material for a batch order.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-tint");
        yield return P(a, "prd_stockout", "Material stockout", "Inquiries and reports", "Components that are short for an order.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-exclamation-triangle");
        yield return P(a, "prd_job_list", "Job list", "Inquiries and reports", "Jobs waiting or in process.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list");
        yield return P(a, "prd_overview", "Production overview", "Inquiries and reports", "Status overview of production orders.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/production-overview-app", "fa-dashboard");
        yield return P(a, "prd_posting", "Production posting", "Inquiries and reports", "Ledger postings from production.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-book");
        yield return P(a, "prd_route_txn", "Route transactions", "Inquiries and reports", "Posted route time and quantity.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-history");
        yield return P(a, "prd_pick_report", "Picking list report", "Inquiries and reports", "Printed picking lists.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-print");
        yield return P(a, "prd_route_report", "Route report", "Inquiries and reports", "Printed route cards.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-file-text-o");
        yield return P(a, "prd_scheduling", "Scheduling", "Periodic tasks", "Schedule selected production orders.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-calendar");
        yield return P(a, "prd_cap_res", "Capacity reservations", "Periodic tasks", "Capacity reserved by scheduled orders.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-lock");
        yield return P(a, "prd_wip", "Work in process", "Periodic tasks", "WIP value of open production orders.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-spinner");
        yield return P(a, "prd_cost_est", "Cost estimates and costing", "Periodic tasks", "Estimate and actual cost of an order.", ErpD365Part3Catalog.FormKind.Forecast, app, icon: "fa-calculator");
        yield return P(a, "prd_batch_cost", "Batch cost", "Periodic tasks", "Cost a process batch order.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-flask");
        yield return P(a, "prd_lean_flow", "Lean production flow", "Periodic tasks", "Lean production-flow quantities.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-random");
        yield return P(a, "prd_lean_var", "Lean production variances", "Periodic tasks", "Variances on a lean production flow.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-balance-scale");
        yield return P(a, "prd_performance", "Production performance analysis", "Periodic tasks", "Efficiency and throughput analysis.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-line-chart");
        yield return P(a, "prd_eng_change", "Engineering change", "Periodic tasks", "Engineering-change orders on a product.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-wrench");
        yield return P(a, "prd_e_notify", "Electronic notifications", "Periodic tasks", "Shop-floor electronic notifications.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-bell");
        yield return P(a, "prd_master_record", "Master production record", "Periodic tasks", "Master production record for a batch.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-book");
        yield return P(a, "prd_epr", "Batch production report (EPR)", "Periodic tasks", "Electronic batch production report.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-file-text-o");
        yield return P(a, "prd_estimate", "Estimation", "Production order status update", "Estimate material and time before release.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-calculator");
        yield return P(a, "prd_release", "Release", "Production order status update", "Release an order to the shop floor.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-unlock");
        yield return P(a, "prd_start", "Start", "Production order status update", "Start a released production order.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-play");
        yield return P(a, "prd_end", "End", "Production order status update", "End a finished production order.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-stop");
        yield return P(a, "prd_reset", "Reset status", "Production order status update", "Reset an order to an earlier status.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-undo");
        yield return P(a, "prd_revert", "Revert completed status for a job", "Production order status update", "Reopen a completed job.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-repeat");
        yield return P(a, "prd_change_bom", "Change product or BOM item", "Production order status update", "Change the product or BOM on an order.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-exchange");
        yield return P(a, "prd_change_route", "Production order change route", "Production order status update", "Change the route on an order.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-random");
        yield return P(a, "prd_kanban_qty_calc", "Kanban quantity calculation", "Production order status update", "Recalculate kanban quantities.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-calculator");
        yield return P(a, "prd_cleanup_journals", "Production journals cleanup", "Cleanup", "Remove posted production journals.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-trash");
        yield return P(a, "prd_cleanup_reg", "Cleanup registrations", "Cleanup", "Remove stale manufacturing-execution registrations.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-eraser");
        yield return P(a, "prd_parameters", "Production control parameters", "Setup", "Company defaults for orders, journals, and posting.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "prd_units", "Production units", "Setup", "Production units that own resources.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-industry");
        yield return P(a, "prd_pools", "Production pools", "Setup", "Pools that group production orders.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-object-group");
        yield return P(a, "prd_alloc_keys", "Allocation keys", "Setup", "Split material or cost across orders.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-pie-chart");
        yield return P(a, "prd_tracked", "Tracked components policy", "Setup", "When components must be tracked on an order.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-barcode");
        yield return P(a, "prd_gantt", "Gantt charts", "Setup", "Gantt view of scheduled orders.", ErpD365Part3Catalog.FormKind.Calendar, app, icon: "fa-bar-chart");
        yield return P(a, "prd_calendars", "Calendars", "Setup", "Working calendars for resources.", ErpD365Part3Catalog.FormKind.Calendar, app, icon: "fa-calendar");
        yield return P(a, "prd_mfg_exec", "Manufacturing execution", "Setup", "Job-card terminals and feedback defaults.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-desktop");
        yield return P(a, "prd_terminals", "Job card terminals", "Setup", "Shop-floor terminals used for job feedback.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tablet");
        yield return P(a, "prd_quality", "Quality management", "Quality", "Quality orders against production.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/quality-app", "fa-check-circle");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> ProjectPages()
    {
        const string a = "projects";
        const string app = ProjectsApp;
        yield return P(a, "pma_workspace", "Project management", "Workspaces", "Workspace for projects, contracts, and resources.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/projects-overview-app", "fa-tasks");
        yield return P(a, "pma_all_projects", "All projects", "Projects", "Every project regardless of status.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/projects-overview-app", "fa-list");
        yield return P(a, "pma_my_projects", "My projects", "Projects", "Projects this worker owns or is assigned to.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/projects-overview-app", "fa-user");
        yield return P(a, "pma_contracts", "Project contracts", "Projects", "Contracts that bill project work.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/project-accounting-app", "fa-file-text-o");
        yield return P(a, "pma_resources", "Project resources", "Projects", "Workers and assets assigned to a project.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-users");
        yield return P(a, "pma_resource_list", "Resources list", "Project resources", "Resource master used on projects.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-id-badge");
        yield return P(a, "pma_resource_avail", "Resource availability", "Project resources", "When a resource is free.", ErpD365Part3Catalog.FormKind.Calendar, app, icon: "fa-calendar");
        yield return P(a, "pma_resource_fulfill", "Resource fulfillment", "Project resources", "Fill open resource requests.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-check");
        yield return P(a, "pma_resource_req", "My resource requests", "Project resources", "Resource requests this worker submitted.", ErpD365Part3Catalog.FormKind.Request, app, icon: "fa-inbox");
        yield return P(a, "pma_item_tasks", "Item tasks", "Item tasks", "Item requirements on a project.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-cube");
        yield return P(a, "pma_item_req", "Item requirements", "Item tasks", "Material required by a project.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-list");
        yield return P(a, "pma_item_req_req", "Project item requisitions", "Item tasks", "Requisitions raised from project item needs.", ErpD365Part3Catalog.FormKind.Procurement, app, "/erp/purchase-requests-app", "fa-list-alt");
        yield return P(a, "pma_purch_orders", "Project purchase orders", "Item tasks", "Purchase orders charged to a project.", ErpD365Part3Catalog.FormKind.Procurement, app, "/erp/purchase-orders-app", "fa-clipboard");
        yield return P(a, "pma_sales_orders", "Project sales orders", "Item tasks", "Sales orders billed from a project.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/sales-orders-app", "fa-shopping-cart");
        yield return P(a, "pma_prod_orders", "Project production orders", "Item tasks", "Production orders charged to a project.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/production-overview-app", "fa-cogs");
        yield return P(a, "pma_timesheets", "All timesheets", "Timesheets", "Every project timesheet.", ErpD365Part3Catalog.FormKind.Payroll, app, icon: "fa-clock-o");
        yield return P(a, "pma_my_timesheets", "My timesheets", "Timesheets", "Timesheets for this worker.", ErpD365Part3Catalog.FormKind.Payroll, app, icon: "fa-user");
        yield return P(a, "pma_timesheets_review", "Timesheets for my review", "Timesheets", "Timesheets waiting on this approver.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-gavel");
        yield return P(a, "pma_unposted_ts", "Unposted timesheets", "Timesheets", "Approved timesheets not yet posted.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-edit");
        yield return P(a, "pma_ts_history", "Timesheet history", "Timesheets", "Posted timesheet history.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-history");
        yield return P(a, "pma_pending_txn", "Pending project transactions", "Project transactions", "Hour, expense, item, and fee lines waiting to post.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-hourglass-half");
        yield return P(a, "pma_my_pending_txn", "My pending project transactions", "Project transactions", "Pending transactions this worker entered.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-user");
        yield return P(a, "pma_posted_txn", "Posted project transactions", "Project transactions", "Posted project transactions.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/project-accounting-app", "fa-check");
        yield return P(a, "pma_my_posted_txn", "My posted project transactions", "Project transactions", "Posted transactions this worker entered.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-user");
        yield return P(a, "pma_invoices", "Project invoices", "Project invoices", "Customer invoices from a project.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/invoices-app", "fa-file-text-o");
        yield return P(a, "pma_invoice_prop", "Project invoice proposals", "Project invoices", "Invoice proposals waiting to be invoiced.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-lightbulb-o");
        yield return P(a, "pma_quotations", "Project quotations", "Quotations", "Quotations that can become a project.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/sales-quotations-app", "fa-file-text");
        yield return P(a, "pma_my_quotations", "My project quotations", "Quotations", "Quotations this worker owns.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-user");
        yield return P(a, "pma_expiring_quot", "My expiring project quotations", "Quotations", "Quotations near the expiry date.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-clock-o");
        yield return P(a, "pma_grants", "Grants", "Grants", "Grant headers and funding sources.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-university");
        yield return P(a, "pma_my_grants", "My grants", "Grants", "Grants this worker manages.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-user");
        yield return P(a, "pma_hour_journal", "Hour journal", "Journals", "Post project hours from a journal.", ErpD365Part3Catalog.FormKind.Payroll, app, icon: "fa-clock-o");
        yield return P(a, "pma_expense_journal", "Expense journal", "Journals", "Post project expenses from a journal.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-credit-card");
        yield return P(a, "pma_item_journal", "Item journal", "Journals", "Post project item consumption.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-cube");
        yield return P(a, "pma_fee_journal", "Fee journal", "Journals", "Post project fees.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-money");
        yield return P(a, "pma_onaccount_journal", "On-account journal", "Journals", "Post on-account project billing.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-file-o");
        yield return P(a, "pma_project_report", "Project report", "Inquiries and reports", "Status and cost of a project.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/project-accounting-app", "fa-bar-chart");
        yield return P(a, "pma_contract_report", "Project contracts report", "Inquiries and reports", "Contract billing versus cost.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-file-text-o");
        yield return P(a, "pma_txn_report", "Project transaction report", "Inquiries and reports", "Posted transactions by project.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list");
        yield return P(a, "pma_forecast_inq", "Forecast inquiries", "Inquiries and reports", "Hour, expense, item, and fee forecasts.", ErpD365Part3Catalog.FormKind.Forecast, app, icon: "fa-line-chart");
        yield return P(a, "pma_hour_inq", "Hour", "Inquiries and reports", "Posted and forecast hours.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-clock-o");
        yield return P(a, "pma_expense_inq", "Expense", "Inquiries and reports", "Posted and forecast expenses.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-credit-card");
        yield return P(a, "pma_item_inq", "Item", "Inquiries and reports", "Posted and forecast item consumption.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-cube");
        yield return P(a, "pma_fee_inq", "Fee", "Inquiries and reports", "Posted and forecast fees.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-money");
        yield return P(a, "pma_onaccount_inq", "On-account", "Inquiries and reports", "On-account billing versus contract.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-file-o");
        yield return P(a, "pma_resource_sched", "Resource scheduling inquiries", "Inquiries and reports", "Resource assignments versus capacity.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-users");
        yield return P(a, "pma_committed", "Committed costs", "Inquiries and reports", "Open purchase commitments on a project.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-lock");
        yield return P(a, "pma_pl_actual", "Project profit and loss (actual vs. budget)", "Profit and loss reports", "Actual versus budget profit and loss.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-line-chart");
        yield return P(a, "pma_wip_actual", "Project WIP (actual vs. budget)", "WIP reports", "Actual versus budget work in process.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-spinner");
        yield return P(a, "pma_consume", "Resource/item consumption", "Consumption reports", "Resource and item consumption versus budget.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-pie-chart");
        yield return P(a, "pma_cashflow", "Project cash flow", "Cash flow reports", "Cash in and out on a project.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-money");
        yield return P(a, "pma_hour_util", "Hour utilization", "Hour utilization reports", "Billable versus non-billable hours.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tachometer");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> QuestionnairePages()
    {
        const string a = "questionnaire";
        const string app = QuestionnaireApp;
        yield return P(a, "qnr_answer_groups", "Answer groups", "Design", "Reusable answer sets for a question.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list");
        yield return P(a, "qnr_questions", "Questions", "Design", "Question library used on questionnaires.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-question-circle");
        yield return P(a, "qnr_questionnaires", "Questionnaires", "Design", "Questionnaire headers and attached questions.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-list-alt");
        yield return P(a, "qnr_report", "Questionnaires report", "Design", "Printed questionnaire design.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-print");
        yield return P(a, "qnr_qa_report", "Questions and answers report", "Design", "Questions with their answer groups.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-file-text-o");
        yield return P(a, "qnr_planning_types", "Planning types", "Distribute", "How a questionnaire is planned for a person.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-calendar");
        yield return P(a, "qnr_ref_types", "Reference types", "Distribute", "What a planned session may reference.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-link");
        yield return P(a, "qnr_user_groups", "Questionnaire user groups", "Distribute", "People who may receive a questionnaire.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-users");
        yield return P(a, "qnr_schedules", "Questionnaire schedules", "Distribute", "Schedules that create planned sessions.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-clock-o");
        yield return P(a, "qnr_planned_session", "Planned answer session", "Distribute", "A session a person should complete.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-inbox");
        yield return P(a, "qnr_planned_report", "Planned answer session report", "Distribute", "Who has a planned session outstanding.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-bar-chart");
        yield return P(a, "qnr_complete", "Complete a questionnaire", "Distribute", "Answer a planned questionnaire session.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-check");
        yield return P(a, "qnr_planned_person", "Planned answer session per person report", "Distribute", "Planned sessions grouped by person.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-user");
        yield return P(a, "qnr_schedule_planned", "Schedule planned answer session", "Distribute", "Create planned sessions from a schedule.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-play");
        yield return P(a, "qnr_answers", "Answers", "View and analyze results", "Recorded answers.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-check-square-o");
        yield return P(a, "qnr_answers_person", "Answers by person", "View and analyze results", "Answers grouped by respondent.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-user");
        yield return P(a, "qnr_respondents", "Questionnaire respondents", "View and analyze results", "People who completed a questionnaire.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-users");
        yield return P(a, "qnr_results", "Questionnaire results", "View and analyze results", "Scored results for a questionnaire.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-bar-chart");
        yield return P(a, "qnr_stats", "Questionnaire statistics", "View and analyze results", "Counts and averages by question.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-pie-chart");
        yield return P(a, "qnr_answers_report", "Answers report", "View and analyze results", "Printed answers.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-print");
        yield return P(a, "qnr_answers_q_report", "Answers per questionnaire report", "View and analyze results", "Answers for one questionnaire.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-file-text-o");
        yield return P(a, "qnr_answers_p_report", "Answers per person report", "View and analyze results", "Answers for one person.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-user");
        yield return P(a, "qnr_analysis_report", "Question analysis report", "View and analyze results", "Analyze answers by question.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-search");
        yield return P(a, "qnr_feedback", "Feedback analysis report", "View and analyze results", "Free-text feedback analysis.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-comment");
        yield return P(a, "qnr_delete_results", "Delete results", "View and analyze results", "Delete recorded answers for a questionnaire.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-trash");
        yield return P(a, "qnr_parameters", "Questionnaire parameters", "Setup", "Company defaults for questionnaires.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "qnr_types", "Questionnaire types", "Setup", "Named questionnaire types.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "qnr_q_types", "Question types", "Setup", "Closed, open, and rating question types.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-question");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> RebatePages()
    {
        const string a = "rebate_mgmt";
        const string app = RebateApp;
        yield return P(a, "rbt_all_deals", "All rebate management deals", "Rebate management deals", "Every customer, royalty, and vendor rebate deal.", ErpD365Part3Catalog.FormKind.Procurement, app, icon: "fa-list");
        yield return P(a, "rbt_cust_deals", "Customer rebate deals", "Rebate management deals", "Rebate deals paid to a customer.", ErpD365Part3Catalog.FormKind.Procurement, app, icon: "fa-user");
        yield return P(a, "rbt_royalty", "Customer royalty deals", "Rebate management deals", "Royalty deals paid to a customer or licensor.", ErpD365Part3Catalog.FormKind.Procurement, app, icon: "fa-star");
        yield return P(a, "rbt_vendor_deals", "Vendor rebate deals", "Rebate management deals", "Rebate deals received from a vendor.", ErpD365Part3Catalog.FormKind.Procurement, app, icon: "fa-truck");
        yield return P(a, "rbt_workbench", "Rebate workbench", "Rebate management deals", "Review, process, and post rebate deals.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-dashboard");
        yield return P(a, "rbt_fifo", "Calculate FIFO purchase price", "Periodic tasks", "FIFO purchase price used by rebate calculation.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-calculator");
        yield return P(a, "rbt_prov_process", "Provision", "Process", "Calculate rebate provisions.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-plus");
        yield return P(a, "rbt_process", "Rebate management", "Process", "Process due rebate deals.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-cogs");
        yield return P(a, "rbt_writeoff_process", "Write-off", "Process", "Write off a rebate provision.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-minus");
        yield return P(a, "rbt_prov_post", "Provision", "Posting", "Post rebate provisions to the ledger.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-book");
        yield return P(a, "rbt_post", "Rebate management", "Posting", "Post processed rebate deals.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-check");
        yield return P(a, "rbt_writeoff_post", "Write-off", "Posting", "Post a rebate write-off.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-times");
        yield return P(a, "rbt_cancel_prov", "Cancel provision", "Canceled", "Reverse a posted rebate provision.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-undo");
        yield return P(a, "rbt_statement", "Statement", "Inquiries and reports", "Rebate statement for a deal.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-file-text-o");
        yield return P(a, "rbt_transactions", "Transactions", "Inquiries and reports", "Posted rebate transactions.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list");
        yield return P(a, "rbt_parameters", "Rebate management parameters", "Setup", "Company defaults for rebate deals and posting.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "rbt_workflows", "Rebate management workflows", "Setup", "Approval workflows for rebate deals.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/workflows-app", "fa-sitemap");
        yield return P(a, "rbt_reduction", "Rebate reduction principles", "Setup", "How a rebate is reduced when targets are missed.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-minus");
        yield return P(a, "rbt_status", "Status", "Status", "Statuses a rebate deal moves through.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-flag");
        yield return P(a, "rbt_cust_groups", "Customer groups", "Rebate management groups setup", "Customer groups used on rebate deals.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-users");
        yield return P(a, "rbt_item_groups", "Item groups", "Rebate management groups setup", "Item groups used on rebate deals.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-cubes");
        yield return P(a, "rbt_vendor_groups", "Vendor groups", "Rebate management groups setup", "Vendor groups used on rebate deals.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-truck");
        yield return P(a, "rbt_posting_profiles", "Rebate management posting profiles", "Rebate management posting setup", "Ledger accounts used when a rebate posts.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-book");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> RetailPages()
    {
        const string a = "retail";
        const string app = RetailApp;
        yield return P(a, "rtc_cat_product", "Category and product management", "Products and categories", "Manage retail categories and the products in them.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/retail-commerce-app", "fa-sitemap");
        yield return P(a, "rtc_by_category", "Products by category", "Products and categories", "Products in a retail category.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-th");
        yield return P(a, "rtc_released_by_cat", "Released products by category", "Products and categories", "Released products in a retail category.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-check");
        yield return P(a, "rtc_kits", "Product kits", "Products and categories", "Kits sold as one retail product.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-cubes");
        yield return P(a, "rtc_released_kits", "Released product kits", "Products and categories", "Kits released to a channel.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-gift");
        yield return P(a, "rtc_kit_orders", "Kit orders", "Products and categories", "Orders that assemble or disassemble a kit.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-wrench");
        yield return P(a, "rtc_images", "Product images", "Products and categories", "Images shown on a channel.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-image");
        yield return P(a, "rtc_commerce_hier", "Commerce category hierarchy", "Products and categories", "Category tree published to channels.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sitemap");
        yield return P(a, "rtc_nav_cats", "Navigational categories", "Products and categories", "Navigation categories on a storefront.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-bars");
        yield return P(a, "rtc_channel_nav", "Channel category attributes", "Products and categories", "Category attributes per channel.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "rtc_cat_images", "Category images", "Products and categories", "Images on a retail category.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-picture-o");
        yield return P(a, "rtc_variant_groups", "Variant groups", "Products and categories", "Size, color, and style groups.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-th-large");
        yield return P(a, "rtc_sizes", "Size groups", "Products and categories", "Size groups used on apparel.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-arrows");
        yield return P(a, "rtc_colors", "Color groups", "Products and categories", "Color groups used on products.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-paint-brush");
        yield return P(a, "rtc_styles", "Style groups", "Products and categories", "Style groups used on products.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-star");
        yield return P(a, "rtc_vendor_import", "Vendor catalog import", "Products and categories", "Import a vendor catalog into retail.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-download");
        yield return P(a, "rtc_catalogs", "All catalogs", "Catalog management", "Retail catalogs published to channels.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-book");
        yield return P(a, "rtc_catalog_images", "Catalog images", "Catalog management", "Images on a retail catalog.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-image");
        yield return P(a, "rtc_assortments", "Assortments", "Catalog management", "Which products a store may sell.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-th");
        yield return P(a, "rtc_recommend", "Product recommendations", "Product recommendations", "Recommendation lists on a channel.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-magic");
        yield return P(a, "rtc_price_groups", "All price groups", "Pricing and discounts", "Price groups used by channels and catalogs.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "rtc_pricing_prio", "Pricing priorities", "Pricing and discounts", "Which price or discount wins.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-sort-amount-desc");
        yield return P(a, "rtc_channel_price", "Channel price groups", "Pricing and discounts", "Price groups assigned to a channel.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-store");
        yield return P(a, "rtc_catalog_price", "Catalog price groups", "Pricing and discounts", "Price groups assigned to a catalog.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-book");
        yield return P(a, "rtc_affiliation_price", "Affiliation price groups", "Pricing and discounts", "Price groups for an affiliation.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-users");
        yield return P(a, "rtc_loyalty_price", "Loyalty price groups", "Pricing and discounts", "Price groups for a loyalty program.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-heart");
        yield return P(a, "rtc_discounts", "All discounts", "Pricing and discounts", "Every retail discount.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-percent");
        yield return P(a, "rtc_simple_disc", "Simple discounts", "Pricing and discounts", "Simple off or amount discounts.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tag");
        yield return P(a, "rtc_qty_disc", "Quantity discounts", "Pricing and discounts", "Quantity-break discounts.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-plus");
        yield return P(a, "rtc_mix_match", "Mix and match discounts", "Pricing and discounts", "Mix-and-match discount deals.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-random");
        yield return P(a, "rtc_threshold", "Threshold discounts", "Pricing and discounts", "Discounts that start after a spend threshold.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-arrow-up");
        yield return P(a, "rtc_shipping_disc", "Shipping discounts", "Pricing and discounts", "Discounts on shipping charges.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-truck");
        yield return P(a, "rtc_coupons", "Coupons", "Pricing and discounts", "Coupon codes that trigger a discount.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-ticket");
        yield return P(a, "rtc_trade_agreements", "Trade agreements", "Pricing and discounts", "Retail trade agreements.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-file-text-o");
        yield return P(a, "rtc_sales_agreements", "Sales agreements", "Sales agreements", "Quantity or value sales agreements.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-handshake-o");
        yield return P(a, "rtc_channels", "Channel deployment", "Channels", "Deploy configuration to a channel.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-cloud-upload");
        yield return P(a, "rtc_stores", "Stores", "Channels", "Brick-and-mortar stores.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/retail-commerce-app?tab=channels", "fa-building");
        yield return P(a, "rtc_online", "Online stores", "Channels", "Online store channels.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-globe");
        yield return P(a, "rtc_call_centers", "Call centers", "Channels", "Call-center channels.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-phone");
        yield return P(a, "rtc_payment_ws", "Payment workspace", "Channels", "Payment methods and status for channels.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-credit-card");
        yield return P(a, "rtc_gift_cards", "Gift cards", "Gift cards", "Issued gift cards.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-gift");
        yield return P(a, "rtc_loyalty", "Loyalty cards", "Loyalty", "Issued loyalty cards.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-heart");
        yield return P(a, "rtc_loyalty_points", "Loyalty reward points", "Loyalty", "Point balances and adjustments.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-star");
        yield return P(a, "rtc_affiliations", "Affiliations", "Employees", "Customer affiliations used for pricing.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-users");
        yield return P(a, "rtc_employees", "Workers", "Employees", "Store workers and job assignments.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/staff-app", "fa-id-badge");
        yield return P(a, "rtc_customers", "All customers", "Customers", "Retail customers.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/contacts-app", "fa-user");
        yield return P(a, "rtc_inventory", "Inventory levels", "Inventory management", "On-hand by store.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/inventory-stock-app", "fa-cubes");
        yield return P(a, "rtc_replenish", "Replenishment", "Inventory management", "Store replenishment from a warehouse.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-refresh");
        yield return P(a, "rtc_dom", "Distributed order management", "Distributed order management", "Route an order to a store or warehouse.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-random");
        yield return P(a, "rtc_continuity", "Continuity programs", "Continuity", "Subscription or continuity order programs.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-repeat");
        yield return P(a, "rtc_pos", "Registers", "POS setup", "Registers used in a store.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/pos-overview-app", "fa-desktop");
        yield return P(a, "rtc_devices", "Devices", "POS setup", "Hardware devices on a register.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tablet");
        yield return P(a, "rtc_receipts", "Receipt formats", "POS setup", "Receipt print formats.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-print");
        yield return P(a, "rtc_layouts", "Screen layouts", "POS setup", "POS screen layouts.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-th");
        yield return P(a, "rtc_buttons", "Button grids", "POS setup", "POS button grids.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-keyboard-o");
        yield return P(a, "rtc_sales_report", "Sales reports", "Inquiries and reports", "Channel sales totals.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-bar-chart");
        yield return P(a, "rtc_channel_sales", "Channel sales by hour report", "Inquiries and reports", "Sales by channel and hour.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-clock-o");
        yield return P(a, "rtc_gift_report", "Gift card reports", "Inquiries and reports", "Issued, redeemed, and voided gift cards.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-gift");
        yield return P(a, "rtc_parameters", "Commerce parameters", "Parameters", "Company defaults for retail and commerce.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "rtc_scheduler", "Commerce scheduler", "Commerce scheduler", "Jobs that sync headquarters and channels.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-refresh");
        yield return P(a, "rtc_returns", "Returns", "Returns", "Customer returns from a channel.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/returns-rma-app", "fa-undo");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> SalesPages()
    {
        const string a = "sales";
        const string app = SalesApp;
        yield return P(a, "sm_so_processing", "Sales order processing and inquiry", "Workspaces", "Workspace for open sales orders and exceptions.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/sales-orders-app", "fa-dashboard");
        yield return P(a, "sm_returns_ws", "Sales return processing", "Workspaces", "Workspace for customer returns.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/returns-rma-app", "fa-undo");
        yield return P(a, "sm_all_customers", "All customers", "Customers", "Customer master used on quotations and orders.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/contacts-app", "fa-user");
        yield return P(a, "sm_recalc_credit", "Recalculate credit limit", "Customers", "Recalculate used credit for customers.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-refresh");
        yield return P(a, "sm_contacts", "All contacts", "Relationships", "Contact persons at a customer or prospect.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/contacts-app", "fa-address-book");
        yield return P(a, "sm_prospects", "All prospects", "Relationships", "Prospects that are not yet customers.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/crm-board-app", "fa-user-plus");
        yield return P(a, "sm_leads", "All leads", "Relationships", "Leads waiting to be qualified.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/crm-board-app", "fa-filter");
        yield return P(a, "sm_opportunities", "All opportunities", "Opportunities", "Sales opportunities.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/crm-opportunities-app", "fa-line-chart");
        yield return P(a, "sm_my_opportunities", "My opportunities", "Opportunities", "Opportunities this worker owns.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-user");
        yield return P(a, "sm_campaigns", "All campaigns", "Marketing", "Marketing campaigns.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/marketing-app", "fa-bullhorn");
        yield return P(a, "sm_my_campaigns", "My campaigns", "Marketing", "Campaigns this worker owns.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-user");
        yield return P(a, "sm_calls", "All call lists", "Marketing", "Telemarketing call lists.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-phone");
        yield return P(a, "sm_quotations", "All quotations", "Sales quotations", "Every sales quotation.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/sales-quotations-app", "fa-file-text");
        yield return P(a, "sm_my_quotations", "My quotations", "Sales quotations", "Quotations this worker owns.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-user");
        yield return P(a, "sm_open_quotations", "Open quotations", "Sales quotations", "Quotations not yet confirmed or lost.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-folder-open-o");
        yield return P(a, "sm_expiring_quot", "Expiring quotations", "Sales quotations", "Quotations near the expiry date.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-clock-o");
        yield return P(a, "sm_all_orders", "All sales orders", "Sales orders", "Every sales order regardless of status.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/sales-orders-app", "fa-shopping-cart");
        yield return P(a, "sm_open_orders", "Open sales orders", "Sales orders", "Orders not yet invoiced.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-folder-open-o");
        yield return P(a, "sm_backorder", "Backorder lines", "Sales orders", "Lines still waiting to ship.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-hourglass");
        yield return P(a, "sm_holds", "Order holds", "Sales orders", "Orders on hold.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-pause");
        yield return P(a, "sm_sales_journal", "Sales journal", "Sales journal", "Journals that post sales invoices.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/invoices-app", "fa-book");
        yield return P(a, "sm_confirm", "Confirm sales order", "Order shipping", "Confirm a sales order to the customer.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-check");
        yield return P(a, "sm_picking", "Generate picking list", "Order shipping", "Generate a picking list from an order.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-hand-paper-o");
        yield return P(a, "sm_packing", "Post packing slip", "Order shipping", "Post a packing slip / delivery note.", ErpD365Part3Catalog.FormKind.Action, app, "/erp/delivery-notes-app", "fa-truck");
        yield return P(a, "sm_invoice", "Invoice", "Order shipping", "Invoice a shipped sales order.", ErpD365Part3Catalog.FormKind.Action, app, "/erp/invoices-app", "fa-file-text-o");
        yield return P(a, "sm_price_disc", "Prices and discounts", "Prices and discounts", "Sales price and discount agreements.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "sm_cust_rebates", "Customer rebates", "Customer rebates", "Customer rebate agreements and claims.", ErpD365Part3Catalog.FormKind.Procurement, app, icon: "fa-percent");
        yield return P(a, "sm_trade_allow", "Trade allowances", "Trade allowances", "Trade allowance agreements.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-handshake-o");
        yield return P(a, "sm_cases", "Cases", "Cases", "Sales cases.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-briefcase");
        yield return P(a, "sm_turnover", "Customer turnover report", "Inquiries and reports", "Customer sales turnover.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-bar-chart");
        yield return P(a, "sm_sales_stats", "Sales statistics", "Inquiries and reports", "Sales totals by item, customer, or period.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-line-chart");
        yield return P(a, "sm_sales_analysis", "Sales analysis by invoice", "Inquiries and reports", "Invoiced sales analysis.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-pie-chart");
        yield return P(a, "sm_performance", "Sales and profitability performance", "Inquiries and reports", "Margin and profitability by sales unit.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tachometer");
        yield return P(a, "sm_ic_orders", "Intercompany order report", "Inquiries and reports", "Intercompany sales orders.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-exchange");
        yield return P(a, "sm_daily_cash", "Daily cash book report", "Inquiries and reports", "Daily cash from invoiced sales.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-money");
        yield return P(a, "sm_cleanup_orders", "Delete sales orders", "Cleanup", "Delete sales orders that were never confirmed.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-trash");
        yield return P(a, "sm_cleanup_quot", "Delete quotations", "Cleanup", "Delete quotations that were never confirmed.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-minus");
        yield return P(a, "sm_parameters", "Sales and marketing parameters", "Setup", "Company defaults for quotations, orders, and CRM.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "sm_workflows", "Sales and marketing workflows", "Setup", "Approval workflows for quotations and orders.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/workflows-app", "fa-sitemap");
        yield return P(a, "sm_sales_mgmt", "Sales management", "Setup", "Sales-unit targets and forecasts.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-line-chart");
        yield return P(a, "sm_source_types", "Source types", "Setup", "Lead and opportunity source types.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "sm_sales_units", "Sales unit/team", "Setup", "Sales units and teams.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-users");
        yield return P(a, "sm_responsibilities", "Responsibilities", "Setup", "Who owns a customer or opportunity.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-id-badge");
        yield return P(a, "sm_approved_cust", "Approved customer list", "Setup", "Customers approved for a restricted item.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-check");
        yield return P(a, "sm_order_pools", "Order pools", "Setup", "Pools that group sales orders.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-object-group");
        yield return P(a, "sm_return_reasons", "Return reason codes", "Setup", "Reasons a customer returns goods.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-undo");
        yield return P(a, "sm_commissions", "Commissions", "Commissions", "Sales commission calculation.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-money");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> ServicePages()
    {
        const string a = "service_mgmt";
        const string app = ServiceApp;
        yield return P(a, "svc_all_orders", "Service orders", "Service orders", "Every service order.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-wrench");
        yield return P(a, "svc_active_orders", "Active service orders", "Service orders", "Service orders not yet posted.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-play");
        yield return P(a, "svc_ready_post", "Service orders ready for posting", "Service orders", "Completed orders waiting to post.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-check");
        yield return P(a, "svc_my_open", "My open service orders", "Service orders", "Open service orders assigned to this worker.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-user");
        yield return P(a, "svc_agreements", "Service agreements", "Service agreements", "Agreements that cover service work.", ErpD365Part3Catalog.FormKind.Plan, app, "/erp/contracts-app", "fa-file-text-o");
        yield return P(a, "svc_active_agreements", "Active service agreements", "Service agreements", "Agreements that are in force.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-check-circle");
        yield return P(a, "svc_templates", "Service templates", "Service agreements", "Templates used to create a service agreement.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-copy");
        yield return P(a, "svc_all_subs", "All service subscriptions", "Service subscriptions", "Every service subscription.", ErpD365Part3Catalog.FormKind.Plan, app, "/erp/sales-orders-app?tab=subscriptions", "fa-refresh");
        yield return P(a, "svc_active_subs", "Active service subscriptions", "Service subscriptions", "Subscriptions that are in force.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-play");
        yield return P(a, "svc_fee_inq", "Subscription fee transaction inquiry", "Inquiries and reports", "Posted subscription fees.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-money");
        yield return P(a, "svc_margin", "Service order margin report", "Inquiries and reports", "Revenue versus cost on a service order.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-line-chart");
        yield return P(a, "svc_work_desc", "Work description report", "Inquiries and reports", "Work performed on a service order.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-file-text-o");
        yield return P(a, "svc_work_receipt", "Work receipt report", "Inquiries and reports", "Receipt given to the customer.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-print");
        yield return P(a, "svc_sub_txn", "Transaction - subscription report", "Inquiries and reports", "Subscription transactions.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list");
        yield return P(a, "svc_dispatch", "Dispatch board", "Perform periodic tasks", "Assign technicians to service orders.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-map-marker");
        yield return P(a, "svc_create_orders", "Create service orders", "Perform periodic tasks", "Create service orders from an agreement.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-plus");
        yield return P(a, "svc_post_orders", "Post service orders", "Perform periodic tasks", "Post completed service orders.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-check");
        yield return P(a, "svc_cancel_orders", "Cancel service orders", "Perform periodic tasks", "Cancel service orders that will not be done.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-ban");
        yield return P(a, "svc_delete_orders", "Delete service orders", "Perform periodic tasks", "Delete draft service orders.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-trash");
        yield return P(a, "svc_create_fee", "Create subscription fee", "Service subscriptions", "Generate the next subscription fee.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-plus-circle");
        yield return P(a, "svc_index_sub", "Index subscription", "Service subscriptions", "Apply an index to subscription prices.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-line-chart");
        yield return P(a, "svc_accrue", "Accrue subscription revenue", "Service subscriptions", "Accrue unearned subscription revenue.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-book");
        yield return P(a, "svc_parameters", "Service management parameters", "Setup", "Company defaults for service orders and agreements.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "svc_sub_params", "Subscription parameters", "Setup", "Company defaults for subscriptions.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-cogs");
        yield return P(a, "svc_tasks", "Service tasks", "Setup", "Task codes used on a service order.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tasks");
        yield return P(a, "svc_teams", "Dispatch teams", "Setup", "Teams that receive dispatched work.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-users");
        yield return P(a, "svc_objects", "Service objects", "Setup", "Objects a service order may cover.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-cube");
        yield return P(a, "svc_object_groups", "Service object groups", "Setup", "Groups of service objects.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-object-group");
        yield return P(a, "svc_intervals", "Service intervals", "Setup", "How often a service agreement creates an order.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-clock-o");
        yield return P(a, "svc_time_window", "Time window", "Setup", "Service time windows offered to a customer.", ErpD365Part3Catalog.FormKind.Calendar, app, icon: "fa-calendar");
        yield return P(a, "svc_stages", "Service stages", "Setup", "Stages a service order moves through.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-flag");
        yield return P(a, "svc_conditions", "Conditions", "Repair", "Repair conditions observed on arrival.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-eye");
        yield return P(a, "svc_symptoms", "Symptom areas", "Repair", "Symptom areas used on a repair.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-stethoscope");
        yield return P(a, "svc_diagnosis", "Diagnosis areas", "Repair", "Diagnosis areas used on a repair.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-search");
        yield return P(a, "svc_resolutions", "Resolutions", "Repair", "How a repair was resolved.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-check");
        yield return P(a, "svc_repair_stages", "Repair stages", "Repair", "Stages a repair job moves through.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/jewellery-repairs-app?tab=jw_repairs", "fa-wrench");
        yield return P(a, "svc_sub_groups", "Subscription groups", "Service subscriptions", "Groups that share subscription defaults.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-object-group");
        yield return P(a, "svc_aftersales", "After-sales RMA", "Service orders", "Return merchandise authorizations after a sale.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/returns-rma-app", "fa-undo");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> SystemPages()
    {
        const string a = "setup";
        const string app = SystemAdminApp;
        yield return P(a, "sys_workspace", "System administration", "Workspaces", "Workspace for users, security, and batch jobs.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/tenant-config-app", "fa-dashboard");
        yield return P(a, "sys_data_mgmt", "Data management", "Workspaces", "Import, export, and data projects.", ErpD365Part3Catalog.FormKind.Action, app, "/erp/data-migrations-app", "fa-database");
        yield return P(a, "sys_features", "Feature management", "Workspaces", "Turn features on or off.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-toggle-on");
        yield return P(a, "sys_opt_advisor", "Optimization advisor", "Workspaces", "Rules that flag configuration issues.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-lightbulb-o");
        yield return P(a, "sys_users", "Users", "Users", "User accounts that may sign in.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/users-app", "fa-users");
        yield return P(a, "sys_online_users", "Online users", "Users", "Users signed in now.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-circle");
        yield return P(a, "sys_user_groups", "User groups", "Users", "Groups used for assignment and security.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-object-group");
        yield return P(a, "sys_user_requests", "User requests", "Users", "Requests for a new user account.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-inbox");
        yield return P(a, "sys_user_info", "User information", "Users", "Profile details for a user.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/users-app", "fa-id-badge");
        yield return P(a, "sys_assign_roles", "Assign users to roles", "Security", "Grant a security role to a user.", ErpD365Part3Catalog.FormKind.Action, app, "/erp/tenant-config-app?tab=security_roles", "fa-user-plus");
        yield return P(a, "sys_sec_config", "Security configuration", "Security", "Roles, duties, and privileges.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/tenant-config-app?tab=security_roles", "fa-shield");
        yield return P(a, "sys_sec_diag", "Security diagnostics", "Security", "Diagnose why a user can or cannot open a page.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-stethoscope");
        yield return P(a, "sys_external_roles", "External roles", "Security", "Roles used by external identities.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-globe");
        yield return P(a, "sys_sod", "Segregation of duties rules", "Security", "Duties that must not be held together.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-ban");
        yield return P(a, "sys_sod_conflicts", "Segregation of duties unresolved conflicts", "Security", "Users who currently break a segregation rule.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-exclamation-triangle");
        yield return P(a, "sys_governance", "Security governance", "Security", "Review and maintain security process.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-gavel");
        yield return P(a, "sys_workflows", "Workflows", "Workflow", "Workflow configurations.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/workflows-app", "fa-sitemap");
        yield return P(a, "sys_workflow_infra", "Workflow infrastructure configuration", "Workflow", "Batch and notification settings for workflow.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-cogs");
        yield return P(a, "sys_user_workflow", "User workflow", "Workflow", "Workflows assigned to this user.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-inbox");
        yield return P(a, "sys_task_list", "Task list", "Inquiries", "Open workflow work items.", ErpD365Part3Catalog.FormKind.Queue, app, "/erp/approvals-app", "fa-tasks");
        yield return P(a, "sys_deleted_attach", "Deleted attachments", "Inquiries", "Attachments that were deleted.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-paperclip");
        yield return P(a, "sys_doc_history", "Document history", "Inquiries", "Document management history.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/documents-app", "fa-history");
        yield return P(a, "sys_db_info", "Database information", "Database", "Database name, size, and options.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-database");
        yield return P(a, "sys_db_log", "Database log", "Database", "Tracked field changes.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/audit-trail-app", "fa-list");
        yield return P(a, "sys_batch_jobs", "Batch jobs", "Database", "Scheduled and running batch jobs.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-cogs");
        yield return P(a, "sys_license", "License configuration", "Setup", "License keys and configuration keys.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-key");
        yield return P(a, "sys_server", "Server configuration", "Setup", "AOS and batch-server settings.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-server");
        yield return P(a, "sys_parameters", "System parameters", "Setup", "Company and system defaults.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/tenant-config-app", "fa-sliders");
        yield return P(a, "sys_personalization", "Personalization", "Setup", "Saved form personalizations.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-paint-brush");
        yield return P(a, "sys_office", "Office app parameters", "Setup", "Excel and Outlook add-in defaults.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-file-excel-o");
        yield return P(a, "sys_email", "Email parameters", "Email", "How the system sends email.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-envelope-o");
        yield return P(a, "sys_email_templates", "System email templates", "Email", "Templates used for system messages.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-file-text-o");
        yield return P(a, "sys_alerts", "Alerts", "Alerts", "Event alerts and rules.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-bell");
        yield return P(a, "sys_batch_cleanup", "Batch job cleanup", "Periodic tasks", "Remove finished batch jobs.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-trash");
        yield return P(a, "sys_notify_cleanup", "Notification cleanup", "Periodic tasks", "Remove stale notifications.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-eraser");
        yield return P(a, "sys_db_cleanup", "Database log cleanup", "Periodic tasks", "Remove old database-log rows.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-database");
        yield return P(a, "sys_consistency", "Database consistency check", "Periodic tasks", "Check referential consistency.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-check");
        yield return P(a, "sys_templates", "Templates", "Templates", "Record templates used when creating a new row.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-copy");
        yield return P(a, "sys_uom_conv", "Unit of measure conversion template", "Templates", "Templates that convert units.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-exchange");
        yield return P(a, "sys_uploads", "Uploads", "Uploads", "Workbook uploads for masters and journals.", ErpD365Part3Catalog.FormKind.Action, app, "/erp/data-migrations-app", "fa-upload");
        yield return P(a, "sys_number_seq", "Number sequences", "Setup", "Number sequences shared across modules.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list-ol");
        yield return P(a, "sys_platform", "Platform services", "Setup", "Platform service switches for this company.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/tenant-config-app?tab=platform", "fa-cloud");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> TaxPages()
    {
        const string a = "tax";
        const string app = TaxApp;
        yield return P(a, "tax_sales_codes", "Sales tax codes", "Sales tax", "Tax codes with rate, ledger, and settlement period.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/vat-app", "fa-percent");
        yield return P(a, "tax_sales_groups", "Sales tax groups", "Sales tax", "Groups of sales tax codes for a customer or vendor.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-object-group");
        yield return P(a, "tax_item_groups", "Item sales tax groups", "Sales tax", "Groups of sales tax codes for an item.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-cube");
        yield return P(a, "tax_authorities", "Sales tax authorities", "Sales tax", "Authorities that receive a settlement.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-university");
        yield return P(a, "tax_settlement", "Sales tax settlement periods", "Sales tax", "Periods used when settling sales tax.", ErpD365Part3Catalog.FormKind.Calendar, app, icon: "fa-calendar");
        yield return P(a, "tax_hs_dims", "Harmonized sales tax dimensions", "Sales tax", "Dimensions used on a harmonized sales tax.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-th");
        yield return P(a, "tax_hs_rules", "Harmonized sales tax rules", "Sales tax", "Rules that apply a harmonized sales tax.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-gavel");
        yield return P(a, "tax_wh_codes", "Withholding tax codes", "Withholding tax", "Withholding tax codes and rates.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/withholding-app", "fa-scissors");
        yield return P(a, "tax_wh_groups", "Withholding tax groups", "Withholding tax", "Groups of withholding codes for a vendor.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-object-group");
        yield return P(a, "tax_posted", "Posted sales tax", "Sales tax inquiries", "Posted sales tax transactions.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/vat-app", "fa-check");
        yield return P(a, "tax_posted_hist", "Posted sales tax history", "Sales tax inquiries", "History of posted sales tax.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-history");
        yield return P(a, "tax_payments", "Sales tax payments", "Sales tax inquiries", "Settlement payments to an authority.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-money");
        yield return P(a, "tax_vouchers_no_tax", "Voucher transactions without sales tax", "Sales tax inquiries", "Vouchers that posted without sales tax.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-exclamation-triangle");
        yield return P(a, "tax_summary", "Sales tax summary", "Sales tax reports", "Summary of sales tax by code or period.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-bar-chart");
        yield return P(a, "tax_by_code", "Sales tax payment by code", "Sales tax reports", "Settlement amounts by tax code.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list");
        yield return P(a, "tax_txn_detail", "Sales tax transactions - details", "Sales tax reports", "Detailed posted sales tax lines.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list-alt");
        yield return P(a, "tax_gl_recon", "Sales tax general journal reconciliation", "Sales tax reports", "Reconcile sales tax to the general journal.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-balance-scale");
        yield return P(a, "tax_spec_ledger", "Sales tax specification by ledger transaction", "Sales tax reports", "Sales tax on each ledger voucher.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-book");
        yield return P(a, "tax_by_customer", "Sales tax by customer", "Sales tax reports", "Sales tax collected from a customer.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-user");
        yield return P(a, "tax_by_vendor", "Sales tax by vendor", "Sales tax reports", "Sales tax paid to a vendor.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-truck");
        yield return P(a, "tax_ledger_recon", "Sales tax/Ledger reconciliation", "Sales tax reports", "Reconcile sales tax to the control account.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-university");
        yield return P(a, "tax_deviation", "Sales tax deviations", "Sales tax reports", "Differences versus the expected tax.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-warning");
        yield return P(a, "tax_list", "Sales tax list", "Sales tax reports", "List of sales tax codes and rates.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list");
        yield return P(a, "tax_txn", "Sales tax transactions", "Sales tax reports", "Posted sales tax transactions.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-exchange");
        yield return P(a, "tax_reporting_codes", "Sales tax reporting codes", "Sales tax reports", "Boxes used on a tax return.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-th");
        yield return P(a, "tax_wh_inq", "Posted withholding tax", "Withholding tax inquiries", "Posted withholding tax transactions.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/withholding-app", "fa-scissors");
        yield return P(a, "tax_wh_txn", "Withholding tax transactions", "Withholding tax reports", "Withholding lines by vendor.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list");
        yield return P(a, "tax_e_messages", "Electronic messages", "Electronic messages", "Electronic tax messages.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-envelope-o");
        yield return P(a, "tax_e_items", "Electronic message items", "Electronic messages", "Items inside an electronic message.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list");
        yield return P(a, "tax_report_period", "Report sales tax for settlement period", "Declarations", "Build the sales tax report for a period.", ErpD365Part3Catalog.FormKind.Action, app, "/erp/vat-app?tab=vat_return", "fa-file-text-o");
        yield return P(a, "tax_settle_post", "Settle and post sales tax", "Declarations", "Settle and post sales tax for a period.", ErpD365Part3Catalog.FormKind.Action, app, "/erp/vat-app", "fa-check");
        yield return P(a, "tax_compliance", "Tax compliance", "Declarations", "UAE and statutory tax compliance filings.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/uae-tax-compliance-app", "fa-gavel");
        yield return P(a, "tax_vat_return", "VAT return", "Declarations", "VAT return for the settlement period.", ErpD365Part3Catalog.FormKind.Action, app, "/erp/vat-app?tab=vat_return", "fa-percent");
        yield return P(a, "tax_einvoice", "E-invoicing", "Electronic messages", "Electronic invoice documents.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/einvoice-documents-app", "fa-file-code-o");
        yield return P(a, "tax_gl_params", "General ledger parameters", "Setup", "Ledger defaults that affect tax posting.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "tax_ar_params", "Accounts receivable parameters", "Setup", "Receivables defaults that affect sales tax.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-users");
        yield return P(a, "tax_ap_params", "Accounts payable parameters", "Setup", "Payables defaults that affect sales tax.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-truck");
        yield return P(a, "tax_posting_groups", "Ledger posting groups", "Sales tax", "Ledger accounts used when sales tax posts.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-book");
        yield return P(a, "tax_exempt_codes", "Sales tax exempt codes", "Sales tax", "Exemption reason codes.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-ban");
        yield return P(a, "tax_exempt_numbers", "Tax exempt numbers", "Sales tax", "Customer or vendor exemption numbers.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-barcode");
        yield return P(a, "tax_country", "Country/region parameters", "Setup", "Tax defaults by country or region.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-globe");
        yield return P(a, "tax_reverse", "Reverse charge item groups", "Setup", "Items that use reverse charge.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-exchange");
        yield return P(a, "tax_reverse_rules", "Reverse charge rules", "Setup", "When reverse charge applies.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-gavel");
        yield return P(a, "tax_foreign", "Foreign trade parameters", "Foreign trade", "Intrastat and foreign-trade defaults.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-plane");
        yield return P(a, "tax_calc_params", "Tax calculation parameters", "Tax configuration", "How tax is calculated on a document.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-calculator");
        yield return P(a, "tax_msg_types", "Message item types", "Electronic messages", "Types of electronic message items.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "tax_msg_status", "Message statuses", "Electronic messages", "Statuses an electronic message moves through.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-flag");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> TimeAttPages()
    {
        const string a = "time_att";
        const string app = TimeAttApp;
        yield return P(a, "tna_approve", "Approve", "Review and approve", "Approve time and attendance registrations.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-check");
        yield return P(a, "tna_calculate", "Calculate", "Review and approve", "Calculate selected registrations.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-calculator");
        yield return P(a, "tna_calculate_all", "Calculate all", "Review and approve", "Calculate all pending registrations.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-cogs");
        yield return P(a, "tna_planned_abs", "Create planned absence", "Review and approve", "Create a planned absence for a worker.", ErpD365Part3Catalog.FormKind.Request, app, "/erp/leave-absence-app?tab=time_off_requests", "fa-calendar-minus-o");
        yield return P(a, "tna_collective", "Collective registration", "Manage registrations", "Register time for a group of workers.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-users");
        yield return P(a, "tna_clock", "Clock in/out", "Manage registrations", "Clock a worker in or out.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-clock-o");
        yield return P(a, "tna_indirect", "Indirect activities", "Manage indirect activities", "Register indirect shop-floor activities.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-tasks");
        yield return P(a, "tna_post_indirect", "Post indirect activities costs", "Manage indirect activities", "Post cost of indirect activities.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-book");
        yield return P(a, "tna_transfer_pay", "Transfer to pay", "Maintain payroll", "Transfer approved time to payroll.", ErpD365Part3Catalog.FormKind.Action, app, "/erp/payroll-app", "fa-exchange");
        yield return P(a, "tna_recalc_bal", "Recalculate worker balances", "Maintain payroll", "Recalculate flex and overtime balances.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-refresh");
        yield return P(a, "tna_recalc_stats", "Recalculate payroll statistics", "Maintain payroll", "Recalculate time statistics used by payroll.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-bar-chart");
        yield return P(a, "tna_adjust_stats", "Adjust payroll statistics", "Maintain payroll", "Manually adjust a worker statistic.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-pencil");
        yield return P(a, "tna_recalc_flex", "Recalculate flex balance", "Manage flex time", "Recalculate flex-time balances.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-refresh");
        yield return P(a, "tna_adjust_flex", "Adjust flex balance", "Manage flex time", "Manually adjust a flex-time balance.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-sliders");
        yield return P(a, "tna_attendance", "Attendance", "Attendance", "Attendance register for a day.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-check-square-o");
        yield return P(a, "tna_attendance_report", "Attendance report", "Attendance", "Printed attendance.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-print");
        yield return P(a, "tna_absence_stats", "Absence statistics", "Absence reports", "Absence counts by worker or type.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-pie-chart");
        yield return P(a, "tna_indirect_cost", "Indirect activities costs", "Indirect activities", "Cost of posted indirect activities.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-money");
        yield return P(a, "tna_raw_reg", "Raw registrations", "Registrations", "Unprocessed clock registrations.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-inbox");
        yield return P(a, "tna_reg_hist", "Registration history", "Registrations", "Processed registration history.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-history");
        yield return P(a, "tna_pay_items", "Pay items", "Payroll", "Pay items generated from time.", ErpD365Part3Catalog.FormKind.Payroll, app, "/erp/payroll-app", "fa-list");
        yield return P(a, "tna_flex_over", "Flex limit exceeded", "Flex time reports", "Workers over the flex limit.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-exclamation-triangle");
        yield return P(a, "tna_flex_overview", "Flex overview", "Flex time reports", "Flex balances by worker.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-area-chart");
        yield return P(a, "tna_cleanup_reg", "Clean up registrations", "Periodic tasks", "Remove stale raw registrations.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-trash");
        yield return P(a, "tna_archive", "Archive future registrations", "Periodic tasks", "Archive future planned registrations.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-archive");
        yield return P(a, "tna_deactivate", "Deactivate past workers", "Periodic tasks", "Stop time registration for leavers.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-ban");
        yield return P(a, "tna_barcodes", "Print barcodes", "Periodic tasks", "Print worker or job barcodes.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-barcode");
        yield return P(a, "tna_parameters", "Time and attendance parameters", "Setup", "Company defaults for registration and approval.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "tna_workflows", "Time and attendance workflows", "Setup", "Approval workflows for registrations.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/workflows-app", "fa-sitemap");
        yield return P(a, "tna_workers", "Time registration workers", "Setup", "Workers who may register time.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/staff-app", "fa-id-badge");
        yield return P(a, "tna_indirect_cat", "Indirect activity categories", "Setup", "Categories of indirect activity.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "tna_calc_params", "Calculation parameters", "Setup", "How overtime and flex are calculated.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-calculator");
        yield return P(a, "tna_calc_groups", "Calculation groups", "Groups", "Worker groups that share calculation rules.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-users");
        yield return P(a, "tna_approval_groups", "Approval groups", "Groups", "Who may approve a worker's time.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-gavel");
        yield return P(a, "tna_flex_groups", "Flex groups", "Groups", "Flex-time groups.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-clock-o");
        yield return P(a, "tna_absence_groups", "Absence groups", "Groups", "Absence groups.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-calendar-times-o");
        yield return P(a, "tna_profiles", "Profiles", "Time profiles", "Daily time profiles.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-list-alt");
        yield return P(a, "tna_profile_types", "Profile types", "Time profiles", "Named profile types.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "tna_pay_types", "Pay types", "Payroll", "Pay types generated from time.", ErpD365Part3Catalog.FormKind.Payroll, app, icon: "fa-money");
        yield return P(a, "tna_job_card", "Configure job card terminals", "Configure job card", "Terminals used to register time.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tablet");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> TransportationPages()
    {
        const string a = "transportation";
        const string app = TransportationApp;
        yield return P(a, "tms_load_plan", "Load planning workbench", "Planning", "Plan inbound and outbound loads.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-dashboard");
        yield return P(a, "tms_in_load_plan", "Inbound load planning workbench", "Planning", "Plan inbound loads from purchase or transfer.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-sign-in");
        yield return P(a, "tms_out_load_plan", "Outbound load planning workbench", "Planning", "Plan outbound loads from sales or transfer.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-sign-out");
        yield return P(a, "tms_load_build", "Load building workbench", "Planning", "Build a load from planned shipments.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-cubes");
        yield return P(a, "tms_rate_route", "Rate route workbench", "Planning", "Rate and route a load.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-road");
        yield return P(a, "tms_all_loads", "All loads", "Loads", "Every transportation load.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-truck");
        yield return P(a, "tms_active_loads", "Active loads", "Loads", "Loads not yet shipped.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-play");
        yield return P(a, "tms_open_loads", "Open loads", "Loads", "Loads still being planned.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-folder-open-o");
        yield return P(a, "tms_waved_loads", "Waved loads", "Loads", "Loads that have a warehouse wave.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-flag");
        yield return P(a, "tms_picking_loads", "Picking loads", "Loads", "Loads being picked.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-hand-paper-o");
        yield return P(a, "tms_ready_ship", "Loads ready to ship", "Loads", "Loads that can be shipped.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-check");
        yield return P(a, "tms_all_ship", "All shipments", "Shipments", "Every transportation shipment.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-ship");
        yield return P(a, "tms_active_ship", "Active shipments", "Shipments", "Shipments not yet delivered.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-play");
        yield return P(a, "tms_open_ship", "Open shipments", "Shipments", "Shipments still being planned.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-folder-open-o");
        yield return P(a, "tms_waved_ship", "Waved shipments", "Shipments", "Shipments on a warehouse wave.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-flag");
        yield return P(a, "tms_picking_ship", "Picking shipments", "Shipments", "Shipments being picked.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-hand-paper-o");
        yield return P(a, "tms_ready_ship_s", "Shipments ready to ship", "Shipments", "Shipments that can leave the dock.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-check");
        yield return P(a, "tms_driver_check", "Driver check-in and check-out", "Dock appointment scheduling", "Check a driver in or out at the dock.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-id-badge");
        yield return P(a, "tms_appointments", "Appointment scheduling", "Dock appointment scheduling", "Dock appointments for a load.", ErpD365Part3Catalog.FormKind.Calendar, app, icon: "fa-calendar");
        yield return P(a, "tms_appt_notify", "Appointment notifications", "Dock appointment scheduling", "Notify a carrier of an appointment.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-bell");
        yield return P(a, "tms_routes", "Routes", "Dock appointment scheduling", "Transportation routes.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-road");
        yield return P(a, "tms_tenders", "Transportation tenders", "Dock appointment scheduling", "Tenders sent to a carrier.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-envelope-o");
        yield return P(a, "tms_match_freight", "Match freight bills and invoices", "Freight bills and invoices", "Match a freight bill to a vendor invoice.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-check-square-o");
        yield return P(a, "tms_status", "Transportation status", "Inquiries and reports", "Status of loads and shipments.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-flag");
        yield return P(a, "tms_freight_detail", "Freight invoice details", "Inquiries and reports", "Freight invoice lines.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-file-text-o");
        yield return P(a, "tms_txn", "Transactions", "Periodic tasks", "Transportation rating transactions.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list");
        yield return P(a, "tms_error_log", "Transportation system error log", "Periodic tasks", "Rating and routing errors.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-exclamation-triangle");
        yield return P(a, "tms_purge", "Transaction purge", "Periodic tasks", "Remove old transportation transactions.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-trash");
        yield return P(a, "tms_clear_session", "Clear load building session information", "Periodic tasks", "Clear a stuck load-building session.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-eraser");
        yield return P(a, "tms_recalc_freight", "Recalculate freight invoice status", "Cleanup", "Recalculate freight invoice match status.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-refresh");
        yield return P(a, "tms_parameters", "Transportation management parameters", "Setup", "Company defaults for loads, rating, and appointments.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "tms_status_masters", "Transportation status masters", "Setup", "Statuses a load or shipment moves through.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-flag-o");
        yield return P(a, "tms_appt_rules", "Appointment rules", "Appointment scheduling", "Rules that offer a dock slot.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-gavel");
        yield return P(a, "tms_carriers", "Shipping carriers", "Carriers", "Carriers used to move a load.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/carriers-app", "fa-truck");
        yield return P(a, "tms_carrier_groups", "Carrier group", "Carriers", "Groups of carriers.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-object-group");
        yield return P(a, "tms_mode", "Mode", "Carriers", "Transportation modes such as road or air.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-plane");
        yield return P(a, "tms_methods", "Transportation methods", "Carriers", "Methods a carrier may use.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-random");
        yield return P(a, "tms_rating", "Rating profile", "Rating", "How a load is rated.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-star");
        yield return P(a, "tms_rate_master", "Rate master", "Rating", "Rate tables used by a profile.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-table");
        yield return P(a, "tms_fuel", "Fuel indexes", "Fuel indexes", "Fuel-surcharge indexes.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tint");
        yield return P(a, "tms_routing", "Route guides", "Routing", "Guides that pick a route.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-map");
        yield return P(a, "tms_hubs", "Hub masters", "Routing", "Hubs used on a multi-leg route.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-building");
        yield return P(a, "tms_freight_recon", "Freight reconciliation", "Freight reconciliation", "Reconcile freight bills to invoices.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-balance-scale");
        yield return P(a, "tms_logistics", "Customs and shipping", "Planning", "Customs documents and carrier bookings.", ErpD365Part3Catalog.FormKind.Order, app, "/erp/carriers-app", "fa-ship");
    }

    private static IEnumerable<ErpD365Part3Catalog.Page> WarehousePages()
    {
        const string a = "warehouse";
        const string app = WarehouseApp;
        yield return P(a, "wh_work_plan", "Work planning", "Workspaces", "Plan outbound and inbound warehouse work.", ErpD365Part3Catalog.FormKind.Queue, app, "/erp/warehouse-wms-app", "fa-dashboard");
        yield return P(a, "wh_out_monitor", "Outbound work monitoring", "Workspaces", "Monitor outbound pick, pack, and ship work.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-eye");
        yield return P(a, "wh_impl", "Warehouse implementation tasks", "Workspaces", "Tasks used when a warehouse goes live.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-list");
        yield return P(a, "wh_mobile", "Warehouse mobile devices", "Workspaces", "Mobile-device users and menus.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-mobile");
        yield return P(a, "wh_all_loads", "All loads", "Loads", "Warehouse loads.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-truck");
        yield return P(a, "wh_active_loads", "Active loads", "Loads", "Loads not yet shipped.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-play");
        yield return P(a, "wh_waved_loads", "Waved loads", "Loads", "Loads that have a wave.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-flag");
        yield return P(a, "wh_ready_ship", "Loads ready to ship", "Loads", "Loads that can be shipped.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-check");
        yield return P(a, "wh_in_load_wb", "Inbound load planning workbench", "Loads", "Plan inbound loads.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-sign-in");
        yield return P(a, "wh_out_load_wb", "Outbound load planning workbench", "Loads", "Plan outbound loads.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-sign-out");
        yield return P(a, "wh_release_so", "Release sales orders to warehouse", "Release to warehouse", "Release sales orders so work can be created.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-unlock");
        yield return P(a, "wh_release_to", "Release transfer orders to warehouse", "Release to warehouse", "Release transfer orders so work can be created.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-exchange");
        yield return P(a, "wh_auto_release", "Automatic release of sales orders", "Release to warehouse", "Automatically release due sales orders.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-refresh");
        yield return P(a, "wh_all_ship", "All shipments", "Shipments", "Warehouse shipments.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-ship");
        yield return P(a, "wh_open_ship", "Open shipments", "Shipments", "Shipments still being planned.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-folder-open-o");
        yield return P(a, "wh_picking_ship", "Picking shipment lines", "Shipments", "Shipment lines being picked.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-hand-paper-o");
        yield return P(a, "wh_ready_ship_s", "Shipments ready to ship", "Shipments", "Shipments that can leave the dock.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-check");
        yield return P(a, "wh_all_waves", "All waves", "Outbound waves", "Every warehouse wave.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-flag");
        yield return P(a, "wh_pending_waves", "Pending waves", "Outbound waves", "Waves waiting to process.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-hourglass");
        yield return P(a, "wh_open_waves", "Open waves", "Outbound waves", "Waves being processed.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-folder-open-o");
        yield return P(a, "wh_closed_waves", "Closed waves", "Outbound waves", "Waves that have finished.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-check-circle");
        yield return P(a, "wh_prod_waves", "All production waves", "Production waves", "Waves that feed production.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-cogs");
        yield return P(a, "wh_kanban_waves", "All kanban waves", "Kanban waves", "Waves that feed kanban.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-refresh");
        yield return P(a, "wh_open_po_work", "Open purchase work", "Inbound", "Inbound work from a purchase order.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-sign-in");
        yield return P(a, "wh_closed_po_work", "Closed purchase work", "Inbound", "Finished inbound purchase work.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-check");
        yield return P(a, "wh_return_work", "Return work", "Inbound", "Inbound work from a customer return.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-undo");
        yield return P(a, "wh_open_so_work", "Open sales work", "Outbound", "Outbound work from a sales order.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-sign-out");
        yield return P(a, "wh_xfer_work", "Open transfer work", "Outbound", "Work from a transfer order.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-exchange");
        yield return P(a, "wh_container_pick", "Open container picking work", "Outbound", "Picking work into a container.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-archive");
        yield return P(a, "wh_open_cc", "Open cycle count work", "Cycle counting", "Open cycle-count work.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-check-square-o");
        yield return P(a, "wh_closed_cc", "Closed cycle count work", "Cycle counting", "Finished cycle counts.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-check");
        yield return P(a, "wh_cc_pending", "Cycle count work pending review", "Cycle counting", "Counts waiting for a supervisor.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-gavel");
        yield return P(a, "wh_replenish", "Replenishment", "Replenishment", "Replenish a pick location from bulk.", ErpD365Part3Catalog.FormKind.Order, app, icon: "fa-refresh");
        yield return P(a, "wh_open_replenish", "Open replenishment work", "Replenishment", "Open replenishment work.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-inbox");
        yield return P(a, "wh_pack", "Packing and containerization", "Packing and containerization", "Pack picked work into a container.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-archive");
        yield return P(a, "wh_containers", "Containers", "Packing and containerization", "Packed containers.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-cube");
        yield return P(a, "wh_onhand", "On-hand inventory", "Inquiries and reports", "Warehouse on-hand by location.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/inventory-stock-app", "fa-cubes");
        yield return P(a, "wh_physical", "Physical on-hand inventory", "Inquiries and reports", "Physical on-hand including license plates.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-th");
        yield return P(a, "wh_work_inq", "Work", "Inquiries and reports", "Warehouse work inquiry.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/warehouse-wms-app", "fa-list");
        yield return P(a, "wh_load_inq", "Loads report", "Inquiries and reports", "Load inquiry.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-truck");
        yield return P(a, "wh_wave_inq", "Wave report", "Inquiries and reports", "Wave inquiry.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-flag");
        yield return P(a, "wh_container_inq", "Container report", "Inquiries and reports", "Container inquiry.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-archive");
        yield return P(a, "wh_recv", "Receiving", "Periodic tasks", "Receive an inbound load or purchase.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-sign-in");
        yield return P(a, "wh_update_receipts", "Update product receipts", "Periodic tasks", "Push warehouse receipts to the purchase order.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-refresh");
        yield return P(a, "wh_mobile_log", "Mobile device log", "Periodic tasks", "Log of mobile-device activity.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-mobile");
        yield return P(a, "wh_app_events", "Warehouse app events", "Periodic tasks", "Events from the warehouse app.", ErpD365Part3Catalog.FormKind.Queue, app, icon: "fa-bell");
        yield return P(a, "wh_cleanup_work", "Work history cleanup", "Cleanup", "Remove finished warehouse work.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-trash");
        yield return P(a, "wh_cleanup_wave", "Wave processing log cleanup", "Cleanup", "Remove wave processing logs.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-eraser");
        yield return P(a, "wh_cleanup_batch", "Wave batch cleanup", "Cleanup", "Remove finished wave batches.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-minus");
        yield return P(a, "wh_parameters", "Warehouse management parameters", "Setup", "Company defaults for work, waves, and mobile devices.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-sliders");
        yield return P(a, "wh_wave_templates", "Wave templates", "Waves", "Templates that build a wave.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-flag");
        yield return P(a, "wh_wave_methods", "Wave process methods", "Waves", "Methods a wave template may run.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-cogs");
        yield return P(a, "wh_work_templates", "Work templates", "Work", "Templates that create warehouse work.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-list-alt");
        yield return P(a, "wh_work_classes", "Work classes", "Work", "Classes that group warehouse work.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-tags");
        yield return P(a, "wh_locations", "Locations", "Warehouse", "Warehouse locations.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/warehouses-app", "fa-map-marker");
        yield return P(a, "wh_warehouses", "Warehouses", "Warehouse", "Warehouse master.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/warehouses-app", "fa-building");
        yield return P(a, "wh_loc_profiles", "Location profiles", "Warehouse", "Profiles that control a location.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-id-card-o");
        yield return P(a, "wh_zones", "Warehouse zones", "Warehouse", "Zones used for work and location directives.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-th-large");
        yield return P(a, "wh_loc_directives", "Location directives", "Setup", "Where to pick or put inventory.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-map");
        yield return P(a, "wh_mobile_menu", "Mobile device menu", "Mobile device", "Menus shown on a mobile device.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-bars");
        yield return P(a, "wh_mobile_steps", "Mobile device steps", "Mobile device", "Steps in a mobile-device menu item.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-list-ol");
        yield return P(a, "wh_pack_profiles", "Packing profiles", "Packing", "How a shipment is packed.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-archive");
        yield return P(a, "wh_cc_plans", "Cycle count plans", "Cycle counting", "Plans that create cycle-count work.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-calendar");
        yield return P(a, "wh_release_pol", "Release to warehouse policies", "Release to warehouse", "When an order is released to the warehouse.", ErpD365Part3Catalog.FormKind.Plan, app, icon: "fa-unlock");
        yield return P(a, "wh_gs1", "GS1", "GS1", "GS1 barcode identifiers.", ErpD365Part3Catalog.FormKind.Master, app, icon: "fa-barcode");
        yield return P(a, "wh_wizards", "Warehouse management initiation wizard", "Wizards", "Wizard that creates a first warehouse setup.", ErpD365Part3Catalog.FormKind.Action, app, icon: "fa-magic");
        yield return P(a, "wh_wms", "Warehouse management", "Workspaces", "Dedicated warehouse work, waves, and mobile RF.", ErpD365Part3Catalog.FormKind.Master, app, "/erp/warehouse-wms-app", "fa-cubes");
    }
}
