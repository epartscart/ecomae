using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Living PHP vs ASP.NET product-surface matrix.
/// Navigation twins are not PHP deletion. Writes stay PHP-authoritative until a human
/// <c>readyForPhpRemoval</c> gate plus dual-sample pass.
/// </summary>
public static class PhpVsAspNetRemovalMatrix
{
    public const bool ReadyForPhpRemoval = false;
    public const bool PhpSourceDeletionAllowed = false;
    public const bool CutoverAllowed = false;
    public const int AspNetInteractiveCompleteCount = 0;
    public const string AsOfUtc = "2026-09-04";

    public static IReadOnlyList<PhpVsAspNetMatrixRow> Rows { get; } =
    [
        // Storefront
        Row("sf-home", "storefront", "templates/*/desktop.php + content/general_pages", "/", "/storefront/app", "aspnet-digest", "php", "Homepage chrome is ASP.NET. Signed-in and guest checkout/create and demo pay are ASP.NET-live. Staff email notify stays PHP."),
        Row("sf-search", "storefront", "content/shop/docpart/ajax_part_search.php", "/shop/part_search", "/en/shop/part_search", "aspnet-digest", "php", "Offer list is ASP.NET; live supplier poll stays PHP. Cart add is /storefront/cart/add."),
        Row("sf-vin", "storefront", "content/laximo + content/general_pages/vin_zapros.php", "/en/katalog-laximo", "/storefront/vin-app", "aspnet-digest", "aspnet", "Live Laximo FindVehicleByVIN and users_vin INSERT / customer message are ASP.NET-live. Captcha, photos, and manager email stay Classic."),
        Row("sf-cart", "storefront", "content/shop/order_process", "/shop/cart", "/en/shop/cart", "aspnet-digest", "aspnet", "Type-2 qty / delete / check-for-order and guest session carts write on ASP.NET. Type-1 catalogue details stay PHP."),
        Row("sf-checkout", "storefront", "content/shop/order_process", "/shop/checkout", "/storefront/checkout-app", "aspnet-digest", "php", "Signed-in and guest checkout/create are ASP.NET-live (session cookie + phone_not_auth). Demo pay create/notify are ASP.NET-live. Staff email stays PHP."),
        Row("sf-orders", "storefront", "content/shop/order_process", "/shop/orders", "/storefront/orders-app", "aspnet-digest", "php", "Customer order list digest. Order/return messages are ASP.NET-live. Guest order writes stay PHP."),
        Row("sf-returns", "storefront", "content/shop/returns", "/shop/returns", "/storefront/returns-app", "aspnet-digest", "aspnet", "Full-qty create-return + return message are ASP.NET-live. Partial-qty split and photos stay PHP."),
        Row("sf-garage", "storefront", "content/shop/docpart garage", "/shop/garage", "/storefront/garage-app", "aspnet-digest", "aspnet", "Save / set-active / delete / notepad / check_car are ASP.NET-live. Search and get_table_cars stay Classic."),
        Row("sf-profile", "storefront", "content/users/profileform.php", "/users/profile", "/storefront/profile-app", "aspnet-digest", "aspnet", "users_profiles UPSERT and password md5(secret_succession) are ASP.NET-live. Email / phone confirm stay PHP."),
        Row("sf-wishlist", "storefront", "modules/shop/bottom_panel/bottom_panel.php", "/shop/zakladki", "/storefront/wishlist-app", "aspnet-digest", "aspnet", "bookmarks cookie add/remove is ASP.NET-live."),
        Row("sf-compare", "storefront", "modules/shop/bottom_panel/bottom_panel.php", "/shop/sravneniya", "/storefront/compare-app", "aspnet-digest", "aspnet", "compare cookie add/remove is ASP.NET-live."),
        Row("sf-bulk-upload", "storefront", "content/shop/bulk_upload", "/shop/bulk-upload", "/storefront/bulk-upload-app", "aspnet-digest", "php", "Excel check/cross/add-selected may be ASP.NET on later branches; history INSERT + /process stay PHP."),
        Row("sf-balance", "storefront", "content/shop/finance/my_balance.php", "/shop/finance/my_balance", "/storefront/account-summary-app", "aspnet-routed", "aspnet", "ajax_create_operation top-up / order-pay is ASP.NET-live on /storefront/payment/create-operation and /storefront/finance/create-operation. Guest checkout cookies are ASP.NET-live."),
        Row("sf-pay-order", "storefront", "content/shop/finance/pay_for_order.php", "/shop/finance/pay_for_order", "/storefront/payment-app", "aspnet-routed", "aspnet", "Demo go_to_pay + notify + pay_for_order are ASP.NET-live. Live acquirer APIs stay unconfigured like PHP demo stubs."),
        Row("sf-umapi", "storefront", "content/umapi_catalog.php", "/umapi_catalog", "/en/umapi_catalog", "aspnet-digest", "php", "UMAPI miss-fill stays PHP."),
        Row("sf-ucats", "storefront", "content/shop/ucats", "/shop/ucats", "/storefront/app", "aspnet-hub", "php", "UCATS product-detail twins are hub-only."),
        Row("sf-workshop-gms", "storefront", "content/shop/workshop/garage_manager_portal.php", "/shop/workshop", "/storefront/garage-manager-app", "aspnet-digest", "php", "GMS board is thin; portal writes stay PHP."),

        // CP shop families
        Row("cp-orders", "cp", "cp/content/shop/orders + order_process", "/CP/shop/orders/orders", "/cp/orders", "aspnet-digest", "aspnet", "OMS item/items status, update_item/update_items (including warehouse reprice + customer-group sell markup), refresh_item_cost, message, courier, delete, comment, viewed, supplier fulfillment stage, and pay-refund are ASP.NET-live. Refund email stays PHP."),
        Row("cp-users", "cp", "cp/content/users", "/CP/users/user_manager", "/cp/users-app", "aspnet-digest", "aspnet", "Staff users.comment, users.unlocked (session delete on lock), create (users + profiles + groups), and password (bcrypt + other-session delete) are ASP.NET-live. Vendor approve stays PHP."),
        Row("cp-lang", "cp", "cp/content/lang", "/CP/lang", "/cp/languages-app", "aspnet-digest", "aspnet", "lang_text_strings flags, description, translation UPSERT, unused-custom delete, and create-string (get_next_str_key) are ASP.NET-live. Restricted-mode and used-found filesystem scan stay PHP."),
        Row("cp-catalogue", "cp", "cp/content/shop/catalogue", "/CP/shop/catalogue/products", "/cp/product-catalogue-app", "aspnet-digest", "aspnet", "shop_catalogue_products min_limit / min_limit_enable UPDATEs, product.php create/edit (text/stickers/related/properties), shop_catalogue_categories_templates create/delete, epc_sku_profiles / spec groups+rows / photo-meta, shop_main_page_groups/products save, special-search save/delete, and catalogue_editor save_tree are ASP.NET-live. Product/SKU/template/category file upload, manual line-list create, and drag-tree UX stay PHP."),
        Row("cp-channels", "cp", "cp/content/shop/channels", "/CP/shop/channels", "/cp/marketplace-channels-app", "aspnet-digest", "aspnet", "toggle_channel UPDATE + sync-log INSERT are ASP.NET-live. Seed / inventory sync / order import stay PHP."),
        Row("cp-carriers", "cp", "cp/content/shop/logistics", "/CP/shop/logistics/carriers", "/cp/carriers-app", "aspnet-digest", "aspnet", "toggle_carrier UPDATE + sync-log INSERT are ASP.NET-live. Seed / create-shipment stay PHP."),
        Row("cp-workshop", "cp", "cp/content/shop/workshop", "/CP/shop/workshop", "/cp/workshop-app", "aspnet-digest", "aspnet", "assign / save_bay / save_tech / set_status / create_job / add_line / create_appointment / convert_appointment are ASP.NET-live. Seed stays PHP."),
        Row("cp-synonyms", "cp", "cp/content/shop/manufacturers_synonyms", "/CP/shop/manufacturers_synonyms", "/cp/synonyms-app", "aspnet-digest", "aspnet", "Manufacturer and synonym add/save/delete are ASP.NET-live. List/get stay digest. PHP manufacturers_synonyms remains the compare twin."),
        Row("cp-crosses", "cp", "cp/content/shop/crosses", "/CP/shop/crosses", "/cp/crosses-app", "aspnet-digest", "aspnet", "save_crosses UPDATE, add_crosses INSERT with brand resolve, del_crosses DELETE, and del_search_crosses filtered DELETE are ASP.NET-live. File import and crossbase stay PHP."),
        Row("cp-prices-upload", "cp", "cp/content/shop/prices_upload", "/CP/shop/prices_upload", "/cp/prices-upload-app", "aspnet-digest", "aspnet", "ajax_6_complete_session last_updated / records_count is ASP.NET-live. CSV import, extract, and sitemap stay PHP."),
        Row("cp-storages", "cp", "cp/content/shop/logistics/groups", "/CP/shop/logistics/storages", "/cp/storages-app", "aspnet-digest", "aspnet", "shop_storages_groups add/delete, shop_storages create/edit, and office membership REPLACE (shop_offices_storages_map) are ASP.NET-live."),
        Row("cp-offices", "cp", "cp/content/shop/logistics/office.php + offices.php + office_geo_nodes.php", "/CP/shop/logistics/offices", "/cp/offices-app", "aspnet-digest", "aspnet", "shop_offices create/edit/delete and office geo REPLACE (shop_offices_geo_map) are ASP.NET-live."),
        Row("cp-delivery-methods", "cp", "cp/content/shop/logistics/obtaining_modes.php + obtaining_mode.php", "/CP/shop/logistics/sposoby-polucheniya", "/cp/delivery-methods-app", "aspnet-digest", "aspnet", "shop_obtaining_modes available UPDATE and caption/order/available/parameters_values save (lang string) are ASP.NET-live."),
        Row("cp-quote-requests", "cp", "cp/content/shop/quote_requests", "/CP/shop/quote-requests", "/cp/quote-requests-app", "aspnet-digest", "aspnet", "admin_note, save_quote line quoting, and send_quote status UPDATE are ASP.NET-live."),
        Row("cp-vendor-approvals", "cp", "cp/content/users/epc_vendor_approvals.php", "/CP/users/vendor_approvals", "/cp/users-app", "aspnet-digest", "aspnet", "Vendor approve (group bind + warehouse/price-list provision) and suspend/reject UPDATEs are ASP.NET-live. Schema-ensure stays PHP."),
        Row("cp-api-clients", "cp", "cp/content/control/portal/epc_api_clients_manage.php", "/CP/control/portal/epc_api_clients_manage", "/cp/api-clients-app", "aspnet-digest", "aspnet", "Super-CP revoke/activate UPDATEs are ASP.NET-live. Create/rotate/key minting stay PHP."),
        Row("cp-price-storage-rules", "cp", "cp/content/shop/pricing/epc_pm_storage_panel.php", "/CP/shop/prices", "/cp/price-lists-app", "aspnet-digest", "aspnet", "Price-storage rule save (ON DUPLICATE KEY) and DELETE are ASP.NET-live."),
        Row("cp-content-pages", "cp", "cp/content/content/content_manager.php + content.php + content_create_edit.php", "/CP/content/content_manager", "/cp/pages-app", "aspnet-digest", "aspnet", "published_flag, main_flag, save_content body, and no-tree create/edit (lang strings + content_access) are ASP.NET-live. TinyMCE image upload and system-content config stay PHP."),
        Row("erp-offices-cash", "erp", "cp/content/shop/logistics/offices_cash.php + offices_cash_editor.php", "/CP/shop/cash", "/erp/cash-accounts-app", "aspnet-digest", "aspnet", "Office cash entry INSERT, cash-code add (custom lang string + shop_offices_cash_codes), and unused code DELETE are ASP.NET-live."),
        Row("erp-wms-locations", "erp", "content/shop/finance/epc_erp_wms.php epc_wms_location_save + epc_wms_location_delete + epc_wms_wave_create + epc_wms_wave_release + epc_wms_receive + epc_wms_work_complete", "/ERP/?epc_erp_shell=1&area=warehouse&tab=wms", "/erp/warehouse-wms-app", "aspnet-digest", "aspnet", "WMS location save INSERT/UPDATE, receive INSERT, location DELETE, work complete, wave create+pick, and wave release are ASP.NET-live. Schema ensure stays PHP."),
        Row("cp-currencies", "cp", "content/shop/finance/nastrojka-kursov-valyut.php", "/CP/shop/finance/nastrojka-kursov-valyut", "/cp/currencies-app", "aspnet-digest", "aspnet", "shop_currencies.rate and available flag UPDATEs are ASP.NET-live. Live FX pull stays PHP."),
        Row("cp-prices-edit", "cp", "cp/content/shop/prices_edit", "/CP/shop/prices_edit", "/cp/prices-edit-app", "aspnet-digest", "aspnet", "shop_docpart_prices_data add/save/del and del_search filtered DELETE are ASP.NET-live. Table list stays PHP."),
        Row("cp-payments", "cp", "cp/content/shop/payments", "/CP/shop/payments", "/cp/payment-gateways-app", "aspnet-digest", "php", "Gateway secrets/config writes stay PHP."),
        Row("cp-fulfillment", "cp", "content/shop/finance/epc_fulfillment_queue.php", "/CP/shop/finance/epc_fulfillment_queue", "/cp/fulfillment-queue-app", "aspnet-digest", "aspnet", "transition / assign / pick_item / pack_item / create_wave / queue-from-order INSERT are ASP.NET-live. Printable packing slip is ASP.NET HTML. Document-control branded PDF templates stay PHP."),
        Row("cp-pos", "cp", "cp/content/shop/pos/ajax_pos.php", "/CP/control/shop/pos", "/cp/pos-overview-app", "aspnet-digest", "aspnet", "open_session / close_session / save_settings and complete_sale sale/line INSERT are ASP.NET-live. Walk-in user create, tax-toolkit totals, ERP SO/invoice/voucher, inventory sale_out, product/customer search, and calc_cart are ASP.NET-live. Printable receipt HTML is ASP.NET-live at /cp/pos/receipt/{id}."),
        Row("cp-collections", "cp", "content/shop/finance/epc_collections_dunning.php", "/CP/shop/finance/epc_collections_dunning", "/cp/collections-dunning-app", "aspnet-digest", "aspnet", "Queue status / payment / profile create / add-invoice / process are ASP.NET-live on /cp/collections-dunning/write. Schema-ensure stays PHP. Case status writes live on the ERP collections twin."),
        Row("cp-credit-limits", "cp", "content/shop/finance/epc_credit_limit.php", "/CP/shop/finance/epc_credit_limit", "/cp/credit-limits-app", "aspnet-digest", "aspnet", "epc_credit_set_limit UPSERT is ASP.NET-live."),
        Row("cp-po-approvals", "cp", "content/shop/finance/epc_po_approval.php", "/CP/shop/finance/epc_po_approval", "/cp/po-approvals-app", "aspnet-digest", "aspnet", "epc_po_approve / epc_po_reject are ASP.NET-live."),
        Row("cp-warranty-rma", "cp", "content/shop/finance/epc_warranty_rma.php + ajax_return_action.php", "/CP/shop/finance/epc_warranty_rma", "/cp/returns-rma-app", "aspnet-digest", "aspnet", "set_return_status / decide_line / finalize_return are ASP.NET-live when statuses already exist. Status seeding stays PHP."),
        Row("cp-vin-requests", "cp", "cp/content/requests/ajax_set_users_vin_viewed.php", "/CP/requests", "/cp/system-requests-app", "aspnet-digest", "aspnet", "users_vin.viewed UPDATE is ASP.NET-live. Live Laximo decode stays PHP."),
        Row("cp-account-ops", "cp", "content/shop/finance/account_operations", "/CP/shop/finance/account_operations", "/cp/credit-limits-app", "aspnet-routed", "php", "Closest twin is credit-limits; account-op ajax stays PHP."),
        Row("cp-payment-systems", "cp", "content/shop/finance/payment_systems", "/CP/shop/finance/payment_systems", "/cp/payment-gateways-app", "aspnet-routed", "php", "Per-gateway go_to_pay stays PHP."),
        Row("cp-tax-toolkit", "cp", "content/shop/finance/epc_tax_toolkit.php", "/CP/shop/finance/epc_tax_toolkit", "/cp/tax-toolkits-app", "aspnet-routed", "php", "Super-CP gated toolkit; tenant tax UI is UAE compliance."),
        Row("cp-einvoice", "cp", "content/shop/finance/epc_einvoice.php", "/CP/shop/finance/epc_einvoice", "/cp/einvoice-documents-app", "aspnet-digest", "aspnet", "Seller / buyer / ASP profile UPSERTs are ASP.NET-live. Create, submit, credit-note, and ASP poll stay PHP."),
        Row("cp-live-fx", "cp", "content/shop/finance/epc_currency_live_rates.php", "/CP/shop/finance/epc_currency_live_rates", "/cp/currencies-app", "aspnet-routed", "php", "Live FX pull stays PHP."),
        Row("cp-custom-ship", "cp", "content/shop/finance/epc_custom_shipping.php", "/CP/shop/finance/epc_custom_shipping", "/cp/carriers-app", "aspnet-digest", "aspnet", "epc_cs_save_declaration / epc_cs_submit_declaration core SQL are ASP.NET-live on /cp/custom-shipping/write. PDF attach, box autofill, LGP, and schema-ensure stay PHP."),
        Row("cp-finance-hub", "cp", "cp/content/shop/finance", "/CP/shop/finance", "/erp", "aspnet-hub", "php", "Module hub is intentional. Specific epc_* hrefs must map before this catch-all."),

        // ERP standalone finance pages closed in this wave
        Row("erp-order-pipeline", "erp", "content/shop/finance/epc_order_erp_pipeline.php", "/CP/shop/finance/epc_order_erp_pipeline", "/erp/order-pipeline-app", "aspnet-digest", "php", "Read-only epc_order_erp_log. Pipeline run stays PHP."),
        Row("erp-inventory-forecast", "erp", "content/shop/finance/epc_inventory_forecast.php", "/CP/shop/finance/epc_inventory_forecast", "/erp/inventory-forecast-app", "aspnet-digest", "aspnet", "epc_forecast_compute UPSERT is ASP.NET-live. Demand-history ingest stays PHP."),
        Row("erp-multi-entity", "erp", "content/shop/finance/epc_multi_entity.php", "/CP/shop/finance/epc_multi_entity", "/erp/multi-entity-app", "aspnet-digest", "aspnet", "epc_entity_create_group / add_member / record_intercompany / eliminate are ASP.NET-live. Schema ensure and consolidated TB stay PHP. ajax_erp multi_entity_save stays dry-run."),
        Row("erp-multi-currency-gl", "erp", "content/shop/finance/epc_multi_currency_gl.php", "/CP/shop/finance/epc_multi_currency_gl", "/erp/multi-currency-gl-app", "aspnet-digest", "aspnet", "epc_mcgl_set_rate UPSERT is ASP.NET-live. Revaluation, journal entry, and seed stay PHP."),
        Row("erp-marketing", "erp", "content/shop/finance/epc_erp_staff.php epc_erp_marketing_create", "/ERP/?epc_erp_shell=1&area=sales&tab=marketing", "/erp/marketing-app", "aspnet-digest", "aspnet", "epc_erp_marketing_create INSERT is ASP.NET-live. Staff schema ensure and sample seed stay PHP."),
        Row("erp-payroll", "erp", "content/shop/finance/epc_wps_payroll.php + erp_tabs_payroll.php", "/CP/shop/finance/epc_wps_payroll", "/erp/payroll-app", "aspnet-digest", "aspnet", "payroll_approve and hr_update_days are ASP.NET-live. Generate/pay and payroll line recalc stay PHP."),
        Row("erp-subscriptions", "erp", "content/shop/finance/epc_erp_subscriptions.php epc_sub_save + epc_sub_set_status + sub_invoice_paid", "/ERP/?epc_erp_shell=1&area=sales&tab=subscriptions", "/erp/sales-orders-app?tab=subscriptions", "aspnet-digest", "aspnet", "Subscription save INSERT/UPDATE, status UPDATE, and invoice-paid UPDATE are ASP.NET-live. Cycle generate and schema ensure stay PHP."),
        Row("erp-contracts", "erp", "content/shop/finance/epc_erp_contracts.php epc_ctr_save + epc_ctr_set_status + epc_ctr_ocr_store", "/ERP/?epc_erp_shell=1&area=sales&tab=contracts", "/erp/contracts-app", "aspnet-digest", "aspnet", "Contract save INSERT/UPDATE, status UPDATE, and OCR text UPDATE are ASP.NET-live. Sign and schema ensure stay PHP."),
        Row("erp-workflow", "erp", "content/shop/finance/epc_erp_staff.php epc_erp_workflow_update_status + epc_erp_workflow_create", "/ERP/?epc_erp_shell=1&area=overview&tab=workflow", "/erp/workflow-app", "aspnet-digest", "aspnet", "Workflow task create INSERT and status UPDATE are ASP.NET-live. Staff schema ensure and sample seed stay PHP."),
        Row("erp-collections-cases", "erp", "content/shop/finance/epc_erp_collections.php epc_coll_case_save epc_coll_case_set_status epc_coll_case_promise epc_coll_activity_log epc_coll_hold_set epc_coll_dunning_run", "/ERP/?epc_erp_shell=1&area=finance&tab=collections", "/erp/collections-dunning-app", "aspnet-digest", "aspnet", "Collections case save, status, promise, activity log, credit hold, and dunning run are ASP.NET-live. Schema ensure stays PHP."),
        Row("erp-procurement-reqs", "erp", "content/shop/finance/epc_erp_procurement.php epc_proc_req_save/add_line/submit/decision/convert", "/ERP/?epc_erp_shell=1&area=purchasing&tab=purchase_requisitions", "/erp/purchase-requests-app", "aspnet-digest", "aspnet", "Requisition save, add-line, submit, approve/reject, and convert-to-PO are ASP.NET-live. Schema ensure stays PHP."),
        Row("erp-ins-claims", "erp", "content/shop/finance/epc_erp_insurance.php epc_ins_claim_save + epc_ins_claim_set_status + epc_ins_doc_delete", "/ERP/?epc_erp_shell=1&area=risk&tab=insurance", "/erp/insurance-compliance-app", "aspnet-digest", "aspnet", "Insurance claim save, claim status UPDATE, and document DELETE are ASP.NET-live. Policy save/delete, doc add, and schema ensure stay PHP."),
        Row("erp-hr-leave-expense", "erp", "content/shop/finance/epc_erp_hr.php epc_hr_employee_save + epc_hr_attendance_log + epc_hr_leave_request + epc_hr_expense_save + epc_hr_leave_set_status + epc_hr_expense_set_status", "/ERP/?epc_erp_shell=1&area=people&tab=hr", "/erp/hr-overview-app", "aspnet-digest", "aspnet", "HR employee save, attendance upsert, leave request INSERT, expense save INSERT, and leave/expense status UPDATEs are ASP.NET-live. Payroll generate and schema ensure stay PHP."),
        Row("erp-cons-deletes", "erp", "content/shop/finance/epc_erp_consolidation.php epc_cons_entity_save + epc_cons_entity_delete + epc_cons_ic_delete", "/ERP/?epc_erp_shell=1&area=consolidations&tab=consolidation_bu", "/erp/consolidations-app", "aspnet-digest", "aspnet", "Consolidation entity save, entity DELETE, and IC DELETE are ASP.NET-live. Figures, IC save, and schema ensure stay PHP."),
        Row("erp-vat-refund-status", "erp", "content/shop/finance/epc_bos_vat_refund.php epc_bos_vat_refund_set_status + epc_bos_vat_refund_save", "/ERP/?epc_erp_shell=1&area=tax&tab=vat_refund", "/erp/vat-app?tab=vat_refund", "aspnet-digest", "aspnet", "Refund save INSERT/UPDATE and status UPDATE are ASP.NET-live. Schema ensure stays PHP."),
        Row("erp-pf-case-cancel", "erp", "content/shop/finance/epc_erp_processflow.php epc_pf_case_cancel + epc_pf_step_delete", "/ERP/?epc_erp_shell=1&area=overview&tab=processflow", "/erp/process-flow-tasks-app", "aspnet-digest", "aspnet", "Process-flow case cancel UPDATE and step DELETE are ASP.NET-live. Start, act, reassign, and seed stay PHP."),
        Row("erp-bos-wf-disable-rule", "erp", "content/shop/finance/epc_bos_workflow.php epc_bos_wf_disable_rule", "/ERP/?epc_erp_shell=1&area=overview&tab=approvals", "/erp/approvals-app", "aspnet-digest", "aspnet", "BOS approval-rule disable UPDATE is ASP.NET-live. Save, decide, and raise stay PHP."),
        Row("erp-bos-compliance-disable", "erp", "content/shop/finance/epc_bos_compliance.php epc_bos_compliance_disable_obligation", "/ERP/?epc_erp_shell=1&area=tax&tab=compliance", "/erp/soc2-compliance-app", "aspnet-digest", "aspnet", "BOS compliance obligation disable UPDATE is ASP.NET-live. Add, file, and retention stay PHP."),
        Row("erp-fy-reopen-period", "erp", "content/shop/finance/epc_erp_closing.php epc_fy_reopen_year + epc_fy_set_period_status", "/ERP/?epc_erp_shell=1&area=finance&tab=year_end", "/erp/period-close-app", "aspnet-digest", "aspnet", "Fiscal-year reopen and period status UPDATEs are ASP.NET-live. Year create, year-end close, and schema ensure stay PHP."),
        Row("erp-fin-period-status", "erp", "content/shop/finance/epc_erp_fin_advanced.php epc_fin_period_set_status", "/ERP/?epc_erp_shell=1&area=cost_acct&tab=fin_advanced", "/cp/fin-advanced-app", "aspnet-digest", "aspnet", "Advanced finance period status UPDATE is ASP.NET-live. Period generate, FX/alloc/accrual writes, and schema ensure stay PHP."),
        Row("erp-wht-settle", "erp", "content/shop/finance/epc_erp_withholding.php epc_wht_settle epc_wht_code_save epc_wht_record epc_wht_certificate_issue", "/ERP/?epc_erp_shell=1&area=tax&tab=withholding", "/erp/withholding-app", "aspnet-digest", "aspnet", "Withholding settle, code save, record, and certificate issue are ASP.NET-live. Schema ensure stays PHP."),
        Row("erp-insights", "erp", "content/shop/finance/epc_insights_suite.php", "/CP/shop/finance/epc_insights_suite", "/erp/dashboard-summary-app", "aspnet-routed", "php", "Insights suite routed to ERP dashboard digest."),
        Row("erp-sales-orders", "erp", "cp/content/shop/finance/erp/erp_tabs_sales_orders.php", "/ERP/?epc_erp_shell=1&area=sales&tab=sales_orders", "/erp/sales-orders-app", "aspnet-digest", "php", "SO list digest; create/post stay PHP ajax_erp."),
        Row("erp-purchase-orders", "erp", "erp_tabs_purchase_orders.php", "/ERP/?epc_erp_shell=1&area=purchasing&tab=purchase_orders", "/erp/purchase-orders-app", "aspnet-digest", "php", "PO list digest; writes PHP."),
        Row("erp-invoices", "erp", "erp_tabs_invoices.php", "/ERP/?epc_erp_shell=1&area=sales&tab=invoices", "/erp/invoices-app", "aspnet-digest", "php", "Invoice list digest; writes PHP."),
        Row("erp-gl", "erp", "erp_tabs_accounting.php", "/ERP/?epc_erp_shell=1&area=finance&tab=gl", "/erp/gl-journals-app", "aspnet-digest", "php", "GL list digest; posting stay PHP (some live write services exist behind dry-run gates)."),
        Row("erp-inventory", "erp", "erp_tabs_inventory.php", "/ERP/?epc_erp_shell=1&area=inventory&tab=inventory", "/erp/inventory-stock-app", "aspnet-digest", "aspnet", "Reorder, movement, transfer, warehouse/item create, storage sync, closing snapshot, csv_text import, and dimension-link save are ASP.NET-live. CSV file-byte upload stays PHP."),
        Row("erp-workspace-favorites", "erp", "ajax_erp.php erp_fav_add/remove", "/ERP/?epc_erp_shell=1&area=overview&tab=favorites", "/erp/workspace-favorites-app", "aspnet-digest", "aspnet", "Favourites add/remove and shortcut add/reorder/delete/delete-key/reset are ASP.NET-live. Schema-ensure stays PHP."),
        Row("erp-jw-repairs", "erp", "ajax_erp.php jw_repair_create/update_status", "/ERP/?epc_erp_shell=1&area=service_mgmt&tab=jw_repairs", "/erp/jewellery-repairs-app", "aspnet-digest", "aspnet", "epc_erp_jw_repairs INSERT and status UPDATE are ASP.NET-live. Sample seed stays PHP."),
        Row("erp-tabs-residual", "erp", "cp/content/shop/finance/erp/erp_tabs_*.php (~160)", "/ERP/?epc_erp_shell=1", "/erp", "aspnet-hub", "php", "Bare ERP shell lands on /erp. ErpPhpTabRouteMap covers named tabs; residual shells use /erp/module-app. Customer master, customer/order settlement, aftersales RMA create/resolve, warranty register, service-job create, job line add, job close, jewellery repair create, karat save, rate-type save, currency save, diamond save, design save, pearl save, color-stone save, barcode generate, tag create/sell, gold-scheme create/enroll/pay, fix/unfix create/settle, barcode-purchase create/sell, SLA create, ticket create/reply, customer-group create/assign, report-scheduler create, virtual-warehouse create/transfer, metal-stock save, fixing save, voucher save, petty-cash save, tourist-VAT save, tourist-refund create/validate, RFID register/start-session/scan, gold-rate set, AML KYC save, AML alert status, repair-receipt save, repair-transfer save, workshop-receive save, repair-delivery save, stock-verification save, repair-sale voucher, POS-advance voucher, and journal voucher are ASP.NET-live."),

        // BOS
        Row("bos-shell", "bos", "bos/index.php + content/shop/finance/epc_bos_*.php", "/BOS/", "/bos/app", "aspnet-hub", "php", "BOS session model stays PHP. Fleet digests are read-only."),

        // Write families that block PHP removal
        Row("write-cp-ajax", "writes", "cp/content/**/ajax_*.php (~430 catalogued)", "CP module ajax", "dry-run /cp/ajax/*", "php-writes", "php", "CpModuleAjaxWriteCatalog: writes=0, phpAuthoritative=true."),
        Row("write-erp-ajax", "writes", "cp/content/shop/finance/erp/ajax_erp.php", "ajax_erp.php", "dry-run /erp/ajax/*", "php-writes", "php", "ErpAjaxWriteCatalog: writes=0. Live GL/SO helpers stay gated."),
        Row("write-storefront-cart", "writes", "content/shop/order_process/ajax_*.php", "cart/checkout ajax", "/storefront/cart/add", "php-writes", "php", "Type-2 add/qty/delete/check and signed-in + guest checkout/create are ASP.NET-live. Demo pay create/notify are ASP.NET-live. Staff email stays PHP."),
        Row("write-payments", "writes", "content/shop/finance/payment_systems/*/go_to_pay.php", "payment notify", "/storefront/payment/notify", "php-writes", "aspnet", "Demo notify + pay_for_order are ASP.NET-live. Card-capture acquirer APIs stay unconfigured like PHP demo stubs."),
        Row("write-laximo", "writes", "content/laximo/com_guayaquil", "Laximo VIN decode", "/storefront/vin/decode", "php-writes", "aspnet", "FindVehicleByVIN SOAP is ASP.NET-live. Catalog tree click-through stays PHP."),
        Row("write-pyprices", "writes", "cp/content/shop/prices_upload + pyprices", "price ingest cron", "/cp/prices-upload-app", "php-writes", "php", "Price file ingest + cron stay PHP/Python."),
        Row("chrome-templates", "chrome", "templates/*/*.php (6 files)", "theme desktop.php", "Php*DesktopChrome", "aspnet-digest", "none", "Theme CSS is reused; PHP desktop.php is compare-only."),
    ];

