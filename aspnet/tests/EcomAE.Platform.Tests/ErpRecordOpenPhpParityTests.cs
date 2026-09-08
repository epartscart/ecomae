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
    [InlineData("/CP/shop/orders/carts?cart_id=15", "/cp/abandoned-carts-app?cart_id=15")]
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
    [InlineData("CpAbandonedCartsApp.razor", "cart_id")]
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
        Assert.Equal("/cp/collections-dunning-app?queue_id=12#erp-row-12",
            ErpRecordOpen.Href("/cp/collections-dunning-app", "queue_id", 12));
        Assert.Equal(
            "/cp/collections-dunning-app?queue_id=12",
            ErpRecordOpen.PreserveRecordQuery(
                "/cp/collections-dunning-app",
                "/CP/shop/finance/epc_collections_dunning?queue_id=12"));
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
        Assert.Contains("ShowGhostScaffold=\"false\"", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
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
