using System.Reflection;
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
        Assert.Contains("PhpEcomaeHomeSections", text, StringComparison.Ordinal);
        Assert.Contains("PhpEcomaeLaylaWidget", text, StringComparison.Ordinal);
        Assert.Contains("Unified ERP", text, StringComparison.Ordinal);
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
        Assert.Contains("layla-avatar.svg", layla, StringComparison.Ordinal);
    }

    [Fact]
    public void MarketingChromeFooterMatchesPhpBreadth()
    {
        var path = FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpEcomaeMarketingChrome.razor");
        var text = File.ReadAllText(path);
        Assert.Contains("epm-footer__legal", text, StringComparison.Ordinal);
        Assert.Contains("/marketing/blockchain", text, StringComparison.Ordinal);
        Assert.Contains("/marketing/privacy", text, StringComparison.Ordinal);
        Assert.Contains("Electronic World Group", text, StringComparison.Ordinal);
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
