using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpRecordOpenPhpParityTests
{
    [Theory]
    [InlineData("/ERP/?epc_erp_shell=1&area=sales&tab=sales_orders&order_id=42", "/erp/sales-orders-app?order_id=42")]
    [InlineData("/ERP/?epc_erp_shell=1&area=purchasing&tab=purchase_orders&order_id=9", "/erp/purchase-orders-app?order_id=9")]
    [InlineData("/ERP/?epc_erp_shell=1&area=sales&tab=invoices&inv_id=17", "/erp/invoices-app?inv_id=17")]
    [InlineData("/ERP/?epc_erp_shell=1&area=finance&tab=gl&journal_id=3", "/erp/gl-journals-app?journal_id=3")]
    [InlineData("/ERP/?epc_erp_shell=1&area=banking&tab=cash_bank&account_id=5", "/erp/cash-accounts-app?account_id=5")]
    [InlineData("/ERP/?epc_erp_shell=1&area=banking&tab=bank_recon&recon_line_id=21", "/erp/bank-reconciliation-app?recon_line_id=21")]
    [InlineData("/ERP/?epc_erp_shell=1&area=banking&tab=reconciliation&recon_line_id=21", "/erp/bank-reconciliation-app?recon_line_id=21")]
    [InlineData("/CP/shop/finance/erp?area=banking&tab=bank_recon&epc_erp_shell=1&recon_line_id=21", "/erp/bank-reconciliation-app?recon_line_id=21")]
    [InlineData("/ERP/?epc_erp_shell=1&area=finance&tab=coa&account_id=19", "/erp/coa-accounts-app?account_id=19")]
    [InlineData("/ERP/?epc_erp_shell=1&area=finance&tab=chart_of_accounts&account_id=19", "/erp/coa-accounts-app?account_id=19")]
    [InlineData("/CP/shop/finance/erp?area=finance&tab=coa&epc_erp_shell=1&account_id=19", "/erp/coa-accounts-app?account_id=19")]
    [InlineData("/ERP/?epc_erp_shell=1&area=overview&tab=favorites&favorite_id=20", "/erp/workspace-favorites-app?favorite_id=20")]
    [InlineData("/ERP/?epc_erp_shell=1&area=overview&tab=shortcut_icons&favorite_id=20", "/erp/workspace-favorites-app?favorite_id=20")]
    [InlineData("/CP/shop/finance/erp?area=overview&tab=favorites&epc_erp_shell=1&favorite_id=20", "/erp/workspace-favorites-app?favorite_id=20")]
    [InlineData("/ERP/?epc_erp_shell=1&area=purchasing&tab=vendors&supplier_id=8", "/erp/suppliers-app?supplier_id=8")]
    [InlineData("/ERP/?epc_erp_shell=1&area=overview&tab=processflow&pf_case=11", "/erp/process-flow-tasks-app?pf_case=11")]
    [InlineData("/ERP/?epc_erp_shell=1&area=purchasing&tab=purchase_requisitions&rq=4", "/erp/purchase-requests-app?rq=4")]
    [InlineData("/ERP/?epc_erp_shell=1&area=purchasing&tab=purchase_requisitions&req_id=4", "/erp/purchase-requests-app?req_id=4")]
    [InlineData("/CP/shop/finance/epc_collections_dunning?queue_id=12", "/cp/collections-dunning-app?queue_id=12")]
    [InlineData("/ERP/?epc_erp_shell=1&area=credit_coll&queue_id=12", "/erp/collections-dunning-app?queue_id=12")]
    [InlineData("/CP/control/portal/epc_api_clients_manage?api_client_id=7", "/cp/api-clients-app?api_client_id=7")]
    [InlineData("/ERP/?epc_erp_shell=1&area=cost_mgmt&tab=cost_models&costm_id=3", "/erp/cost-models-app?costm_id=3")]
    [InlineData("/ERP/?epc_erp_shell=1&area=tax&tab=elec_reporting&format_id=6", "/erp/electronic-reporting-app?format_id=6")]
    [InlineData("/CP/shop/finance/epc_einvoice?ei_id=8", "/cp/einvoice-documents-app?ei_id=8")]
    [InlineData("/ERP/?epc_erp_shell=1&area=tax&tab=einvoice&ei_id=8", "/erp/einvoice-documents-app?ei_id=8")]
    [InlineData("/ERP/?epc_erp_shell=1&area=projects&tab=projects&project_id=4", "/erp/projects-overview-app?project_id=4")]
    [InlineData("/ERP/?epc_erp_shell=1&area=production&tab=manufacturing&wo_id=7", "/erp/production-overview-app?wo_id=7")]
    [InlineData("/CP/shop/accessories?listing_id=9", "/cp/accessories-app?listing_id=9")]
    [InlineData("/CP/shop/crosses?cross_id=14", "/cp/crosses-app?cross_id=14")]
    [InlineData("/CP/shop/manufacturers_synonyms?manufacturer_id=5", "/cp/synonyms-app?manufacturer_id=5")]
    [InlineData("/CP/control/portal/epc_promotions_engine?promo_id=6", "/cp/promotions-app?promo_id=6")]
    [InlineData("/CP/control/portal/epc_visual_page_editor?layout_id=8", "/cp/page-builder-app?layout_id=8")]
    [InlineData("/CP/control/portal/epc_tax_toolkit_manage?toolkit_id=3", "/cp/tax-toolkits-app?toolkit_id=3")]
    [InlineData("/CP/control/portal/epc_event_bus?event_id=3", "/cp/event-bus-app?event_id=3")]
    [InlineData("/ERP/?epc_erp_shell=1&area=cost_acct&tab=fin_advanced&period_id=5", "/erp/fin-advanced-app?period_id=5")]
    [InlineData("/ERP/?epc_erp_shell=1&area=tax&tab=blockchain_proofs&proof_id=4", "/erp/blockchain-proofs-app?proof_id=4")]
    [InlineData("/ERP/?epc_erp_shell=1&area=audit_wb&tab=blockchain_proofs&proof_id=4", "/erp/blockchain-proofs-app?proof_id=4")]
    [InlineData("/CP/control/version_control?item_id=4", "/cp/ops-guides-app?item_id=4")]
    [InlineData("/CP/control/portal/epc_marketplace?app_id=7", "/cp/marketplace-apps-app?app_id=7")]
    [InlineData("/CP/control/portal/epc_nl_reporting?report_id=8", "/cp/nl-reporting-app?report_id=8")]
    [InlineData("/CP/control/portal/epc_power_bi?pbi_id=5", "/cp/power-bi-app?pbi_id=5")]
    [InlineData("/CP/general_pages/epc_metabase_embed?mb_id=6", "/cp/metabase-app?mb_id=6")]
    [InlineData("/CP/control/portal/epc_bi_metrics?mb_id=6", "/cp/metabase-app?mb_id=6")]
    [InlineData("/CP/shop/marketing/marketing?review_id=9", "/cp/marketing-growth-app?review_id=9")]
    [InlineData("/CP/control/portal/epc_commerce_isolation_audit?run_id=3", "/cp/isolation-audit-app?run_id=3")]
    [InlineData("/CP/control/portal/industry_settings?pack_id=2", "/cp/industry-packs-app?pack_id=2")]
    [InlineData("/CP/control/portal/epc_industry_packs?pack_id=2", "/cp/industry-packs-app?pack_id=2")]
    [InlineData("/CP/control/portal/epc_config_sandbox?snapshot_id=4", "/cp/config-sandbox-app?snapshot_id=4")]
    [InlineData("/CP/control/portal/epc_platform_governance?rule_id=5", "/cp/platform-governance-app?rule_id=5")]
    [InlineData("/CP/control/portal/epc_super_cp_communication?task_id=6", "/cp/platform-communication-app?task_id=6")]
    [InlineData("/CP/general_pages/epc_ai_service?query_id=8", "/cp/ai-service-app?query_id=8")]
    [InlineData("/CP/control/portal/epc_ai_copilot?query_id=8", "/cp/ai-service-app?query_id=8")]
    [InlineData("/CP/control/portal/epc_free_tools?account_id=4", "/cp/free-tools-app?account_id=4")]
    [InlineData("/CP/control/portal/epc_free_tools_admin?account_id=4", "/cp/free-tools-app?account_id=4")]
    [InlineData("/CP/control/portal/epc_super_cp_customer_board?user_id=9", "/cp/customer-board-app?user_id=9")]
    [InlineData("/CP/control/portal/epc_super_cp_info_blocks?block_id=3", "/cp/info-blocks-app?block_id=3")]
    [InlineData("/CP/control/notifications_settings?notif_id=7", "/cp/notifications-app?notif_id=7")]
    [InlineData("/CP/control/portal/epc_notifications?notif_id=7", "/cp/notifications-app?notif_id=7")]
    [InlineData("/CP/control/portal/epc_social_media_hub?social_id=5", "/cp/social-hub-app?social_id=5")]
    [InlineData("/ERP/?epc_erp_shell=1&area=sales&tab=crm&ticket_id=8", "/erp/crm-tickets-app?ticket_id=8")]
    [InlineData("/CP/content/dopolnitelnye-teksty?text_id=4", "/cp/additional-texts-app?text_id=4")]
    [InlineData("/CP/requests?vin_id=6", "/cp/system-requests-app?vin_id=6")]
    [InlineData("/ERP/?epc_erp_shell=1&area=consolidations&tab=consolidation_bu&cons_id=3", "/erp/consolidations-app?cons_id=3")]
    [InlineData("/CP/shop/finance/epc_po_approval?po_req_id=4", "/cp/po-approvals-app?po_req_id=4")]
    [InlineData("/ERP/?epc_erp_shell=1&area=budgeting&tab=budgeting&budget_id=5", "/erp/budgets-app?budget_id=5")]
    [InlineData("/ERP/?epc_erp_shell=1&area=warehouse&tab=wms&work_id=7", "/erp/warehouse-wms-app?work_id=7")]
    [InlineData("/CP/shop/taby-poiska?tab_id=3", "/cp/search-tabs-app?tab_id=3")]
    [InlineData("/CP/shop/taby-poiska/tab-poiska?tab_id=3", "/cp/search-tabs-app?tab_id=3")]
    [InlineData("/CP/shop/logistics/storages/storage?id=5", "/cp/storages-app?storage_id=5")]
    [InlineData("/CP/shop/logistics/storages?storage_id=5", "/cp/storages-app?storage_id=5")]
    [InlineData("/CP/shop/filter?filter_id=4", "/cp/product-filters-app?filter_id=4")]
    [InlineData("/CP/shop/filter/setting?id=4", "/cp/product-filters-app?filter_id=4")]
    [InlineData("/CP/shop/geo/nodes?geo_id=4", "/cp/geo-regions-app?geo_id=4")]
    [InlineData("/CP/shop/geo?geo_id=4", "/cp/geo-regions-app?geo_id=4")]
    [InlineData("/CP/shop/logistics/sposoby-polucheniya?obtaining_mode_id=4", "/cp/delivery-methods-app?obtaining_mode_id=4")]
    [InlineData("/CP/shop/logistics/sposoby-polucheniya/sposob-polucheniya?obtaining_mode_id=4", "/cp/delivery-methods-app?obtaining_mode_id=4")]
    [InlineData("/CP/content/content_manager/content?content_id=12", "/cp/pages-app?content_id=12")]
    [InlineData("/CP/content/edit_content?content_id=12", "/cp/pages-app?content_id=12")]
    [InlineData("/CP/menu/menu_edit?menu_id=3", "/cp/menus-app?menu_id=3")]
    [InlineData("/CP/menu/menu_manager?menu_id=3", "/cp/menus-app?menu_id=3")]
    [InlineData("/CP/shop/catalogue/catalogue_editor?product_id=8", "/cp/product-catalogue-app?product_id=8")]
    [InlineData("/CP/shop/catalogue/products?product_id=8", "/cp/product-catalogue-app?product_id=8")]
    [InlineData("/CP/shop/logistics/offices/office?office_id=2", "/cp/offices-app?office_id=2")]
    [InlineData("/CP/shop/logistics/offices?office_id=2", "/cp/offices-app?office_id=2")]
    [InlineData("/CP/modules/module?module_id=4", "/cp/modules-app?module_id=4")]
    [InlineData("/CP/modules/modules_manager?module_id=4", "/cp/modules-app?module_id=4")]
    [InlineData("/CP/shop/finance/erp/uae-tax-compliance?epc_erp_shell=1&leg_id=4", "/cp/uae-tax-compliance-app?leg_id=4")]
    [InlineData("/ERP/?epc_erp_shell=1&area=tax&tab=tax_compliance&leg_id=4", "/erp/uae-tax-compliance-app?leg_id=4")]
    [InlineData("/CP/control/portal/epc_auto_price_engine?aprice_id=6", "/cp/auto-price-app?aprice_id=6")]
    [InlineData("/CP/users/usergroups?ugroup_id=3", "/cp/groups-app?ugroup_id=3")]
    [InlineData("/ERP/?epc_erp_shell=1&area=jewellery&tab=jw_karat&karat_id=3", "/erp/jewellery-masters-app?tab=jw_karat&karat_id=3")]
    [InlineData("/ERP/?epc_erp_shell=1&area=inventory_mgmt&tab=jw_stock_verification&verify_id=4", "/erp/jewellery-stock-verification-app?tab=jw_stock_verification&verify_id=4")]
    [InlineData("/CP/shop/finance/erp?area=inventory_mgmt&tab=jw_stock_verification&epc_erp_shell=1&verify_id=4", "/erp/jewellery-stock-verification-app?tab=jw_stock_verification&verify_id=4")]
    [InlineData("/ERP/?epc_erp_shell=1&area=service_mgmt&tab=jw_repairs&repair_id=5", "/erp/jewellery-repairs-app?tab=jw_repairs&repair_id=5")]
    [InlineData("/CP/shop/finance/erp?area=service_mgmt&tab=jw_repairs&epc_erp_shell=1&repair_id=5", "/erp/jewellery-repairs-app?tab=jw_repairs&repair_id=5")]
    [InlineData("/ERP/?epc_erp_shell=1&area=sales&tab=jw_retail_sales&voc_id=6", "/erp/jewellery-retail-app?tab=jw_retail_sales&voc_id=6")]
    [InlineData("/CP/shop/finance/erp?area=sales&tab=jw_retail_sales&epc_erp_shell=1&voc_id=6", "/erp/jewellery-retail-app?tab=jw_retail_sales&voc_id=6")]
    [InlineData("/ERP/?epc_erp_shell=1&area=setup&tab=workflow_automation&workflow_id=3", "/erp/workflows-app?workflow_id=3")]
    [InlineData("/CP/control/portal/epc_workflow_builder?workflow_id=3", "/cp/workflows-app?workflow_id=3")]
    [InlineData("/CP/templates/templates_manager?tpl_id=4", "/cp/templates-manager-app?tpl_id=4")]
    [InlineData("/CP/templates_control?tpl_id=4", "/cp/templates-manager-app?tpl_id=4")]
    [InlineData("/CP/plugins/plugins_manager?plugin_id=5", "/cp/plugins-manager-app?plugin_id=5")]
    [InlineData("/CP/plugins_control?plugin_id=5", "/cp/plugins-manager-app?plugin_id=5")]
    [InlineData("/CP/content/sitemap?sm_id=4", "/cp/sitemap-app?sm_id=4")]
    [InlineData("/CP/shop/pricing?plist_id=4", "/cp/price-lists-app?plist_id=4")]
    [InlineData("/CP/shop/bulk_upload?upload_id=6", "/cp/bulk-upload-app?upload_id=6")]
    [InlineData("/ERP/?epc_erp_shell=1&area=fixed_assets&tab=fixed_assets&asset_id=4", "/erp/fixed-assets-app?asset_id=4")]
    [InlineData("/CP/shop/finance/erp?area=fixed_assets&tab=fixed_assets&epc_erp_shell=1&asset_id=4", "/erp/fixed-assets-app?asset_id=4")]
    [InlineData("/ERP/?epc_erp_shell=1&area=inventory_mgmt&tab=inv_groups&transfer_id=5", "/erp/stock-transfers-app?transfer_id=5")]
    [InlineData("/CP/shop/finance/erp?area=inventory_mgmt&tab=inv_groups&epc_erp_shell=1&transfer_id=5", "/erp/stock-transfers-app?transfer_id=5")]
    [InlineData("/ERP/?epc_erp_shell=1&area=sales&tab=proposals&quote_id=6", "/erp/sales-quotations-app?quote_id=6")]
    [InlineData("/CP/shop/finance/erp?area=sales&tab=proposals&epc_erp_shell=1&quote_id=6", "/erp/sales-quotations-app?quote_id=6")]
    [InlineData("/ERP/?epc_erp_shell=1&area=sales&tab=marketing&campaign_id=7", "/erp/marketing-app?campaign_id=7")]
    [InlineData("/CP/shop/finance/erp?area=sales&tab=marketing&epc_erp_shell=1&campaign_id=7", "/erp/marketing-app?campaign_id=7")]
    [InlineData("/ERP/?epc_erp_shell=1&area=purchasing&tab=rfq&rfq_id=8", "/erp/rfq-app?rfq_id=8")]
    [InlineData("/CP/shop/finance/erp?area=purchasing&tab=rfq&epc_erp_shell=1&rfq_id=8", "/erp/rfq-app?rfq_id=8")]
    [InlineData("/ERP/?epc_erp_shell=1&area=sales&tab=delivery_notes&delivery_note_id=9", "/erp/delivery-notes-app?delivery_note_id=9")]
    [InlineData("/CP/shop/finance/erp?area=sales&tab=delivery_notes&epc_erp_shell=1&delivery_note_id=9", "/erp/delivery-notes-app?delivery_note_id=9")]
    [InlineData("/ERP/?epc_erp_shell=1&area=banking&tab=payment_batches&batch_id=10", "/erp/payment-batches-app?batch_id=10")]
    [InlineData("/CP/shop/finance/erp?area=banking&tab=payment_batches&epc_erp_shell=1&batch_id=10", "/erp/payment-batches-app?batch_id=10")]
    [InlineData("/ERP/?epc_erp_shell=1&area=people&tab=expense_reports&expense_id=11", "/erp/expense-reports-app?expense_id=11")]
    [InlineData("/CP/shop/finance/erp?area=people&tab=expense_reports&epc_erp_shell=1&expense_id=11", "/erp/expense-reports-app?expense_id=11")]
    [InlineData("/ERP/?epc_erp_shell=1&area=common&tab=agenda&event_id=12", "/erp/agenda-app?event_id=12")]
    [InlineData("/CP/shop/finance/erp?area=common&tab=agenda&epc_erp_shell=1&event_id=12", "/erp/agenda-app?event_id=12")]
    [InlineData("/ERP/?epc_erp_shell=1&area=common&tab=documents&document_id=13", "/erp/documents-app?document_id=13")]
    [InlineData("/CP/shop/finance/erp?area=common&tab=documents&epc_erp_shell=1&document_id=13", "/erp/documents-app?document_id=13")]
    [InlineData("/ERP/?epc_erp_shell=1&area=setup&tab=print_designer&template_id=14", "/erp/print-designer-app?template_id=14")]
    [InlineData("/CP/shop/finance/erp?area=setup&tab=print_designer&epc_erp_shell=1&template_id=14", "/erp/print-designer-app?template_id=14")]
    [InlineData("/ERP/?epc_erp_shell=1&area=finance&tab=opening&batch_id=15", "/erp/opening-app?batch_id=15")]
    [InlineData("/ERP/?epc_erp_shell=1&area=finance&tab=opening_balances&batch_id=15", "/erp/opening-app?batch_id=15")]
    [InlineData("/CP/shop/finance/erp?area=finance&tab=opening&epc_erp_shell=1&batch_id=15", "/erp/opening-app?batch_id=15")]
    [InlineData("/ERP/?epc_erp_shell=1&area=finance&tab=year_end&period_id=16", "/erp/period-close-app?period_id=16")]
    [InlineData("/CP/shop/finance/erp?area=finance&tab=year_end&epc_erp_shell=1&period_id=16", "/erp/period-close-app?period_id=16")]
    [InlineData("/ERP/?epc_erp_shell=1&area=common&tab=contacts&contact_id=17", "/erp/contacts-app?contact_id=17")]
    [InlineData("/CP/shop/finance/erp?area=common&tab=contacts&epc_erp_shell=1&contact_id=17", "/erp/contacts-app?contact_id=17")]
    [InlineData("/ERP/?epc_erp_shell=1&area=ar&tab=ar_setup&contact_id=17", "/erp/contacts-app?tab=ar_setup&contact_id=17")]
    [InlineData("/CP/shop/finance/erp?area=ar&tab=ar_setup&epc_erp_shell=1&contact_id=17", "/erp/contacts-app?tab=ar_setup&contact_id=17")]
    [InlineData("/ERP/?epc_erp_shell=1&area=inventory_mgmt&tab=ledger&movement_id=22", "/erp/stock-movements-app?movement_id=22")]
    [InlineData("/ERP/?epc_erp_shell=1&area=inventory_mgmt&tab=movements&movement_id=22", "/erp/stock-movements-app?movement_id=22")]
    [InlineData("/CP/shop/finance/erp?area=inventory_mgmt&tab=ledger&epc_erp_shell=1&movement_id=22", "/erp/stock-movements-app?movement_id=22")]
    [InlineData("/CP/shop/finance/epc_inventory_forecast?inv_forecast_id=23", "/erp/inventory-forecast-app?inv_forecast_id=23")]
    [InlineData("/ERP/?epc_erp_shell=1&area=production&tab=quality&ncr_id=24", "/erp/quality-app?ncr_id=24")]
    [InlineData("/CP/shop/finance/erp?area=production&tab=quality&epc_erp_shell=1&ncr_id=24", "/erp/quality-app?ncr_id=24")]
    [InlineData("/CP/shop/finance/epc_order_erp_pipeline?pipeline_log_id=25", "/erp/order-pipeline-app?pipeline_log_id=25")]
    [InlineData("/ERP/?epc_erp_shell=1&area=common&tab=doc_attachment&attach_id=26", "/erp/doc-attachments-app?attach_id=26")]
    [InlineData("/CP/shop/finance/erp?area=common&tab=doc_attachment&epc_erp_shell=1&attach_id=26", "/erp/doc-attachments-app?attach_id=26")]
    [InlineData("/ERP/?epc_erp_shell=1&area=planning&tab=order_planning&opl_rec_id=27", "/erp/order-planning-app?opl_rec_id=27")]
    [InlineData("/ERP/?epc_erp_shell=1&area=planning&tab=master_planning&opl_rec_id=27", "/erp/order-planning-app?tab=master_planning&opl_rec_id=27")]
    [InlineData("/CP/shop/finance/erp?area=planning&tab=order_planning&epc_erp_shell=1&opl_rec_id=27", "/erp/order-planning-app?opl_rec_id=27")]
    [InlineData("/ERP/?epc_erp_shell=1&area=inventory&tab=rfid&session_id=18", "/erp/rfid-app?session_id=18")]
    [InlineData("/CP/shop/finance/erp?area=inventory&tab=rfid&epc_erp_shell=1&session_id=18", "/erp/rfid-app?session_id=18")]
    [InlineData("/CP/shop/crm/crm_main?lead_id=6", "/cp/crm-board-app?lead_id=6")]
    [InlineData("/CP/shop/crm?lead_id=6", "/cp/crm-board-app?lead_id=6")]
    [InlineData("/ERP/?epc_erp_shell=1&area=purchasing&tab=jw_purchase_fixing&fixing_id=4", "/erp/jewellery-fixing-app?tab=jw_purchase_fixing&fixing_id=4")]
    [InlineData("/CP/shop/finance/erp?area=purchasing&tab=jw_purchase_fixing&epc_erp_shell=1&fixing_id=4", "/erp/jewellery-fixing-app?tab=jw_purchase_fixing&fixing_id=4")]
    [InlineData("/CP/shop/workshop?job_id=5", "/cp/workshop-app?job_id=5")]
    [InlineData("/CP/shop/finance/epc_credit_limit?credit_id=7", "/cp/credit-limits-app?credit_id=7")]
    [InlineData("/ERP/?epc_erp_shell=1&area=landed_cost_area&tab=landed_cost&sheet_id=6", "/erp/landed-cost-app?sheet_id=6")]
    [InlineData("/CP/control/portal/epc_soc2_compliance?soc2_id=8", "/cp/soc2-compliance-app?soc2_id=8")]
    [InlineData("/ERP/?epc_erp_shell=1&area=tax&tab=compliance&soc2_id=8", "/erp/soc2-compliance-app?soc2_id=8")]
    [InlineData("/CP/shop/orders/carts?cart_id=15", "/cp/abandoned-carts-app?cart_id=15")]
    [InlineData("/ERP/?epc_erp_shell=1&area=tax&tab=aml_compliance&kyc_id=7", "/erp/aml-compliance-app?kyc_id=7")]
    [InlineData("/ERP/?epc_erp_shell=1&area=tax&tab=aml_compliance&kyc=7", "/erp/aml-compliance-app?kyc=7")]
    [InlineData("/ERP/?epc_erp_shell=1&area=sales&tab=opportunities&opp_id=6", "/erp/crm-opportunities-app?opp_id=6")]
    [InlineData("/ERP/?epc_erp_shell=1&area=setup&tab=tenant_config&config_id=8", "/erp/tenant-config-app?config_id=8")]
    [InlineData("/CP/control/portal/epc_tenant_config?config_id=8", "/cp/tenant-config-app?config_id=8")]
    [InlineData("/ERP/?epc_erp_shell=1&area=audit_wb&tab=audit&event_id=3", "/erp/audit-trail-app?event_id=3")]
    [InlineData("/ERP/?epc_erp_shell=1&area=risk&tab=doc_expiry&doc=4", "/erp/doc-expiry-app?doc=4")]
    [InlineData("/ERP/?epc_erp_shell=1&area=risk&tab=doc_expiry&document_id=4", "/erp/doc-expiry-app?document_id=4")]
    [InlineData("/ERP/?epc_erp_shell=1&area=tax&tab=withholding&txn_id=5", "/erp/withholding-app?txn_id=5")]
    [InlineData("/ERP/?epc_erp_shell=1&area=risk&tab=insurance&pol=7", "/erp/insurance-compliance-app?pol=7")]
    [InlineData("/ERP/?epc_erp_shell=1&area=risk&tab=insurance&policy_id=7", "/erp/insurance-compliance-app?policy_id=7")]
    [InlineData("/CP/shop/returns-manager?page=detail&return_id=8", "/cp/returns-rma-app?return_id=8")]
    [InlineData("/CP/shop/finance/epc_warranty_rma?rma_id=6", "/cp/returns-rma-app?rma_id=6")]
    [InlineData("/CP/shop/quote-requests?quote_id=15", "/cp/quote-requests-app?quote_id=15")]
    [InlineData("/CP/control/portal/epc_marketing_broadcast?campaign_id=11", "/cp/marketing-broadcast-app?campaign_id=11")]
    [InlineData("/ERP/?epc_erp_shell=1&area=setup&tab=data_import&migration_id=9", "/erp/data-migrations-app?migration_id=9")]
    [InlineData("/CP/control/portal/epc_db_migrations?migration_id=9", "/cp/data-migrations-app?migration_id=9")]
    public void AspNetPrimaryHref_KeepsErpRecordId(string php, string expected)
    {
        var href = PhpSurfaceLinkMap.AspNetPrimaryHref(php);
        Assert.Equal(expected, href);
        Assert.DoesNotContain("/php-reference/", href, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AreaHubWithoutRecordId_StillMapsToModule()
    {
        Assert.Equal(
            "/erp/sales-orders-app",
            PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=sales"));
    }

    [Fact]
    public void RowHref_IsAspNetRecordUrl()
    {
        Assert.Equal("/erp/sales-orders-app?so_id=42#erp-row-42", ErpRecordOpen.Href("/erp/sales-orders-app", "so_id", 42));
        Assert.Equal("/erp/invoices-app?inv_id=7#erp-row-7", ErpRecordOpen.Href("/erp/invoices-app", "inv_id", 7));
        Assert.DoesNotContain("/php-reference/", ErpRecordOpen.Href("/erp/contracts-app", "contract_id", 3), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OpenModuleHref_StaysOnAspNetAndJumpsToBody()
    {
        var href = ErpRecordOpen.OpenModuleHref("/ERP/?epc_erp_shell=1&area=sales&tab=sales_orders");
        Assert.Equal("/erp/sales-orders-app#erp-module-body", href);
        Assert.DoesNotContain("/php-reference/", href, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NewHref_JumpsToCreateForm()
    {
        Assert.Equal(
            "/erp/sales-orders-app#erp-module-new",
            ErpRecordOpen.NewHref("/ERP/?epc_erp_shell=1&area=sales&tab=sales_orders"));
    }

    [Fact]
    public void SalesOrdersApp_RowOpenUsesRecordHelper()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/ErpSalesOrdersApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(\"/erp/sales-orders-app\", \"so_id\"", text, StringComparison.Ordinal);
        Assert.Contains("btn-primary", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpReferenceOnlyHref(phpHref)", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("id=\"erp-module-new\"", text, StringComparison.Ordinal);
        Assert.Contains("BuildErpSalesOrderDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"so_id\", \"order_id\")", text, StringComparison.Ordinal);
        Assert.Contains("so_id=", text, StringComparison.Ordinal);
        Assert.Contains("NotesExcerpt", text, StringComparison.Ordinal);
        Assert.Contains("AmountExVat", text, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stay Classic", text, StringComparison.Ordinal);
        Assert.Contains("/erp/orders/settlement", text, StringComparison.Ordinal);
        Assert.Contains("erp-sales-orders.js", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);

        Assert.Equal("/erp/sales-orders-app?so_id=42#erp-row-42",
            ErpRecordOpen.Href("/erp/sales-orders-app", "so_id", 42));
    }

    [Fact]
    public void GlJournalsApp_OpenLoadsNoteReferenceAndSiblings()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpGlJournalsApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpGlJournalDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"journal_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("journal_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(\"/erp/gl-journals-app\", \"journal_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("DescriptionExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("Reference", razor, StringComparison.Ordinal);
        Assert.Contains("same-source siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reverse stays Classic", razor, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryVoucherSaveForm", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/gl-journals-app?journal_id=3#erp-row-3",
            ErpRecordOpen.Href("/erp/gl-journals-app", "journal_id", 3));
        Assert.Equal(
            "/erp/gl-journals-app?journal_id=3",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/gl-journals-app",
                "/ERP/?epc_erp_shell=1&area=finance&tab=gl&journal_id=3"));
    }

    [Theory]
    [InlineData("ErpPurchaseOrdersApp.razor", "po_id")]
    [InlineData("ErpInvoicesApp.razor", "inv_id")]
    [InlineData("ErpPurchasesApp.razor", "purchase_id")]
    [InlineData("ErpGlJournalsApp.razor", "journal_id")]
    [InlineData("ErpCashEntriesApp.razor", "entry_id")]
    [InlineData("ErpContractsApp.razor", "contract_id")]
    [InlineData("ErpReceivablesApp.razor", "customer_id")]
    [InlineData("ErpSuppliersApp.razor", "supplier_id")]
    public void TransactionalApps_RowOpenIsRecordUrl(string fileName, string param)
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages", fileName));
        Assert.Contains("ErpRecordOpen.Href(", text, StringComparison.Ordinal);
        Assert.Contains("\"" + param + "\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpReferenceOnlyHref(phpHref)", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ErpRfqApp.razor", "rfq_id")]
    [InlineData("ErpWarehousesApp.razor", "warehouse_id")]
    [InlineData("ErpDeliveryNotesApp.razor", "delivery_note_id")]
    [InlineData("ErpSalesQuotationsApp.razor", "quote_id")]
    [InlineData("ErpMarketingApp.razor", "campaign_id")]
    [InlineData("ErpContactsApp.razor", "contact_id")]
    [InlineData("ErpCashAccountsApp.razor", "account_id")]
    [InlineData("ErpOnPremisesApp.razor", "license_id")]
    [InlineData("ErpPayablesApp.razor", "supplier_id")]
    [InlineData("CpPurchaseRequestsApp.razor", "req_id")]
    [InlineData("CpCollectionsDunningApp.razor", "queue_id")]
    [InlineData("CpApiClientsApp.razor", "api_client_id")]
    [InlineData("CpFinanceCloseApp.razor", "batch_id")]
    [InlineData("CpCostModelsApp.razor", "costm_id")]
    [InlineData("CpElectronicReportingApp.razor", "format_id")]
    [InlineData("CpEinvoiceDocumentsApp.razor", "ei_id")]
    [InlineData("CpProjectsOverviewApp.razor", "project_id")]
    [InlineData("CpProductionOverviewApp.razor", "wo_id")]
    [InlineData("CpAccessoriesApp.razor", "listing_id")]
    [InlineData("CpCrossesApp.razor", "cross_id")]
    [InlineData("CpSynonymsApp.razor", "manufacturer_id")]
    [InlineData("CpPromotionsApp.razor", "promo_id")]
    [InlineData("CpPageBuilderApp.razor", "layout_id")]
    [InlineData("CpTaxToolkitsApp.razor", "toolkit_id")]
    [InlineData("CpLandedCostApp.razor", "sheet_id")]
    [InlineData("CpSoc2ComplianceApp.razor", "soc2_id")]
    [InlineData("CpAbandonedCartsApp.razor", "cart_id")]
    [InlineData("CpAmlComplianceApp.razor", "kyc_id")]
    [InlineData("CpCrmOpportunitiesApp.razor", "opp_id")]
    [InlineData("CpTenantConfigApp.razor", "config_id")]
    [InlineData("CpAuditTrailApp.razor", "event_id")]
    [InlineData("CpDocExpiryApp.razor", "doc")]
    [InlineData("ErpWithholdingApp.razor", "txn_id")]
    [InlineData("CpInsuranceComplianceApp.razor", "pol")]
    [InlineData("CpReturnsRmaApp.razor", "rma_id")]
    [InlineData("CpQuoteRequestsApp.razor", "quote_id")]
    [InlineData("CpMarketingBroadcastApp.razor", "campaign_id")]
    [InlineData("CpDataMigrationsApp.razor", "migration_id")]
    public void DumpListApps_RowOpenIsRecordUrl(string fileName, string param)
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages", fileName));
        Assert.Contains("ErpRecordOpen.Href(", text, StringComparison.Ordinal);
        Assert.Contains("\"" + param + "\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PurchaseRequestsApp_OpenLoadsDetailAndAcceptsPhpRq()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpPurchaseRequestsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"req_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"req_id\", \"rq\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpPurchaseRequestDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No lines yet.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
    }

    [Fact]
    public void QuoteRequestsApp_OpenLoadsDetailAndAcceptsPhpQuoteId()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpQuoteRequestsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"quote_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"quote_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpQuoteRequestDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No lines yet.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CollectionsDunningApp_OpenLoadsDetailAndAcceptsPhpQueueId()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpCollectionsDunningApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"queue_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"queue_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpCollectionsDunningDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No log yet.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CrmOpportunitiesApp_OpenLoadsDetailAndAcceptsPhpOppId()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpCrmOpportunitiesApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"opp_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"opp_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpCrmOpportunityDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No activities yet.", text, StringComparison.Ordinal);
        Assert.Contains("No notes yet.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TenantConfigApp_OpenLoadsDetailAndAcceptsPhpConfigId()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpTenantConfigApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"config_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"config_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpTenantConfigDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No history yet.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AuditTrailApp_OpenLoadsDetailAndAcceptsPhpEventId()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpAuditTrailApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"event_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"event_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpAuditTrailDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No JSON yet.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
    }

    [Fact]
    public void EventBusApp_OpenLoadsExcerptAndAcceptsPhpEventId()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpEventBusApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"event_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"event_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpEventBusDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No payload excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);

        Assert.Equal("/cp/event-bus-app?event_id=3#erp-row-3",
            ErpRecordOpen.Href("/cp/event-bus-app", "event_id", 3));
        Assert.Equal(
            "/cp/event-bus-app?event_id=3",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/event-bus-app",
                "/CP/control/portal/epc_event_bus?event_id=3"));
    }

    [Fact]
    public void FinAdvancedApp_OpenLoadsAllocAccrualAndFx()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpFinAdvancedApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"period_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"period_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpFinAdvancedPeriodDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No allocation rules yet.", text, StringComparison.Ordinal);
        Assert.Contains("No accruals yet.", text, StringComparison.Ordinal);
        Assert.Contains("No FX runs yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);

        Assert.Equal("/erp/fin-advanced-app?period_id=5#erp-row-5",
            ErpRecordOpen.Href("/erp/fin-advanced-app", "period_id", 5));
        Assert.Equal(
            "/erp/fin-advanced-app?period_id=5",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/fin-advanced-app",
                "/ERP/?epc_erp_shell=1&area=cost_acct&tab=fin_advanced&period_id=5"));
        Assert.Equal(
            "/cp/fin-advanced-app?period_id=5",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/fin-advanced-app",
                "/ERP/?epc_erp_shell=1&area=cost_acct&tab=fin_advanced&period_id=5"));
    }

    [Fact]
    public void BlockchainProofsApp_OpenLoadsExcerptAndBatchSiblings()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpBlockchainProofsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"proof_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"proof_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpBlockchainProofDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No payload excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No batch siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("merkle_proof_json", text, StringComparison.Ordinal);

        Assert.Equal("/erp/blockchain-proofs-app?proof_id=4#erp-row-4",
            ErpRecordOpen.Href("/erp/blockchain-proofs-app", "proof_id", 4));
        Assert.Equal(
            "/erp/blockchain-proofs-app?proof_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/blockchain-proofs-app",
                "/ERP/?epc_erp_shell=1&area=tax&tab=blockchain_proofs&proof_id=4"));
        Assert.Equal(
            "/cp/blockchain-proofs-app?proof_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/blockchain-proofs-app",
                "/ERP/?epc_erp_shell=1&area=audit_wb&tab=blockchain_proofs&proof_id=4"));
    }

    [Fact]
    public void OpsGuidesApp_OpenLoadsGroupSiblingsAndAcceptsPhpItemId()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpOpsGuidesApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"item_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"item_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpOpsGuideItemDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No group siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);

        Assert.Equal("/cp/ops-guides-app?item_id=4#erp-row-4",
            ErpRecordOpen.Href("/cp/ops-guides-app", "item_id", 4));
        Assert.Equal(
            "/cp/ops-guides-app?item_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/ops-guides-app",
                "/CP/control/version_control?item_id=4"));
    }

    [Fact]
    public void MarketplaceAppsApp_OpenLoadsExcerptInstallsAndReviews()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpMarketplaceAppsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"app_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"app_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpMarketplaceAppDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No description excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No installs yet.", text, StringComparison.Ordinal);
        Assert.Contains("No reviews yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("review_text", text, StringComparison.Ordinal);

        Assert.Equal("/cp/marketplace-apps-app?app_id=7#erp-row-7",
            ErpRecordOpen.Href("/cp/marketplace-apps-app", "app_id", 7));
        Assert.Equal(
            "/cp/marketplace-apps-app?app_id=7",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/marketplace-apps-app",
                "/CP/control/portal/epc_marketplace?app_id=7"));
    }

    [Fact]
    public void NlReportingApp_OpenLoadsQueryExcerptAndRuns()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpNlReportingApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"report_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"report_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpNlReportDefinitionDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No query excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No runs yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("query_template", text, StringComparison.Ordinal);
        Assert.DoesNotContain("file_path", text, StringComparison.Ordinal);

        Assert.Equal("/cp/nl-reporting-app?report_id=8#erp-row-8",
            ErpRecordOpen.Href("/cp/nl-reporting-app", "report_id", 8));
        Assert.Equal(
            "/cp/nl-reporting-app?report_id=8",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/nl-reporting-app",
                "/CP/control/portal/epc_nl_reporting?report_id=8"));
    }

    [Fact]
    public void PowerBiApp_OpenLoadsNotesExcerptAndCategorySiblings()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpPowerBiApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"pbi_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"pbi_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpPowerBiReportDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No notes excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No category siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<iframe", text, StringComparison.OrdinalIgnoreCase);

        Assert.Equal("/cp/power-bi-app?pbi_id=5#erp-row-5",
            ErpRecordOpen.Href("/cp/power-bi-app", "pbi_id", 5));
        Assert.Equal(
            "/cp/power-bi-app?pbi_id=5",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/power-bi-app",
                "/CP/control/portal/epc_power_bi?pbi_id=5"));
    }

    [Fact]
    public void MetabaseApp_OpenLoadsSiteUrlAndCategorySiblings()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpMetabaseApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"mb_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"mb_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpMetabaseDashboardDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No site URL yet.", text, StringComparison.Ordinal);
        Assert.Contains("No category siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret_key", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<iframe", text, StringComparison.OrdinalIgnoreCase);

        Assert.Equal("/cp/metabase-app?mb_id=6#erp-row-6",
            ErpRecordOpen.Href("/cp/metabase-app", "mb_id", 6));
        Assert.Equal(
            "/cp/metabase-app?mb_id=6",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/metabase-app",
                "/CP/general_pages/epc_metabase_embed?mb_id=6"));
    }

    [Fact]
    public void MarketingGrowthApp_OpenLoadsNotesExcerptAndStrategySiblings()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpMarketingGrowthApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"review_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"review_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpMarketingGrowthReviewDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No notes excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No strategy siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);

        Assert.Equal("/cp/marketing-growth-app?review_id=9#erp-row-9",
            ErpRecordOpen.Href("/cp/marketing-growth-app", "review_id", 9));
        Assert.Equal(
            "/cp/marketing-growth-app?review_id=9",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/marketing-growth-app",
                "/CP/shop/marketing/marketing?review_id=9"));
    }

    [Fact]
    public void IsolationAuditApp_OpenLoadsReportExcerptAndSameDayViolations()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpIsolationAuditApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"run_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"run_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpIsolationAuditRunDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No report excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No same-day violations yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("report_json", text, StringComparison.Ordinal);

        Assert.Equal("/cp/isolation-audit-app?run_id=3#erp-row-3",
            ErpRecordOpen.Href("/cp/isolation-audit-app", "run_id", 3));
        Assert.Equal(
            "/cp/isolation-audit-app?run_id=3",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/isolation-audit-app",
                "/CP/control/portal/epc_commerce_isolation_audit?run_id=3"));
    }

    [Fact]
    public void IndustryPacksApp_OpenLoadsModulesExcerptAndAssignments()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpIndustryPacksApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"pack_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"pack_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpIndustryPackDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No modules excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No assignments yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("gl_template", text, StringComparison.Ordinal);
        Assert.DoesNotContain("tax_rules", text, StringComparison.Ordinal);

        Assert.Equal("/cp/industry-packs-app?pack_id=2#erp-row-2",
            ErpRecordOpen.Href("/cp/industry-packs-app", "pack_id", 2));
        Assert.Equal(
            "/cp/industry-packs-app?pack_id=2",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/industry-packs-app",
                "/CP/control/portal/industry_settings?pack_id=2"));
    }

    [Fact]
    public void ConfigSandboxApp_OpenLoadsConfigExcerptAndChangeKeys()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpConfigSandboxApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"snapshot_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"snapshot_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpConfigSandboxSnapshotDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No config excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No changes yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("old_value", text, StringComparison.Ordinal);
        Assert.DoesNotContain("new_value", text, StringComparison.Ordinal);
        Assert.DoesNotContain("config_data", text, StringComparison.Ordinal);

        Assert.Equal("/cp/config-sandbox-app?snapshot_id=4#erp-row-4",
            ErpRecordOpen.Href("/cp/config-sandbox-app", "snapshot_id", 4));
        Assert.Equal(
            "/cp/config-sandbox-app?snapshot_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/config-sandbox-app",
                "/CP/control/portal/epc_config_sandbox?snapshot_id=4"));
    }

    [Fact]
    public void PlatformGovernanceApp_OpenLoadsDescriptionExcerptAndCategorySiblings()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpPlatformGovernanceApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"rule_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"rule_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpPlatformGovernanceRuleDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No description excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No category siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("config_json", text, StringComparison.Ordinal);

        Assert.Equal("/cp/platform-governance-app?rule_id=5#erp-row-5",
            ErpRecordOpen.Href("/cp/platform-governance-app", "rule_id", 5));
        Assert.Equal(
            "/cp/platform-governance-app?rule_id=5",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/platform-governance-app",
                "/CP/control/portal/epc_platform_governance?rule_id=5"));
    }

    [Fact]
    public void PlatformCommunicationApp_OpenLoadsDescriptionExcerptAndCategorySiblings()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpPlatformCommunicationApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"task_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"task_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpPlatformCommunicationTaskDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No description excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No category siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);

        Assert.Equal("/cp/platform-communication-app?task_id=6#erp-row-6",
            ErpRecordOpen.Href("/cp/platform-communication-app", "task_id", 6));
        Assert.Equal(
            "/cp/platform-communication-app?task_id=6",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/platform-communication-app",
                "/CP/control/portal/epc_super_cp_communication?task_id=6"));
    }

    [Fact]
    public void AiServiceApp_OpenLoadsInputExcerptAndServiceSiblings()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpAiServiceApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"query_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"query_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpAiServiceQueryDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No input excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No service siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("output_text", text, StringComparison.Ordinal);
        Assert.DoesNotContain("api_key", text, StringComparison.Ordinal);

        Assert.Equal("/cp/ai-service-app?query_id=8#erp-row-8",
            ErpRecordOpen.Href("/cp/ai-service-app", "query_id", 8));
        Assert.Equal(
            "/cp/ai-service-app?query_id=8",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/ai-service-app",
                "/CP/general_pages/epc_ai_service?query_id=8"));
    }

    [Fact]
    public void FreeToolsApp_OpenLoadsLastLoginAndSavedToolTitles()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpFreeToolsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"account_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"account_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpFreeToolsAccountDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No saved tools yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("pass_hash", text, StringComparison.Ordinal);
        Assert.DoesNotContain("del_code_hash", text, StringComparison.Ordinal);
        Assert.DoesNotContain("`token`", text, StringComparison.Ordinal);

        Assert.Equal("/cp/free-tools-app?account_id=4#erp-row-4",
            ErpRecordOpen.Href("/cp/free-tools-app", "account_id", 4));
        Assert.Equal(
            "/cp/free-tools-app?account_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/free-tools-app",
                "/CP/control/portal/epc_free_tools?account_id=4"));
    }

    [Fact]
    public void CustomerBoardApp_OpenLoadsLastVisitAndGroups()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpCustomerBoardApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"user_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"user_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpCustomerBoardUserDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No groups yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("password", text, StringComparison.OrdinalIgnoreCase);

        Assert.Equal("/cp/customer-board-app?user_id=9#erp-row-9",
            ErpRecordOpen.Href("/cp/customer-board-app", "user_id", 9));
        Assert.Equal(
            "/cp/customer-board-app?user_id=9",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/customer-board-app",
                "/CP/control/portal/epc_super_cp_customer_board?user_id=9"));
    }

    [Fact]
    public void InfoBlocksApp_OpenLoadsContentExcerptAndPlacementSiblings()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpInfoBlocksApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"block_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"block_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpInfoBlocksBlockDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No content excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No placement siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);

        Assert.Equal("/cp/info-blocks-app?block_id=3#erp-row-3",
            ErpRecordOpen.Href("/cp/info-blocks-app", "block_id", 3));
        Assert.Equal(
            "/cp/info-blocks-app?block_id=3",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/info-blocks-app",
                "/CP/control/portal/epc_super_cp_info_blocks?block_id=3"));
    }

    [Fact]
    public void NotificationsApp_OpenLoadsBodyExcerptAndKeepsHero()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpNotificationsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"notif_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"notif_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpNotificationsDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No body excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No category siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("epc-cn-hero", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("metadata", text, StringComparison.Ordinal);
        Assert.DoesNotContain("action_url", text, StringComparison.Ordinal);

        Assert.Equal("/cp/notifications-app?notif_id=7#erp-row-7",
            ErpRecordOpen.Href("/cp/notifications-app", "notif_id", 7));
        Assert.Equal(
            "/cp/notifications-app?notif_id=7",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/notifications-app",
                "/CP/control/notifications_settings?notif_id=7"));
    }

    [Fact]
    public void SocialHubApp_OpenLoadsLastTestAndDraftCaptions()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpSocialHubApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"social_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"social_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpSocialHubAccountDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No drafts yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("encrypted_credentials", text, StringComparison.Ordinal);
        Assert.DoesNotContain("last_error", text, StringComparison.Ordinal);

        Assert.Equal("/cp/social-hub-app?social_id=5#erp-row-5",
            ErpRecordOpen.Href("/cp/social-hub-app", "social_id", 5));
        Assert.Equal(
            "/cp/social-hub-app?social_id=5",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/social-hub-app",
                "/CP/control/portal/epc_social_media_hub?social_id=5"));
    }

    [Fact]
    public void CrmActivitiesApp_OpenLoadsNotesExcerptAndRelatedSiblings()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpCrmActivitiesApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"activity_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"activity_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpCrmActivitiesDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No notes excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No related siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);

        Assert.Equal("/cp/crm-activities-app?activity_id=9#erp-row-9",
            ErpRecordOpen.Href("/cp/crm-activities-app", "activity_id", 9));
        Assert.Equal(
            "/cp/crm-activities-app?activity_id=9",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/crm-activities-app",
                "/CP/shop/crm/crm_main?tab=activities&activity_id=9"));
    }

    [Fact]
    public void CrmTicketsApp_OpenLoadsMessageExcerptsAndKeepsWrites()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpCrmTicketsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"ticket_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"ticket_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpCrmTicketsDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No messages yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("Create SLA", text, StringComparison.Ordinal);
        Assert.Contains("Create ticket", text, StringComparison.Ordinal);
        Assert.Contains("/erp/tickets/reply", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-ct-hero", text, StringComparison.Ordinal);

        Assert.Equal("/cp/crm-tickets-app?ticket_id=8#erp-row-8",
            ErpRecordOpen.Href("/cp/crm-tickets-app", "ticket_id", 8));
        Assert.Equal(
            "/cp/crm-tickets-app?ticket_id=8",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/crm-tickets-app",
                "/ERP/?epc_erp_shell=1&area=sales&tab=crm&ticket_id=8"));
        Assert.Equal(
            "/erp/crm-tickets-app?tab=tickets&ticket_id=8#erp-row-8",
            ErpRecordOpen.Href("/erp/crm-tickets-app?tab=tickets", "ticket_id", 8));
    }

    [Fact]
    public void AdditionalTextsApp_OpenLoadsContentExcerptAndKeepsWrites()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpAdditionalTextsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"text_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"text_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpAdditionalTextsDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No content excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No placement siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("/cp/additional-texts/write", text, StringComparison.Ordinal);
        Assert.Contains("/cp/additional-texts/delete", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", text, StringComparison.Ordinal);

        Assert.Equal("/cp/additional-texts-app?text_id=4#erp-row-4",
            ErpRecordOpen.Href("/cp/additional-texts-app", "text_id", 4));
        Assert.Equal(
            "/cp/additional-texts-app?text_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/additional-texts-app",
                "/CP/content/dopolnitelnye-teksty?text_id=4"));
    }

    [Fact]
    public void SystemRequestsApp_OpenLoadsVinExcerptAndKeepsViewedWrite()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpSystemRequestsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"vin_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"vin_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpSystemRequestsDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No request excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No messages yet.", text, StringComparison.Ordinal);
        Assert.Contains("No same-user siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("/cp/requests/set-vin-viewed", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", text, StringComparison.Ordinal);

        Assert.Equal("/cp/system-requests-app?vin_id=6#erp-row-6",
            ErpRecordOpen.Href("/cp/system-requests-app", "vin_id", 6));
        Assert.Equal(
            "/cp/system-requests-app?vin_id=6",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/system-requests-app",
                "/CP/requests?vin_id=6"));
    }

    [Fact]
    public void ConsolidationsApp_OpenLoadsFiguresAndIcAndKeepsWrites()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpConsolidationsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"cons_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"cons_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpConsolidationsDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No figures yet.", text, StringComparison.Ordinal);
        Assert.Contains("No IC rows yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/cons-entity-save", text, StringComparison.Ordinal);
        Assert.Contains("/erp/consolidations/figures/save", text, StringComparison.Ordinal);
        Assert.Contains("/erp/consolidations/ic/save", text, StringComparison.Ordinal);
        Assert.Contains("Save entity", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w14-hero", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);

        Assert.Equal("/cp/consolidations-app?cons_id=3#erp-row-3",
            ErpRecordOpen.Href("/cp/consolidations-app", "cons_id", 3));
        Assert.Equal(
            "/erp/consolidations-app?cons_id=3",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/consolidations-app",
                "/ERP/?epc_erp_shell=1&area=consolidations&tab=consolidation_bu&cons_id=3"));
    }

    [Fact]
    public void PoApprovalsApp_OpenLoadsDescriptionNotesAndKeepsApproveReject()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpPoApprovalsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"po_req_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"po_req_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpPoApprovalsDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No description excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No notes excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No approval steps yet.", text, StringComparison.Ordinal);
        Assert.Contains("No same-site siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("/cp/po-approvals/approve", text, StringComparison.Ordinal);
        Assert.Contains("/cp/po-approvals/reject", text, StringComparison.Ordinal);
        Assert.Contains("Approve", text, StringComparison.Ordinal);
        Assert.Contains("Reject", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.Contains("CpPhpModuleCopy.PurposeFor", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w18-hero", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);

        Assert.Equal("/cp/po-approvals-app?po_req_id=4#erp-row-4",
            ErpRecordOpen.Href("/cp/po-approvals-app", "po_req_id", 4));
        Assert.Equal(
            "/cp/po-approvals-app?po_req_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/po-approvals-app",
                "/CP/shop/finance/epc_po_approval?po_req_id=4"));
    }

    [Fact]
    public void BudgetsApp_OpenLoadsNoteExcerptAndKeepsWrites()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpBudgetsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"budget_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"budget_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpBudgetsDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No note excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No budget lines yet.", text, StringComparison.Ordinal);
        Assert.Contains("No same-FY siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("/erp/pm/budgets/save", text, StringComparison.Ordinal);
        Assert.Contains("/erp/pm/budget-lines/add", text, StringComparison.Ordinal);
        Assert.Contains("/erp/budgets/advance", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-bud-hero", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);

        Assert.Equal("/cp/budgets-app?budget_id=5#erp-row-5",
            ErpRecordOpen.Href("/cp/budgets-app", "budget_id", 5));
        Assert.Equal(
            "/erp/budgets-app?budget_id=5",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/budgets-app",
                "/ERP/?epc_erp_shell=1&area=budgeting&tab=budgeting&budget_id=5"));
    }

    [Fact]
    public void WarehouseWmsApp_OpenLoadsLocationsAndKeepsComplete()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpWarehouseWmsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"work_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"work_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpWarehouseWmsDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("From location", text, StringComparison.Ordinal);
        Assert.Contains("No same-wave siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("/erp/wms/receive", text, StringComparison.Ordinal);
        Assert.Contains("/erp/wms/locations/save", text, StringComparison.Ordinal);
        Assert.Contains("/erp/wms/waves/create", text, StringComparison.Ordinal);
        Assert.Contains("/erp/wms/work/complete", text, StringComparison.Ordinal);
        Assert.Contains("Complete", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-wms-hero", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);

        Assert.Equal("/cp/warehouse-wms-app?work_id=7#erp-row-7",
            ErpRecordOpen.Href("/cp/warehouse-wms-app", "work_id", 7));
        Assert.Equal(
            "/erp/warehouse-wms-app?work_id=7",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/warehouse-wms-app",
                "/ERP/?epc_erp_shell=1&area=warehouse&tab=wms&work_id=7"));
    }

    [Fact]
    public void SearchTabsApp_OpenLoadsParametersExcerptAndKeepsWrites()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpSearchTabsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"tab_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"tab_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpSearchTabsDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No parameters excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No enabled siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("/cp/search-tabs/write", text, StringComparison.Ordinal);
        Assert.Contains("name=\"action\" value=\"activation\"", text, StringComparison.Ordinal);
        Assert.Contains("Save tab", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);

        Assert.Equal("/cp/search-tabs-app?tab_id=3#erp-row-3",
            ErpRecordOpen.Href("/cp/search-tabs-app", "tab_id", 3));
        Assert.Equal(
            "/cp/search-tabs-app?tab_id=3",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/search-tabs-app",
                "/CP/shop/taby-poiska?tab_id=3"));
    }

    [Fact]
    public void StoragesApp_OpenLoadsCurrencyInterfaceAndKeepsWrites()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpStoragesApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"storage_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"storage_id\", \"id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpStoragesDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No storekeeper excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No same-interface siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("/cp/storages/groups", text, StringComparison.Ordinal);
        Assert.Contains("/cp/storages/write", text, StringComparison.Ordinal);
        Assert.Contains("/cp/storages/membership", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-st-hero", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-st-kpis", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("connection_options", text, StringComparison.Ordinal);

        Assert.Equal("/cp/storages-app?storage_id=5#erp-row-5",
            ErpRecordOpen.Href("/cp/storages-app", "storage_id", 5));
        Assert.Equal(
            "/cp/storages-app?storage_id=5",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/storages-app",
                "/CP/shop/logistics/storages?storage_id=5"));
    }

    [Fact]
    public void ProductFiltersApp_OpenLoadsStoragesExcerptAndKeepsWrites()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpProductFiltersApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"filter_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"filter_id\", \"id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpProductFiltersDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No storage-scope excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No same-manufacturer siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("/cp/product-filters/write", text, StringComparison.Ordinal);
        Assert.Contains("name=\"action\" value=\"save_storages\"", text, StringComparison.Ordinal);
        Assert.Contains("Add filter", text, StringComparison.Ordinal);
        Assert.Contains("Save filter", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);

        Assert.Equal("/cp/product-filters-app?filter_id=4#erp-row-4",
            ErpRecordOpen.Href("/cp/product-filters-app", "filter_id", 4));
        Assert.Equal(
            "/cp/product-filters-app?filter_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/product-filters-app",
                "/CP/shop/filter?filter_id=4"));
    }

    [Fact]
    public void GeoRegionsApp_OpenLoadsCaptionExcerptAndKeepsTreeSave()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpGeoRegionsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"geo_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"geo_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpGeoRegionsDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No caption excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No same-parent siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("/cp/geo-regions/write", text, StringComparison.Ordinal);
        Assert.Contains("Save tree", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);

        Assert.Equal("/cp/geo-regions-app?geo_id=4#erp-row-4",
            ErpRecordOpen.Href("/cp/geo-regions-app", "geo_id", 4));
        Assert.Equal(
            "/cp/geo-regions-app?geo_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/geo-regions-app",
                "/CP/shop/geo/nodes?geo_id=4"));
    }

    [Fact]
    public void DeliveryMethodsApp_OpenLoadsParametersExcerptAndKeepsWrites()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpDeliveryMethodsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"obtaining_mode_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"obtaining_mode_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpDeliveryModeDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No parameters excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No same-available siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("/cp/delivery-methods/write", text, StringComparison.Ordinal);
        Assert.Contains("name=\"action\" value=\"activation\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"obtain_mode_id\"", text, StringComparison.Ordinal);
        Assert.Contains("Save method", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-del-hero", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-del-kpis", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);

        Assert.Equal("/cp/delivery-methods-app?obtaining_mode_id=4#erp-row-4",
            ErpRecordOpen.Href("/cp/delivery-methods-app", "obtaining_mode_id", 4));
        Assert.Equal(
            "/cp/delivery-methods-app?obtaining_mode_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/delivery-methods-app",
                "/CP/shop/logistics/sposoby-polucheniya?obtaining_mode_id=4"));
    }

    [Fact]
    public void PagesApp_OpenLoadsBodyExcerptAndKeepsWrites()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpPagesApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"content_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"content_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpPagesDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No body excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No same-parent siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("/cp/content/published", text, StringComparison.Ordinal);
        Assert.Contains("/cp/content/body", text, StringComparison.Ordinal);
        Assert.Contains("/cp/content/save", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-users-workspace", text, StringComparison.Ordinal);
        Assert.Contains("content_id=", text, StringComparison.Ordinal);
        Assert.Contains("_selected", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);

        Assert.Equal("/cp/pages-app?content_id=12#erp-row-12",
            ErpRecordOpen.Href("/cp/pages-app", "content_id", 12));
        Assert.Equal(
            "/cp/pages-app?content_id=12",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/pages-app",
                "/CP/content/content_manager/content?content_id=12"));
    }

    [Fact]
    public void MenusApp_OpenLoadsStructureExcerptAndKeepsWrites()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpMenusApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"menu_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"menu_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpMenusDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No structure excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No same-frontend siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("/cp/menus/write", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-users-workspace", text, StringComparison.Ordinal);
        Assert.Contains("menu_id=", text, StringComparison.Ordinal);
        Assert.Contains("_selected", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);

        Assert.Equal("/cp/menus-app?menu_id=3#erp-row-3",
            ErpRecordOpen.Href("/cp/menus-app", "menu_id", 3));
        Assert.Equal(
            "/cp/menus-app?menu_id=3",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/menus-app",
                "/CP/menu/menu_edit?menu_id=3"));
    }

    [Fact]
    public void ProductCatalogueApp_OpenLoadsTextExcerptAndKeepsWrites()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpProductCatalogueApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"product_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"product_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpProductCatalogueDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No product-text excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No same-category siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("/cp/catalogue/products/write", text, StringComparison.Ordinal);
        Assert.Contains("/cp/catalogue/set-min-limit", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-users-workspace", text, StringComparison.Ordinal);
        Assert.Contains("product_id=", text, StringComparison.Ordinal);
        Assert.Contains("_selected", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);

        Assert.Equal("/cp/product-catalogue-app?product_id=8#erp-row-8",
            ErpRecordOpen.Href("/cp/product-catalogue-app", "product_id", 8));
        Assert.Equal(
            "/cp/product-catalogue-app?product_id=8",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/product-catalogue-app",
                "/CP/shop/catalogue/catalogue_editor?product_id=8"));
    }

    [Fact]
    public void OfficesApp_OpenLoadsUsersExcerptAndKeepsWrites()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpOfficesApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"office_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"office_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpOfficesDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No staff-users excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No same-city siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("/cp/offices/write", text, StringComparison.Ordinal);
        Assert.Contains("/cp/offices/delete", text, StringComparison.Ordinal);
        Assert.Contains("/cp/offices/geo", text, StringComparison.Ordinal);
        Assert.Contains("office_id=", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);

        Assert.Equal("/cp/offices-app?office_id=2#erp-row-2",
            ErpRecordOpen.Href("/cp/offices-app", "office_id", 2));
        Assert.Equal(
            "/cp/offices-app?office_id=2",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/offices-app",
                "/CP/shop/logistics/offices/office?office_id=2"));
    }

    [Fact]
    public void ModulesApp_OpenLoadsBodyExcerptAndKeepsWrites()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpModulesApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"module_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"module_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpModulesDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No body excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No same-position siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("/cp/modules/write", text, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref(_phpTab)", text, StringComparison.Ordinal);
        Assert.Contains("module_id=", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);

        Assert.Equal("/cp/modules-app?module_id=4#erp-row-4",
            ErpRecordOpen.Href("/cp/modules-app", "module_id", 4));
        Assert.Equal(
            "/cp/modules-app?module_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/modules-app",
                "/CP/modules/module?module_id=4"));
    }

    [Fact]
    public void UaeTaxApp_OpenLoadsSummaryExcerptAndKeepsWrites()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpUaeTaxComplianceApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"leg_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"leg_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpUaeTaxComplianceDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No ERP summary excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No same-category siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", text, StringComparison.Ordinal);
        Assert.Contains("/erp/uae-tax/fta-fetch", text, StringComparison.Ordinal);
        Assert.Contains("/erp/uae-tax/legislation/ask", text, StringComparison.Ordinal);
        Assert.Contains("/erp/uae-tax/legislation/regen", text, StringComparison.Ordinal);
        Assert.Contains("/erp/uae-tax/ct-adjustments/save", text, StringComparison.Ordinal);
        Assert.Contains("/erp/uae-tax/legislation/checklist/set", text, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref(_phpTab)", text, StringComparison.Ordinal);
        Assert.Contains("leg_id=", text, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-uae-hero", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("pdf_url", text, StringComparison.Ordinal);
        Assert.DoesNotContain("compliance_actions_json", text, StringComparison.Ordinal);

        Assert.Equal("/cp/uae-tax-compliance-app?leg_id=4#erp-row-4",
            ErpRecordOpen.Href("/cp/uae-tax-compliance-app", "leg_id", 4));
        Assert.Equal(
            "/erp/uae-tax-compliance-app?leg_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/uae-tax-compliance-app",
                "/ERP/?epc_erp_shell=1&area=tax&tab=tax_compliance&leg_id=4"));
        Assert.Equal(
            "/cp/uae-tax-compliance-app?leg_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/uae-tax-compliance-app",
                "/CP/shop/finance/erp/uae-tax-compliance?epc_erp_shell=1&leg_id=4"));
    }

    [Fact]
    public void AutoPriceApp_OpenLoadsNotesExcerpt()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpAutoPriceApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"aprice_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"aprice_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpAutoPriceDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No notes excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No same-site siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", text, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref(_phpTab)", text, StringComparison.Ordinal);
        Assert.Contains("HasStaffAccess", text, StringComparison.Ordinal);
        Assert.Contains("aprice_id=", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-ap-hero", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-ap-kpis", text, StringComparison.Ordinal);
        Assert.DoesNotContain("config_json", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);

        Assert.Equal("/cp/auto-price-app?aprice_id=6#erp-row-6",
            ErpRecordOpen.Href("/cp/auto-price-app", "aprice_id", 6));
        Assert.Equal(
            "/cp/auto-price-app?aprice_id=6",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/auto-price-app",
                "/CP/control/portal/epc_auto_price_engine?aprice_id=6"));
    }

    [Fact]
    public void GroupsApp_OpenLoadsDescriptionExcerpt()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpGroupsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"ugroup_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"ugroup_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpGroupsDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No description excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No same-parent siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-kpi", text, StringComparison.Ordinal);
        Assert.Contains("PhpCpModulePageHeader", text, StringComparison.Ordinal);
        Assert.Contains("ugroup_id=", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(\"/CP/users/usergroups\")\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);

        Assert.Equal("/cp/groups-app?ugroup_id=3#erp-row-3",
            ErpRecordOpen.Href("/cp/groups-app", "ugroup_id", 3));
        Assert.Equal(
            "/cp/groups-app?ugroup_id=3",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/groups-app",
                "/CP/users/usergroups?ugroup_id=3"));
    }

    [Fact]
    public void JewelleryMastersApp_OpenLoadsDescriptionExcerptAndKeepsWrites()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryMastersApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildCpJewelleryMastersDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"karat_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("karat_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"karat_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("DescriptionExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("PosRateMinMax", razor, StringComparison.Ordinal);
        Assert.Contains("same-division siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ErpJewelleryKaratSaveForm", razor, StringComparison.Ordinal);
        Assert.Contains("Save karat", razor, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryKaratSeedForm", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w16-hero", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);

        Assert.Equal("/cp/jewellery-masters-app?karat_id=3#erp-row-3",
            ErpRecordOpen.Href("/cp/jewellery-masters-app", "karat_id", 3));
        Assert.Equal(
            "/erp/jewellery-masters-app?karat_id=3",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/jewellery-masters-app",
                "/ERP/?epc_erp_shell=1&area=jewellery&tab=jw_karat&karat_id=3"));
    }

    [Fact]
    public void JewelleryStockVerificationApp_OpenLoadsRemarksExcerptAndKeepsWrites()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryStockVerificationApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildCpJewelleryStockVerificationDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"verify_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("verify_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"verify_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("RemarksExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("VerifiedBy", razor, StringComparison.Ordinal);
        Assert.Contains("MetalStone", razor, StringComparison.Ordinal);
        Assert.Contains("RemainingPcs", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ErpJewelleryStockVerifySaveForm", razor, StringComparison.Ordinal);
        Assert.Contains("Save stock verification", razor, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryMetalStockSaveForm", razor, StringComparison.Ordinal);
        Assert.Contains("Save metal stock", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("Sample seed stays Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w16-hero", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/cp/jewellery-stock-verification-app?verify_id=4#erp-row-4",
            ErpRecordOpen.Href("/cp/jewellery-stock-verification-app", "verify_id", 4));
        Assert.Equal(
            "/erp/jewellery-stock-verification-app?verify_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/jewellery-stock-verification-app",
                "/ERP/?epc_erp_shell=1&area=inventory_mgmt&tab=jw_stock_verification&verify_id=4"));
        Assert.Equal(
            "/cp/jewellery-stock-verification-app?verify_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/jewellery-stock-verification-app",
                "/CP/shop/finance/erp?area=inventory_mgmt&tab=jw_stock_verification&epc_erp_shell=1&verify_id=4"));
    }

    [Fact]
    public void JewelleryRepairsApp_OpenLoadsNarrationExcerptAndOmitsPhone()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryRepairsApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildCpJewelleryRepairDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"repair_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("repair_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"repair_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NarrationExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("StoneDetailsExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("Phone omitted", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ErpJewelleryRepairCreateForm", razor, StringComparison.Ordinal);
        Assert.Contains("Create repair", razor, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryRepairReceiptSaveForm", razor, StringComparison.Ordinal);
        Assert.Contains("Save repair receipt", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("jw-status-row", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("Sample seed stays Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("_opened.Phone", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("_opened.CustomerPhone", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("_opened.Mobile", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w16-hero", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/cp/jewellery-repairs-app?repair_id=5#erp-row-5",
            ErpRecordOpen.Href("/cp/jewellery-repairs-app", "repair_id", 5));
        Assert.Equal(
            "/erp/jewellery-repairs-app?repair_id=5",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/jewellery-repairs-app",
                "/ERP/?epc_erp_shell=1&area=service_mgmt&tab=jw_repairs&repair_id=5"));
        Assert.Equal(
            "/cp/jewellery-repairs-app?repair_id=5",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/jewellery-repairs-app",
                "/CP/shop/finance/erp?area=service_mgmt&tab=jw_repairs&epc_erp_shell=1&repair_id=5"));
    }

    [Fact]
    public void JewelleryRetailApp_OpenLoadsNarrationExcerptAndOmitsPii()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryRetailApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildCpJewelleryVoucherDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"voc_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("voc_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"voc_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NarrationExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("PartyCode", razor, StringComparison.Ordinal);
        Assert.Contains("Salesman", razor, StringComparison.Ordinal);
        Assert.Contains("Customer PII omitted", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ErpJewelleryVoucherSaveForm", razor, StringComparison.Ordinal);
        Assert.Contains("Save voucher", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("_opened.CustomerName", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("_opened.Mobile", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("_opened.Email", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w16-hero", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/cp/jewellery-retail-app?voc_id=6#erp-row-6",
            ErpRecordOpen.Href("/cp/jewellery-retail-app", "voc_id", 6));
        Assert.Equal(
            "/erp/jewellery-retail-app?voc_id=6",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/jewellery-retail-app",
                "/ERP/?epc_erp_shell=1&area=sales&tab=jw_retail_sales&voc_id=6"));
        Assert.Equal(
            "/cp/jewellery-retail-app?voc_id=6",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/jewellery-retail-app",
                "/CP/shop/finance/erp?area=sales&tab=jw_retail_sales&epc_erp_shell=1&voc_id=6"));
    }

    [Fact]
    public void WorkflowsApp_OpenLoadsDescriptionExcerptAndKeepsLockedChrome()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpWorkflowsApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildCpWorkflowDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"workflow_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("workflow_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"workflow_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("DescriptionExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("trigger_config omitted", razor, StringComparison.Ordinal);
        Assert.Contains("same-trigger siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/erp/automation/deactivate", razor, StringComparison.Ordinal);
        Assert.Contains("Disable automation", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-auto-hero", razor, StringComparison.Ordinal);
        Assert.Contains("ErpAutomationCatalogue", razor, StringComparison.Ordinal);
        Assert.Contains("epc-wf-table", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w16-hero", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/cp/workflows-app?workflow_id=3#erp-row-3",
            ErpRecordOpen.Href("/cp/workflows-app", "workflow_id", 3));
        Assert.Equal(
            "/erp/workflows-app?workflow_id=3",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/workflows-app",
                "/ERP/?epc_erp_shell=1&area=setup&tab=workflow_automation&workflow_id=3"));
        Assert.Equal(
            "/cp/workflows-app?workflow_id=3",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/workflows-app",
                "/CP/control/portal/epc_workflow_builder?workflow_id=3"));
    }

    [Fact]
    public void TemplatesManagerApp_OpenLoadsDataValueExcerptAndKeepsClassicSwitch()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpTemplatesManagerApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildCpTemplatesManagerDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"tpl_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("tpl_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"tpl_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("DataValueExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-frontend siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Delete and style generate stay on the Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("/cp/templates-manager/set-current", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"template_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("class=\"hpanel\"", razor, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref(_phpTab)", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/cp/templates-manager-app?tpl_id=4#erp-row-4",
            ErpRecordOpen.Href("/cp/templates-manager-app", "tpl_id", 4));
        Assert.Equal(
            "/cp/templates-manager-app?tpl_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/templates-manager-app",
                "/CP/templates/templates_manager?tpl_id=4"));
        Assert.Equal(
            "/cp/templates-manager-app?tpl_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/templates-manager-app",
                "/CP/templates_control?tpl_id=4"));
    }

    [Fact]
    public void PluginsManagerApp_OpenLoadsDataValueExcerptAndKeepsClassicActivate()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpPluginsManagerApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildCpPluginsManagerDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"plugin_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("plugin_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"plugin_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("DataValueExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-frontend siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Lock, delete, and backend 2FA (plugin 10) activate stay on the Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("/cp/plugins-manager/activate", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"flag_value\"", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("class=\"hpanel\"", razor, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref(_phpTab)", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/cp/plugins-manager-app?plugin_id=5#erp-row-5",
            ErpRecordOpen.Href("/cp/plugins-manager-app", "plugin_id", 5));
        Assert.Equal(
            "/cp/plugins-manager-app?plugin_id=5",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/plugins-manager-app",
                "/CP/plugins/plugins_manager?plugin_id=5"));
        Assert.Equal(
            "/cp/plugins-manager-app?plugin_id=5",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/plugins-manager-app",
                "/CP/plugins_control?plugin_id=5"));
    }

    [Fact]
    public void SitemapApp_OpenLoadsContentExcerptAndKeepsClassicRebuild()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpSitemapApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildCpSitemapDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"sm_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("sm_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"sm_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("ContentExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-published siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Rebuild stays on the Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("class=\"hpanel\"", razor, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref(_phpTab)", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("content_id", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w22-hero", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/cp/sitemap-app?sm_id=4#erp-row-4",
            ErpRecordOpen.Href("/cp/sitemap-app", "sm_id", 4));
        Assert.Equal(
            "/cp/sitemap-app?sm_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/sitemap-app",
                "/CP/content/sitemap?sm_id=4"));
    }

    [Fact]
    public void PriceListsApp_OpenLoadsStatsAndErrorExcerptsAndKeepsStorageWrites()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpPriceListsApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildCpPriceListDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"plist_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("plist_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"plist_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("StatsExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("ErrorExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-active siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stored_relpath omitted", razor, StringComparison.Ordinal);
        Assert.Contains("save_storage_rule", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("class=\"hpanel\"", razor, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref(_phpTab)", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("_opened.Stored", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-pl-hero", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/cp/price-lists-app?plist_id=4#erp-row-4",
            ErpRecordOpen.Href("/cp/price-lists-app", "plist_id", 4));
        Assert.Equal(
            "/cp/price-lists-app?plist_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/price-lists-app",
                "/CP/shop/pricing?plist_id=4"));
    }

    [Fact]
    public void BulkUploadApp_OpenLoadsNotesResultAndCsvExcerptsAndKeepsClassicWrites()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpBulkUploadApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildCpBulkUploadDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"upload_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("upload_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"upload_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NotesExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("ResultExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("CsvExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-priority siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("File bodies omitted", razor, StringComparison.Ordinal);
        Assert.Contains("process_upload", razor, StringComparison.Ordinal);
        Assert.Contains("mark_reviewed", razor, StringComparison.Ordinal);
        Assert.Contains("create_quote", razor, StringComparison.Ordinal);
        Assert.Contains("add_to_cart", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-scp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpCpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref(_phpTab)", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-bulk-hero", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/cp/bulk-upload-app?upload_id=6#erp-row-6",
            ErpRecordOpen.Href("/cp/bulk-upload-app", "upload_id", 6));
        Assert.Equal(
            "/cp/bulk-upload-app?upload_id=6",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/bulk-upload-app",
                "/CP/shop/bulk_upload?upload_id=6"));
    }

    [Fact]
    public void FixedAssetsApp_OpenLoadsNoteExcerptAndKeepsErpChrome()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpFixedAssetsApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpFixedAssetDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"asset_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("asset_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"asset_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NoteExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("TrackingId", razor, StringComparison.Ordinal);
        Assert.Contains("SerialNo", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Depreciation run stays on the Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/fixed-assets-app?asset_id=4#erp-row-4",
            ErpRecordOpen.Href("/erp/fixed-assets-app", "asset_id", 4));
        Assert.Equal(
            "/erp/fixed-assets-app?asset_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/fixed-assets-app",
                "/ERP/?epc_erp_shell=1&area=fixed_assets&tab=fixed_assets&asset_id=4"));
    }

    [Fact]
    public void StockTransfersApp_OpenLoadsNotesExcerptAndKeepsErpChrome()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpStockTransfersApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpStockTransferDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"transfer_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("transfer_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"transfer_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NotesExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("ShippedAt", razor, StringComparison.Ordinal);
        Assert.Contains("ReceivedAt", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Line bodies omitted", razor, StringComparison.Ordinal);
        Assert.Contains("Ship and receive stay on the Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/stock-transfers-app?transfer_id=5#erp-row-5",
            ErpRecordOpen.Href("/erp/stock-transfers-app", "transfer_id", 5));
        Assert.Equal(
            "/erp/stock-transfers-app?transfer_id=5",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/stock-transfers-app",
                "/ERP/?epc_erp_shell=1&area=inventory_mgmt&tab=inv_groups&transfer_id=5"));
    }

    [Fact]
    public void SalesQuotationsApp_OpenLoadsNotesExcerptAndKeepsErpChrome()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpSalesQuotationsApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpSalesQuotationDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"quote_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("quote_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"quote_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NotesExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("TimeUpdated", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Line bodies omitted", razor, StringComparison.Ordinal);
        Assert.Contains("Convert and expiry stay on the Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/sales-quotations-app?quote_id=6#erp-row-6",
            ErpRecordOpen.Href("/erp/sales-quotations-app", "quote_id", 6));
        Assert.Equal(
            "/erp/sales-quotations-app?quote_id=6",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/sales-quotations-app",
                "/ERP/?epc_erp_shell=1&area=sales&tab=proposals&quote_id=6"));
    }

    [Fact]
    public void MarketingApp_OpenLoadsNotesExcerptAndKeepsErpChrome()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpMarketingApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpMarketingCampaignDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"campaign_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("campaign_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"campaign_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NotesExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/erp/marketing/create", razor, StringComparison.Ordinal);
        Assert.Contains("Create campaign", razor, StringComparison.Ordinal);
        Assert.Contains("Schema seed stays on the Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/marketing-app?campaign_id=7#erp-row-7",
            ErpRecordOpen.Href("/erp/marketing-app", "campaign_id", 7));
        Assert.Equal(
            "/erp/marketing-app?campaign_id=7",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/marketing-app",
                "/ERP/?epc_erp_shell=1&area=sales&tab=marketing&campaign_id=7"));
    }

    [Fact]
    public void RfqApp_OpenLoadsDescriptionExcerptAndKeepsErpChrome()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpRfqApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpRfqDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"rfq_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("rfq_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"rfq_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("DescriptionExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Save stays on the Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/rfq-app?rfq_id=8#erp-row-8",
            ErpRecordOpen.Href("/erp/rfq-app", "rfq_id", 8));
        Assert.Equal(
            "/erp/rfq-app?rfq_id=8",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/rfq-app",
                "/ERP/?epc_erp_shell=1&area=purchasing&tab=rfq&rfq_id=8"));
    }

    [Fact]
    public void DeliveryNotesApp_OpenLoadsNotesExcerptAndKeepsErpChrome()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpDeliveryNotesApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpDeliveryNoteDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"delivery_note_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("delivery_note_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"delivery_note_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NotesExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("DeliveredAt", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PDF path omitted", razor, StringComparison.Ordinal);
        Assert.Contains("Create stays on the Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("pdf_path", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/delivery-notes-app?delivery_note_id=9#erp-row-9",
            ErpRecordOpen.Href("/erp/delivery-notes-app", "delivery_note_id", 9));
        Assert.Equal(
            "/erp/delivery-notes-app?delivery_note_id=9",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/delivery-notes-app",
                "/ERP/?epc_erp_shell=1&area=sales&tab=delivery_notes&delivery_note_id=9"));
    }

    [Fact]
    public void PaymentBatchesApp_OpenLoadsNotesExcerptAndKeepsErpChrome()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpPaymentBatchesApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpPaymentBatchDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"batch_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("batch_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"batch_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NotesExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Save stays on the Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/payment-batches-app?batch_id=10#erp-row-10",
            ErpRecordOpen.Href("/erp/payment-batches-app", "batch_id", 10));
        Assert.Equal(
            "/erp/payment-batches-app?batch_id=10",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/payment-batches-app",
                "/ERP/?epc_erp_shell=1&area=banking&tab=payment_batches&batch_id=10"));
    }

    [Fact]
    public void ExpenseReportsApp_OpenLoadsNotesExcerptAndKeepsErpChrome()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpExpenseReportsApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpExpenseReportDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"expense_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("expense_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"expense_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NotesExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("PeriodFrom", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Save stays on the Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/expense-reports-app?expense_id=11#erp-row-11",
            ErpRecordOpen.Href("/erp/expense-reports-app", "expense_id", 11));
        Assert.Equal(
            "/erp/expense-reports-app?expense_id=11",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/expense-reports-app",
                "/ERP/?epc_erp_shell=1&area=people&tab=expense_reports&expense_id=11"));
    }

    [Fact]
    public void AgendaApp_OpenLoadsNotesExcerptAndKeepsErpChrome()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpAgendaApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpAgendaEventDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"event_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("event_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"event_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NotesExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-type siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Add event writes here", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/agenda/events/save", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/agenda-app?event_id=12#erp-row-12",
            ErpRecordOpen.Href("/erp/agenda-app", "event_id", 12));
        Assert.Equal(
            "/erp/agenda-app?event_id=12",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/agenda-app",
                "/ERP/?epc_erp_shell=1&area=common&tab=agenda&event_id=12"));
    }

    [Fact]
    public void DocumentsApp_OpenLoadsNotesExcerptAndOmitsFilePath()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpDocumentsApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpDocumentDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"document_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("document_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"document_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NotesExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-category siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("File path omitted", razor, StringComparison.Ordinal);
        Assert.Contains("Upload stays on the Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("file_path", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("FilePath", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/documents-app?document_id=13#erp-row-13",
            ErpRecordOpen.Href("/erp/documents-app", "document_id", 13));
        Assert.Equal(
            "/erp/documents-app?document_id=13",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/documents-app",
                "/ERP/?epc_erp_shell=1&area=common&tab=documents&document_id=13"));
    }

    [Fact]
    public void PrintDesignerApp_OpenLoadsHtmlCssExcerptsAndKeepsErpChrome()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpPrintDesignerApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpPrintTemplateDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"template_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("template_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"template_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("HeaderHtmlExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("FooterHtmlExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("CustomCssExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-type siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Save writes here", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/print-designer/save", razor, StringComparison.Ordinal);
        Assert.Contains("ErpPhpCreateWell", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/print-designer-app?template_id=14#erp-row-14",
            ErpRecordOpen.Href("/erp/print-designer-app", "template_id", 14));
        Assert.Equal(
            "/erp/print-designer-app?template_id=14",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/print-designer-app",
                "/ERP/?epc_erp_shell=1&area=setup&tab=print_designer&template_id=14"));
    }

    [Fact]
    public void OpeningApp_OpenLoadsNoteExcerptAndOmitsLineMeta()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpOpeningApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpOpeningBatchDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"batch_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("batch_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"batch_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NoteExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Line JSON omitted", razor, StringComparison.Ordinal);
        Assert.Contains("Create and add-line write here", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/opening/create-batch", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/opening/add-coa-line", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/opening/add-inv-line", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("meta_json", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/opening-app?batch_id=15#erp-row-15",
            ErpRecordOpen.Href("/erp/opening-app", "batch_id", 15));
        Assert.Equal(
            "/erp/opening-app?batch_id=15",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/opening-app",
                "/ERP/?epc_erp_shell=1&area=finance&tab=opening&batch_id=15"));
    }

    [Fact]
    public void PeriodCloseApp_OpenLoadsNoteExcerptAndOmitsChecklist()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpPeriodCloseApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpFiscalPeriodDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"period_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("period_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"period_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NoteExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Checklist JSON omitted", razor, StringComparison.Ordinal);
        Assert.Contains("Reopen and status writes stay here", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/periods/soft-close", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/fy-reopen", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("checklist_json", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/period-close-app?period_id=16#erp-row-16",
            ErpRecordOpen.Href("/erp/period-close-app", "period_id", 16));
        Assert.Equal(
            "/erp/period-close-app?period_id=16",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/period-close-app",
                "/ERP/?epc_erp_shell=1&area=finance&tab=year_end&period_id=16"));
    }

    [Fact]
    public void ContactsApp_OpenLoadsNotesExcerptAndOmitsEmailPhone()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpContactsApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpContactDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"contact_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("contact_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"contact_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NotesExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("_opened.Address", razor, StringComparison.Ordinal);
        Assert.Contains("_opened.CurrencyCode", razor, StringComparison.Ordinal);
        Assert.Contains("same-city siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Email/phone omitted", razor, StringComparison.Ordinal);
        Assert.Contains("Customer master and address-book writes stay here", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/customers/master-save", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/contacts/party-contacts/save", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/contacts/addresses/save", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/contacts/parties/save", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("_opened.Email", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("_opened.Phone", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/contacts-app?contact_id=17#erp-row-17",
            ErpRecordOpen.Href("/erp/contacts-app", "contact_id", 17));
        Assert.Equal(
            "/erp/contacts-app?contact_id=17",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/contacts-app",
                "/ERP/?epc_erp_shell=1&area=common&tab=contacts&contact_id=17"));
        Assert.Equal(
            "/erp/contacts-app?tab=ar_setup&contact_id=17",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/contacts-app?tab=ar_setup",
                "/ERP/?epc_erp_shell=1&area=ar&tab=ar_setup&contact_id=17"));
    }

    [Fact]
    public void RfidApp_OpenLoadsCompletedTimeAndOmitsReaderSecrets()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpRfidApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpRfidSessionDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"session_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("session_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"session_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("TimeCompleted", razor, StringComparison.Ordinal);
        Assert.Contains("TotalUnexpected", razor, StringComparison.Ordinal);
        Assert.Contains("ScannedBy", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reader IP/TID omitted", razor, StringComparison.Ordinal);
        Assert.Contains("Register, start, and scan write here", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/rfid/register", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/rfid/start-session", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/rfid/scan", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ip_address", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("_opened.RfidTid", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/rfid-app?session_id=18#erp-row-18",
            ErpRecordOpen.Href("/erp/rfid-app", "session_id", 18));
        Assert.Equal(
            "/erp/rfid-app?session_id=18",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/rfid-app",
                "/ERP/?epc_erp_shell=1&area=inventory&tab=rfid&session_id=18"));
    }

    [Fact]
    public void CoaAccountsApp_OpenLoadsDescriptionExcerptAndRemapsByTab()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpCoaAccountsApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpCoaAccountDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"account_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("account_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"account_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("DescriptionExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("SystemFlag", razor, StringComparison.Ordinal);
        Assert.Contains("TimeCreated", razor, StringComparison.Ordinal);
        Assert.Contains("same-type siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Writes stay on the Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/coa-accounts-app?account_id=19#erp-row-19",
            ErpRecordOpen.Href("/erp/coa-accounts-app", "account_id", 19));
        Assert.Equal(
            "/erp/coa-accounts-app?account_id=19",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/coa-accounts-app",
                "/ERP/?epc_erp_shell=1&area=finance&tab=coa&account_id=19"));
        Assert.Equal(
            "/erp/cash-accounts-app?account_id=5",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/cash-accounts-app",
                "/ERP/?epc_erp_shell=1&area=banking&tab=cash_bank&account_id=5"));
    }

    [Fact]
    public void WorkspaceFavoritesApp_OpenLoadsUrlIconAndColor()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpWorkspaceFavoritesApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpWorkspaceFavoriteDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"favorite_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("favorite_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"favorite_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("TargetUrl", razor, StringComparison.Ordinal);
        Assert.Contains("IconClass", razor, StringComparison.Ordinal);
        Assert.Contains("IconColor", razor, StringComparison.Ordinal);
        Assert.Contains("same-surface siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Favourite and shortcut writes stay here", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/erp-fav-add", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/shortcut-add", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/shortcut-delete", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/workspace-favorites-app?favorite_id=20#erp-row-20",
            ErpRecordOpen.Href("/erp/workspace-favorites-app", "favorite_id", 20));
        Assert.Equal(
            "/erp/workspace-favorites-app?favorite_id=20",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/workspace-favorites-app",
                "/ERP/?epc_erp_shell=1&area=overview&tab=favorites&favorite_id=20"));
    }

    [Fact]
    public void BankReconciliationApp_OpenLoadsLineDateAndCreatedTimeAndRemapsByTab()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpBankReconciliationApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpBankReconciliationLineDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"recon_line_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("recon_line_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"recon_line_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("LineDate", razor, StringComparison.Ordinal);
        Assert.Contains("TimeCreated", razor, StringComparison.Ordinal);
        Assert.Contains("same-account siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Match and instrument writes stay here", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/bank-reconciliation/match", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/bank-instruments/save", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/bank-instruments/status", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.Contains("_openedId > 0 || tab != \"bank_instruments\"", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/bank-reconciliation-app?recon_line_id=21#erp-row-21",
            ErpRecordOpen.Href("/erp/bank-reconciliation-app", "recon_line_id", 21));
        Assert.Equal(
            "/erp/bank-reconciliation-app?recon_line_id=21",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/bank-reconciliation-app",
                "/ERP/?epc_erp_shell=1&area=banking&tab=bank_recon&recon_line_id=21"));
        Assert.Equal(
            "/erp/bank-reconciliation-app?tab=bank_instruments",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/bank-reconciliation-app?tab=bank_instruments",
                "/ERP/?epc_erp_shell=1&area=banking&tab=bank_instruments"));
    }

    [Fact]
    public void StockMovementsApp_OpenLoadsNoteExcerptAndRemapsByTab()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpStockMovementsApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpInventoryMovementDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"movement_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("movement_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"movement_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NoteExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("BatchNo", razor, StringComparison.Ordinal);
        Assert.Contains("TotalCost", razor, StringComparison.Ordinal);
        Assert.Contains("same-warehouse siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Writes stay on the Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/stock-movements-app?movement_id=22#erp-row-22",
            ErpRecordOpen.Href("/erp/stock-movements-app", "movement_id", 22));
        Assert.Equal(
            "/erp/stock-movements-app?movement_id=22",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/stock-movements-app",
                "/ERP/?epc_erp_shell=1&area=inventory_mgmt&tab=ledger&movement_id=22"));
        Assert.Equal(
            "/erp/inventory-stock-app?warehouse_id=3",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/inventory-stock-app",
                "/ERP/?epc_erp_shell=1&area=inventory_mgmt&tab=inventory&warehouse_id=3"));
    }

    [Fact]
    public void WarehousesApp_OpenLoadsNameExcerptAndSameActiveSiblings()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpWarehousesApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpWarehouseDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"warehouse_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("warehouse_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"warehouse_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NameExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-active siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Create and transfer stay on the virtual-warehouse tab", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/warehouses-app?warehouse_id=4#erp-row-4",
            ErpRecordOpen.Href("/erp/warehouses-app", "warehouse_id", 4));
        Assert.Equal(
            "/erp/warehouses-app?warehouse_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/warehouses-app",
                "/ERP/?epc_erp_shell=1&area=warehouse&tab=warehouse&warehouse_id=4"));
    }

    [Fact]
    public void ThreeWayMatchApp_OpenLoadsNotesExcerptAndSameStatusSiblings()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpThreeWayMatchApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpThreeWayMatchDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"po_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("po_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"po_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NotesExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Writes stay on the Classic twin", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/three-way-match-app?po_id=9#erp-row-9",
            ErpRecordOpen.Href("/erp/three-way-match-app", "po_id", 9));
        Assert.Equal(
            "/erp/three-way-match-app?po_id=9",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/three-way-match-app",
                "/ERP/?epc_erp_shell=1&area=purchasing&tab=three_way_match&po_id=9"));
    }

    [Fact]
    public void ContractsApp_OpenLoadsBodyAndOcrExcerptsAndSameStatusSiblings()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpContractsApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpContractDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"contract_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("contract_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"contract_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("BodyExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("OcrExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Signature hashes stay off this pane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/contracts-app?contract_id=4#erp-row-4",
            ErpRecordOpen.Href("/erp/contracts-app", "contract_id", 4));
        Assert.Equal(
            "/erp/contracts-app?contract_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/contracts-app",
                "/ERP/?epc_erp_shell=1&area=sales&tab=contracts&contract_id=4"));
    }

    [Fact]
    public void StaffApp_OpenLoadsUserIdCreatedTimeAndSameDepartmentSiblings()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpStaffApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpStaffProfileDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"staff_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("staff_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"staff_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("UserId", razor, StringComparison.Ordinal);
        Assert.Contains("TimeCreated", razor, StringComparison.Ordinal);
        Assert.Contains("same-department siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("email/phone stay off this pane", razor, StringComparison.Ordinal);
        Assert.Contains("Writes stay Classic", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/staff-app?staff_id=6#erp-row-6",
            ErpRecordOpen.Href("/erp/staff-app", "staff_id", 6));
        Assert.Equal(
            "/erp/staff-app?staff_id=6",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/staff-app",
                "/ERP/?epc_erp_shell=1&area=hr&tab=staff&staff_id=6"));
    }

    [Fact]
    public void PayrollApp_OpenLoadsNoteExcerptAndSameStatusSiblings()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpPayrollApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpPayrollRunDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"payroll_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("payroll_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"payroll_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NoteExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Bank details stay off this pane", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/payroll/generate", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/payroll-approve", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/payroll/pay", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/payroll-app?payroll_id=3#erp-row-3",
            ErpRecordOpen.Href("/erp/payroll-app", "payroll_id", 3));
        Assert.Equal(
            "/erp/payroll-app?payroll_id=3",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/payroll-app",
                "/ERP/?epc_erp_shell=1&area=hr&tab=payroll&payroll_id=3"));
    }

    [Fact]
    public void CustomerGroupsApp_OpenLoadsDescriptionExcerptAndSameTypeSiblings()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpCustomerGroupsApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpCustomerGroupDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"cgroup_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("cgroup_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"cgroup_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("DescriptionExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-type siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Create and assign stay on this page", razor, StringComparison.Ordinal);
        Assert.Contains("ErpCustomerGroupsCreateForm", razor, StringComparison.Ordinal);
        Assert.Contains("ErpCustomerGroupsAssignForm", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/customer-groups-app?cgroup_id=5#erp-row-5",
            ErpRecordOpen.Href("/erp/customer-groups-app", "cgroup_id", 5));
        Assert.Equal(
            "/erp/customer-groups-app?cgroup_id=5",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/customer-groups-app",
                "/ERP/?epc_erp_shell=1&area=sales&tab=customer_groups&cgroup_id=5"));
    }

    [Fact]
    public void RecruitmentApp_OpenLoadsNotesExcerptsAndSiblings()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpRecruitmentApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpRecruitmentJobDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpRecruitmentApplicantDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"hrt_job_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"applicant_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("hrt_job_id=", razor, StringComparison.Ordinal);
        Assert.Contains("applicant_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"hrt_job_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"applicant_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NotesExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("same-stage siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("email/phone stay off this pane", razor, StringComparison.Ordinal);
        Assert.Contains("Job save, applicant add, and stage stay on this page", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecruitmentJobSave", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecruitmentApplicantAdd", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/recruitment/applicants/stage", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("_openedApplicant.Email", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("_openedApplicant.Phone", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/recruitment-app?hrt_job_id=4#erp-row-4",
            ErpRecordOpen.Href("/erp/recruitment-app", "hrt_job_id", 4));
        Assert.Equal("/erp/recruitment-app?applicant_id=8#erp-row-8",
            ErpRecordOpen.Href("/erp/recruitment-app", "applicant_id", 8));
        Assert.Equal(
            "/erp/recruitment-app?hrt_job_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/recruitment-app",
                "/ERP/?epc_erp_shell=1&area=hr&tab=recruitment&hrt_job_id=4"));
        Assert.Equal(
            "/erp/recruitment-app?applicant_id=8",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/recruitment-app",
                "/ERP/?epc_erp_shell=1&area=hr&tab=recruitment&applicant_id=8"));
    }

    [Fact]
    public void PerformanceApp_OpenLoadsNotesExcerptAndSameStatusSiblings()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpPerformanceApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpPerformanceReviewDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"hrt_review_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("hrt_review_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"hrt_review_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NotesExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Finalize stays Classic", razor, StringComparison.Ordinal);
        Assert.Contains("ErpPerformanceReviewSave", razor, StringComparison.Ordinal);
        Assert.Contains("ErpPerformanceGoalAdd", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/performance-app?hrt_review_id=6#erp-row-6",
            ErpRecordOpen.Href("/erp/performance-app", "hrt_review_id", 6));
        Assert.Equal(
            "/erp/performance-app?hrt_review_id=6",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/performance-app",
                "/ERP/?epc_erp_shell=1&area=people&tab=performance&hrt_review_id=6"));
    }

    [Fact]
    public void ReportSchedulerApp_OpenLoadsCompanyLastSentAndSameTypeSiblings()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpReportSchedulerApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpReportScheduleDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"rsched_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("rsched_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"rsched_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("CompanyId", razor, StringComparison.Ordinal);
        Assert.Contains("LastSentAt", razor, StringComparison.Ordinal);
        Assert.Contains("same-type siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Recipients and body stay off this pane", razor, StringComparison.Ordinal);
        Assert.Contains("Send and email stay Classic", razor, StringComparison.Ordinal);
        Assert.Contains("ErpReportSchedulerCreateForm", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("_opened.Recipients", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("_opened.Body", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/report-scheduler-app?rsched_id=7#erp-row-7",
            ErpRecordOpen.Href("/erp/report-scheduler-app", "rsched_id", 7));
        Assert.Equal(
            "/erp/report-scheduler-app?rsched_id=7",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/report-scheduler-app",
                "/ERP/?epc_erp_shell=1&area=reports&tab=report_scheduler&rsched_id=7"));
    }

    [Fact]
    public void ProjectAccountingApp_OpenLoadsDetailExcerptAndSameProjectSiblings()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpProjectAccountingApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpPrjaRecognitionDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpPrjaBudgetDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpPrjaTxnDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"prja_rec_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"prja_budget_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"prja_txn_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("prja_rec_id=", razor, StringComparison.Ordinal);
        Assert.Contains("prja_budget_id=", razor, StringComparison.Ordinal);
        Assert.Contains("prja_txn_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"prja_rec_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("DetailExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-project siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Recognition stays Classic", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/project-accounting/budgets/save", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/project-accounting/txns/add", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/project-accounting-app?prja_rec_id=5#erp-row-5",
            ErpRecordOpen.Href("/erp/project-accounting-app", "prja_rec_id", 5));
        Assert.Equal(
            "/erp/project-accounting-app?prja_rec_id=5",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/project-accounting-app",
                "/ERP/?epc_erp_shell=1&area=projects&tab=project_accounting&prja_rec_id=5"));
        Assert.Equal("/erp/project-accounting-app?prja_budget_id=3#erp-row-3",
            ErpRecordOpen.Href("/erp/project-accounting-app", "prja_budget_id", 3));
        Assert.Equal("/erp/project-accounting-app?prja_txn_id=4#erp-row-4",
            ErpRecordOpen.Href("/erp/project-accounting-app", "prja_txn_id", 4));
    }

    [Fact]
    public void ProductInfoApp_OpenLoadsTrackExpiryOptionsComboAndSiblings()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpProductInfoApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpProductInfoItemDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpProductInfoFieldDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpProductInfoVariantDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"pm_item_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"pm_field_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"pm_variant_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("pm_item_id=", razor, StringComparison.Ordinal);
        Assert.Contains("pm_field_id=", razor, StringComparison.Ordinal);
        Assert.Contains("pm_variant_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"pm_item_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"pm_field_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"pm_variant_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("TrackExpiry", razor, StringComparison.Ordinal);
        Assert.Contains("OptionsExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("ComboExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-type siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("same-item siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Dimension-link save stays Classic", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/inv-create-item", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/product-info-app?pm_item_id=9#erp-row-9",
            ErpRecordOpen.Href("/erp/product-info-app", "pm_item_id", 9));
        Assert.Equal(
            "/erp/product-info-app?pm_item_id=9",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/product-info-app",
                "/ERP/?epc_erp_shell=1&area=pim&tab=product_info&pm_item_id=9"));
        Assert.Equal("/erp/product-info-app?pm_field_id=3#erp-row-3",
            ErpRecordOpen.Href("/erp/product-info-app", "pm_field_id", 3));
        Assert.Equal("/erp/product-info-app?pm_variant_id=4#erp-row-4",
            ErpRecordOpen.Href("/erp/product-info-app", "pm_variant_id", 4));
    }

    [Fact]
    public void InventoryReportApp_OpenLoadsCompanyIdAndSiblings()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpInventoryReportApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpInventoryReportCategoryDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpInventoryReportSnapshotDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"invrep_cat_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"invrep_snap_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("invrep_cat_id=", razor, StringComparison.Ordinal);
        Assert.Contains("invrep_snap_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"invrep_cat_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"invrep_snap_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("CompanyId", razor, StringComparison.Ordinal);
        Assert.Contains("same-level siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("same-category siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Snapshot generate stays Classic", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/inventory-report-app?invrep_cat_id=8#erp-row-8",
            ErpRecordOpen.Href("/erp/inventory-report-app", "invrep_cat_id", 8));
        Assert.Equal(
            "/erp/inventory-report-app?invrep_cat_id=8",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/inventory-report-app",
                "/ERP/?epc_erp_shell=1&area=inventory&tab=inventory_report&invrep_cat_id=8"));
        Assert.Equal("/erp/inventory-report-app?invrep_snap_id=5#erp-row-5",
            ErpRecordOpen.Href("/erp/inventory-report-app", "invrep_snap_id", 5));
    }

    [Fact]
    public void MultiEntityApp_OpenLoadsCreatedMembersNoteAndSiblings()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpMultiEntityApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpEntityGroupDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpIntercompanyTxnDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"me_group_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"me_ic_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("me_group_id=", razor, StringComparison.Ordinal);
        Assert.Contains("me_ic_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"me_group_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"me_ic_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("CreatedAt", razor, StringComparison.Ordinal);
        Assert.Contains("DescriptionExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Consolidated trial balance stays Classic", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/multi-entity/write", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/multi-entity-app?me_group_id=6#erp-row-6",
            ErpRecordOpen.Href("/erp/multi-entity-app", "me_group_id", 6));
        Assert.Equal(
            "/erp/multi-entity-app?me_group_id=6",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/multi-entity-app",
                "/ERP/?epc_erp_shell=1&area=finance&tab=multi_entity&me_group_id=6"));
        Assert.Equal("/erp/multi-entity-app?me_ic_id=4#erp-row-4",
            ErpRecordOpen.Href("/erp/multi-entity-app", "me_ic_id", 4));
    }

    [Fact]
    public void MultiCurrencyGlApp_OpenLoadsCreatedNoteSiteAndSiblings()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpMultiCurrencyGlApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpFxRateDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpGlCurrencyEntryDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"mcgl_rate_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"mcgl_entry_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("mcgl_rate_id=", razor, StringComparison.Ordinal);
        Assert.Contains("mcgl_entry_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"mcgl_rate_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"mcgl_entry_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("CreatedAt", razor, StringComparison.Ordinal);
        Assert.Contains("SiteKey", razor, StringComparison.Ordinal);
        Assert.Contains("DescriptionExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-pair siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("same-type siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Revaluation stays Classic", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/multi-currency-gl/set-rate", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/currency/set-rate", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/multi-currency-gl-app?mcgl_rate_id=3#erp-row-3",
            ErpRecordOpen.Href("/erp/multi-currency-gl-app", "mcgl_rate_id", 3));
        Assert.Equal(
            "/erp/multi-currency-gl-app?mcgl_rate_id=3",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/multi-currency-gl-app",
                "/ERP/?epc_erp_shell=1&area=finance&tab=multi_currency_gl&mcgl_rate_id=3"));
        Assert.Equal("/erp/multi-currency-gl-app?mcgl_entry_id=8#erp-row-8",
            ErpRecordOpen.Href("/erp/multi-currency-gl-app", "mcgl_entry_id", 8));
    }

    [Fact]
    public void ProcurementCategoriesApp_OpenLoadsCompanyAndSiblings()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpProcurementCategoriesApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpProcCategoryDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpProcPolicyDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"proc_cat_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"proc_pol_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("proc_cat_id=", razor, StringComparison.Ordinal);
        Assert.Contains("proc_pol_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"proc_cat_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"proc_pol_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("CompanyId", razor, StringComparison.Ordinal);
        Assert.Contains("same-parent siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("same-category siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ErpProcurementCategorySave", razor, StringComparison.Ordinal);
        Assert.Contains("ErpProcurementPolicySave", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/procurement-categories-app?proc_cat_id=7#erp-row-7",
            ErpRecordOpen.Href("/erp/procurement-categories-app", "proc_cat_id", 7));
        Assert.Equal(
            "/erp/procurement-categories-app?proc_cat_id=7",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/procurement-categories-app",
                "/ERP/?epc_erp_shell=1&area=purchasing&tab=procurement_categories&proc_cat_id=7"));
        Assert.Equal("/erp/procurement-categories-app?proc_pol_id=4#erp-row-4",
            ErpRecordOpen.Href("/erp/procurement-categories-app", "proc_pol_id", 4));
    }

    [Fact]
    public void InventoryForecastApp_OpenLoadsSiteLeadSafetyEoqAndKeepsRecompute()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpInventoryForecastApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpInventoryForecastDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"inv_forecast_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("inv_forecast_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"inv_forecast_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("SiteKey", razor, StringComparison.Ordinal);
        Assert.Contains("LeadTimeDays", razor, StringComparison.Ordinal);
        Assert.Contains("SafetyStock", razor, StringComparison.Ordinal);
        Assert.Contains("Eoq", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Recompute writes stay here", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/inventory-forecast/recompute", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref(_phpTab)", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/inventory-forecast-app?inv_forecast_id=23#erp-row-23",
            ErpRecordOpen.Href("/erp/inventory-forecast-app", "inv_forecast_id", 23));
        Assert.Equal(
            "/erp/inventory-forecast-app?inv_forecast_id=23",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/inventory-forecast-app",
                "/CP/shop/finance/epc_inventory_forecast?inv_forecast_id=23"));
        Assert.Equal(
            "/erp/cash-accounts-app?tab=cash_forecast",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/cash-accounts-app?tab=cash_forecast",
                "/ERP/?epc_erp_shell=1&area=banking&tab=cash_forecast"));
    }

    [Fact]
    public void QualityApp_OpenLoadsCorrectiveExcerptAndRemapsByNcrView()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpQualityApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpQualityNcrDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"ncr_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("ncr_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"ncr_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("ActionExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("TimeClosed", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NCR update writes stay here", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/quality/ncr-update", razor, StringComparison.Ordinal);
        Assert.Contains("qv=orders&amp;order_id=", razor, StringComparison.Ordinal);
        Assert.Contains("qv=plans&amp;plan_id=", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("company_id", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/quality-app?qv=ncr&ncr_id=24#erp-row-24",
            ErpRecordOpen.Href("/erp/quality-app?qv=ncr", "ncr_id", 24));
        Assert.Equal(
            "/erp/quality-app?ncr_id=24",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/quality-app",
                "/ERP/?epc_erp_shell=1&area=production&tab=quality&ncr_id=24"));
        Assert.Equal(
            "/erp/quality-app?order_id=5",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/quality-app",
                "/ERP/?epc_erp_shell=1&area=production&tab=quality&order_id=5"));
    }

    [Fact]
    public void OrderPipelineApp_OpenLoadsDetailsExcerptAndKeepsExecutionPhp()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpOrderPipelineApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpOrderPipelineLogDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"pipeline_log_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("pipeline_log_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"pipeline_log_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("DetailsExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("same-order siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Full details JSON omitted", razor, StringComparison.Ordinal);
        Assert.Contains("Pipeline execution stays PHP", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref(_phpTab)", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/order-pipeline-app?pipeline_log_id=25#erp-row-25",
            ErpRecordOpen.Href("/erp/order-pipeline-app", "pipeline_log_id", 25));
        Assert.Equal(
            "/erp/order-pipeline-app?pipeline_log_id=25",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/order-pipeline-app",
                "/CP/shop/finance/epc_order_erp_pipeline?pipeline_log_id=25"));
        Assert.Equal(
            "/erp/sales-orders-app?order_id=42",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/sales-orders-app",
                "/ERP/?epc_erp_shell=1&area=sales&tab=sales_orders&order_id=42"));
    }

    [Fact]
    public void DocAttachmentsApp_OpenLoadsDescriptionExcerptAndOmitsStorage()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpDocAttachmentsApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpDocAttachmentDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"attach_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("attach_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"attach_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("DescriptionExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("TimeCreated", razor, StringComparison.Ordinal);
        Assert.Contains("same-type siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Storage location omitted", razor, StringComparison.Ordinal);
        Assert.Contains("document_upload", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("file_path", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("FilePath", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/doc-attachments-app?attach_id=26#erp-row-26",
            ErpRecordOpen.Href("/erp/doc-attachments-app", "attach_id", 26));
        Assert.Equal(
            "/erp/doc-attachments-app?attach_id=26",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/doc-attachments-app",
                "/ERP/?epc_erp_shell=1&area=common&tab=doc_attachment&attach_id=26"));
        Assert.Equal(
            "/erp/documents-app?document_id=13",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/documents-app",
                "/ERP/?epc_erp_shell=1&area=common&tab=documents&document_id=13"));
    }

    [Fact]
    public void OrderPlanningApp_OpenLoadsItemIdAndUpdatedTime()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/ErpOrderPlanningApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildErpOrderPlanningRecommendationDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"opl_rec_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("opl_rec_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"opl_rec_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("TimeUpdated", razor, StringComparison.Ordinal);
        Assert.Contains("Item id", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("opl_set_status", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpParityModuleBody", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/erp/order-planning-app?opl_rec_id=27#erp-row-27",
            ErpRecordOpen.Href("/erp/order-planning-app", "opl_rec_id", 27));
        Assert.Equal(
            "/erp/order-planning-app?tab=master_planning&opl_rec_id=27#erp-row-27",
            ErpRecordOpen.Href("/erp/order-planning-app?tab=master_planning", "opl_rec_id", 27));
        Assert.Equal(
            "/erp/order-planning-app?opl_rec_id=27",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/order-planning-app",
                "/ERP/?epc_erp_shell=1&area=planning&tab=order_planning&opl_rec_id=27"));
        Assert.Equal(
            "/erp/order-planning-app?tab=master_planning&opl_rec_id=27",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/order-planning-app?tab=master_planning",
                "/ERP/?epc_erp_shell=1&area=planning&tab=master_planning&opl_rec_id=27"));
    }

    [Fact]
    public void CrmBoardApp_OpenLoadsNotesExcerptAndOmitsContact()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpCrmBoardApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildCpCrmLeadDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"lead_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("lead_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"lead_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NotesExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("ContactName", razor, StringComparison.Ordinal);
        Assert.Contains("Email/phone omitted", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref(_phpTab)", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("_opened.Email", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("_opened.Phone", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w16-hero", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/cp/crm-board-app?lead_id=6#erp-row-6",
            ErpRecordOpen.Href("/cp/crm-board-app", "lead_id", 6));
        Assert.Equal(
            "/cp/crm-board-app?lead_id=6",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/crm-board-app",
                "/CP/shop/crm/crm_main?lead_id=6"));
    }

    [Fact]
    public void JewelleryFixingApp_OpenLoadsRemarksExcerptAndKeepsWrites()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryFixingApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildCpJewelleryFixingDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"fixing_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("fixing_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"fixing_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("RemarksExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("PartyName", razor, StringComparison.Ordinal);
        Assert.Contains("RateType", razor, StringComparison.Ordinal);
        Assert.Contains("FixRate", razor, StringComparison.Ordinal);
        Assert.Contains("UnfixedQty", razor, StringComparison.Ordinal);
        Assert.Contains("ReferenceVoc", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ErpJewelleryFixingSaveForm", razor, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryFixUnfixCreateForm", razor, StringComparison.Ordinal);
        Assert.Contains("ErpJewelleryFixUnfixSettleForm", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("table-epc", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w16-hero", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/cp/jewellery-fixing-app?fixing_id=4#erp-row-4",
            ErpRecordOpen.Href("/cp/jewellery-fixing-app", "fixing_id", 4));
        Assert.Equal(
            "/erp/jewellery-fixing-app?fixing_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/jewellery-fixing-app",
                "/ERP/?epc_erp_shell=1&area=purchasing&tab=jw_purchase_fixing&fixing_id=4"));
        Assert.Equal(
            "/cp/jewellery-fixing-app?fixing_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/jewellery-fixing-app",
                "/CP/shop/finance/erp?area=purchasing&tab=jw_purchase_fixing&epc_erp_shell=1&fixing_id=4"));
    }

    [Fact]
    public void WorkshopApp_OpenLoadsNotesExcerptAndKeepsWrites()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpWorkshopApp.razor"));
        Assert.Contains("ErpOpenedRecordBanner", razor, StringComparison.Ordinal);
        Assert.Contains("BuildCpWorkshopDetailAsync", razor, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"job_id\")", razor, StringComparison.Ordinal);
        Assert.Contains("job_id=", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"job_id\"", razor, StringComparison.Ordinal);
        Assert.Contains("NotesExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("ComplaintExcerpt", razor, StringComparison.Ordinal);
        Assert.Contains("_opened.Vin", razor, StringComparison.Ordinal);
        Assert.Contains("_opened.Odometer", razor, StringComparison.Ordinal);
        Assert.Contains("EstimateApproved", razor, StringComparison.Ordinal);
        Assert.Contains("UnderWarranty", razor, StringComparison.Ordinal);
        Assert.Contains("PartsTotal", razor, StringComparison.Ordinal);
        Assert.Contains("LabourTotal", razor, StringComparison.Ordinal);
        Assert.Contains("TaxTotal", razor, StringComparison.Ordinal);
        Assert.Contains("TimePromised", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("_opened.CustomerPhone", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("_opened.CustomerEmail", razor, StringComparison.Ordinal);
        Assert.Contains("Phone/email omitted", razor, StringComparison.Ordinal);
        Assert.Contains("same-status siblings", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"assign\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_bay\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_tech\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"set_status\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"create_job\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"add_line\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"create_appointment\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"convert_appointment\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", razor, StringComparison.Ordinal);
        Assert.Contains("epc-scp-kpi", razor, StringComparison.Ordinal);
        Assert.Contains("epc-scp-table-card", razor, StringComparison.Ordinal);
        Assert.Contains("epc-scp-data-table", razor, StringComparison.Ordinal);
        Assert.Contains("PhpCpModulePageHeader", razor, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref(_phpTab)", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w16-hero", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", razor, StringComparison.Ordinal);

        Assert.Equal("/cp/workshop-app?job_id=5#erp-row-5",
            ErpRecordOpen.Href("/cp/workshop-app", "job_id", 5));
        Assert.Equal(
            "/cp/workshop-app?job_id=5",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/workshop-app",
                "/CP/shop/workshop?job_id=5"));
    }

    [Fact]
    public void CreditLimitsApp_OpenLoadsNotesExcerptAndKeepsWrites()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpCreditLimitsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"credit_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"credit_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpCreditLimitsDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No notes excerpt yet.", text, StringComparison.Ordinal);
        Assert.Contains("No same-status siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", text, StringComparison.Ordinal);
        Assert.Contains("/cp/credit-limits/set", text, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref(_phpTab)", text, StringComparison.Ordinal);
        Assert.Contains("credit_id=", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w16-hero", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);

        Assert.Equal("/cp/credit-limits-app?credit_id=7#erp-row-7",
            ErpRecordOpen.Href("/cp/credit-limits-app", "credit_id", 7));
        Assert.Equal(
            "/cp/credit-limits-app?credit_id=7",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/credit-limits-app",
                "/CP/shop/finance/epc_credit_limit?credit_id=7"));
    }

    [Fact]
    public void DocExpiryApp_OpenLoadsDetailAndAcceptsPhpDoc()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpDocExpiryApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"doc\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"doc\", \"document_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpDocExpiryDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No reminders yet.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WithholdingApp_OpenLoadsDetailAndAcceptsPhpTxnId()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/ErpWithholdingApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"txn_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"txn_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildErpWithholdingTxnDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("none yet.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
    }

    [Fact]
    public void InsuranceComplianceApp_OpenLoadsDetailAndAcceptsPhpPol()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpInsuranceComplianceApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"pol\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"pol\", \"policy_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpInsuranceComplianceDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No documents yet.", text, StringComparison.Ordinal);
        Assert.Contains("No claims yet.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ReturnsRmaApp_OpenLoadsDetailAndAcceptsPhpReturnId()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpReturnsRmaApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"rma_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"rma_id\")", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"return_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpReturnsRmaDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpShopReturnDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No items yet.", text, StringComparison.Ordinal);
        Assert.Contains("No lines yet.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
    }

    [Fact]
    public void MarketingBroadcastApp_OpenLoadsBodyAndSendLog()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpMarketingBroadcastApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"campaign_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"campaign_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpMarketingBroadcastDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No body yet.", text, StringComparison.Ordinal);
        Assert.Contains("No log yet.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
    }

    [Fact]
    public void DataMigrationsApp_OpenLoadsMappingErrorsAndLines()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpDataMigrationsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"migration_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"migration_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpDataMigrationsDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No mapping yet.", text, StringComparison.Ordinal);
        Assert.Contains("No rows yet.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadId_AcceptsReqIdAndPhpRqAlias()
    {
        Assert.Equal("/erp/purchase-requests-app?req_id=9#erp-row-9",
            ErpRecordOpen.Href("/erp/purchase-requests-app", "req_id", 9));
        Assert.Equal(
            "/erp/purchase-requests-app?rq=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/purchase-requests-app",
                "/ERP/?epc_erp_shell=1&area=purchasing&tab=purchase_requisitions&rq=4"));
        Assert.Equal(
            "/erp/purchase-requests-app?req_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/purchase-requests-app",
                "/ERP/?epc_erp_shell=1&area=purchasing&tab=purchase_requisitions&req_id=4"));
        Assert.Equal("/cp/quote-requests-app?quote_id=15#erp-row-15",
            ErpRecordOpen.Href("/cp/quote-requests-app", "quote_id", 15));
        Assert.Equal(
            "/cp/quote-requests-app?quote_id=15",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/quote-requests-app",
                "/CP/shop/quote-requests?quote_id=15"));
        Assert.Equal("/cp/collections-dunning-app?queue_id=12#erp-row-12",
            ErpRecordOpen.Href("/cp/collections-dunning-app", "queue_id", 12));
        Assert.Equal(
            "/cp/collections-dunning-app?queue_id=12",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/collections-dunning-app",
                "/CP/shop/finance/epc_collections_dunning?queue_id=12"));
        Assert.Equal("/cp/api-clients-app?api_client_id=7#erp-row-7",
            ErpRecordOpen.Href("/cp/api-clients-app", "api_client_id", 7));
        Assert.Equal(
            "/cp/api-clients-app?api_client_id=7",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/api-clients-app",
                "/CP/control/portal/epc_api_clients_manage?api_client_id=7"));
        Assert.Equal("/cp/marketing-broadcast-app?campaign_id=11#erp-row-11",
            ErpRecordOpen.Href("/cp/marketing-broadcast-app", "campaign_id", 11));
        Assert.Equal(
            "/cp/marketing-broadcast-app?campaign_id=11",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/marketing-broadcast-app",
                "/CP/control/portal/epc_marketing_broadcast?campaign_id=11"));
        Assert.Equal("/erp/data-migrations-app?migration_id=9#erp-row-9",
            ErpRecordOpen.Href("/erp/data-migrations-app", "migration_id", 9));
        Assert.Equal(
            "/erp/data-migrations-app?migration_id=9",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/data-migrations-app",
                "/ERP/?epc_erp_shell=1&area=setup&tab=data_import&migration_id=9"));
        Assert.Equal(
            "/cp/data-migrations-app?migration_id=9",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/data-migrations-app",
                "/CP/control/portal/epc_db_migrations?migration_id=9"));
    }

    [Fact]
    public void ApiClientsApp_OpenLoadsAllowedActionsAndPrefillsToggle()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpApiClientsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"api_client_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"api_client_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpApiClientDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No allowed actions yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("/cp/api-clients/toggle", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("client_key_hash", text, StringComparison.Ordinal);

        Assert.Equal("/cp/finance-close-app?batch_id=5#erp-row-5",
            ErpRecordOpen.Href("/cp/finance-close-app", "batch_id", 5));
        Assert.Equal(
            "/cp/finance-close-app?batch_id=5",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/finance-close-app",
                "/cp/finance-close-app?batch_id=5"));
    }

    [Fact]
    public void FinanceCloseApp_OpenLoadsNoteAndOpeningLines()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpFinanceCloseApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"batch_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"batch_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpFinanceCloseBatchDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No lines yet.", text, StringComparison.Ordinal);

        Assert.Equal("/erp/cost-models-app?costm_id=3#erp-row-3",
            ErpRecordOpen.Href("/erp/cost-models-app", "costm_id", 3));
        Assert.Equal(
            "/erp/cost-models-app?costm_id=3",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/cost-models-app",
                "/ERP/?epc_erp_shell=1&area=cost_mgmt&tab=cost_models&costm_id=3"));
    }

    [Fact]
    public void CostModelsApp_OpenLoadsTxnsAndCloseDetail()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpCostModelsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"costm_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"costm_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpCostModelItemDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No transactions yet.", text, StringComparison.Ordinal);
        Assert.Contains("No closes yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("/erp/cost-models/txns/add", text, StringComparison.Ordinal);
        Assert.Contains("/erp/cost-models/items/set", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);

        Assert.Equal("/erp/electronic-reporting-app?format_id=6#erp-row-6",
            ErpRecordOpen.Href("/erp/electronic-reporting-app", "format_id", 6));
        Assert.Equal(
            "/erp/electronic-reporting-app?format_id=6",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/electronic-reporting-app",
                "/ERP/?epc_erp_shell=1&area=tax&tab=elec_reporting&format_id=6"));
    }

    [Fact]
    public void ElectronicReportingApp_OpenLoadsFieldsAndRunPreviews()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpElectronicReportingApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"format_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"format_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpElectronicReportingFormatDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No fields yet.", text, StringComparison.Ordinal);
        Assert.Contains("No runs yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("/erp/electronic-reporting/formats/save", text, StringComparison.Ordinal);
        Assert.Contains("/erp/electronic-reporting/fields/add", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);

        Assert.Equal("/cp/einvoice-documents-app?ei_id=8#erp-row-8",
            ErpRecordOpen.Href("/cp/einvoice-documents-app", "ei_id", 8));
        Assert.Equal(
            "/cp/einvoice-documents-app?ei_id=8",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/einvoice-documents-app",
                "/CP/shop/finance/epc_einvoice?ei_id=8"));
        Assert.Equal(
            "/erp/einvoice-documents-app?ei_id=8",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/einvoice-documents-app",
                "/ERP/?epc_erp_shell=1&area=tax&tab=einvoice&ei_id=8"));
    }

    [Fact]
    public void EinvoiceDocumentsApp_OpenLoadsPayloadsLinesAndEvents()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpEinvoiceDocumentsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"ei_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"ei_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpEinvoiceDocumentDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No lines yet.", text, StringComparison.Ordinal);
        Assert.Contains("No events yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("/erp/ajax/einvoice-save-seller", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-ei-hero", text, StringComparison.Ordinal);

        Assert.Equal("/erp/projects-overview-app?project_id=4#erp-row-4",
            ErpRecordOpen.Href("/erp/projects-overview-app", "project_id", 4));
        Assert.Equal(
            "/erp/projects-overview-app?project_id=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/projects-overview-app",
                "/ERP/?epc_erp_shell=1&area=projects&tab=projects&project_id=4"));
    }

    [Fact]
    public void ProjectsOverviewApp_OpenLoadsBudgetTasksAndTimesheets()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpProjectsOverviewApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"project_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"project_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpProjectDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No tasks yet.", text, StringComparison.Ordinal);
        Assert.Contains("No timesheets yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("/erp/projects/save", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-prj-hero", text, StringComparison.Ordinal);

        Assert.Equal("/erp/production-overview-app?wo_id=7#erp-row-7",
            ErpRecordOpen.Href("/erp/production-overview-app", "wo_id", 7));
        Assert.Equal(
            "/erp/production-overview-app?wo_id=7",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/production-overview-app",
                "/ERP/?epc_erp_shell=1&area=production&tab=manufacturing&wo_id=7"));
    }

    [Fact]
    public void ProductionOverviewApp_OpenLoadsCostsAndBomLines()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpProductionOverviewApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"wo_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"wo_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpProductionWorkOrderDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No BOM lines yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("/erp/manufacturing/work-orders/create", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-mfg-hero", text, StringComparison.Ordinal);

        Assert.Equal("/cp/accessories-app?listing_id=9#erp-row-9",
            ErpRecordOpen.Href("/cp/accessories-app", "listing_id", 9));
        Assert.Equal(
            "/cp/accessories-app?listing_id=9",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/accessories-app",
                "/CP/shop/accessories?listing_id=9"));
    }

    [Fact]
    public void AccessoriesApp_OpenLoadsDescriptionAndPhotos()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpAccessoriesApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"listing_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"listing_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpAccessoriesListingDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No description yet.", text, StringComparison.Ordinal);
        Assert.Contains("No photos yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("/cp/accessories/listings/write", text, StringComparison.Ordinal);

        Assert.Equal("/cp/crosses-app?cross_id=14#erp-row-14",
            ErpRecordOpen.Href("/cp/crosses-app", "cross_id", 14));
        Assert.Equal(
            "/cp/crosses-app?cross_id=14",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/crosses-app",
                "/CP/shop/crosses?cross_id=14"));
    }

    [Fact]
    public void CrossesApp_OpenLoadsPairAndSiblings()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpCrossesApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"cross_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"cross_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpCrossPairDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No siblings yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("/cp/crosses/write", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-x-hero", text, StringComparison.Ordinal);

        Assert.Equal("/cp/synonyms-app?manufacturer_id=5#erp-row-5",
            ErpRecordOpen.Href("/cp/synonyms-app", "manufacturer_id", 5));
        Assert.Equal(
            "/cp/synonyms-app?manufacturer_id=5",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/synonyms-app",
                "/CP/shop/manufacturers_synonyms?manufacturer_id=5"));
    }

    [Fact]
    public void SynonymsApp_OpenLoadsManufacturerAndSynonymIds()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpSynonymsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"manufacturer_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"manufacturer_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpSynonymDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No synonyms yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("/cp/synonyms/write", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-nw-hero", text, StringComparison.Ordinal);



        Assert.Equal("/cp/promotions-app?promo_id=6#erp-row-6",
            ErpRecordOpen.Href("/cp/promotions-app", "promo_id", 6));
        Assert.Equal(
            "/cp/promotions-app?promo_id=6",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/promotions-app",
                "/CP/control/portal/epc_promotions_engine?promo_id=6"));
    }

    [Fact]
    public void PromotionsApp_OpenLoadsValidityWindow()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpPromotionsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"promo_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"promo_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpPromotionDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No validity window yet.", text, StringComparison.Ordinal);

        Assert.Equal("/cp/page-builder-app?layout_id=8#erp-row-8",
            ErpRecordOpen.Href("/cp/page-builder-app", "layout_id", 8));
        Assert.Equal(
            "/cp/page-builder-app?layout_id=8",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/page-builder-app",
                "/CP/control/portal/epc_visual_page_editor?layout_id=8"));
    }

    [Fact]
    public void PageBuilderApp_OpenLoadsLayoutAndBrandJson()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpPageBuilderApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"layout_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"layout_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpPageBuilderLayoutDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No brand JSON yet.", text, StringComparison.Ordinal);
        Assert.Contains("No layout JSON yet.", text, StringComparison.Ordinal);

        Assert.Equal("/cp/tax-toolkits-app?toolkit_id=3#erp-row-3",
            ErpRecordOpen.Href("/cp/tax-toolkits-app", "toolkit_id", 3));
        Assert.Equal(
            "/cp/tax-toolkits-app?toolkit_id=3",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/tax-toolkits-app",
                "/CP/control/portal/epc_tax_toolkit_manage?toolkit_id=3"));
    }

    [Fact]
    public void TaxToolkitsApp_OpenLoadsRulesInstallsAndUpdates()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpTaxToolkitsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"toolkit_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"toolkit_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpTaxToolkitDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No installs yet.", text, StringComparison.Ordinal);
        Assert.Contains("No updates yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);

        Assert.Equal("/erp/landed-cost-app?sheet_id=6#erp-row-6",
            ErpRecordOpen.Href("/erp/landed-cost-app", "sheet_id", 6));
        Assert.Equal(
            "/erp/landed-cost-app?sheet_id=6",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/landed-cost-app",
                "/ERP/?epc_erp_shell=1&area=landed_cost_area&tab=landed_cost&sheet_id=6"));
        Assert.Equal(
            "/cp/landed-cost-app?sheet_id=6",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/landed-cost-app",
                "/ERP/?epc_erp_shell=1&area=landed_cost_area&tab=landed_cost&sheet_id=6"));
    }

    [Fact]
    public void LandedCostApp_OpenLoadsSheetDetailExpensesAndLines()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpLandedCostApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"sheet_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"sheet_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpLandedCostSheetDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No expenses yet.", text, StringComparison.Ordinal);
        Assert.Contains("No lines yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-lc-hero", text, StringComparison.Ordinal);

        Assert.Equal("/cp/soc2-compliance-app?soc2_id=8#erp-row-8",
            ErpRecordOpen.Href("/cp/soc2-compliance-app", "soc2_id", 8));
        Assert.Equal(
            "/cp/soc2-compliance-app?soc2_id=8",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/soc2-compliance-app",
                "/CP/control/portal/epc_soc2_compliance?soc2_id=8"));
        Assert.Equal(
            "/erp/soc2-compliance-app?soc2_id=8",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/soc2-compliance-app",
                "/ERP/?epc_erp_shell=1&area=tax&tab=compliance&soc2_id=8"));
    }

    [Fact]
    public void Soc2ComplianceApp_OpenLoadsControlDetailAndEvidence()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpSoc2ComplianceApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"soc2_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"soc2_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpSoc2ControlDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No evidence yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("/erp/compliance/obligations/add", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-soc2-hero", text, StringComparison.Ordinal);

        Assert.Equal("/cp/abandoned-carts-app?cart_id=15#erp-row-15",
            ErpRecordOpen.Href("/cp/abandoned-carts-app", "cart_id", 15));
        Assert.Equal(
            "/cp/abandoned-carts-app?cart_id=15",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/abandoned-carts-app",
                "/CP/shop/orders/carts?cart_id=15"));
    }

    [Fact]
    public void AbandonedCartsApp_OpenLoadsLineDetailAndSiblings()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpAbandonedCartsApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"cart_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"cart_id\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpAbandonedCartsDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No sibling lines yet.", text, StringComparison.Ordinal);

        Assert.Equal("/erp/aml-compliance-app?kyc_id=7#erp-row-7",
            ErpRecordOpen.Href("/erp/aml-compliance-app", "kyc_id", 7));
        Assert.Equal(
            "/erp/aml-compliance-app?kyc_id=7",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/aml-compliance-app",
                "/ERP/?epc_erp_shell=1&area=tax&tab=aml_compliance&kyc_id=7"));
        Assert.Equal(
            "/cp/aml-compliance-app?kyc=7",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/aml-compliance-app",
                "/ERP/?epc_erp_shell=1&area=tax&tab=aml_compliance&kyc=7"));
    }

    [Fact]
    public void AmlComplianceApp_OpenLoadsKycDetailAndAcceptsPhpKycId()
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages/CpAmlComplianceApp.razor"));
        Assert.Contains("ErpRecordOpen.Href(_listHref, \"kyc_id\"", text, StringComparison.Ordinal);
        Assert.Contains("ErpOpenedRecordBanner", text, StringComparison.Ordinal);
        Assert.Contains("ReadId(ctx.Request, \"kyc_id\", \"kyc\")", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpAmlComplianceKycDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("No transactions yet.", text, StringComparison.Ordinal);
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);

        Assert.DoesNotContain("epc-prm-hero", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-pb-hero", text, StringComparison.Ordinal);

        Assert.DoesNotContain("epc-aml-hero", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-w14-hero", text, StringComparison.Ordinal);

        Assert.Equal("/erp/crm-opportunities-app?opp_id=6#erp-row-6",
            ErpRecordOpen.Href("/erp/crm-opportunities-app", "opp_id", 6));
        Assert.Equal(
            "/erp/crm-opportunities-app?opp_id=6",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/crm-opportunities-app",
                "/ERP/?epc_erp_shell=1&area=sales&tab=opportunities&opp_id=6"));
        Assert.Equal("/erp/tenant-config-app?config_id=8#erp-row-8",
            ErpRecordOpen.Href("/erp/tenant-config-app", "config_id", 8));
        Assert.Equal(
            "/cp/tenant-config-app?config_id=8",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/tenant-config-app",
                "/CP/control/portal/epc_tenant_config?config_id=8"));
        Assert.Equal("/erp/audit-trail-app?event_id=3#erp-row-3",
            ErpRecordOpen.Href("/erp/audit-trail-app", "event_id", 3));
        Assert.Equal(
            "/erp/audit-trail-app?event_id=3",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/audit-trail-app",
                "/ERP/?epc_erp_shell=1&area=audit_wb&tab=audit&event_id=3"));
        Assert.Equal("/erp/doc-expiry-app?doc=4#erp-row-4",
            ErpRecordOpen.Href("/erp/doc-expiry-app", "doc", 4));
        Assert.Equal(
            "/erp/doc-expiry-app?doc=4",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/doc-expiry-app",
                "/ERP/?epc_erp_shell=1&area=risk&tab=doc_expiry&doc=4"));
        Assert.Equal("/erp/withholding-app?txn_id=5#erp-row-5",
            ErpRecordOpen.Href("/erp/withholding-app", "txn_id", 5));
        Assert.Equal(
            "/erp/withholding-app?txn_id=5",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/withholding-app",
                "/ERP/?epc_erp_shell=1&area=tax&tab=withholding&txn_id=5"));
        Assert.Equal("/erp/insurance-compliance-app?pol=7#erp-row-7",
            ErpRecordOpen.Href("/erp/insurance-compliance-app", "pol", 7));
        Assert.Equal(
            "/erp/insurance-compliance-app?pol=7",
            ErpRecordOpen.PreserveRecordQuery(
                "/erp/insurance-compliance-app",
                "/ERP/?epc_erp_shell=1&area=risk&tab=insurance&pol=7"));
        Assert.Equal("/cp/returns-rma-app?rma_id=6#erp-row-6",
            ErpRecordOpen.Href("/cp/returns-rma-app", "rma_id", 6));
        Assert.Equal(
            "/cp/returns-rma-app?return_id=8",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/returns-rma-app",
                "/CP/shop/returns-manager?page=detail&return_id=8"));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cp", "content", "shop", "finance", "erp", "ajax_erp.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cp", "content", "shop", "finance", "erp", "ajax_erp.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root.");
    }
}
