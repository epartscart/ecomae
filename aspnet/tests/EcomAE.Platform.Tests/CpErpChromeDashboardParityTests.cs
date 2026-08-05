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
        Assert.Contains("<span>Ecom BOS</span>", src);
        Assert.Contains("data-topnav-toggle=", src);
        Assert.Contains("hidden data-topnav-panel=", src);
        Assert.Contains("bindErpTopNav", src);
        Assert.DoesNotContain("epc-erp-topnav-item:hover .epc-erp-topnav-panel", src);
        Assert.DoesNotContain("font-size:.8rem", src);
        Assert.Contains("font-size:14px", src);
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
