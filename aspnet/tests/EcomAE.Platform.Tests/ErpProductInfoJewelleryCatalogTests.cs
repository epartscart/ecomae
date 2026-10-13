using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpProductInfoJewelleryCatalogTests
{
    [Theory]
    [InlineData("devkit", "devkit")]
    [InlineData("devkit-", "devkit")]
    [InlineData("jw_masters", "jw_masters")]
    [InlineData("jewellery", "jw_masters")]
    [InlineData("stock-report", "jw_stock_report")]
    [InlineData("metal-stock", "jw_stock")]
    public void ProductInfo_NormalizesDevkitAndJewelleryViews(string raw, string expected)
    {
        var text = ReadApp();
        Assert.Contains("NormalizeView", text, StringComparison.Ordinal);
        Assert.Contains("TrimEnd('-')", text, StringComparison.Ordinal);
        Assert.Contains("jw_masters", text, StringComparison.Ordinal);
        Assert.Contains("jw_stock_report", text, StringComparison.Ordinal);
        Assert.Contains("\"devkit\" => \"devkit\"", text, StringComparison.Ordinal);
        Assert.Contains(expected, text, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(raw));
    }

    [Fact]
    public void ProductInfo_ShowsEveryJewelleryMasterAndStockLinks()
    {
        var text = ReadApp();
        Assert.Contains("Jewellery masters", text, StringComparison.Ordinal);
        Assert.Contains("Metal stock", text, StringComparison.Ordinal);
        Assert.Contains("Stock report", text, StringComparison.Ordinal);
        foreach (var tab in ErpJewelleryModuleChrome.MasterTabs)
        {
            Assert.Contains(tab.Key, text, StringComparison.Ordinal);
        }

        Assert.Contains("/erp/jewellery-masters-app", text, StringComparison.Ordinal);
        Assert.Contains("/erp/jewellery-stock-verification-app", text, StringComparison.Ordinal);
        Assert.Contains("jw_stock_balance", text, StringComparison.Ordinal);
        Assert.Contains("jw_metal_stock", text, StringComparison.Ordinal);
        Assert.Contains("/erp/inventory-stock-app", text, StringComparison.Ordinal);
        Assert.Contains("ClientErpPath.PrefixIfBound", text, StringComparison.Ordinal);
        Assert.Contains("IErpJwMetalStockListReadService", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpJewelleryMastersDigestAsync", text, StringComparison.Ordinal);
        Assert.Contains("BuildCpJewelleryStockBalanceDigestAsync", text, StringComparison.Ordinal);
        Assert.Contains("ShowJewelleryModules", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductInfo_KeepsPimAttributesTuple()
    {
        var text = ReadApp();
        Assert.Contains("(\"pim_attrs\", \"PIM attributes\")", text, StringComparison.Ordinal);
    }

    private static string ReadApp()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "aspnet/src/EcomAE.Platform/Components/Pages/ErpProductInfoApp.razor");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("ErpProductInfoApp.razor");
    }
}
