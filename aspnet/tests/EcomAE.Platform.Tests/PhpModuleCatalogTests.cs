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
        Assert.True(PhpModuleCatalog.BosSectionCount >= 11);
        Assert.True(PhpModuleCatalog.BosModuleCount >= 90);
        Assert.True(PhpModuleCatalog.StorefrontSurfaceCount >= 10);

        Assert.Equal(PhpModuleCatalog.CpBrochureFeatureCount, PhpModuleCatalog.CpBrochureFeatures.Count);
        Assert.Equal(PhpModuleCatalog.ErpAreaCount, PhpModuleCatalog.ErpAreas.Count);
        Assert.Equal(PhpModuleCatalog.ErpTabCount, PhpModuleCatalog.ErpTabs.Count);
        Assert.Equal(PhpModuleCatalog.ErpCategoryCount, PhpModuleCatalog.ErpCategories.Count);
        Assert.Equal(PhpModuleCatalog.BosSectionCount, PhpModuleCatalog.BosSections.Count);
        Assert.Equal(PhpModuleCatalog.BosModuleCount, PhpModuleCatalog.BosModules.Count);
        Assert.Equal(PhpModuleCatalog.StorefrontSurfaceCount, PhpModuleCatalog.StorefrontSurfaces.Count);
        Assert.True(PhpModuleCatalog.MarketingSurfaceCount >= 30);
        Assert.Equal(PhpModuleCatalog.MarketingSurfaceCount, PhpModuleCatalog.MarketingSurfaces.Count);
    }

    [Fact]
    public void BuildSummaryReportsHybridPolicyAndZeroInteractiveComplete()
    {
        var summary = PhpModuleCatalog.BuildSummary();
        Assert.Equal("aspnet-primary-browse-php-reference-only", summary["policy"]);
        Assert.Equal(0, summary["aspNetInteractiveComplete"]);
        Assert.False((bool)summary["cutoverAllowed"]);
        Assert.False((bool)summary["readyForPhpRemoval"]);
        Assert.True((int)summary["totalTracked"] >= 725);
        Assert.True((bool)summary["deeplinkFloorOk"]);
        var coverage = Assert.IsType<Dictionary<string, object>>(summary["directoryCoverage"]);
        Assert.Equal(725, coverage["fullCatalogFloor"]);
        Assert.Equal("ErpCategories+ErpAreas+ErpTabs", coverage["erpDashboard"]);
        Assert.Equal("BosSections+BosModules", coverage["bosFleet"]);
        Assert.Empty(Assert.IsType<string[]>(coverage["omittedKinds"]));
    }

    [Fact]
    public void HybridWorkspaceHrefEncodesPhpReferencePathOnly()
    {
        var href = PhpModuleCatalog.HybridWorkspaceHref("/cp/app", "/CP/control/shop/orders");
        Assert.StartsWith("/cp/app?php=", href, StringComparison.Ordinal);
        Assert.Contains("php-reference", href, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("%2FCP%2F", href, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void EveryTrackedCatalogHrefIsAllowedPhpDeeplink()
    {
        Assert.True(PhpModuleCatalog.TotalTrackedCount >= 725);
        var bad = PhpModuleCatalog.AllTrackedLinks()
            .Where(link => !PhpModuleCatalog.IsAllowedTrackedHref(link.Href))
            .Select(link => $"{link.Id}:{link.Href}")
            .Take(20)
            .ToArray();
        Assert.True(bad.Length == 0, "disallowed tracked hrefs: " + string.Join(", ", bad));
    }

    [Fact]
    public void CatalogIdsAreUniqueWithinEachDirectoryList()
    {
        // Cross-kind id reuse is allowed (e.g. ERP area "logistics" vs storefront surface);
        // each hybrid directory list must still be unique so nothing is omitted/overwritten.
        static void AssertUnique(IReadOnlyList<PhpModuleCatalog.ModuleLink> links, string label)
        {
            var ids = links.Select(link => link.Id).ToArray();
            Assert.True(ids.Length == ids.Distinct(StringComparer.Ordinal).Count(), $"{label} has duplicate ModuleLink ids");
        }

        AssertUnique(PhpModuleCatalog.ErpCategories, "ErpCategories");
        AssertUnique(PhpModuleCatalog.ErpAreas, "ErpAreas");
        AssertUnique(PhpModuleCatalog.ErpTabs, "ErpTabs");
        AssertUnique(PhpModuleCatalog.BosSections, "BosSections");
        AssertUnique(PhpModuleCatalog.BosModules, "BosModules");
        AssertUnique(PhpModuleCatalog.CpBrochureFeatures, "CpBrochureFeatures");
        AssertUnique(PhpModuleCatalog.StorefrontSurfaces, "StorefrontSurfaces");
        AssertUnique(PhpModuleCatalog.MarketingSurfaces, "MarketingSurfaces");
        Assert.True(PhpModuleCatalog.TotalTrackedCount >= 725);
    }

    [Fact]
    public void EveryTrackedCatalogHrefBuildsHybridWorkspaceUrl()
    {
        foreach (var link in PhpModuleCatalog.AllTrackedLinks())
        {
            var href = PhpModuleCatalog.HybridWorkspaceHref("/cp/app", link.Href);
            Assert.StartsWith("/cp/app?php=", href, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(href));
        }
    }

    [Fact]
    public void IsAllowedPhpDeeplinkRejectsAspNetPreviewRoutes()
    {
        Assert.False(PhpModuleCatalog.IsAllowedPhpDeeplink("/cp/app"));
        Assert.False(PhpModuleCatalog.IsAllowedPhpDeeplink("/erp/dashboard-summary-app"));
        Assert.False(PhpModuleCatalog.IsAllowedPhpDeeplink("/storefront/cart-app"));
        Assert.False(PhpModuleCatalog.IsAllowedPhpDeeplink("javascript:alert(1)"));
        Assert.True(PhpModuleCatalog.IsAllowedPhpDeeplink("/CP/menu/menu_manager"));
        Assert.True(PhpModuleCatalog.IsAllowedPhpDeeplink("https://epartscart.com/shop/part_search"));
    }
}
