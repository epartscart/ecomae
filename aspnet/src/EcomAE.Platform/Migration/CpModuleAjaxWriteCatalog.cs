namespace EcomAE.Platform.Migration;

/// <summary>
/// Wave C/D inventory of CloudPanel module ajax write surfaces
/// (procurement, document_control, customer_mgmt, auto_price, CRM, bulk/marketing/catalogue/
/// crosses/prices/portal leftovers). Never invents cutoverAllowed=true; PHP remains authoritative.
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
        new("bulk_upload", "dashboard", "registry", "/cp/module-ajax/dry-run/bulk_upload/dashboard", "cp/content/shop/bulk_upload/ajax_bulk_cp.php"),
        new("bulk_upload", "list_history", "registry", "/cp/module-ajax/dry-run/bulk_upload/list_history", "cp/content/shop/bulk_upload/ajax_bulk_cp.php"),
        new("bulk_upload", "get_upload", "registry", "/cp/module-ajax/dry-run/bulk_upload/get_upload", "cp/content/shop/bulk_upload/ajax_bulk_cp.php"),
        new("bulk_upload", "search_customers", "registry", "/cp/module-ajax/dry-run/bulk_upload/search_customers", "cp/content/shop/bulk_upload/ajax_bulk_cp.php"),
        new("bulk_upload", "process_upload", "dedicated", "/cp/module-ajax/bulk_upload/process_upload/dry-run", "cp/content/shop/bulk_upload/ajax_bulk_cp.php"),
        new("bulk_upload", "mark_reviewed", "dedicated", "/cp/module-ajax/bulk_upload/mark_reviewed/dry-run", "cp/content/shop/bulk_upload/ajax_bulk_cp.php"),
        new("bulk_upload", "add_to_cart", "dedicated", "/cp/module-ajax/bulk_upload/add_to_cart/dry-run", "cp/content/shop/bulk_upload/ajax_bulk_cp.php"),
        new("bulk_upload", "create_shop_quote", "dedicated", "/cp/module-ajax/bulk_upload/create_shop_quote/dry-run", "cp/content/shop/bulk_upload/ajax_bulk_cp.php"),
        new("bulk_upload", "create_crm_quote", "dedicated", "/cp/module-ajax/bulk_upload/create_crm_quote/dry-run", "cp/content/shop/bulk_upload/ajax_bulk_cp.php"),
        new("marketing", "toggle_task", "dedicated", "/cp/module-ajax/marketing/toggle_task/dry-run", "cp/content/shop/marketing/ajax_marketing.php"),
        new("marketing", "save_kpi", "dedicated", "/cp/module-ajax/marketing/save_kpi/dry-run", "cp/content/shop/marketing/ajax_marketing.php"),
        new("marketing", "save_review", "dedicated", "/cp/module-ajax/marketing/save_review/dry-run", "cp/content/shop/marketing/ajax_marketing.php"),
        new("marketing", "snapshot", "registry", "/cp/module-ajax/dry-run/marketing/snapshot", "cp/content/shop/marketing/ajax_marketing.php"),
        new("catalogue_products", "save_product_status_limit", "dedicated", "/cp/module-ajax/catalogue_products/save_product_status_limit/dry-run", "cp/content/shop/catalogue/ajax_operations_products.php"),
        new("catalogue_products", "save_product_value_limit", "dedicated", "/cp/module-ajax/catalogue_products/save_product_value_limit/dry-run", "cp/content/shop/catalogue/ajax_operations_products.php"),
        new("catalogue_products", "get_table", "registry", "/cp/module-ajax/dry-run/catalogue_products/get_table", "cp/content/shop/catalogue/ajax_operations_products.php"),
        new("catalogue_products", "caption", "registry", "/cp/module-ajax/dry-run/catalogue_products/caption", "cp/content/shop/catalogue/ajax_operations_products.php"),
        new("catalogue_products", "id", "registry", "/cp/module-ajax/dry-run/catalogue_products/id", "cp/content/shop/catalogue/ajax_operations_products.php"),
        new("catalogue_products", "category_id", "registry", "/cp/module-ajax/dry-run/catalogue_products/category_id", "cp/content/shop/catalogue/ajax_operations_products.php"),
        new("catalogue_products", "published_flag", "registry", "/cp/module-ajax/dry-run/catalogue_products/published_flag", "cp/content/shop/catalogue/ajax_operations_products.php"),
        new("catalogue_products", "exist", "registry", "/cp/module-ajax/dry-run/catalogue_products/exist", "cp/content/shop/catalogue/ajax_operations_products.php"),
        new("catalogue_products", "price", "registry", "/cp/module-ajax/dry-run/catalogue_products/price", "cp/content/shop/catalogue/ajax_operations_products.php"),
        new("catalogue_products", "storage_id", "registry", "/cp/module-ajax/dry-run/catalogue_products/storage_id", "cp/content/shop/catalogue/ajax_operations_products.php"),
        new("catalogue_products", "reserved", "registry", "/cp/module-ajax/dry-run/catalogue_products/reserved", "cp/content/shop/catalogue/ajax_operations_products.php"),
        new("catalogue_products", "issued", "registry", "/cp/module-ajax/dry-run/catalogue_products/issued", "cp/content/shop/catalogue/ajax_operations_products.php"),
        new("catalogue_products", "min_limit", "registry", "/cp/module-ajax/dry-run/catalogue_products/min_limit", "cp/content/shop/catalogue/ajax_operations_products.php"),
        new("catalogue_products", "min_limit_enable", "registry", "/cp/module-ajax/dry-run/catalogue_products/min_limit_enable", "cp/content/shop/catalogue/ajax_operations_products.php"),
        new("manufacturers_synonyms", "get_manufacturers", "registry", "/cp/module-ajax/dry-run/manufacturers_synonyms/get_manufacturers", "cp/content/shop/manufacturers_synonyms/ajax_operations.php"),
        new("manufacturers_synonyms", "get_synonyms", "registry", "/cp/module-ajax/dry-run/manufacturers_synonyms/get_synonyms", "cp/content/shop/manufacturers_synonyms/ajax_operations.php"),
        new("manufacturers_synonyms", "add_manufacturer", "dedicated", "/cp/module-ajax/manufacturers_synonyms/add_manufacturer/dry-run", "cp/content/shop/manufacturers_synonyms/ajax_operations.php"),
        new("manufacturers_synonyms", "save_manufacturer", "dedicated", "/cp/module-ajax/manufacturers_synonyms/save_manufacturer/dry-run", "cp/content/shop/manufacturers_synonyms/ajax_operations.php"),
        new("manufacturers_synonyms", "del_manufacturer", "dedicated", "/cp/module-ajax/manufacturers_synonyms/del_manufacturer/dry-run", "cp/content/shop/manufacturers_synonyms/ajax_operations.php"),
        new("manufacturers_synonyms", "add_synonym", "dedicated", "/cp/module-ajax/manufacturers_synonyms/add_synonym/dry-run", "cp/content/shop/manufacturers_synonyms/ajax_operations.php"),
        new("manufacturers_synonyms", "save_synonym", "dedicated", "/cp/module-ajax/manufacturers_synonyms/save_synonym/dry-run", "cp/content/shop/manufacturers_synonyms/ajax_operations.php"),
        new("manufacturers_synonyms", "del_synonym", "dedicated", "/cp/module-ajax/manufacturers_synonyms/del_synonym/dry-run", "cp/content/shop/manufacturers_synonyms/ajax_operations.php"),
        new("crosses_cp", "lookup_crosses", "registry", "/cp/module-ajax/dry-run/crosses_cp/lookup_crosses", "cp/content/shop/crosses/ajax_epc_cross_cp.php"),
        new("crosses_cp", "verify_crosses", "registry", "/cp/module-ajax/dry-run/crosses_cp/verify_crosses", "cp/content/shop/crosses/ajax_epc_cross_cp.php"),
        new("crosses_cp", "add_cross_link", "dedicated", "/cp/module-ajax/crosses_cp/add_cross_link/dry-run", "cp/content/shop/crosses/ajax_epc_cross_cp.php"),
        new("crosses_cp", "add_cross_bulk", "dedicated", "/cp/module-ajax/crosses_cp/add_cross_bulk/dry-run", "cp/content/shop/crosses/ajax_epc_cross_cp.php"),
        new("crosses_cp", "sync_from_crossbase", "dedicated", "/cp/module-ajax/crosses_cp/sync_from_crossbase/dry-run", "cp/content/shop/crosses/ajax_epc_cross_cp.php"),
        new("crosses_cp", "import_full_catalog", "dedicated", "/cp/module-ajax/crosses_cp/import_full_catalog/dry-run", "cp/content/shop/crosses/ajax_epc_cross_cp.php"),
        new("crosses_cp", "repair_empty_brands", "dedicated", "/cp/module-ajax/crosses_cp/repair_empty_brands/dry-run", "cp/content/shop/crosses/ajax_epc_cross_cp.php"),
        new("crosses", "get_table_crosses", "registry", "/cp/module-ajax/dry-run/crosses/get_table_crosses", "cp/content/shop/crosses/ajax_operations.php"),
        new("crosses", "add_crosses", "dedicated", "/cp/module-ajax/crosses/add_crosses/dry-run", "cp/content/shop/crosses/ajax_operations.php"),
        new("crosses", "save_crosses", "dedicated", "/cp/module-ajax/crosses/save_crosses/dry-run", "cp/content/shop/crosses/ajax_operations.php"),
        new("crosses", "del_crosses", "dedicated", "/cp/module-ajax/crosses/del_crosses/dry-run", "cp/content/shop/crosses/ajax_operations.php"),
        new("crosses", "del_search_crosses", "dedicated", "/cp/module-ajax/crosses/del_search_crosses/dry-run", "cp/content/shop/crosses/ajax_operations.php"),
        new("crosses", "get_search_manufacturer", "registry", "/cp/module-ajax/dry-run/crosses/get_search_manufacturer", "cp/content/shop/crosses/ajax_operations.php"),
        new("prices_edit", "get_table", "registry", "/cp/module-ajax/dry-run/prices_edit/get_table", "cp/content/shop/prices_edit/ajax_operations.php"),
        new("prices_edit", "add", "dedicated", "/cp/module-ajax/prices_edit/add/dry-run", "cp/content/shop/prices_edit/ajax_operations.php"),
        new("prices_edit", "save", "dedicated", "/cp/module-ajax/prices_edit/save/dry-run", "cp/content/shop/prices_edit/ajax_operations.php"),
        new("prices_edit", "del", "dedicated", "/cp/module-ajax/prices_edit/del/dry-run", "cp/content/shop/prices_edit/ajax_operations.php"),
        new("prices_edit", "del_search", "dedicated", "/cp/module-ajax/prices_edit/del_search/dry-run", "cp/content/shop/prices_edit/ajax_operations.php"),
        new("prices_send", "list_brands", "registry", "/cp/module-ajax/dry-run/prices_send/list_brands", "cp/content/shop/prices_send/ajax_operations.php"),
        new("prices_send", "ensure_office_storage_links", "dedicated", "/cp/module-ajax/prices_send/ensure_office_storage_links/dry-run", "cp/content/shop/prices_send/ajax_operations.php"),
        new("prices_send", "send_prices", "dedicated", "/cp/module-ajax/prices_send/send_prices/dry-run", "cp/content/shop/prices_send/ajax_operations.php"),
        new("prices_send", "check_office_storages_map", "registry", "/cp/module-ajax/dry-run/prices_send/check_office_storages_map", "cp/content/shop/prices_send/ajax_operations.php"),
        new("prices_send", "create_prices", "dedicated", "/cp/module-ajax/prices_send/create_prices/dry-run", "cp/content/shop/prices_send/ajax_operations.php"),
        new("sku_media", "list", "registry", "/cp/module-ajax/dry-run/sku_media/list", "content/shop/catalogue/ajax_epc_sku_media.php"),
        new("sku_media", "get", "registry", "/cp/module-ajax/dry-run/sku_media/get", "content/shop/catalogue/ajax_epc_sku_media.php"),
        new("sku_media", "ensure", "dedicated", "/cp/module-ajax/sku_media/ensure/dry-run", "content/shop/catalogue/ajax_epc_sku_media.php"),
        new("sku_media", "save_profile", "dedicated", "/cp/module-ajax/sku_media/save_profile/dry-run", "content/shop/catalogue/ajax_epc_sku_media.php"),
        new("sku_media", "delete_profile", "dedicated", "/cp/module-ajax/sku_media/delete_profile/dry-run", "content/shop/catalogue/ajax_epc_sku_media.php"),
        new("sku_media", "upload_photo", "dedicated", "/cp/module-ajax/sku_media/upload_photo/dry-run", "content/shop/catalogue/ajax_epc_sku_media.php"),
        new("sku_media", "update_photo", "dedicated", "/cp/module-ajax/sku_media/update_photo/dry-run", "content/shop/catalogue/ajax_epc_sku_media.php"),
        new("sku_media", "delete_photo", "dedicated", "/cp/module-ajax/sku_media/delete_photo/dry-run", "content/shop/catalogue/ajax_epc_sku_media.php"),
        new("sku_media", "add_spec_group", "dedicated", "/cp/module-ajax/sku_media/add_spec_group/dry-run", "content/shop/catalogue/ajax_epc_sku_media.php"),
        new("sku_media", "delete_spec_group", "dedicated", "/cp/module-ajax/sku_media/delete_spec_group/dry-run", "content/shop/catalogue/ajax_epc_sku_media.php"),
        new("sku_media", "add_spec_row", "dedicated", "/cp/module-ajax/sku_media/add_spec_row/dry-run", "content/shop/catalogue/ajax_epc_sku_media.php"),
        new("sku_media", "update_spec_row", "dedicated", "/cp/module-ajax/sku_media/update_spec_row/dry-run", "content/shop/catalogue/ajax_epc_sku_media.php"),
        new("sku_media", "delete_spec_row", "dedicated", "/cp/module-ajax/sku_media/delete_spec_row/dry-run", "content/shop/catalogue/ajax_epc_sku_media.php"),
        new("sku_media", "meta", "registry", "/cp/module-ajax/dry-run/sku_media/meta", "content/shop/catalogue/ajax_epc_sku_media.php"),
        new("portal_integrations", "save_mobile", "dedicated", "/cp/module-ajax/portal_integrations/save_mobile/dry-run", "cp/content/control/portal/ajax_integrations.php"),
        new("portal_integrations", "save_feature_flags", "dedicated", "/cp/module-ajax/portal_integrations/save_feature_flags/dry-run", "cp/content/control/portal/ajax_integrations.php"),
        new("portal_integrations", "save_tenant_smtp", "dedicated", "/cp/module-ajax/portal_integrations/save_tenant_smtp/dry-run", "cp/content/control/portal/ajax_integrations.php"),
        new("portal_integrations", "test_tenant_smtp", "registry", "/cp/module-ajax/dry-run/portal_integrations/test_tenant_smtp", "cp/content/control/portal/ajax_integrations.php"),
        new("portal_governance", "save_rule", "dedicated", "/cp/module-ajax/portal_governance/save_rule/dry-run", "cp/content/control/portal/ajax_platform_governance.php"),
        new("portal_governance", "list_rules", "registry", "/cp/module-ajax/dry-run/portal_governance/list_rules", "cp/content/control/portal/ajax_platform_governance.php"),
        new("portal_page_editor", "load_layout", "registry", "/cp/module-ajax/dry-run/portal_page_editor/load_layout", "cp/content/control/portal/ajax_visual_page_editor.php"),
        new("portal_page_editor", "save_layout", "dedicated", "/cp/module-ajax/portal_page_editor/save_layout/dry-run", "cp/content/control/portal/ajax_visual_page_editor.php"),
        new("portal_social", "generate_caption", "registry", "/cp/module-ajax/dry-run/portal_social/generate_caption", "cp/content/control/portal/ajax_epc_social_media.php"),
        new("portal_social", "save_draft", "dedicated", "/cp/module-ajax/portal_social/save_draft/dry-run", "cp/content/control/portal/ajax_epc_social_media.php"),
        new("portal_social", "test_account", "registry", "/cp/module-ajax/dry-run/portal_social/test_account", "cp/content/control/portal/ajax_epc_social_media.php"),
        new("portal_social", "publish_draft", "dedicated", "/cp/module-ajax/portal_social/publish_draft/dry-run", "cp/content/control/portal/ajax_epc_social_media.php"),
        new("portal_social", "publish_now", "dedicated", "/cp/module-ajax/portal_social/publish_now/dry-run", "cp/content/control/portal/ajax_epc_social_media.php"),
        new("portal_free_tools", "toggle", "dedicated", "/cp/module-ajax/portal_free_tools/toggle/dry-run", "cp/content/control/portal/ajax_epc_free_tools_admin.php"),
        new("portal_free_tools", "stats", "registry", "/cp/module-ajax/dry-run/portal_free_tools/stats", "cp/content/control/portal/ajax_epc_free_tools_admin.php"),
        new("prices_upload", "ajax_1_prepare_tmp_dir", "dedicated", "/cp/module-ajax/prices_upload/ajax_1_prepare_tmp_dir/dry-run", "cp/content/shop/prices_upload/"),
        new("prices_upload", "ajax_2_extract_files", "dedicated", "/cp/module-ajax/prices_upload/ajax_2_extract_files/dry-run", "cp/content/shop/prices_upload/"),
        new("prices_upload", "ajax_3_excel_convert", "dedicated", "/cp/module-ajax/prices_upload/ajax_3_excel_convert/dry-run", "cp/content/shop/prices_upload/"),
        new("prices_upload", "ajax_4_prepare_csv", "dedicated", "/cp/module-ajax/prices_upload/ajax_4_prepare_csv/dry-run", "cp/content/shop/prices_upload/"),
        new("prices_upload", "ajax_5_import_csv_to_db", "dedicated", "/cp/module-ajax/prices_upload/ajax_5_import_csv_to_db/dry-run", "cp/content/shop/prices_upload/"),
        new("prices_upload", "ajax_6_complete_session", "dedicated", "/cp/module-ajax/prices_upload/ajax_6_complete_session/dry-run", "cp/content/shop/prices_upload/"),
        new("prices_upload", "ajax_7_enable_keys", "dedicated", "/cp/module-ajax/prices_upload/ajax_7_enable_keys/dry-run", "cp/content/shop/prices_upload/"),
        new("portal_broadcast", "count_recipients", "registry", "/cp/module-ajax/dry-run/portal_broadcast/count_recipients", "cp/content/control/portal/ajax_marketing_broadcast.php"),
        new("portal_broadcast", "template_preview", "registry", "/cp/module-ajax/dry-run/portal_broadcast/template_preview", "cp/content/control/portal/ajax_marketing_broadcast.php"),
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
                "Dedicated routes cover classified write/mutate actions; registry covers reads via POST /cp/module-ajax/dry-run/{module}/{action}.",
                "CRM actions dispatch through ajax_crm.php → epc_crm_handle_ajax_action in epc_crm_helpers.php.",
                "prices_upload ajax_1..ajax_7 are file-level pipeline steps (ajax_5/ajax_6 also have richer dedicated routes).",
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

