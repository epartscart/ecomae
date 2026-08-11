using System.Reflection;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards www.ecomae.com ASP.NET home against half-PHP / half-ASP.NET mixing.
/// Product home must match PHP epc_ecomae_platform_page_home (no hybrid module directory).
/// </summary>
public sealed class EcomaeMarketingHomeParityTests
{
    [Fact]
    public void MarketingPreviewApp_HasNoHybridModuleDirectory()
    {
        var path = FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/MarketingPreviewApp.razor");
        var text = File.ReadAllText(path);
        Assert.DoesNotContain("PhpHybridModuleDirectory", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-moddir", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("php-reference/home", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PhpEcomaeMarketingHub", text, StringComparison.Ordinal);
        Assert.Contains("PhpEcomaeLifeOsFilmBand", text, StringComparison.Ordinal);
        Assert.Contains("PhpEcomaeHomeSections", text, StringComparison.Ordinal);
        Assert.Contains("PhpEcomaeLaylaWidget", text, StringComparison.Ordinal);
        Assert.Contains("Unified ERP", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf("PhpEcomaeMarketingHub", StringComparison.Ordinal)
            < text.IndexOf("PhpEcomaeLifeOsFilmBand", StringComparison.Ordinal));
        Assert.True(text.IndexOf("PhpEcomaeLifeOsFilmBand", StringComparison.Ordinal)
            < text.IndexOf("PhpEcomaeHomeSections", StringComparison.Ordinal));
        Assert.True(text.IndexOf("PhpEcomaeHomeSections", StringComparison.Ordinal)
            < text.IndexOf("PhpEcomaeLaylaWidget", StringComparison.Ordinal));
        Assert.True(text.IndexOf("</PhpEcomaeMarketingChrome>", StringComparison.Ordinal)
            < text.IndexOf("PhpEcomaeLaylaWidget", StringComparison.Ordinal));
    }

    [Fact]
    public void LifeOsFilmBand_IsVideoForwardPlatformExplainer()
    {
        var path = FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpEcomaeLifeOsFilmBand.razor");
        var text = File.ReadAllText(path);
        Assert.Contains("epm-lofilm", text, StringComparison.Ordinal);
        Assert.Contains("epm-lofilm-video", text, StringComparison.Ordinal);
        Assert.Contains("Understand the LifeOS platform", text, StringComparison.Ordinal);
        Assert.Contains("Film.VideoUrl", text, StringComparison.Ordinal);
        Assert.Contains("Explore LifeOS", text, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"epm-hub", text, StringComparison.Ordinal); // must not emit hero markup
    }

    [Fact]
    public void HomeSections_UsesPhpDemoPortalBlock()
    {
        var path = FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpEcomaeHomeSections.razor");
        var text = File.ReadAllText(path);
        Assert.Contains("PhpEcomaeDemoPortal", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Try the platform", text, StringComparison.Ordinal);
    }

    [Fact]
    public void DemoPortalAndLaylaComponentsEmitPhpMarkers()
    {
        var demo = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpEcomaeDemoPortal.razor"));
        Assert.Contains("epc-demo-portal", demo, StringComparison.Ordinal);
        Assert.Contains("Try ecomae live", demo, StringComparison.Ordinal);
        Assert.Contains("demo@ecomae.com", demo, StringComparison.Ordinal);
        Assert.Contains("demo1234", demo, StringComparison.Ordinal);
        Assert.Contains("erp-demo?demo=1", demo, StringComparison.Ordinal);

        var layla = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpEcomaeLaylaWidget.razor"));
        Assert.Contains("epc-layla-splash", layla, StringComparison.Ordinal);
        Assert.Contains("epc-layla-footer-widget", layla, StringComparison.Ordinal);
        Assert.Contains("/platform-assets/layla-avatar.svg", layla, StringComparison.Ordinal);
        Assert.DoesNotContain("<HeadContent>", layla, StringComparison.Ordinal);
        Assert.Contains("splashIsModal", layla, StringComparison.Ordinal);
        Assert.Contains("unlockScroll", layla, StringComparison.Ordinal);
        Assert.DoesNotContain("<HeadContent>", demo, StringComparison.Ordinal);
    }

    [Fact]
    public void MarketingStylesIncludeLaylaAndDemoPortalCss()
    {
        Assert.Contains(LegacyPresentationAssets.MarketingStylesheets, href => href.Contains("epc_ecomae_layla_widget.css", StringComparison.Ordinal));
        Assert.Contains(LegacyPresentationAssets.MarketingStylesheets, href => href.Contains("epc_ecomae_demo_portal.css", StringComparison.Ordinal));
        Assert.True(File.Exists(FindRepoFile("content/general_pages/epc_ecomae_layla_widget.css")));
        Assert.True(File.Exists(FindRepoFile("content/general_pages/epc_ecomae_demo_portal.css")));
        Assert.True(File.Exists(FindRepoFile("content/files/images/ecomae-platform/layla-avatar.svg")));
    }

    [Fact]
    public void HomeSections_VerifyProofUsesPhpBlockchainVerify()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpEcomaeHomeSections.razor"));
        Assert.Contains("/epc-blockchain-verify.php", text, StringComparison.Ordinal);
        Assert.Contains("epc-ehm-rev-fallback", text, StringComparison.Ordinal);
    }

