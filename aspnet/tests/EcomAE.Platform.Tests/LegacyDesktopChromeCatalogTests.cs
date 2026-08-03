using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacyDesktopChromeCatalogTests
{
    [Fact]
    public void ControlPanelTopnavCoversPhpNavLabels()
    {
        var groups = LegacyDesktopChromeCatalog.ControlPanelTopnav();
        Assert.Equal(LegacyChromeNavCatalog.ControlPanel.Count, groups.Count);
        Assert.All(groups, g => Assert.NotEmpty(g.Links));
        Assert.Contains(groups, g => g.Links.Any(l => l.Href.StartsWith("/CP/", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void ErpTopnavUsesCategoryAreaMapNotArbitrarySlice()
    {
        var groups = LegacyDesktopChromeCatalog.ErpTopnav();
        Assert.Equal(PhpModuleCatalog.ErpCategoryCount, groups.Count);

        var r2r = Assert.Single(groups, g => g.Id == "record_to_report");
        Assert.Contains(r2r.Links, l => string.Equals(l.Group, "finance", StringComparison.OrdinalIgnoreCase));

        var o2c = Assert.Single(groups, g => g.Id == "order_to_cash");
        Assert.Contains(o2c.Links, l => string.Equals(l.Group, "sales", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BosTopnavHasSectionPanelsAndPhpReachableLinks()
    {
        var groups = LegacyDesktopChromeCatalog.BosTopnav();
        Assert.Equal(LegacyChromeNavCatalog.Bos.Count, groups.Count);
        Assert.All(groups, g => Assert.NotEmpty(g.Links));
        Assert.Contains(groups.SelectMany(g => g.Links), l =>
            l.Href.StartsWith("/BOS/", StringComparison.OrdinalIgnoreCase)
            || l.Href.StartsWith("/CP/", StringComparison.OrdinalIgnoreCase)
            || l.Href.StartsWith("/ERP/", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("cp", "#header")]
    [InlineData("erp", ".epc-erp-topnav")]
    [InlineData("bos", ".bos-topnav")]
    [InlineData("storefront", "#header-full-top")]
    public void RequiredStructuralSelectorsDocumentProbeTargets(string surface, string expected)
    {
        var selectors = LegacyDesktopChromeCatalog.RequiredStructuralSelectors(surface);
        Assert.Contains(expected, selectors);
    }
}
