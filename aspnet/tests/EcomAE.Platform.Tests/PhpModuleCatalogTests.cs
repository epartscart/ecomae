using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpModuleCatalogTests
{
    [Fact]
    public void GeneratedCatalogCoversFullPhpInventoryFloors()
    {
        Assert.True(PhpModuleCatalog.CpBrochureFeatureCount >= 380);
        Assert.True(PhpModuleCatalog.ErpAreaCount >= 35);
        Assert.True(PhpModuleCatalog.ErpTabCount >= 140);
        Assert.True(PhpModuleCatalog.ErpCategoryCount >= 8);
        Assert.True(PhpModuleCatalog.BosModuleCount >= 90);
        Assert.True(PhpModuleCatalog.StorefrontSurfaceCount >= 10);

        Assert.Equal(PhpModuleCatalog.CpBrochureFeatureCount, PhpModuleCatalog.CpBrochureFeatures.Count);
        Assert.Equal(PhpModuleCatalog.ErpAreaCount, PhpModuleCatalog.ErpAreas.Count);
        Assert.Equal(PhpModuleCatalog.ErpTabCount, PhpModuleCatalog.ErpTabs.Count);
        Assert.Equal(PhpModuleCatalog.ErpCategoryCount, PhpModuleCatalog.ErpCategories.Count);
        Assert.Equal(PhpModuleCatalog.BosModuleCount, PhpModuleCatalog.BosModules.Count);
        Assert.Equal(PhpModuleCatalog.StorefrontSurfaceCount, PhpModuleCatalog.StorefrontSurfaces.Count);
    }

    [Fact]
    public void BuildSummaryReportsHybridPolicyAndZeroInteractiveComplete()
    {
        var summary = PhpModuleCatalog.BuildSummary();
        Assert.Equal("hybrid-deeplink-to-php-until-aspnet-module-complete", summary["policy"]);
        Assert.Equal(0, summary["aspNetInteractiveComplete"]);
        Assert.True((int)summary["totalTracked"] > 500);
    }

    [Fact]
    public void HybridWorkspaceHrefEncodesPhpPath()
    {
        var href = PhpModuleCatalog.HybridWorkspaceHref("/cp/app", "/CP/control/shop/orders");
        Assert.StartsWith("/cp/app?php=", href, StringComparison.Ordinal);
        Assert.Contains("CP", href, StringComparison.Ordinal);
    }

    [Fact]
    public void ErpAndCpLinksPointAtPhpChrome()
    {
        Assert.Contains(PhpModuleCatalog.ErpAreas, a => a.Href.StartsWith("/ERP/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(PhpModuleCatalog.CpBrochureFeatures, f => f.Href.StartsWith("/CP/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(PhpModuleCatalog.BosModules, m =>
            m.Href.StartsWith("/BOS/", StringComparison.OrdinalIgnoreCase)
            || m.Href.StartsWith("/CP/", StringComparison.OrdinalIgnoreCase)
            || m.Href.StartsWith("/ERP/", StringComparison.OrdinalIgnoreCase));
    }
}
