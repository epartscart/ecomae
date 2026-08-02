using EcomAE.Platform.Services;
using EcomAE.Platform.Surfaces;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontShellCatalogTests
{
    [Fact]
    public void BuildReturnsStorefrontCommerceSections()
    {
        var tenant = new TenantContext("tenant.example", "/", TenantSurface.Storefront, TenantMode.LiveTenant, "tenant_example", "tenant_example");
        var shell = new MigrationSurfaceShellCatalog().Build("storefront", tenant);

        Assert.Equal("Storefront / customer commerce", shell.Surface);
        Assert.Equal("presentation-shell-scaffolded", shell.ShellStatus);
        Assert.Contains(shell.Sections, section => section.Key == "catalog" && section.Capabilities.Contains("part search"));
        Assert.Contains(shell.Sections, section => section.Key == "cart" && section.MigrationStatus == "pending-port");
        Assert.Contains(shell.NextParityChecks, check => check.Contains("checkout parity", StringComparison.Ordinal));
    }
}
