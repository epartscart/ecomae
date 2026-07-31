using EcomAE.Platform.Configuration;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class MigrationRouteCutoverPolicyTests
{
    [Fact]
    public void ApiCanReceiveShadowTrafficButRequiresPhpFallback()
    {
        var policy = new MigrationRouteCutoverPolicy();
        var tenant = TenantContext.ForKnownTenant("platform", "www.ecomae.com", TenantMode.Platform, TenantSurface.Api, "/api/v1/catalog/status");

        var decision = policy.Decide(tenant);

        Assert.Equal("aspnet-shadow-with-php-fallback", decision.TargetRuntime);
        Assert.True(decision.ReadyForAspNetTraffic);
        Assert.True(decision.RequiresPhpFallback);
    }

    [Fact]
    public void AdminSurfacesStayPhpPrimaryUntilBusinessParityCompletes()
    {
        var policy = new MigrationRouteCutoverPolicy();
        var tenant = TenantContext.ForKnownTenant("platform", "www.ecomae.com", TenantMode.Platform, TenantSurface.Erp, "/erp/");

        var decision = policy.Decide(tenant);

        Assert.Equal("aspnet-shell-php-primary", decision.TargetRuntime);
        Assert.False(decision.ReadyForAspNetTraffic);
        Assert.Contains("Administrative shells", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void StorefrontStaysPhpPrimaryUntilCheckoutParityCompletes()
    {
        var policy = new MigrationRouteCutoverPolicy();
        var tenant = TenantContext.ForKnownTenant("tenant", "taxofin.com", TenantMode.LiveTenant, TenantSurface.Storefront, "/");

        var decision = policy.Decide(tenant);

        Assert.Equal("php-primary", decision.TargetRuntime);
        Assert.Contains("checkout", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApiCanBeDisabledByConfiguration()
    {
        var policy = new MigrationRouteCutoverPolicy(Options.Create(new MigrationRouteCutoverOptions { ApiShadowTrafficEnabled = false }));
        var tenant = TenantContext.ForKnownTenant("platform", "www.ecomae.com", TenantMode.Platform, TenantSurface.Api, "/api/v1/catalog/status");

        var decision = policy.Decide(tenant);

        Assert.Equal("php-primary", decision.TargetRuntime);
        Assert.False(decision.ReadyForAspNetTraffic);
        Assert.True(decision.RequiresPhpFallback);
    }

    [Fact]
    public void RouteConstantUsesMigrationNamespace()
    {
        Assert.Equal("/migration/route-cutover", EcomAeRoutes.MigrationRouteCutover);
    }
}
