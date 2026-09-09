using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpVsAspNetRemovalMatrixTests
{
    [Fact]
    public void RemovalFlagsStayLocked()
    {
        Assert.False(PhpVsAspNetRemovalMatrix.ReadyForPhpRemoval);
        Assert.False(PhpVsAspNetRemovalMatrix.PhpSourceDeletionAllowed);
        Assert.False(PhpVsAspNetRemovalMatrix.CutoverAllowed);
        Assert.Equal(0, PhpVsAspNetRemovalMatrix.AspNetInteractiveCompleteCount);

        var report = PhpVsAspNetRemovalMatrix.BuildReport();
        Assert.Equal(false, report["readyForPhpRemoval"]);
        Assert.Equal(false, report["phpSourceDeletionAllowed"]);
        Assert.Equal(false, report["cutoverAllowed"]);
        Assert.Equal(0, report["aspNetInteractiveCompleteCount"]);
        Assert.Equal(true, report["keepPhpProjectAvailable"]);
        Assert.Equal(EcomAeRoutes.PhpVsAspNetMatrix, report["endpoint"]);
    }

    [Fact]
    public void MatrixCoversStorefrontCpErpAndWrites()
    {
        var surfaces = PhpVsAspNetRemovalMatrix.Rows.Select(r => r.Surface).Distinct(StringComparer.Ordinal).ToHashSet();
        Assert.Contains("storefront", surfaces);
        Assert.Contains("cp", surfaces);
        Assert.Contains("erp", surfaces);
        Assert.Contains("writes", surfaces);
        Assert.True(PhpVsAspNetRemovalMatrix.Rows.Count >= 40);
        Assert.DoesNotContain(PhpVsAspNetRemovalMatrix.Rows, r => r.Status == "missing-app");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.WritesOwner == "php");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "sf-cart" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-orders" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "sf-returns" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-users" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-lang" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-channels" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-carriers" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-workshop" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-fulfillment" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-currencies" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-prices-edit" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-synonyms" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-crosses" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-catalogue" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-warranty-rma" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-vin-requests" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-payroll" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-inventory" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-workspace-favorites" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-jw-repairs" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-prices-upload" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-storages" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-offices" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-delivery-methods" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-quote-requests" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-vendor-approvals" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-api-clients" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-price-storage-rules" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-content-pages" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-offices-cash" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-wms-locations" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-subscriptions" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-contracts" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-workflow" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-collections-cases" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-collections" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-procurement-reqs" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-ins-claims" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-vat-refund-status" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-pf-case-cancel" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-bos-wf-disable-rule" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-bos-compliance-disable" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-hr-leave-expense" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-projects" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-cons-deletes" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-fy-reopen-period" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-fin-period-status" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-wht-settle" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "sf-garage" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "sf-vin" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "sf-pay-order" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-fulfillment" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-pos" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-collections" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "cp-custom-ship" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "write-laximo" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "write-payments" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-marketing" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-projects" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-multi-entity" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "erp-multi-currency-gl" && r.WritesOwner == "aspnet");
        Assert.Contains(PhpVsAspNetRemovalMatrix.Rows, r => r.Id == "write-storefront-cart" && r.WritesOwner == "php");
    }

    [Theory]
    [InlineData("/CP/shop/finance", "/erp")]
    [InlineData("/CP/shop/finance/epc_credit_limit", "/cp/credit-limits-app")]
    [InlineData("/CP/shop/finance/epc_po_approval", "/cp/po-approvals-app")]
    [InlineData("/CP/shop/finance/epc_warranty_rma", "/cp/returns-rma-app")]
    [InlineData("/CP/shop/finance/epc_wps_payroll", "/erp/payroll-app")]
    [InlineData("/CP/shop/finance/epc_order_erp_pipeline", "/erp/order-pipeline-app")]
    [InlineData("/CP/shop/finance/epc_inventory_forecast", "/erp/inventory-forecast-app")]
    [InlineData("/CP/shop/finance/epc_multi_entity", "/erp/multi-entity-app")]
    [InlineData("/CP/shop/finance/epc_multi_currency_gl", "/erp/multi-currency-gl-app")]
    public void FinanceHrefsMapToDedicatedAppsNotBareHub(string phpHref, string expected)
    {
        Assert.Equal(expected, PhpSurfaceLinkMap.MapCpPhpPath(phpHref));
        Assert.Equal(expected, PhpSurfaceLinkMap.AspNetPrimaryHref(phpHref));
    }

    [Fact]
    public void NewFinanceDigestAppsExist()
    {
        var root = FindRepoRoot();
        foreach (var name in new[]
        {
            "ErpOrderPipelineApp.razor",
            "ErpInventoryForecastApp.razor",
            "ErpMultiEntityApp.razor",
            "ErpMultiCurrencyGlApp.razor"
        })
        {
            var path = Path.Combine(root, "aspnet", "src", "EcomAE.Platform", "Components", "Pages", name);
            Assert.True(File.Exists(path), path);
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("Compare PHP reference", text, StringComparison.Ordinal);
            Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LiveWriteAppsUseNativeFormsNotBlazorHandlers()
    {
        var root = FindRepoRoot();
        var cases = new (string File, string Needle)[]
        {
            ("StorefrontCartApp.razor", "/storefront/cart/change-count-need"),
            ("StorefrontPaymentApp.razor", "/storefront/payment/create-operation"),
            ("StorefrontVinApp.razor", "/storefront/vin/decode"),
            ("StorefrontSellerRequestApp.razor", "SellerWriteHref"),
            ("StorefrontCustomerRequestsApp.razor", "MessageWriteHref"),
            ("StorefrontOrdersApp.razor", "/storefront/payment/create-operation"),
            ("StorefrontOrdersApp.razor", "PayOnPlaceHref"),
            ("ErpPayrollApp.razor", "/erp/payroll/generate"),
            ("ErpPayrollApp.razor", "/erp/ajax/payroll-approve"),
            ("ErpPayrollApp.razor", "/erp/payroll/pay"),
            ("ErpPayrollApp.razor", "/erp/payroll/update-days"),
            ("ErpInventoryForecastApp.razor", "/erp/inventory-forecast/recompute"),
            ("ErpMultiEntityApp.razor", "/erp/multi-entity/write"),
            ("ErpMultiCurrencyGlApp.razor", "/erp/multi-currency-gl/set-rate"),
            ("CpCreditLimitsApp.razor", "/cp/credit-limits/set"),
            ("CpPoApprovalsApp.razor", "/cp/po-approvals/approve"),
            ("CpOrdersApp.razor", "/cp/orders/set-item-status"),
            ("CpOrdersApp.razor", "/cp/orders/set-items-status"),
            ("CpOrdersApp.razor", "/cp/orders/add-comment"),
            ("CpOrdersApp.razor", "/cp/orders/fulfillment-set-stage"),
            ("StorefrontReturnsApp.razor", "ReturnsMessageHref"),
            ("StorefrontReturnsApp.razor", "ReturnsCreateHref"),
            ("CpOrdersApp.razor", "/cp/orders/update-item"),
            ("CpUsersApp.razor", "/cp/users/set-comment"),
            ("CpUsersApp.razor", "/cp/users/set-unlocked"),
            ("CpUsersApp.razor", "/cp/users/create"),
            ("CpUsersApp.razor", "/cp/users/set-password"),
            ("CpUsersApp.razor", "/cp/vendors/approvals"),
            ("CpQuoteRequestsApp.razor", "/cp/quote-requests/note"),
            ("CpQuoteRequestsApp.razor", "/cp/quote-requests/save-lines"),
            ("CpQuoteRequestsApp.razor", "/cp/quote-requests/send"),
            ("CpApiClientsApp.razor", "/cp/api-clients/toggle"),
            ("CpPriceListsApp.razor", "/cp/prices/storage-rules"),
            ("CpPagesApp.razor", "/cp/content/published"),
            ("CpPagesApp.razor", "/cp/content/main"),
            ("CpPagesApp.razor", "/cp/content/body"),
            ("CpPagesApp.razor", "/cp/content/save"),
            ("CpPagesApp.razor", "/cp/content/tree"),
            ("CpMenusApp.razor", "/cp/menus/write"),
            ("CpModulesApp.razor", "/cp/modules/write"),
            ("ErpCashAccountsApp.razor", "/erp/offices-cash/add"),
            ("ErpCashAccountsApp.razor", "/erp/offices-cash/add"),
            ("ErpCashAccountsApp.razor", "/erp/offices-cash/codes/add"),
            ("ErpCashAccountsApp.razor", "/erp/offices-cash/codes/delete"),
            ("CpWarehouseWmsApp.razor", "/erp/wms/receive"),
            ("CpWarehouseWmsApp.razor", "/erp/wms/locations/save"),
            ("CpWarehouseWmsApp.razor", "/erp/wms/locations/delete"),
            ("CpWarehouseWmsApp.razor", "/erp/wms/waves/release"),
            ("CpWarehouseWmsApp.razor", "/erp/wms/waves/create"),
            ("CpWarehouseWmsApp.razor", "/erp/wms/work/complete"),
            ("ErpSalesOrdersApp.razor", "/erp/subscriptions/save"),
            ("ErpSalesOrdersApp.razor", "/erp/subscriptions/status"),
            ("ErpContractsApp.razor", "/erp/contracts/save"),
            ("ErpContractsApp.razor", "/erp/ajax/ctr-status"),
            ("ErpWorkflowApp.razor", "/erp/workflow/status"),
            ("CpCollectionsDunningApp.razor", "/erp/collections/cases/save"),
            ("ErpWorkflowApp.razor", "/erp/workflow/create"),
            ("CpCollectionsDunningApp.razor", "/erp/collections/cases/status"),
            ("CpCollectionsDunningApp.razor", "/erp/collections/cases/promise"),
            ("CpCollectionsDunningApp.razor", "/erp/collections/activity/log"),
            ("CpCollectionsDunningApp.razor", "/erp/collections/hold/set"),
            ("CpCollectionsDunningApp.razor", "/erp/collections/dunning/run"),
            ("CpCollectionsDunningApp.razor", "/cp/collections-dunning/write"),
            ("CpPromotionsApp.razor", "/cp/promotions/write"),
            ("CpCrmBoardApp.razor", "/cp/crm/action"),
            ("CpCrmOpportunitiesApp.razor", "/cp/crm/opportunities/write"),
            ("CpCrmActivitiesApp.razor", "/cp/crm/activities/write"),
            ("CpCrmTicketsApp.razor", "/cp/crm/tickets/write"),
            ("CpMarketingGrowthApp.razor", "/cp/marketing-growth/write"),
            ("ErpSalesQuotationsApp.razor", "/cp/crm/quotes/write"),
            ("CpCrmOpportunitiesApp.razor", "/cp/crm/projects/write"),
            ("CpCrmOpportunitiesApp.razor", "/cp/crm/contracts/write"),
            ("CpCrmOpportunitiesApp.razor", "/cp/crm/expenses/write"),
            ("CpDocumentControlApp.razor", "/cp/document-control/write"),
            ("CpAutoPriceApp.razor", "/cp/auto-price/write"),
            ("CpBulkUploadApp.razor", "/cp/bulk-upload/write"),
            ("CpFreeToolsApp.razor", "/cp/free-tools/write"),
            ("CpPlatformGovernanceApp.razor", "/cp/platform-governance/write"),
            ("CpMobileAppsApp.razor", "/cp/mobile-apps/write"),
            ("CpTenantFeaturesApp.razor", "/cp/tenant-features/write"),
            ("CpTenantsApp.razor", "/cp/tenants/write"),
            ("CpSocialHubApp.razor", "/cp/social-hub/write"),
            ("CpInfoBlocksApp.razor", "/cp/info-blocks/write"),
            ("CpPlatformCommunicationApp.razor", "/cp/platform-communication/write"),
            ("CpPriceConfigsApp.razor", "/cp/price-configs/write"),
            ("CpWorkflowsApp.razor", "/cp/workflows/write"),
            ("CpPowerBiApp.razor", "/cp/power-bi/write"),
            ("CpMetabaseApp.razor", "/cp/metabase/write"),
            ("CpAbandonedCartsApp.razor", "/cp/abandoned-carts/write"),
            ("CpNlReportingApp.razor", "/cp/nl-reporting/write"),
            ("CpConfigSandboxApp.razor", "/cp/config-sandbox/write"),
            ("CpMarketplaceAppsApp.razor", "/cp/marketplace-apps/write"),
            ("CpPurchaseRequestsApp.razor", "/erp/procurement/requisitions/save"),
            ("CpPurchaseRequestsApp.razor", "/erp/procurement/requisitions/add-line"),
            ("CpPurchaseRequestsApp.razor", "/erp/procurement/requisitions/submit"),
            ("CpPurchaseRequestsApp.razor", "/erp/procurement/requisitions/decision"),
            ("CpPurchaseRequestsApp.razor", "/erp/ajax/proc-req-convert"),
            ("CpInsuranceComplianceApp.razor", "/erp/ajax/ins-claim-add"),
            ("CpInsuranceComplianceApp.razor", "/erp/ajax/ins-claim-status"),
            ("CpInsuranceComplianceApp.razor", "/erp/ajax/ins-doc-delete"),
            ("CpHrOverviewApp.razor", "/erp/hr/employees/save"),
            ("CpHrOverviewApp.razor", "/erp/hr/attendance/log"),
            ("CpHrOverviewApp.razor", "/erp/hr/payroll/generate"),
            ("CpHrOverviewApp.razor", "/erp/ajax/hr-leave-request"),
            ("CpHrOverviewApp.razor", "/erp/ajax/hr-leave-status"),
            ("CpHrOverviewApp.razor", "/erp/ajax/hr-expense-save"),
            ("CpHrOverviewApp.razor", "/erp/ajax/hr-expense-status"),
            ("CpProjectsOverviewApp.razor", "/erp/projects/save"),
            ("CpProjectsOverviewApp.razor", "/erp/projects/tasks/save"),
            ("CpProjectsOverviewApp.razor", "/erp/projects/timesheets/log"),
            ("CpConsolidationsApp.razor", "/erp/ajax/cons-entity-save"),
            ("CpConsolidationsApp.razor", "/erp/consolidations/figures/save"),
            ("CpCarriersApp.razor", "/erp/custom-shipping/delete"),
            ("ErpSalesOrdersApp.razor", "/erp/edit-lock/heartbeat"),
            ("ErpSalesOrdersApp.razor", "/erp/edit-lock/release"),
            ("CpBudgetsApp.razor", "/erp/budgets/advance"),
            ("ErpPeriodCloseApp.razor", "/erp/periods/soft-close"),
            ("CpWorkflowsApp.razor", "/erp/automation/deactivate"),
            ("ErpBankReconciliationApp.razor", "/erp/bank-reconciliation/match"),
            ("CpFinAdvancedApp.razor", "/erp/fin/periods/generate"),
            ("ErpRecruitmentApp.razor", "/erp/recruitment/applicants/stage"),
            ("ErpOpeningApp.razor", "/erp/opening/add-inv-line"),
            ("ErpOpeningApp.razor", "/erp/opening/add-coa-line"),
            ("ErpOpeningApp.razor", "/erp/opening/create-batch"),
            ("ErpMultiCurrencyGlApp.razor", "/erp/currency/set-rate"),
            ("ErpPeriodCloseApp.razor", "/erp/fiscal/set-lock"),
            ("CpUaeTaxComplianceApp.razor", "/erp/uae-tax/ct-adjustments/save"),
            ("ErpContractsApp.razor", "/erp/contracts/sign"),
            ("ErpContractsApp.razor", "/erp/contracts/ocr"),
            ("ErpBosDashboardApp.razor", "/erp/bos-intel/toggle"),
            ("ErpQualityApp.razor", "/erp/quality/ncr-update"),
            ("ErpOrderPlanningApp.razor", "/erp/order-planning/set-status"),
            ("CpProductionOverviewApp.razor", "/erp/mfgr/planned/firm"),
            ("CpProductionOverviewApp.razor", "/erp/mfgr/routes/save"),
            ("CpProductionOverviewApp.razor", "/erp/mfgr/work-centers/save"),
            ("CpBudgetsApp.razor", "/erp/pm/listings/attach"),
            ("CpBudgetsApp.razor", "/erp/pm/cheques/save"),
            ("CpBudgetsApp.razor", "/erp/pm/toggle"),
            ("CpBudgetsApp.razor", "/erp/pm/budget-lines/add"),
            ("CpBudgetsApp.razor", "/erp/pm/budgets/save"),
            ("CpBudgetsApp.razor", "/erp/pm/save"),
            ("CpUaeTaxComplianceApp.razor", "/erp/uae-tax/legislation/checklist/set"),
            ("ErpPrintDesignerApp.razor", "/erp/print-designer/save"),
            ("CpTenantConfigApp.razor", "/erp/tenant-config/save"),
            ("CpDocExpiryApp.razor", "/erp/doc-expiry/delete"),
            ("CpFinAdvancedApp.razor", "/erp/fin/alloc/save"),
            ("CpDocExpiryApp.razor", "/erp/doc-expiry/save"),
            ("ErpBankReconciliationApp.razor", "/erp/bank-instruments/status"),
            ("ErpBankReconciliationApp.razor", "/erp/bank-instruments/save"),
            ("ErpCashAccountsApp.razor", "/erp/cash-forecast/lines/add"),
            ("ErpCashAccountsApp.razor", "ErpCashForecastSave"),
            ("ErpOrderPlanningApp.razor", "ErpOrderPlanningParamsSave"),
            ("ErpPerformanceApp.razor", "ErpPerformanceGoalAdd"),
            ("ErpPerformanceApp.razor", "ErpPerformanceReviewSave"),
            ("CpBudgetsApp.razor", "ErpBudgetPlanPositionAdd"),
            ("CpBudgetsApp.razor", "ErpBudgetPlanLineAdd"),
            ("CpBudgetsApp.razor", "ErpBudgetPlanSave"),
            ("ErpRecruitmentApp.razor", "ErpRecruitmentApplicantAdd"),
            ("ErpRecruitmentApp.razor", "ErpRecruitmentJobSave"),
            ("ErpProcurementCategoriesApp.razor", "ErpProcurementPolicySave"),
            ("ErpProcurementCategoriesApp.razor", "ErpProcurementCategorySave"),
            ("ErpQualityApp.razor", "ErpQualityTestAddForm"),
            ("ErpQualityApp.razor", "ErpQualityPlanSaveForm"),
            ("CpTenantConfigApp.razor", "/erp/security/users/assign-role"),
            ("CpTenantConfigApp.razor", "/erp/security/roles/attach-duty"),
            ("CpTenantConfigApp.razor", "/erp/security/duties/attach-priv"),
            ("CpTenantConfigApp.razor", "/erp/security/roles/save"),
            ("CpTenantConfigApp.razor", "/erp/security/duties/save"),
            ("CpTenantConfigApp.razor", "/erp/security/privileges/save"),
            ("CpJewelleryRetailApp.razor", "/erp/retail/discounts/save"),
            ("CpTenantConfigApp.razor", "/erp/platform/jobs/run"),
            ("CpJewelleryRetailApp.razor", "/erp/retail/assortments/set"),
            ("ErpProjectAccountingApp.razor", "/erp/project-accounting/txns/add"),
            ("CpCostModelsApp.razor", "/erp/cost-models/txns/add"),
            ("CpIntegrationsApp.razor", "/erp/integrations/events/raise"),
            ("CpCostModelsApp.razor", "/erp/cost-models/items/set"),
            ("ErpMultiEntityApp.razor", "/erp/multi-entity/preference/save"),
            ("CpIntegrationsApp.razor", "/erp/integrations/subscriptions/save"),
            ("ErpAgendaApp.razor", "/erp/agenda/events/save"),
            ("ErpGuideApp.razor", "/erp/guide/articles/save"),
            ("ErpProcessFlowTasksApp.razor", "/erp/process-flow/processes/save"),
            ("ErpProcessFlowTasksApp.razor", "/erp/process-flow/steps/save"),
            ("CpIntegrationsApp.razor", "/erp/integrations/entities/save"),
            ("CpTenantConfigApp.razor", "/erp/platform/features/save"),
            ("CpTenantConfigApp.razor", "/erp/platform/jobs/save"),
            ("ErpProjectAccountingApp.razor", "/erp/project-accounting/budgets/save"),
            ("CpTenantConfigApp.razor", "/erp/org/calendars/save"),
            ("CpTenantConfigApp.razor", "/erp/org/holidays/add"),
            ("ErpContactsApp.razor", "/erp/contacts/party-contacts/save"),
            ("CpJewelleryRetailApp.razor", "/erp/retail/channels/save"),
            ("CpElectronicReportingApp.razor", "/erp/electronic-reporting/fields/add"),
            ("ErpContactsApp.razor", "/erp/contacts/addresses/save"),
            ("CpElectronicReportingApp.razor", "/erp/electronic-reporting/formats/save"),
            ("ErpContactsApp.razor", "/erp/contacts/parties/save"),
            ("ErpApprovalsApp.razor", "/erp/approvals/requests/raise"),
            ("ErpPeriodCloseApp.razor", "/erp/fiscal-years/create"),
            ("ErpApprovalsApp.razor", "/erp/approvals/requests/decide"),
            ("ErpApprovalsApp.razor", "/erp/approvals/rules/save"),
            ("ErpProcessFlowTasksApp.razor", "/erp/process-flow/cases/reassign"),
            ("CpSoc2ComplianceApp.razor", "/erp/compliance/obligations/add"),
            ("CpProductionOverviewApp.razor", "/erp/manufacturing/work-orders/create"),
            ("CpProductionOverviewApp.razor", "/erp/manufacturing/bom/save"),
            ("CpConsolidationsApp.razor", "/erp/consolidations/ic/save"),
            ("CpConsolidationsApp.razor", "/erp/ajax/cons-entity-delete"),
            ("CpConsolidationsApp.razor", "/erp/ajax/cons-ic-delete"),
            ("ErpVatApp.razor", "/erp/ajax/bos-vat-refund-save"),
            ("ErpVatApp.razor", "/erp/ajax/bos-vat-refund-status"),
            ("ErpVatApp.razor", "/erp/tourist-refund/create"),
            ("ErpVatApp.razor", "/erp/tourist-refund/validate"),
            ("ErpRfidApp.razor", "/erp/rfid/register"),
            ("ErpRfidApp.razor", "/erp/rfid/start-session"),
            ("ErpRfidApp.razor", "/erp/rfid/scan"),
            ("CpCrmTicketsApp.razor", "/erp/tickets/reply"),
            ("CpJewelleryMastersApp.razor", "/erp/gold-rate/set"),
            ("CpAmlComplianceApp.razor", "/erp/aml/kyc-save"),
            ("CpAmlComplianceApp.razor", "/erp/aml/alert-status"),
            ("ErpSalesOrdersApp.razor", "/erp/subscriptions/generate"),
            ("ErpSalesOrdersApp.razor", "/erp/ajax/sub-invoice-paid"),
            ("ErpProcessFlowTasksApp.razor", "/erp/ajax/pf-case-cancel"),
            ("ErpProcessFlowTasksApp.razor", "/erp/ajax/pf-step-delete"),
            ("ErpApprovalsApp.razor", "/erp/ajax/bos-wf-disable-rule"),
            ("CpSoc2ComplianceApp.razor", "/erp/ajax/bos-compliance-disable-obligation"),
            ("ErpPeriodCloseApp.razor", "/erp/ajax/fy-reopen"),
            ("ErpPeriodCloseApp.razor", "/erp/ajax/fy-period-status"),
            ("CpFinAdvancedApp.razor", "/erp/fin/periods/status"),
            ("ErpWithholdingApp.razor", "/erp/ajax/wht-settle"),
            ("ErpWithholdingApp.razor", "/erp/withholding/codes/save"),
            ("ErpWithholdingApp.razor", "/erp/withholding/txns/record"),
            ("ErpWithholdingApp.razor", "/erp/withholding/txns/certificate"),
            ("CpLanguagesApp.razor", "/cp/lang/set-is-custom"),
            ("CpLanguagesApp.razor", "/cp/lang/set-is-error"),
            ("CpLanguagesApp.razor", "/cp/lang/set-same"),
            ("CpLanguagesApp.razor", "/cp/lang/set-used-found"),
            ("CpLanguagesApp.razor", "/cp/lang/save-translation"),
            ("CpLanguagesApp.razor", "/cp/lang/save-description"),
            ("CpLanguagesApp.razor", "/cp/lang/delete-not-used"),
            ("CpLanguagesApp.razor", "/cp/lang/create-string"),
            ("CpMarketplaceChannelsApp.razor", "/cp/channels/write"),
            ("CpCarriersApp.razor", "/cp/logistics/write"),
            ("CpWorkshopApp.razor", "/cp/workshop/write"),
            ("CpFulfillmentQueueApp.razor", "/cp/fulfillment-queue/write"),
            ("CpSynonymsApp.razor", "/cp/synonyms/write"),
            ("CpCrossesApp.razor", "/cp/crosses/write"),
            ("CpPricesEditApp.razor", "/cp/prices-edit/write"),
            ("CpCurrenciesApp.razor", "/cp/currencies/set-rate"),
            ("CpCurrenciesApp.razor", "/cp/currencies/set-available"),
            ("CpProductCatalogueApp.razor", "/cp/catalogue/set-min-limit"),
            ("CpProductCatalogueApp.razor", "/cp/catalogue/templates-actions"),
            ("CpProductCatalogueApp.razor", "/cp/catalogue/line-lists/write"),
            ("CpProductCatalogueApp.razor", "/cp/catalogue/tree-lists/write"),
            ("CpProductCatalogueApp.razor", "/cp/catalogue/sku-media/write"),
            ("CpProductCatalogueApp.razor", "/cp/catalogue/main-page-products/write"),
            ("CpProductCatalogueApp.razor", "/cp/catalogue/special-searches/write"),
            ("CpProductCatalogueApp.razor", "/cp/catalogue/editor/write"),
            ("CpProductCatalogueApp.razor", "/cp/catalogue/products/write"),
            ("CpProductCatalogueApp.razor", "/cp/catalogue/reviews/write"),
            ("CpProductCatalogueApp.razor", "/cp/catalogue/products/delete"),
            ("ErpWorkspaceFavoritesApp.razor", "/erp/ajax/erp-fav-add"),
            ("ErpWorkspaceFavoritesApp.razor", "/erp/ajax/erp-fav-remove"),
            ("ErpWorkspaceFavoritesApp.razor", "/erp/ajax/shortcut-add"),
            ("ErpWorkspaceFavoritesApp.razor", "/erp/ajax/shortcut-reorder"),
            ("ErpWorkspaceFavoritesApp.razor", "/erp/ajax/shortcut-delete"),
            ("ErpWorkspaceFavoritesApp.razor", "/erp/ajax/shortcut-delete-key"),
            ("ErpWorkspaceFavoritesApp.razor", "/erp/ajax/shortcut-reset"),
            ("CpStoragesApp.razor", "/cp/storages/groups"),
            ("CpStoragesApp.razor", "/cp/storages/write"),
            ("CpStoragesApp.razor", "/cp/storages/membership"),
            ("CpOfficesApp.razor", "/cp/offices/write"),
            ("CpOfficesApp.razor", "/cp/offices/delete"),
            ("CpOfficesApp.razor", "/cp/offices/geo"),
            ("CpDeliveryMethodsApp.razor", "/cp/delivery-methods/write"),
            ("CpGeoRegionsApp.razor", "/cp/geo-regions/write"),
            ("CpSearchTabsApp.razor", "/cp/search-tabs/write"),
            ("CpAdditionalTextsApp.razor", "/cp/additional-texts/write"),
            ("CpAdditionalTextsApp.razor", "/cp/additional-texts/delete"),
            ("CpSliderBannersApp.razor", "/cp/slider-banners/write"),
            ("CpProductFiltersApp.razor", "/cp/product-filters/write"),
            ("CpOrderStatusesApp.razor", "/cp/order-statuses/write"),
            ("CpPricesUploadApp.razor", "/cp/prices/complete-session"),
            ("ErpPayrollApp.razor", "/erp/ajax/hr-update-days"),
            ("CpHrOverviewApp.razor", "/erp/ajax/hr-update-days"),
            ("ErpInventoryStockApp.razor", "/erp/ajax/inv-set-reorder-level"),
            ("ErpInventoryStockApp.razor", "/erp/ajax/inv-record-movement"),
            ("ErpInventoryStockApp.razor", "/erp/ajax/inv-transfer"),
            ("ErpInventoryStockApp.razor", "/erp/ajax/inv-create-warehouse"),
            ("ErpInventoryStockApp.razor", "/erp/ajax/inv-create-item"),
            ("ErpInventoryStockApp.razor", "/erp/ajax/inv-sync-warehouses"),
            ("ErpInventoryStockApp.razor", "/erp/ajax/inv-run-closing"),
            ("ErpInventoryStockApp.razor", "/erp/ajax/inv-import-csv"),
            ("ErpInventoryStockApp.razor", "/erp/ajax/dim-save"),
            ("ErpContactsApp.razor", "/erp/customers/master-save"),
            ("ErpReceivablesApp.razor", "/erp/customers/master-save"),
            ("ErpReceivablesApp.razor", "/erp/customers/settlement"),
            ("ErpSalesOrdersApp.razor", "/erp/orders/settlement"),
            ("CpReturnsRmaApp.razor", "/erp/aftersales/rma-create"),
            ("ErpProductInfoApp.razor", "/erp/ajax/inv-create-item"),
            ("CpJewelleryRepairsApp.razor", "/erp/ajax/jw-repair-update-status"),
            ("CpJewelleryRepairsApp.razor", "ErpJewelleryRepairCreateForm"),
            ("CpJewelleryRepairsApp.razor", "confirmWrites"),
            ("CpJewelleryMastersApp.razor", "ErpJewelleryKaratSaveForm"),
            ("CpJewelleryMastersApp.razor", "Save karat"),
            ("CpJewelleryMastersApp.razor", "ErpJewelleryRateTypeSaveForm"),
            ("CpJewelleryMastersApp.razor", "ErpJewelleryCurrencySaveForm"),
            ("CpJewelleryMastersApp.razor", "Save currency"),
            ("CpJewelleryMastersApp.razor", "ErpJewelleryDiamondSaveForm"),
            ("CpJewelleryMastersApp.razor", "Save diamond"),
            ("CpJewelleryMastersApp.razor", "ErpJewelleryDesignSaveForm"),
            ("CpJewelleryMastersApp.razor", "Save design"),
            ("CpJewelleryMastersApp.razor", "ErpJewelleryPearlSaveForm"),
            ("CpJewelleryMastersApp.razor", "Save pearl"),
            ("CpJewelleryMastersApp.razor", "ErpJewelleryColorStoneSaveForm"),
            ("CpJewelleryMastersApp.razor", "Save color stone"),
            ("CpJewelleryMastersApp.razor", "Generate barcode"),
            ("CpJewelleryMastersApp.razor", "Create tag"),
            ("CpJewelleryMastersApp.razor", "Sell tag"),
            ("CpJewelleryMastersApp.razor", "Create scheme"),
            ("CpJewelleryMastersApp.razor", "Enroll customer"),
            ("CpJewelleryMastersApp.razor", "Pay instalment"),
            ("CpJewelleryRetailApp.razor", "Generate barcode"),
            ("CpJewelleryStockVerificationApp.razor", "ErpJewelleryMetalStockSaveForm"),
            ("CpJewelleryStockVerificationApp.razor", "Save metal stock"),
            ("CpJewelleryFixingApp.razor", "ErpJewelleryFixingSaveForm"),
            ("CpJewelleryFixingApp.razor", "Save fixing"),
            ("CpJewelleryFixingApp.razor", "Create fix / unfix"),
            ("CpJewelleryFixingApp.razor", "Settle unfix"),
            ("ErpPurchaseOrdersApp.razor", "Create barcode purchase"),
            ("ErpPurchaseOrdersApp.razor", "Sell barcode purchase"),
            ("CpCrmTicketsApp.razor", "Create SLA"),
            ("CpCrmTicketsApp.razor", "Create ticket"),
            ("CpJewelleryRetailApp.razor", "ErpJewelleryVoucherSaveForm"),
            ("CpJewelleryRetailApp.razor", "Save voucher"),
            ("ErpCashAccountsApp.razor", "ErpJewelleryPettyCashSaveForm"),
            ("ErpCashAccountsApp.razor", "Save petty cash"),
            ("CpUaeTaxComplianceApp.razor", "ErpJewelleryTouristVatSaveForm"),
            ("CpUaeTaxComplianceApp.razor", "Save tourist VAT"),
            ("CpJewelleryRepairsApp.razor", "ErpJewelleryRepairReceiptSaveForm"),
            ("CpJewelleryRepairsApp.razor", "Save repair receipt"),
            ("CpJewelleryRepairsApp.razor", "ErpJewelleryRepairTransferSaveForm"),
            ("CpJewelleryRepairsApp.razor", "Save transfer"),
            ("CpJewelleryRepairsApp.razor", "ErpJewelleryWorkshopReceiveSaveForm"),
            ("CpJewelleryRepairsApp.razor", "Save workshop receive"),
            ("CpJewelleryRepairsApp.razor", "ErpJewelleryRepairDeliverySaveForm"),
            ("CpJewelleryRepairsApp.razor", "Save delivery"),
            ("CpJewelleryStockVerificationApp.razor", "ErpJewelleryStockVerifySaveForm"),
            ("CpJewelleryStockVerificationApp.razor", "Save stock verification"),
            ("CpJewelleryRepairsApp.razor", "Save repair sale"),
            ("CpReturnsRmaApp.razor", "ErpAftersalesRmaResolve"),
            ("CpReturnsRmaApp.razor", "Resolve RMA"),
            ("CpReturnsRmaApp.razor", "Register warranty"),
            ("CpReturnsRmaApp.razor", "Create service job"),
            ("CpReturnsRmaApp.razor", "Add job line"),
            ("CpReturnsRmaApp.razor", "Close job"),
            ("CpJewelleryMastersApp.razor", "Save rate type"),
            ("ErpMarketingApp.razor", "/erp/marketing/create"),
            ("CpReturnsRmaApp.razor", "/cp/returns/action"),
            ("CpSystemRequestsApp.razor", "/cp/requests/set-vin-viewed"),
            ("StorefrontWishlistApp.razor", "WishlistRemoveHref"),
            ("StorefrontCompareApp.razor", "CompareRemoveHref"),
            ("StorefrontProfileApp.razor", "ProfileWriteHref"),
            ("CpPosOverviewApp.razor", "/cp/pos/open-session"),
            ("CpPosOverviewApp.razor", "/cp/pos/complete-sale"),
            ("CpPosOverviewApp.razor", "Save POS advance"),
            ("ErpGlJournalsApp.razor", "Save journal voucher"),
            ("CpFulfillmentQueueApp.razor", "/cp/fulfillment-queue/write"),
            ("CpCollectionsDunningApp.razor", "/cp/collections-dunning/write"),
        };
        foreach (var (name, needle) in cases)
        {
            var path = Path.Combine(root, "aspnet", "src", "EcomAE.Platform", "Components", "Pages", name);
            var text = File.ReadAllText(path);
            Assert.Contains(needle, text, StringComparison.Ordinal);
            Assert.Contains("confirmWrites", text, StringComparison.Ordinal);
            Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
            Assert.DoesNotContain("@onchange", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MappedMatrixRowsAgreeWithLinkMap()
    {
        foreach (var row in PhpVsAspNetRemovalMatrix.Rows.Where(r =>
                     r.PhpHref.StartsWith("/CP/", StringComparison.OrdinalIgnoreCase)
                     || r.PhpHref.StartsWith("/ERP/", StringComparison.OrdinalIgnoreCase)))
        {
            var mapped = PhpSurfaceLinkMap.AspNetPrimaryHref(row.PhpHref);
            var expectedPath = row.AspNetRoute.Split('?', 2)[0];
            Assert.StartsWith(expectedPath, mapped, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.sln"))
                || Directory.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repo root from " + AppContext.BaseDirectory);
    }
}
