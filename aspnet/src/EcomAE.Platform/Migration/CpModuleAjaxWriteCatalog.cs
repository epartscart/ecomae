namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave C inventory of CloudPanel module ajax write surfaces:
/// procurement, document_control, customer_mgmt, auto_price, CRM (ajax_crm.php + helpers).
/// Never invents cutoverAllowed=true; PHP remains authoritative.
/// </summary>
public interface ICpModuleAjaxWriteCatalog
{
    CpModuleAjaxWriteCatalogReport BuildReport();
    bool TryGet(string module, string action, out CpModuleAjaxWriteCatalogEntry entry);
    IReadOnlyList<CpModuleAjaxWriteCatalogEntry> All { get; }
}

public sealed class CpModuleAjaxWriteCatalog : ICpModuleAjaxWriteCatalog
{
    private static readonly CpModuleAjaxWriteCatalogEntry[] Entries =
    [
        new("procurement", "create_supplier", "dedicated", "/cp/module-ajax/procurement/create_supplier/dry-run", "cp/content/shop/procurement/ajax_procurement.php"),
        new("procurement", "update_supplier", "dedicated", "/cp/module-ajax/procurement/update_supplier/dry-run", "cp/content/shop/procurement/ajax_procurement.php"),
        new("procurement", "sync_suppliers", "dedicated", "/cp/module-ajax/procurement/sync_suppliers/dry-run", "cp/content/shop/procurement/ajax_procurement.php"),
        new("procurement", "create_purchase", "dedicated", "/cp/module-ajax/procurement/create_purchase/dry-run", "cp/content/shop/procurement/ajax_procurement.php"),
        new("procurement", "supplier_payment", "dedicated", "/cp/module-ajax/procurement/supplier_payment/dry-run", "cp/content/shop/procurement/ajax_procurement.php"),
        new("procurement", "record_advance", "dedicated", "/cp/module-ajax/procurement/record_advance/dry-run", "cp/content/shop/procurement/ajax_procurement.php"),
        new("procurement", "purchase_from_order", "dedicated", "/cp/module-ajax/procurement/purchase_from_order/dry-run", "cp/content/shop/procurement/ajax_procurement.php"),
        new("procurement", "supplier_settlement", "dedicated", "/cp/module-ajax/procurement/supplier_settlement/dry-run", "cp/content/shop/procurement/ajax_procurement.php"),
        new("procurement", "purchase_adjustment", "dedicated", "/cp/module-ajax/procurement/purchase_adjustment/dry-run", "cp/content/shop/procurement/ajax_procurement.php"),
        new("document_control", "save_company", "dedicated", "/cp/module-ajax/document_control/save_company/dry-run", "cp/content/shop/document_control/ajax_document_control.php"),
        new("document_control", "save_template", "dedicated", "/cp/module-ajax/document_control/save_template/dry-run", "cp/content/shop/document_control/ajax_document_control.php"),
        new("document_control", "upload_logo", "dedicated", "/cp/module-ajax/document_control/upload_logo/dry-run", "cp/content/shop/document_control/ajax_document_control.php"),
        new("document_control", "upload_attachment", "dedicated", "/cp/module-ajax/document_control/upload_attachment/dry-run", "cp/content/shop/document_control/ajax_document_control.php"),
        new("document_control", "delete_attachment", "dedicated", "/cp/module-ajax/document_control/delete_attachment/dry-run", "cp/content/shop/document_control/ajax_document_control.php"),
        new("document_control", "sync_einvoice_seller", "dedicated", "/cp/module-ajax/document_control/sync_einvoice_seller/dry-run", "cp/content/shop/document_control/ajax_document_control.php"),
        new("customer_mgmt", "save_customer", "dedicated", "/cp/module-ajax/customer_mgmt/save_customer/dry-run", "cp/content/shop/customer_mgmt/ajax_customer_mgmt.php"),
        new("customer_mgmt", "customer_advance", "dedicated", "/cp/module-ajax/customer_mgmt/customer_advance/dry-run", "cp/content/shop/customer_mgmt/ajax_customer_mgmt.php"),
        new("customer_mgmt", "einvoice_create", "dedicated", "/cp/module-ajax/customer_mgmt/einvoice_create/dry-run", "cp/content/shop/customer_mgmt/ajax_customer_mgmt.php"),
        new("auto_price", "discover_search", "registry", "/cp/module-ajax/dry-run/auto_price/discover_search", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "list_discovery_sources", "registry", "/cp/module-ajax/dry-run/auto_price/list_discovery_sources", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "add_discovery_source", "dedicated", "/cp/module-ajax/auto_price/add_discovery_source/dry-run", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "delete_discovery_source", "dedicated", "/cp/module-ajax/auto_price/delete_discovery_source/dry-run", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "toggle_discovery_source", "dedicated", "/cp/module-ajax/auto_price/toggle_discovery_source/dry-run", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "test_source_login", "registry", "/cp/module-ajax/dry-run/auto_price/test_source_login", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "fetch_prices", "registry", "/cp/module-ajax/dry-run/auto_price/fetch_prices", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "start_job", "dedicated", "/cp/module-ajax/auto_price/start_job/dry-run", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "job_status", "registry", "/cp/module-ajax/dry-run/auto_price/job_status", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "crawl_sources", "dedicated", "/cp/module-ajax/auto_price/crawl_sources/dry-run", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "crawl_status", "registry", "/cp/module-ajax/dry-run/auto_price/crawl_status", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "skip_source", "dedicated", "/cp/module-ajax/auto_price/skip_source/dry-run", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "discover_counts", "registry", "/cp/module-ajax/dry-run/auto_price/discover_counts", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "list_discover_queue", "registry", "/cp/module-ajax/dry-run/auto_price/list_discover_queue", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "warehouse_market_match", "registry", "/cp/module-ajax/dry-run/auto_price/warehouse_market_match", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "match_catalogue_market", "registry", "/cp/module-ajax/dry-run/auto_price/match_catalogue_market", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "bulk_approve", "dedicated", "/cp/module-ajax/auto_price/bulk_approve/dry-run", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "advise_category", "registry", "/cp/module-ajax/dry-run/auto_price/advise_category", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "list_categories", "registry", "/cp/module-ajax/dry-run/auto_price/list_categories", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "list_my_imports", "registry", "/cp/module-ajax/dry-run/auto_price/list_my_imports", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "dismiss_duplicate", "dedicated", "/cp/module-ajax/auto_price/dismiss_duplicate/dry-run", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "shell_kpi", "registry", "/cp/module-ajax/dry-run/auto_price/shell_kpi", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "load_tab_html", "registry", "/cp/module-ajax/dry-run/auto_price/load_tab_html", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "product_line_prices", "registry", "/cp/module-ajax/dry-run/auto_price/product_line_prices", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "product_lines_tax_tree", "registry", "/cp/module-ajax/dry-run/auto_price/product_lines_tax_tree", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "warehouse_list", "registry", "/cp/module-ajax/dry-run/auto_price/warehouse_list", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "warehouse_compare_selected", "registry", "/cp/module-ajax/dry-run/auto_price/warehouse_compare_selected", "cp/content/control/portal/ajax_auto_price.php"),
        new("auto_price", "country_info", "registry", "/cp/module-ajax/dry-run/auto_price/country_info", "cp/content/control/portal/ajax_auto_price.php"),
        new("crm", "crm_save_lead", "dedicated", "/cp/module-ajax/crm/crm_save_lead/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "save_lead", "dedicated", "/cp/module-ajax/crm/save_lead/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_delete_lead", "dedicated", "/cp/module-ajax/crm/crm_delete_lead/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "delete_lead", "dedicated", "/cp/module-ajax/crm/delete_lead/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_save_opportunity", "dedicated", "/cp/module-ajax/crm/crm_save_opportunity/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "save_opportunity", "dedicated", "/cp/module-ajax/crm/save_opportunity/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_update_stage", "dedicated", "/cp/module-ajax/crm/crm_update_stage/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "update_stage", "dedicated", "/cp/module-ajax/crm/update_stage/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_convert_lead", "dedicated", "/cp/module-ajax/crm/crm_convert_lead/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "convert_lead", "dedicated", "/cp/module-ajax/crm/convert_lead/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_won_hint", "registry", "/cp/module-ajax/dry-run/crm/crm_won_hint", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "won_hint", "registry", "/cp/module-ajax/dry-run/crm/won_hint", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_get_timeline", "registry", "/cp/module-ajax/dry-run/crm/crm_get_timeline", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "get_timeline", "registry", "/cp/module-ajax/dry-run/crm/get_timeline", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_save_activity", "dedicated", "/cp/module-ajax/crm/crm_save_activity/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "save_activity", "dedicated", "/cp/module-ajax/crm/save_activity/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_toggle_activity", "dedicated", "/cp/module-ajax/crm/crm_toggle_activity/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "toggle_activity", "dedicated", "/cp/module-ajax/crm/toggle_activity/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_dashboard", "registry", "/cp/module-ajax/dry-run/crm/crm_dashboard", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "dashboard", "registry", "/cp/module-ajax/dry-run/crm/dashboard", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_pipeline", "registry", "/cp/module-ajax/dry-run/crm/crm_pipeline", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "pipeline", "registry", "/cp/module-ajax/dry-run/crm/pipeline", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_save_quote", "dedicated", "/cp/module-ajax/crm/crm_save_quote/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_accept_quote", "dedicated", "/cp/module-ajax/crm/crm_accept_quote/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_quote_preview", "registry", "/cp/module-ajax/dry-run/crm/crm_quote_preview", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_quote_email", "dedicated", "/cp/module-ajax/crm/crm_quote_email/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_save_ticket", "dedicated", "/cp/module-ajax/crm/crm_save_ticket/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_save_project", "dedicated", "/cp/module-ajax/crm/crm_save_project/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_save_project_task", "dedicated", "/cp/module-ajax/crm/crm_save_project_task/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_save_contract", "dedicated", "/cp/module-ajax/crm/crm_save_contract/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_save_expense", "dedicated", "/cp/module-ajax/crm/crm_save_expense/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_approve_expense", "dedicated", "/cp/module-ajax/crm/crm_approve_expense/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_get_lead", "registry", "/cp/module-ajax/dry-run/crm/crm_get_lead", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "get_lead", "registry", "/cp/module-ajax/dry-run/crm/get_lead", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_get_opportunity", "registry", "/cp/module-ajax/dry-run/crm/crm_get_opportunity", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "get_opportunity", "registry", "/cp/module-ajax/dry-run/crm/get_opportunity", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_get_ticket", "registry", "/cp/module-ajax/dry-run/crm/crm_get_ticket", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "get_ticket", "registry", "/cp/module-ajax/dry-run/crm/get_ticket", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_update_ticket_status", "dedicated", "/cp/module-ajax/crm/crm_update_ticket_status/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "update_ticket_status", "dedicated", "/cp/module-ajax/crm/update_ticket_status/dry-run", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_get_project", "registry", "/cp/module-ajax/dry-run/crm/crm_get_project", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "get_project", "registry", "/cp/module-ajax/dry-run/crm/get_project", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_adv_dashboard", "registry", "/cp/module-ajax/dry-run/crm/crm_adv_dashboard", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "adv_dashboard", "registry", "/cp/module-ajax/dry-run/crm/adv_dashboard", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_customer_360", "registry", "/cp/module-ajax/dry-run/crm/crm_customer_360", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "customer_360", "registry", "/cp/module-ajax/dry-run/crm/customer_360", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_score_lead", "registry", "/cp/module-ajax/dry-run/crm/crm_score_lead", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "score_lead", "registry", "/cp/module-ajax/dry-run/crm/score_lead", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "crm_quote_tax", "registry", "/cp/module-ajax/dry-run/crm/crm_quote_tax", "cp/content/shop/crm/ajax_crm.php"),
        new("crm", "quote_tax", "registry", "/cp/module-ajax/dry-run/crm/quote_tax", "cp/content/shop/crm/ajax_crm.php"),
    ];

