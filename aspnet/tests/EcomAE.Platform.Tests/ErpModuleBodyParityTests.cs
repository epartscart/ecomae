using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// ERP menu destinations must look like PHP panels structurally, but production
/// product URLs stay ASP.NET-primary. PHP is temporary compare under /php-reference/* only.
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
                continue;
            }

            var text = File.ReadAllText(file);
            if (text.Contains("linear-gradient(135deg", StringComparison.OrdinalIgnoreCase)
                || System.Text.RegularExpressions.Regex.IsMatch(text, @"class=""epc-[a-z0-9]+-hero"""))
            {
                offenders.Add(name);
            }
        }

        Assert.True(offenders.Count == 0, "Marketing hero leftovers: " + string.Join(", ", offenders));
    }

    [Fact]
    public void ErpAppSources_PreferPhpTableAndKpiClasses_OnAspNetPrimary()
    {
        var root = FindRepoRoot();
        var sales = Path.Combine(root, "aspnet", "src", "EcomAE.Platform", "Components", "Pages", "ErpSalesOrdersApp.razor");
        var text = File.ReadAllText(sales);
        Assert.Contains("epc-erp-kpi", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
        Assert.Contains("PhpErpModulePageHeader", text, StringComparison.Ordinal);
        Assert.Contains("PhpErpD365ActionPane", text, StringComparison.Ordinal);
        Assert.Contains("AspNetPrimaryHref", text, StringComparison.Ordinal);
        // Row Open must not send production users into PHP reference as primary.
        Assert.DoesNotContain("PhpReferenceOnlyHref(phpHref)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpReferenceOnlyHref(_phpTab)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ModuleApp_DoesNotEmbedPhpByDefault()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "aspnet", "src", "EcomAE.Platform", "Components", "Pages", "ErpModuleApp.razor");
        var text = File.ReadAllText(path);
        Assert.Contains("PhpErpModulePageHeader", text, StringComparison.Ordinal);
        Assert.Contains("Opt-in only", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Query[\"php\"]", text, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref(_phpTab)", text, StringComparison.Ordinal);
        // Must not always-on embed with PhpHref="@_phpTab" outside the opt-in block.
        Assert.DoesNotContain("<PhpHybridWorkspaceFrame PhpHref=\"@_phpTab\"", text, StringComparison.Ordinal);
        // Compare CTA must not accidentally point at AspNetPrimaryHref.
        Assert.DoesNotContain("_compareHref = PhpSurfaceLinkMap.AspNetPrimaryHref", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedChrome_PhpReferenceIsSecondaryCompareOnly()
    {
        var root = FindRepoRoot();
        var header = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpModulePageHeader.razor"));
        Assert.Contains("btn-primary", header, StringComparison.Ordinal);
        Assert.Contains("AspNetPrimaryHref", header, StringComparison.Ordinal);
        Assert.Contains("Compare PHP reference", header, StringComparison.Ordinal);
        Assert.Contains("PhpReferenceOnlyHref", header, StringComparison.Ordinal);
        // Primary button must not use PhpReferenceOnlyHref
        var primaryIdx = header.IndexOf("btn-primary", StringComparison.Ordinal);
        var primaryBlock = header.Substring(primaryIdx, Math.Min(220, header.Length - primaryIdx));
        Assert.Contains("AspNetPrimaryHref", primaryBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpReferenceOnlyHref", primaryBlock, StringComparison.Ordinal);

        var pane = File.ReadAllText(Path.Combine(root,
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpD365ActionPane.razor"));
        Assert.Contains("is-primary", pane, StringComparison.Ordinal);
        Assert.Contains("AspNetPrimaryHref", pane, StringComparison.Ordinal);
        Assert.Contains("Compare PHP", pane, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("payables", "/erp/module-app?tab=payables")]
    [InlineData("receivables", "/erp/module-app?tab=receivables")]
    public void BalanceTabsMapToAspNetModuleShell(string tab, string expected)
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
    public void AreaHubsStayOnAspNetApps(string php, string expected)
    {
        var href = PhpSurfaceLinkMap.AspNetPrimaryHref(php);
        Assert.Equal(expected, href);
        Assert.DoesNotContain("/php-reference/", href, StringComparison.OrdinalIgnoreCase);
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
