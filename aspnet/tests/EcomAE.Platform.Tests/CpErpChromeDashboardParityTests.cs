using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guardrails so CP/ERP chrome stay aligned with PHP topnav (px fonts, click panels)
/// and dashboards keep NetSuite / Command Centre structural markers.
/// </summary>
public sealed class CpErpChromeDashboardParityTests
{
    [Fact]
    public void CpTopnavUsesPhpBrandAndClickToggle()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor"));
        Assert.Contains("<span>Control</span>", src);
        Assert.Contains("fa-th-large", src);
        Assert.Contains("data-topnav-toggle=", src);
        Assert.Contains("hidden data-topnav-panel=", src);
        Assert.Contains("epc-cp-topnav-caret", src);
        Assert.Contains("bindCpTopNav", src);
        Assert.DoesNotContain("epc-cp-topnav-item:hover .epc-cp-topnav-panel", src);
        Assert.DoesNotContain("font-size:.78rem", src);
        Assert.DoesNotContain(">CONTROL<", src);
    }

    [Fact]
    public void ErpTopnavUsesPhpBrandClickToggleAndPxShell()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor"));
        Assert.Contains("fa-cubes", src);
        Assert.Contains("BrandLabel", src);
        Assert.Contains("data-topnav-toggle=", src);
        Assert.Contains("hidden data-topnav-panel=", src);
        Assert.Contains("bindErpTopNav", src);
        Assert.DoesNotContain("epc-erp-topnav-item:hover .epc-erp-topnav-panel", src);
        Assert.DoesNotContain("font-size:.8rem", src);
        Assert.True(
            src.Contains("font-size:14px", StringComparison.Ordinal)
            || src.Contains("LegacyPhpFontAssets.BaseFontSize", StringComparison.Ordinal),
            "ERP chrome must use unified 14px base font size");
        Assert.Contains("LegacyPhpFontAssets.StackFor(\"erp\")", src);
        Assert.Contains("font-size:22px", src);
    }

    [Fact]
    public void CpCommandCentreUsesCommerceKpisNotAdminTenantPulse()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpCommandCentreApp.razor"));
        Assert.Contains("Orders today", src);
        Assert.Contains("Open orders", src);
        Assert.Contains("Warehouse qty", src);
        Assert.Contains("Vendors", src);
        Assert.Contains("Clients", src);
        Assert.DoesNotContain("Admin users", src);
        Assert.DoesNotContain("Portal tenants", src);
        Assert.DoesNotContain("PhpHybridModuleDirectory", src);
    }

    [Fact]
    public void ErpHomeUsesNetSuiteDashChartsAndGauge()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpBosDashboardApp.razor"));
        Assert.Contains("class=\"ns-dash\"", src);
        Assert.Contains("ns-hero", src);
        Assert.Contains("ns-gauge", src);
        Assert.Contains("id=\"nsChartAr\"", src);
        Assert.Contains("id=\"nsChartTrend\"", src);
        Assert.Contains("chart.js@4.4.1", src);
        Assert.Contains("A/R aging", src);
        Assert.DoesNotContain("PhpHybridModuleDirectory", src);
        Assert.DoesNotContain("epc-erp-banner", src);
    }

    [Fact]
    public void DesktopChromeCatalogStillDocumentsTopnavSelectors()
    {
        Assert.Contains(".epc-cp-topnav", LegacyDesktopChromeCatalog.RequiredStructuralSelectors("cp"));
        Assert.Contains(".epc-erp-topnav-panel-hub", LegacyDesktopChromeCatalog.RequiredStructuralSelectors("erp"));
    }

    [Fact]
    public void CpTopnavInlineScriptUsesPointerdownToggle()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor"));
        Assert.Contains("pointerdown", src);
        Assert.Contains("aria-expanded", src);
    }

    [Fact]
    public void CpIndustryPacksApp_KeepsStylesInHeadContentNotInsideChrome()
    {
        // Live HTML pipelines have stripped body <style> tags and left CSS as visible text
        // (e.g. /cp/industry-packs-app showing .epc-pack-hero { ... }). Styles must use HeadOutlet.
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpIndustryPacksApp.razor"));
        Assert.Contains("<HeadContent>", src, StringComparison.Ordinal);
        Assert.Contains(".epc-pack-hero", src, StringComparison.Ordinal);
        var styleAt = src.IndexOf("<style>", StringComparison.Ordinal);
        var chromeAt = src.IndexOf("<PhpCpDesktopChrome", StringComparison.Ordinal);
        Assert.True(styleAt >= 0 && chromeAt > styleAt, "page styles must appear before PhpCpDesktopChrome");
        Assert.DoesNotContain("<style>", src[chromeAt..], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpBosDesktopChrome.razor")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Layout/PhpChromeLayout.razor")]
    public void DesktopChrome_EmitsInlineStylesInHeadAndBodyStream(string relative)
    {
        // Live SSR drops HeadContent on /cp|/erp|/bos login + shells; body <style> is the durable path.
        var src = File.ReadAllText(FindRepoFile(relative));
        Assert.Contains("<HeadContent>", src, StringComparison.Ordinal);
        Assert.Contains("<style>", src, StringComparison.Ordinal);
        var open = src.IndexOf("<HeadContent>", StringComparison.Ordinal);
        var close = src.IndexOf("</HeadContent>", StringComparison.Ordinal);
        Assert.True(open >= 0 && close > open);
        var head = src.Substring(open, close - open);
        Assert.Contains("<style>", head, StringComparison.Ordinal);
        var after = src[(close + "</HeadContent>".Length)..];
        Assert.Contains("<style>", after, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor", "ecomae-cp-chrome-css")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor", "ecomae-erp-chrome-css")]
    [InlineData("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpBosDesktopChrome.razor", "ecomae-bos-chrome-css")]
    public void DesktopChrome_MarksBodyInlineCssFallback(string relative, string marker)
    {
        var src = File.ReadAllText(FindRepoFile(relative));
        Assert.Contains(marker, src, StringComparison.Ordinal);
        Assert.Contains("body-inline", src, StringComparison.Ordinal);
    }

    [Fact]
    public void StorefrontChrome_EmitsCriticalCssInBodyStreamForLiveProve()
    {
        // Live FORCE_LIVE prove saw HeadContent dropped for /storefront/app while
        // body <style> widgets survived — critical header CSS must live in body.
        var src = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor"));
        Assert.Contains("<HeadContent>", src, StringComparison.Ordinal);
        Assert.Contains("body-inline", src, StringComparison.Ordinal);
        var close = src.IndexOf("</HeadContent>", StringComparison.Ordinal);
        Assert.True(close > 0);
        var after = src[(close + "</HeadContent>".Length)..];
        Assert.Contains("<style>", after, StringComparison.Ordinal);
        Assert.Contains("header-call-box a { background:#ef4444", after, StringComparison.Ordinal);
        Assert.Contains("background:linear-gradient(135deg,#090f1d", after, StringComparison.Ordinal);
        Assert.Contains("color:rgba(255,255,255,.88) !important", after, StringComparison.Ordinal);
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

        throw new FileNotFoundException($"Could not locate {relative}");
    }
}
