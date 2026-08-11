using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards Super ERP + Tenant ERP + ERP-only module graphical presentation vs PHP erp_ui.
/// </summary>
public sealed class ErpModulePhpLookParityTests
{
    [Fact]
    public void ModuleParityCss_RemapsHeroKpiTableUnderErpShell()
    {
        var css = File.ReadAllText(FindRepoFile("content/shop/finance/epc_erp_aspnet_module_parity.css"));
        Assert.Contains("[class$=\"-hero\"]", css, StringComparison.Ordinal);
        Assert.Contains("epc-erp-page-hd", css, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", css, StringComparison.Ordinal);
        Assert.Contains("table-epc", css, StringComparison.Ordinal);
        Assert.Contains(".epc-erp-cp-shell .epc-erp-content-body", css, StringComparison.Ordinal);
        Assert.Contains("background-image: none", css, StringComparison.Ordinal);
    }

    [Fact]
    public void PlatformAssetsBridge_ServesErpModuleParityCss()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs"));
        Assert.Contains("/platform-assets/epc_erp_aspnet_module_parity.css", text, StringComparison.Ordinal);
        Assert.Contains("content/shop/finance/epc_erp_aspnet_module_parity.css", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ErpStylesheetOrder_LoadsParityAfterProfessional()
    {
        var list = LegacyPresentationAssets.ErpStylesheets.ToList();
        var professional = list.FindIndex(s => s.Contains("epc_erp_professional", StringComparison.OrdinalIgnoreCase));
        var parity = list.FindIndex(s => s.Contains("epc_erp_aspnet_module_parity", StringComparison.OrdinalIgnoreCase));
        Assert.True(professional >= 0, "professional css missing");
        Assert.True(parity >= 0, "parity css missing");
        Assert.True(parity > professional, "parity must load after professional so remaps win");
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
