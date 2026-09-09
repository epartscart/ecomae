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
