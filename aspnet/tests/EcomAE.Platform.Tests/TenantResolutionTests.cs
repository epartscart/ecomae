using EcomAE.Platform.Configuration;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class TenantResolutionTests
{
    [Theory]
    [InlineData("www.ecomae.com", "/CP", TenantSurface.ControlPanel, TenantMode.Platform)]
    [InlineData("www.ecomae.com", "/ERP", TenantSurface.Erp, TenantMode.Platform)]
    [InlineData("www.ecomae.com", "/BOS", TenantSurface.Bos, TenantMode.Platform)]
    [InlineData("tenant.example", "/CP", TenantSurface.ControlPanel, TenantMode.LiveTenant)]
    [InlineData("tenant.example", "/ERP", TenantSurface.Erp, TenantMode.ErpOnlyTenant)]
    [InlineData("tenant.example", "/", TenantSurface.Storefront, TenantMode.LiveTenant)]
    [InlineData("tenant.example", "/api/v1/catalog", TenantSurface.Api, TenantMode.LiveTenant)]
    public async Task ResolverClassifiesSurfaceAndMode(string host, string path, TenantSurface surface, TenantMode mode)
    {
        var resolver = new RouteTenantResolver(Options.Create(new EcomAeOptions { PlatformHost = "www.ecomae.com" }));
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        context.Request.Path = path;

        var tenant = await resolver.ResolveAsync(context);

        Assert.Equal(surface, tenant.Surface);
        Assert.Equal(mode, tenant.Mode);
        Assert.Equal(host, tenant.Host);
    }
}
