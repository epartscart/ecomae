using EcomAE.Platform.Services;
using EcomAE.Platform.Surfaces;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class SurfaceShellCatalogTests
{
    [Fact]
    public void BuildReturnsErpShellSectionsForErpOnlyTenant()
    {
        var tenant = new TenantContext("erp-only.example", "/ERP", TenantSurface.Erp, TenantMode.ErpOnlyTenant, "erp_only_example", "erp_only_example");
        var shell = new MigrationSurfaceShellCatalog().Build("erp", tenant);

        Assert.Equal("Super ERP / tenant ERP", shell.Surface);
        Assert.Equal("presentation-shell-scaffolded", shell.ShellStatus);
        Assert.Equal("ErpOnlyTenant", shell.TenantMode);
        Assert.Contains(shell.Sections, section => section.Key == "finance-dashboard" && section.MigrationStatus == "mapped");
        Assert.Contains(shell.NextParityChecks, check => check.Contains("ERP-only tenant", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildReturnsBosShellWithAuditSection()
    {
        var shell = new MigrationSurfaceShellCatalog().Build("bos", null);

        Assert.Equal("Super BOS", shell.Surface);
        Assert.Contains(shell.Sections, section => section.Key == "audit" && section.MigrationStatus == "pending-port");
    }
}
