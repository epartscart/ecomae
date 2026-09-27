using EcomAE.Platform.Cp;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpDemoTenantsServiceTests
{
    [Fact]
    public void Route_and_php_constants_match()
    {
        Assert.Equal("/cp/demo-tenants/write", EcomAeRoutes.CpDemoTenantsWrite);
        Assert.Equal(30, CpDemoTenantsService.MaxActive);
        Assert.Equal(3, CpDemoTenantsService.DemoDays);
        Assert.Equal("SELECT * FROM `epc_portal_tenants` WHERE `is_demo` = 1 ORDER BY `demo_expires_at` ASC, `created_at` DESC", CpDemoTenantsService.ListSql);
        Assert.Contains("`status` IN ('dns_pending', 'live')", CpDemoTenantsService.CountActiveSql, StringComparison.Ordinal);
    }

    [Fact]
    public void Helpers_match_php()
    {
        Assert.Equal("acmedemo_1", CpDemoTenantsService.NormalizeSiteKey(" Acme-Demo_1! "));
        Assert.Equal(0, CpDemoTenantsService.DaysLeft(0, 1000));
        Assert.Equal(0, CpDemoTenantsService.DaysLeft(500, 1000));
        Assert.Equal(1, CpDemoTenantsService.DaysLeft(1000 + 3600, 1000));
        Assert.Equal(3, CpDemoTenantsService.DaysLeft(1000 + (3 * 86400), 1000));
        Assert.True(CpDemoTenantsService.IsErpOnly("{\"demo_erp_only\":1}", "autoparts"));
        Assert.True(CpDemoTenantsService.IsErpOnly("", "erp_only"));
        Assert.False(CpDemoTenantsService.IsErpOnly("{\"demo_erp_only\":0}", "autoparts"));
        Assert.False(CpDemoTenantsService.IsErpOnly("not json", "autoparts"));
    }

    [Fact]
    public void Drop_refuses_protected_and_unsafe_names()
    {
        Assert.True(CpDemoTenantsService.IsDroppableDatabase("demo_acme_01"));
        Assert.False(CpDemoTenantsService.IsDroppableDatabase("docpart"));
        Assert.False(CpDemoTenantsService.IsDroppableDatabase("ECOMAE"));
        Assert.False(CpDemoTenantsService.IsDroppableDatabase("epartscart"));
        Assert.False(CpDemoTenantsService.IsDroppableDatabase(""));
        Assert.False(CpDemoTenantsService.IsDroppableDatabase("x`; DROP"));
    }

    [Fact]
    public void Page_is_php_twin_without_digest_or_plain_passwords()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpDemoTenantsApp.razor"));
        Assert.Contains("Demo sandbox tenants", razor, StringComparison.Ordinal);
        Assert.Contains("linear-gradient(135deg,#4c1d95,#7c3aed)", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"@WriteHref\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\" value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"extend\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"convert\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"delete\"", razor, StringComparison.Ordinal);
        Assert.Contains("Delete demo and drop DB?", razor, StringComparison.Ordinal);
        Assert.Contains("_isAdmin && _isSuper", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ISurfaceDashboardSummaryReporter", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("operator_temp_password\"]", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);

        var program = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpDemoTenantsService, EcomAE.Platform.Cp.CpDemoTenantsService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("EcomAeRoutes.CpDemoTenantsWrite", module, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repository root with aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj was not found.");
    }
}
