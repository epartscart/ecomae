using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpMasterLookupTests
{
    [Theory]
    [InlineData("customer", "customer")]
    [InlineData("customers", "customer")]
    [InlineData("user_id", "customer")]
    [InlineData("supplier", "supplier")]
    [InlineData("vendors", "supplier")]
    [InlineData("item-code", "item")]
    [InlineData("sku", "item")]
    [InlineData("jw-metal-stock", "jw-metal-stock")]
    [InlineData("metal-stock", "jw-metal-stock")]
    [InlineData("jw-item", "jw-metal-stock")]
    [InlineData("jw-karat", "jw-karat")]
    [InlineData("karat", "jw-karat")]
    [InlineData("jw-currency", "jw-currency")]
    [InlineData("jw-addin", "jw-addin")]
    [InlineData("project", "project")]
    [InlineData("re_project", "re-project")]
    [InlineData("account", "account")]
    [InlineData("property", "property")]
    [InlineData("tenant", "tenant")]
    [InlineData("contractor", "contractor")]
    [InlineData("nope", "")]
    public void ResolveKind_NormalizesAliases(string raw, string expected)
        => Assert.Equal(expected, ErpMasterLookupReadService.ResolveKind(raw));

    [Fact]
    public void Filter_RanksExactCodeAndIdAheadOfPartialName()
    {
        var rows = new[]
        {
            new ErpMasterOption(9, "customer", "C-009", "Acme Parts", "acme@example.com"),
            new ErpMasterOption(12, "customer", "C-012", "Beta Motors", "beta@example.com"),
            new ErpMasterOption(21, "customer", "ACME", "Other", ""),
        };

        var byName = ErpMasterLookupReadService.Filter(rows, "acme", 10);
        Assert.Equal(2, byName.Count);
        Assert.Equal("ACME", byName[0].Code);

        var byId = ErpMasterLookupReadService.Filter(rows, "12", 10);
        Assert.Equal(12, byId[0].Id);
        Assert.True(ErpMasterLookupReadService.Matches(rows[0], "Acme"));
        Assert.False(ErpMasterLookupReadService.Matches(rows[1], "Acme"));
    }

    [Fact]
    public void RouteAndAssetAreWired()
    {
        Assert.Equal("/erp/lookups", EcomAeRoutes.ErpLookups);
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("MapGet(EcomAeRoutes.ErpLookups, HandleErpLookupsAsync)", module, StringComparison.Ordinal);
        Assert.Contains("IErpMasterLookupReadService", module, StringComparison.Ordinal);

        var assets = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpAppAssets.cs"));
        Assert.Contains("\"erp-master-picker.js\"", assets, StringComparison.Ordinal);
        Assert.True(File.Exists(FindRepoFile("aspnet/src/EcomAE.Platform/wwwroot/erp/erp-master-picker.js")));

        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpMasterLookupReadService", program, StringComparison.Ordinal);

        var chrome = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor"));
        Assert.Contains("/erp/erp-master-picker.js", chrome, StringComparison.Ordinal);
    }

    [Fact]
    public void PickerFillsIdAndOptionalCodeNameSiblings()
    {
        var picker = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/ErpMasterPicker.razor"));
        Assert.Contains("IErpMasterLookupReadService", picker, StringComparison.Ordinal);
        Assert.Contains("data-erp-fill-code", picker, StringComparison.Ordinal);
        Assert.Contains("data-erp-fill-name", picker, StringComparison.Ordinal);
        Assert.Contains("data-code=\"@row.Code\"", picker, StringComparison.Ordinal);
        Assert.Contains("data-name=\"@row.Name\"", picker, StringComparison.Ordinal);
        Assert.Contains("ValueMode", picker, StringComparison.Ordinal);

        var js = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/wwwroot/erp/erp-master-picker.js"));
        Assert.Contains("/erp/lookups?kind=", js, StringComparison.Ordinal);
        Assert.Contains("data-erp-fill-code", js, StringComparison.Ordinal);
        Assert.Contains("data-erp-fill-name", js, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpSalesOrdersApp.razor", "Kind=\"customer\"", "so-customer")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpPurchaseOrdersApp.razor", "Kind=\"supplier\"", "po-supplier")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpCashEntriesApp.razor", "Kind=\"customer\"", "rv_user_id")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpCashEntriesApp.razor", "Kind=\"supplier\"", "pv_supplier_id")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpCashEntriesApp.razor", "Kind=\"account\"", "rv_account_id")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpSalesQuotationsApp.razor", "Kind=\"customer\"", "customer_user_id")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpRfqApp.razor", "Kind=\"supplier\"", "supplier_id")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpProjectAccountingApp.razor", "Kind=\"project\"", "projectId")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpProjectAccountingApp.razor", "Kind=\"item\"", "item_code")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpReceivablesApp.razor", "CodeFieldName=\"customer_account\"", "customer_name")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpPayablesApp.razor", "Kind=\"supplier\"", "supplier_id")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpRealEstateApp.razor", "Kind=\"property\"", "propertyId")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpRealEstateApp.razor", "Kind=\"re-project\"", "projectId")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpRealEstateApp.razor", "Kind=\"tenant\"", "tenantId")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Pages/ErpRealEstateApp.razor", "Kind=\"contractor\"", "relatedId")]
    public void HighTrafficFormsUseMasterPicker(string relative, string needle, string field)
    {
        var text = File.ReadAllText(FindRepoFile(relative));
        Assert.Contains("<ErpMasterPicker", text, StringComparison.Ordinal);
        Assert.Contains(needle, text, StringComparison.Ordinal);
        Assert.Contains(field, text, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchCatalogSurfacesMasterLookupForEveryIndustry()
    {
        var catalog = ErpGlobalSearchCatalog.Build(jewellery: false, fitOut: false, realEstate: false);
        Assert.Contains(catalog, e => e.Href == "/erp/lookups?kind=customer");
        Assert.Contains(ErpGlobalSearchCatalog.Filter(catalog, "supplier"), e => e.Label.Contains("Master lookup", StringComparison.Ordinal));
    }

    [Fact]
    public void EnterpriseCssStylesThePicker()
    {
        var css = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/wwwroot/css/epc-enterprise-design-system.css"));
        Assert.Contains(".epc-erp-master-picker", css, StringComparison.Ordinal);
        Assert.Contains(".epc-erp-master-picker__q", css, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
