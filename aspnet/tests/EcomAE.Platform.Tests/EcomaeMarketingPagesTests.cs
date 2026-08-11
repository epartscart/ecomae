using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class EcomaeMarketingPagesTests
{
    [Fact]
    public void CatalogCoversCoreMarketingRoutesFromPhpRouter()
    {
        Assert.True(EcomaeMarketingPages.Count >= 30);
        Assert.Contains(EcomaeMarketingPages.All, p => p.Id == "home" && p.Href == EcomaeMarketingPages.AspNetHome);
        Assert.Contains(EcomaeMarketingPages.All, p => p.Id == "platform");
        Assert.Contains(EcomaeMarketingPages.All, p => p.Id == "blockchain");
        Assert.Contains(EcomaeMarketingPages.All, p => p.Id == "docs");
        Assert.Contains(EcomaeMarketingPages.All, p => p.Id == "bos_marketing");
        Assert.Contains(EcomaeMarketingPages.All, p => p.Id == "privacy");
        Assert.Equal(EcomaeMarketingPages.Count, PhpModuleCatalog.MarketingSurfaceCount);
        Assert.Equal(EcomaeMarketingPages.Count, PhpModuleCatalog.MarketingSurfaces.Count);
    }

    [Fact]
    public void MarketingPathsAreAllowedPhpDeeplinks()
    {
        Assert.True(EcomaeMarketingPages.IsMarketingPhpPath("/"));
        Assert.True(EcomaeMarketingPages.IsMarketingPhpPath("/platform"));
        Assert.True(EcomaeMarketingPages.IsMarketingPhpPath("/platform/demo"));
        Assert.True(EcomaeMarketingPages.IsMarketingPhpPath("/bos"));
        Assert.True(EcomaeMarketingPages.IsMarketingPhpPath("https://www.ecomae.com/blockchain"));
        Assert.False(EcomaeMarketingPages.IsMarketingPhpPath("/BOS/"));
        Assert.False(EcomaeMarketingPages.IsMarketingPhpPath("/CP/"));
        // Primary product marketing routes are ASP.NET /marketing/* (not PHP live paths).
        Assert.False(EcomaeMarketingPages.IsMarketingPhpPath("/marketing/app"));

        foreach (var page in EcomaeMarketingPages.All)
        {
            Assert.True(PhpModuleCatalog.IsAllowedTrackedHref(page.Href), page.Href);
            Assert.True(PhpModuleCatalog.IsAllowedAspNetBrowseHref(page.Href), page.Href);
        }
    }

    [Fact]
    public void MarketingPresentationLockForbidsCutover()
    {
        var report = new MarketingPresentationLockReporter().BuildReport();
        Assert.Contains("parity-gate", report.Status, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("100%-aspnet-core-live-php-reference-kept", report.TargetEndState);
        Assert.False(report.CutoverAllowed);
        Assert.False(report.ReadyForPhpRemoval);
        Assert.Equal("/marketing/app", report.AspNetPreviewRoute);
        Assert.Contains(report.RequiredLiveMarkers, m => m.Contains("epm-hub", StringComparison.Ordinal));
        Assert.NotEmpty(report.UnlockCriteria);
        Assert.True(report.MarketingPageFloor >= 30);
    }

    [Fact]
    public void MarketingAssetsExposeAnimatedHubMarkers()
    {
        Assert.Contains(LegacyPresentationAssets.MarketingStylesheets, href => href.Contains("/platform-assets/epc_ecomae_platform_marketing.css", StringComparison.Ordinal));
        Assert.Contains(LegacyPresentationAssets.MarketingStylesheets, href => href.Contains("/platform-assets/epc_ecomae_home_sections.css", StringComparison.Ordinal));
        Assert.Contains(LegacyPresentationAssets.MarketingStylesheets, href => href.Contains("/platform-assets/epc_ecomae_marketing_lifeos_film.css", StringComparison.Ordinal));
        Assert.Contains(LegacyPresentationAssets.MarketingStylesheets, href => href.Contains("/platform-assets/epc_ecomae_layla_widget.css", StringComparison.Ordinal));
        Assert.Contains(LegacyPresentationAssets.MarketingStylesheets, href => href.Contains("/platform-assets/epc_ecomae_demo_portal.css", StringComparison.Ordinal));
        Assert.Contains(LegacyPresentationAssets.MarketingScripts, href => href.Contains("/platform-assets/epc_ecomae_home_3d.js", StringComparison.Ordinal));
        Assert.Equal("/platform-assets/ecomae-mark.svg", LegacyPresentationAssets.BrandMarkUrl);
        var home = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/MarketingPreviewApp.razor"));
        Assert.Contains("PhpEcomaeMarketingHub", home, StringComparison.Ordinal);
        Assert.Contains("PhpEcomaeLifeOsFilmBand", home, StringComparison.Ordinal);
        // Film band must follow hub (after hero), not precede it.
        Assert.True(home.IndexOf("PhpEcomaeMarketingHub", StringComparison.Ordinal)
            < home.IndexOf("PhpEcomaeLifeOsFilmBand", StringComparison.Ordinal));
        Assert.True(home.IndexOf("PhpEcomaeLifeOsFilmBand", StringComparison.Ordinal)
            < home.IndexOf("PhpEcomaeHomeSections", StringComparison.Ordinal));
        Assert.Contains(LegacyPresentationAssets.RequiredGraphicalMarkers("marketing"), m => m.Contains("epm-hub__orbit-spin", StringComparison.Ordinal));
        Assert.Equal("epm-body", LegacyPresentationAssets.BodyClassFor("marketing"));
        Assert.Contains("epm-hub", LegacyPresentationAssets.LegacyChromeSourceFor("marketing"), StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root with aspnet/EcomAE.AspNetCore.sln");
    }
}