    public static IReadOnlyDictionary<string, object> BuildReport()
    {
        var rows = Rows.Select(r =>
        {
            var mapped = string.IsNullOrWhiteSpace(r.PhpHref) || !r.PhpHref.StartsWith('/')
                ? r.AspNetRoute
                : PhpSurfaceLinkMap.AspNetPrimaryHref(r.PhpHref);
            var mapsToExpected = string.Equals(mapped, r.AspNetRoute, StringComparison.OrdinalIgnoreCase)
                || (r.AspNetRoute.Contains('?', StringComparison.Ordinal)
                    && mapped.StartsWith(r.AspNetRoute.Split('?')[0], StringComparison.OrdinalIgnoreCase));
            return new Dictionary<string, object?>
            {
                ["id"] = r.Id,
                ["surface"] = r.Surface,
                ["phpSource"] = r.PhpSource,
                ["phpHref"] = r.PhpHref,
                ["aspNetRoute"] = r.AspNetRoute,
                ["mappedHref"] = mapped,
                ["mapsToExpected"] = mapsToExpected,
                ["status"] = r.Status,
                ["writesOwner"] = r.WritesOwner,
                ["note"] = r.Note
            };
        }).ToList();

        var byStatus = Rows.GroupBy(r => r.Status, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var bySurface = Rows.GroupBy(r => r.Surface, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var stillMissingDedicatedApp = Rows
            .Where(r => r.Status is "missing-app" or "aspnet-hub")
            .Select(r => r.Id)
            .ToArray();
        var phpWrites = Rows.Where(r => r.WritesOwner == "php").Select(r => r.Id).ToArray();

        return new Dictionary<string, object>
        {
            ["role"] = "php-vs-aspnet-removal-matrix",
            ["asOfUtc"] = AsOfUtc,
            ["readyForPhpRemoval"] = ReadyForPhpRemoval,
            ["phpSourceDeletionAllowed"] = PhpSourceDeletionAllowed,
            ["cutoverAllowed"] = CutoverAllowed,
            ["aspNetInteractiveCompleteCount"] = AspNetInteractiveCompleteCount,
            ["keepPhpProjectAvailable"] = true,
            ["phpFileInventory"] = new Dictionary<string, object>
            {
                ["content"] = 1437,
                ["cp"] = 638,
                ["templates"] = 6,
                ["bos"] = 2,
                ["productFacingApprox"] = 2083,
                ["note"] = "Exclude vendor/node_modules/pyprices venv and repo-root setup scripts. Laximo SDK dominates content/laximo."
            },
            ["counts"] = new Dictionary<string, object>
            {
                ["rows"] = Rows.Count,
                ["byStatus"] = byStatus,
                ["bySurface"] = bySurface,
                ["phpWriteRows"] = phpWrites.Length,
                ["hubOrMissing"] = stillMissingDedicatedApp.Length
            },
            ["stillBlockingPhpRemoval"] = new[]
            {
                "Remaining PHP writes: residual CP/ERP ajax catalog, live acquirer APIs, PyPrices ingest, UMAPI miss-fill. VIN request ledger is ASP.NET-live (captcha/files/email stay Classic). Guest checkout cookies are ASP.NET-live.",
                "aspnet-complete interactive module count is 0 (no dual-sample deletion gate).",
                "Digests + href remaps are not a deletion gate.",
                "Human MODULE_FUNCTION_TEST_PASS + dual-sample evidence required per family."
            },
            ["closedThisWave"] = new[]
            {
                "Type-2 cart qty/delete/check + cart add → ASP.NET live writes",
                "OMS set_item_status → /cp/orders/set-item-status live",
                "Payroll approve → /erp/ajax/payroll-approve live",
                "Credit limit set → /cp/credit-limits/set live",
                "PO approve/reject → /cp/po-approvals/approve|reject live",
                "Inventory forecast recompute → /erp/inventory-forecast/recompute live",
                "shop/finance/epc_credit_limit → /cp/credit-limits-app",
                "shop/finance/epc_po_approval → /cp/po-approvals-app",
                "shop/finance/epc_warranty_rma → /cp/returns-rma-app",
                "shop/finance/epc_wps_payroll → /erp/payroll-app",
                "shop/finance/epc_subscription_billing → /erp/sales-orders-app?tab=subscriptions",
                "shop/finance/epc_order_erp_pipeline → /erp/order-pipeline-app (new digest)",
                "shop/finance/epc_inventory_forecast → /erp/inventory-forecast-app (new digest)",
                "shop/finance/epc_multi_entity → /erp/multi-entity-app (new digest)",
                "shop/finance/epc_multi_currency_gl → /erp/multi-currency-gl-app (new digest)",
                "/CP/shop/finance hub still maps to /erp (intentional)",
                "Fulfilment queue transition / assign / pick / pack / wave → /cp/fulfillment-queue/write live",
                "Dunning queue status / payment → /cp/collections-dunning/write live",
                "Marketing campaign create → /erp/marketing/create live",
                "Multi-entity group/member/IC/eliminate → /erp/multi-entity/write live",
                "FX rate set → /erp/multi-currency-gl/set-rate live"
            },
            ["rows"] = rows,
            ["endpoint"] = "/migration/php-vs-aspnet-matrix",
            ["note"] = "Do not delete PHP. Reference keep ≠ removal. This matrix is the browse/digest gap board, not a cutover switch."
        };
    }

    private static PhpVsAspNetMatrixRow Row(
        string id,
        string surface,
        string phpSource,
        string phpHref,
        string aspNetRoute,
        string status,
        string writesOwner,
        string note)
        => new(id, surface, phpSource, phpHref, aspNetRoute, status, writesOwner, note);
}

public sealed record PhpVsAspNetMatrixRow(
    string Id,
    string Surface,
    string PhpSource,
    string PhpHref,
    string AspNetRoute,
    string Status,
    string WritesOwner,
    string Note);
