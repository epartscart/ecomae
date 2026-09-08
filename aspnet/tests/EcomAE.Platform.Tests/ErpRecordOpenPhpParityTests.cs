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
    public void DumpListApps_RowOpenIsRecordUrl(string fileName, string param)
    {
        var root = FindRepoRoot();
        var text = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Pages", fileName));
        Assert.Contains("ErpRecordOpen.Href(", text, StringComparison.Ordinal);
        Assert.Contains("\"" + param + "\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)\">Open", text, StringComparison.Ordinal);
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