    private static readonly Dictionary<string, CpModuleAjaxWriteCatalogEntry> ByKey =
        Entries.ToDictionary(e => Key(e.Module, e.Action), StringComparer.OrdinalIgnoreCase);

    public static string Key(string module, string action) =>
        string.Concat((module ?? "").Trim().ToLowerInvariant(), "/", (action ?? "").Trim().ToLowerInvariant());

    public IReadOnlyList<CpModuleAjaxWriteCatalogEntry> All => Entries;

    public bool TryGet(string module, string action, out CpModuleAjaxWriteCatalogEntry entry) =>
        ByKey.TryGetValue(Key(module, action), out entry!);

    public CpModuleAjaxWriteCatalogReport BuildReport()
    {
        var dedicated = Entries.Count(e => e.Coverage == "dedicated");
        var registry = Entries.Count(e => e.Coverage == "registry");
        var byModule = Entries
            .GroupBy(e => e.Module, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CpModuleAjaxWriteModuleSummary(
                g.Key,
                g.Count(),
                g.Count(x => x.Coverage == "dedicated"),
                g.First().PhpAjax))
            .ToList();
        return new(
            Role: "cp-module-ajax-write-catalog",
            Status: "building-toward-zero-php",
            CutoverAllowed: false,
            ReadyForPhpRemoval: false,
            PhpAuthoritative: true,
            TotalActions: Entries.Length,
            DedicatedDryRuns: dedicated,
            RegistryDryRuns: registry,
            CoveragePct: Entries.Length == 0 ? 0 : (int)Math.Round(100.0 * (dedicated + registry) / Entries.Length),
            Modules: byModule,
            Actions: Entries,
            Notes:
            [
                "Coverage means ASP.NET dry-run gate exists (writes=0); live CP module ajax remains PHP.",
                "Dedicated routes cover classified write/mutate actions; registry covers the long-tail reads via POST /cp/module-ajax/dry-run/{module}/{action}.",
                "CRM actions dispatch through ajax_crm.php → epc_crm_handle_ajax_action in epc_crm_helpers.php.",
                "cutoverAllowed stays false until dual-sample + human RELEASE_OWNER_APPROVAL.md.",
            ]);
    }
}

public sealed record CpModuleAjaxWriteCatalogEntry(string Module, string Action, string Coverage, string AspNetRouteHint, string PhpAjax);
public sealed record CpModuleAjaxWriteModuleSummary(string Module, int TotalActions, int DedicatedDryRuns, string PhpAjax);
public sealed record CpModuleAjaxWriteCatalogReport(
    string Role, string Status, bool CutoverAllowed, bool ReadyForPhpRemoval, bool PhpAuthoritative,
    int TotalActions, int DedicatedDryRuns, int RegistryDryRuns, int CoveragePct,
    IReadOnlyList<CpModuleAjaxWriteModuleSummary> Modules,
    IReadOnlyList<CpModuleAjaxWriteCatalogEntry> Actions, IReadOnlyList<string> Notes);

