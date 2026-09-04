using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpParityChromeWaveTests
{
    [Fact]
    public void PhpTabChrome_CoversRemainingErpTabs()
    {
        Assert.True(ErpPhpTabChromeRows.TryGet("tenant_config", out var tenant));
        Assert.False(string.IsNullOrWhiteSpace(tenant.Title));
        Assert.True(ErpPhpTabChromeRows.TryGet("collections", out var col));
        Assert.Contains(col.Columns, c => c.Contains("Customer", StringComparison.OrdinalIgnoreCase));
        Assert.True(ErpPhpTabChromeRows.All.Count >= 140);
    }

    [Fact]
    public void DumpCatalog_GroupsTabsByApp()
    {
        var tenant = PhpParityDumpCatalog.TabsForPath("/cp/tenant-config-app");
        Assert.Contains(tenant, t => t.Key == "tenant_config" || t.Key == "setup" || t.Key == "erp_setup");
        Assert.True(tenant.Count >= 3, "tenant-config should host multiple PHP setup tabs");

        var consol = PhpParityDumpCatalog.TabsForPath("/cp/consolidations-app");
        Assert.True(consol.Count >= 3);

        Assert.Equal("tenant_config", PhpParityDumpCatalog.DefaultTab("/cp/tenant-config-app", "tenant_config"));
        Assert.Equal("/cp/tenant-config-app", PhpParityDumpCatalog.NormalizePath("/cp/tenant-config-app?tab=setup"));
    }

    [Fact]
    public void ChromeCatalog_UsesPhpColumnsForDumpTabs()
    {
        var crm = ErpPhpModuleChromeCatalog.ForTab("tickets");
        Assert.Contains(crm.Sections[0].Columns, c => c.Contains("Subject", StringComparison.OrdinalIgnoreCase) || c == "#");
        var land = ErpPhpModuleChromeCatalog.ForTab("landed_cost");
        Assert.Contains(land.Sections[0].Columns, c => c.Contains("Freight", StringComparison.OrdinalIgnoreCase) || c.Contains("Amount", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ThinCpDumpApps_IncludeParityBody()
    {
        foreach (var name in new[]
                 {
                     "CpTenantConfigApp.razor", "CpConsolidationsApp.razor", "CpCrmTicketsApp.razor",
                     "CpBudgetsApp.razor", "CpFinAdvancedApp.razor", "CpLandedCostApp.razor",
                     "CpPosOverviewApp.razor", "CpWarehouseWmsApp.razor", "CpAmlComplianceApp.razor",
                 })
        {
            var text = ReadApp(name);
            Assert.Contains("PhpParityModuleBody", text, StringComparison.Ordinal);
            Assert.Contains("HasStaffAccess", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Nothing to show yet.", text, StringComparison.Ordinal);
            Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ParitySaveRoute_IsDedicatedHtmlPost()
    {
        Assert.Equal("/erp/php-parity/save", EcomAeRoutes.ErpParityModuleSaveForm);
        var body = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpParityModuleBody.razor"));
        Assert.Contains("ErpParityModuleSaveForm", body, StringComparison.Ordinal);
        Assert.Contains("method=\"post\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", body, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", body, StringComparison.Ordinal);
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
                return dir.FullName;
            dir = dir.Parent;
        }

        dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cp", "content", "shop", "finance", "erp", "ajax_erp.php")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root.");
    }
}
