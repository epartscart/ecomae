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
