using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// ERP menu destinations must present like PHP erp_main panels — not marketing digest heroes.
/// </summary>
public sealed class ErpModuleBodyParityTests
{
    [Fact]
    public void ErpStylesheetsIncludeAspNetModuleParityCss()
    {
        Assert.Contains(
            LegacyPresentationAssets.ErpStylesheets,
            s => s.Contains("epc_erp_aspnet_module_parity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ErpAppSources_DoNotUseMarketingHeroGradients()
    {
        var root = FindRepoRoot();
        var dir = Path.Combine(root, "aspnet", "src", "EcomAE.Platform", "Components", "Pages");
        Assert.True(Directory.Exists(dir), dir);

        var offenders = new List<string>();
        foreach (var file in Directory.GetFiles(dir, "Erp*App.razor"))
        {
            var name = Path.GetFileName(file);
            if (string.Equals(name, "ErpLoginApp.razor", StringComparison.OrdinalIgnoreCase))
            {
                continue; // login keeps its own portal look
            }

            var text = File.ReadAllText(file);
            if (text.Contains("linear-gradient(135deg", StringComparison.OrdinalIgnoreCase)
                || text.Contains("-hero\"", StringComparison.Ordinal)
                || text.Contains("class=\"epc-") && text.Contains("-hero"))
            {
                // Allow class name only if not a hero section pattern
                if (text.Contains("linear-gradient(135deg", StringComparison.OrdinalIgnoreCase)
                    || System.Text.RegularExpressions.Regex.IsMatch(text, @"class=""epc-[a-z0-9]+-hero"""))
                {
                    offenders.Add(name);
                }
            }
        }

        Assert.True(offenders.Count == 0, "Marketing hero leftovers: " + string.Join(", ", offenders));
    }

    [Fact]
    public void ErpAppSources_PreferPhpTableAndKpiClasses()
    {
        var root = FindRepoRoot();
        var sales = Path.Combine(root, "aspnet", "src", "EcomAE.Platform", "Components", "Pages", "ErpSalesOrdersApp.razor");
        var text = File.ReadAllText(sales);
        Assert.Contains("epc-erp-kpi", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", text, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", text, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(_phpTab)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleAppEmbedsPhpReferenceHybridFrame()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "aspnet", "src", "EcomAE.Platform", "Components", "Pages", "ErpModuleApp.razor");
        var text = File.ReadAllText(path);
        Assert.Contains("PhpHybridWorkspaceFrame", text, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("payables", "/erp/module-app?tab=payables")]
    [InlineData("receivables", "/erp/module-app?tab=receivables")]
    public void BalanceTabsMapToPhpHybridModuleShell(string tab, string expected)
    {
        Assert.True(ErpPhpTabRouteMap.TryMapTab(tab, out var href));
        Assert.Equal(expected, href);
    }

    [Theory]
    [InlineData("/ERP/?epc_erp_shell=1&area=ap", "/erp/module-app?tab=payables")]
    [InlineData("/ERP/?epc_erp_shell=1&area=ar", "/erp/module-app?tab=receivables")]
    [InlineData("/ERP/?epc_erp_shell=1&area=purchasing", "/erp/purchase-orders-app")]
    [InlineData("/ERP/?epc_erp_shell=1&area=sales", "/erp/sales-orders-app")]
    [InlineData("/ERP/?epc_erp_shell=1&area=finance", "/erp/gl-journals-app")]
    public void AreaHubsMatchModuleNames(string php, string expected)
    {
        Assert.Equal(expected, PhpSurfaceLinkMap.AspNetPrimaryHref(php));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cp", "content", "shop", "finance", "erp", "ajax_erp.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cp", "content", "shop", "finance", "erp", "ajax_erp.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root.");
    }
}
