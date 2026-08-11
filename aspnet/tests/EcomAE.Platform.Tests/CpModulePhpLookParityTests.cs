using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards Super CP + Tenant CP module graphical presentation against PHP epc-scp-* look.
/// Invented per-app heroes must be neutralized by module-parity CSS; showcase apps use PHP markers.
/// </summary>
public sealed class CpModulePhpLookParityTests
{
    [Fact]
    public void ControlPanelStylesheets_IncludeAspNetModuleParityCss()
    {
        Assert.Contains(
            LegacyPresentationAssets.ControlPanelStylesheets,
            href => href.Contains("epc_cp_aspnet_module_parity", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            LegacyPresentationAssets.ControlPanelStylesheets,
            href => href.Contains("/platform-assets/epc_cp_aspnet_module_parity.css", StringComparison.Ordinal));
    }

    [Fact]
    public void ModuleParityCss_RemapsInventedHeroKpiTableToScp()
    {
        var css = File.ReadAllText(FindRepoFile("content/general_pages/epc_cp_aspnet_module_parity.css"));
        Assert.Contains("[class$=\"-hero\"]", css, StringComparison.Ordinal);
        Assert.Contains("epc-scp-panel__hero", css, StringComparison.Ordinal);
        Assert.Contains("epc-scp-kpi__card", css, StringComparison.Ordinal);
        Assert.Contains("epc-scp-data-table", css, StringComparison.Ordinal);
        Assert.Contains("epc-scp-table-card", css, StringComparison.Ordinal);
        Assert.Contains("linear-gradient(135deg, #0f172a", css, StringComparison.Ordinal);
    }

    [Fact]
    public void PhpHelperServesModuleParityCss()
    {
        var php = File.ReadAllText(FindRepoFile("content/general_pages/epc_cp_aspnet_module_parity_css.php"));
        Assert.Contains("epc_cp_aspnet_module_parity.css", php, StringComparison.Ordinal);
    }

    [Fact]
    public void PlatformAssetsBridge_ServesCpModuleParityCss()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs"));
        Assert.Contains("/platform-assets/epc_cp_aspnet_module_parity.css", text, StringComparison.Ordinal);
        Assert.Contains("content/general_pages/epc_cp_aspnet_module_parity.css", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PhpCpModulePageHeader_UsesScpPanelHero()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpModulePageHeader.razor"));
        Assert.Contains("epc-scp-panel__hero", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-dashboard__title", text, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref", text, StringComparison.Ordinal);
        Assert.DoesNotContain("linear-gradient", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PhpCpDesktopChrome_UsesPhpTopbarCtaGlyphsAndBosHost()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor"));
        Assert.Contains("epc-cp-topbar-cta__glyph", text, StringComparison.Ordinal);
        Assert.Contains("epc-cp-topbar-cta__label", text, StringComparison.Ordinal);
        Assert.Contains("epc-cp-bos-host", text, StringComparison.Ordinal);
        Assert.Contains("epc-boc-mode", text, StringComparison.Ordinal);
        Assert.Contains("Quick destinations", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CpUsersApp.razor")]
    [InlineData("CpGroupsApp.razor")]
    [InlineData("CpModulesApp.razor")]
    [InlineData("CpTenantsApp.razor")]
    public void ShowcaseCpApps_UsePhpScpMarkers(string fileName)
    {
        var text = File.ReadAllText(FindRepoFile(
            $"aspnet/src/EcomAE.Platform/Components/Pages/{fileName}"));
        Assert.Contains("PhpCpModulePageHeader", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-kpi", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-kpi__card", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-table-card", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-data-table", text, StringComparison.Ordinal);
        Assert.DoesNotContain("linear-gradient(135deg", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CpOrdersApp_KeepsPhpOmsDualPaneWithScpMarkers()
    {
        // #992 dual-pane OMS already uses PHP epc-scp / epc-orders-page markers;
        // do not regress to a thin PhpCpModulePageHeader list.
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/CpOrdersApp.razor"));
        Assert.Contains("epc-orders-page__hero", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-kpi__card", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-orders-workspace", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-data-table", text, StringComparison.Ordinal);
        Assert.Contains("CpOrdersOmsStylesheets", text, StringComparison.Ordinal);
        Assert.DoesNotContain("linear-gradient(135deg", text, StringComparison.Ordinal);
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
