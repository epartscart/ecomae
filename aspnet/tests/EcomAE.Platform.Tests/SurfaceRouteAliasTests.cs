using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class SurfaceRouteAliasTests
{
    [Fact]
    public void SurfaceAliasesIncludeLowercaseUppercaseAndTrailingSlashForms()
    {
        Assert.Contains("/cp", EcomAeRoutes.ControlPanelAliases);
        Assert.Contains("/cp/", EcomAeRoutes.ControlPanelAliases);
        Assert.Contains("/CP", EcomAeRoutes.ControlPanelAliases);
        Assert.Contains("/erp", EcomAeRoutes.ErpAliases);
        Assert.Contains("/ERP/", EcomAeRoutes.ErpAliases);
        Assert.Contains("/bos", EcomAeRoutes.BosAliases);
        Assert.Contains("/bos/", EcomAeRoutes.BosAliases);
        Assert.Contains("/BOS", EcomAeRoutes.BosAliases);
    }

    [Fact]
    public void ShellAliasCatalogsAreDocumentationOnlyNotDuplicateMinimalApis()
    {
        // Regression lock: ControlPanelModule/ErpModule/BosModule must not MapGet these
        // aliases alongside Blazor @page "/cp|/erp|/bos" (AmbiguousMatch → HTTP 500).
        Assert.Equal(4, EcomAeRoutes.ControlPanelAliases.Length);
        Assert.Equal(4, EcomAeRoutes.ErpAliases.Length);
        Assert.Equal(4, EcomAeRoutes.BosAliases.Length);
        Assert.DoesNotContain(EcomAeRoutes.ControlPanelAliases, a => a.Contains("login", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(EcomAeRoutes.ErpAliases, a => a.Contains("login", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(EcomAeRoutes.BosAliases, a => a.Contains("login", StringComparison.OrdinalIgnoreCase));
    }
}