    [Fact]
    public void MarketingChromeFooterMatchesPhpBreadth()
    {
        var path = FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpEcomaeMarketingChrome.razor");
        var text = File.ReadAllText(path);
        Assert.Contains("epm-footer__legal", text, StringComparison.Ordinal);
        Assert.Contains("href=\"/blockchain\"", text, StringComparison.Ordinal);
        Assert.Contains("href=\"/privacy\"", text, StringComparison.Ordinal);
        Assert.Contains("href=\"/platform\"", text, StringComparison.Ordinal);
        Assert.Contains("href=\"/platform/pricing\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/marketing/platform", text, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/marketing/privacy", text, StringComparison.Ordinal);
        Assert.Contains("Electronic World Group", text, StringComparison.Ordinal);
    }

    [Fact]
    public void MarketingHubOrbitClassUsesRazorInterpolation()
    {
        var path = FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpEcomaeMarketingHub.razor");
        var text = File.ReadAllText(path);
        Assert.DoesNotContain("class=\"epm-hub__node@featured\"", text, StringComparison.Ordinal);
        Assert.Contains("epm-hub__node{featured}", text, StringComparison.Ordinal);
        Assert.Contains("href=\"/platform\"", text, StringComparison.Ordinal);
        Assert.Contains("href=\"/platform/demo\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void MarketingStubMapsToPhpCanonicalPaths()
    {
        Assert.True(EcomaeMarketingPages.TryMapMarketingStubToPhp("/marketing/platform", out var platform));
        Assert.Equal("/platform", platform);
        Assert.True(EcomaeMarketingPages.TryMapMarketingStubToPhp("/marketing/pricing", out var pricing));
        Assert.Equal("/platform/pricing", pricing);
        Assert.True(EcomaeMarketingPages.TryMapMarketingStubToPhp("/marketing/bos", out var bos));
        Assert.Equal(EcomaeMarketingPages.BosKnowledgePhp, bos);
        Assert.True(EcomaeMarketingPages.TryMapMarketingStubToPhp("/marketing/privacy#top", out var privacy));
        Assert.Equal("/privacy#top", privacy);
        Assert.False(EcomaeMarketingPages.TryMapMarketingStubToPhp("/marketing/app", out _));
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

            // Walk up from test bin to repo root.
            var alt = Path.Combine(dir.FullName, "..", "..", "..", "..", "..", relative);
            alt = Path.GetFullPath(alt);
            if (File.Exists(alt))
            {
                return alt;
            }

            dir = dir.Parent;
        }

        // Fallback: assembly location relative search used by other tests.
        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var rooted = Path.GetFullPath(Path.Combine(asmDir, "..", "..", "..", "..", "..", relative));
        Assert.True(File.Exists(rooted), $"Missing repo file: {relative} (looked at {rooted})");
        return rooted;
    }
}
