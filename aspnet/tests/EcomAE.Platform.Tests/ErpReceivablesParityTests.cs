using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Leftover from the ERP empty-tab digest wiring: receivables is PHP
/// <c>epc_erp_receivables</c> customers, not aging buckets.
/// </summary>
public sealed class ErpReceivablesParityTests
{
    [Fact]
    public void ReceivablesAppUsesPhpReceivablesDigestNotAging()
    {
        var text = ReadApp("ErpReceivablesApp.razor");
        Assert.Contains("BuildErpReceivablesDigestAsync", text, StringComparison.Ordinal);
        Assert.Contains("OrderReceivableDue", text, StringComparison.Ordinal);
        Assert.Contains("CompleteOrderCount", text, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildErpAgingDigestAsync", text, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
    }

    [Fact]
    public void OnPremisesAppWiresLicenseDigest()
    {
        var text = ReadApp("ErpOnPremisesApp.razor");
        Assert.Contains("ListOnPremisesLicensesAsync", text, StringComparison.Ordinal);
        Assert.Contains("LicenseKeyPreview", text, StringComparison.Ordinal);
        Assert.Contains("UsersMax", text, StringComparison.Ordinal);
        Assert.Contains("epc-erp-kpi", text, StringComparison.Ordinal);
        Assert.Contains("table-epc", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("receivables", "/erp/receivables-app")]
    [InlineData("on_premises", "/erp/on-premises-app")]
    [InlineData("print_designer", "/erp/print-designer-app")]
    [InlineData("vat", "/erp/vat-app")]
    public void CriticalTabsHaveDedicatedApps(string tab, string expected)
    {
        Assert.True(ErpPhpTabRouteMap.TryMapTab(tab, out var href));
        Assert.Equal(expected, href);
    }

    [Fact]
    public void ModuleAppListsTabCatalogWithoutStackLabels()
    {
        var text = ReadApp("ErpModuleApp.razor");
        Assert.Contains("ERP tab catalog", text, StringComparison.Ordinal);
        Assert.Contains("_hasDedicatedApp", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Compare PHP reference", text, StringComparison.Ordinal);
    }

    private static string ReadApp(string fileName)
    {
        var path = Path.Combine(FindRepoRoot(), "aspnet", "src", "EcomAE.Platform", "Components", "Pages", fileName);
        Assert.True(File.Exists(path), path);
        return File.ReadAllText(path);
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

        throw new InvalidOperationException("Could not locate repo root.");
    }
}
