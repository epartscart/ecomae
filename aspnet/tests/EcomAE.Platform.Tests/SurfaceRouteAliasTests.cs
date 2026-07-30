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
}
