using EcomAE.Platform.Configuration;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class TenantResolutionTests
{
    [Theory]
    [InlineData("www.ecomae.com", "/CP", TenantSurface.ControlPanel, TenantMode.Platform, "platform")]
    [InlineData("www.ecomae.com", "/ERP", TenantSurface.Erp, TenantMode.Platform, "platform")]
    [InlineData("www.ecomae.com", "/BOS", TenantSurface.Bos, TenantMode.Platform, "platform")]
    [InlineData("tenant.example", "/CP", TenantSurface.ControlPanel, TenantMode.LiveTenant, "tenant_example")]
    [InlineData("erp-only.example", "/ERP", TenantSurface.Erp, TenantMode.ErpOnlyTenant, "erp_only_example")]
    [InlineData("tenant.example", "/", TenantSurface.Storefront, TenantMode.LiveTenant, "tenant_example")]
    [InlineData("tenant.example", "/api/v1/catalog", TenantSurface.Api, TenantMode.LiveTenant, "tenant_example")]
    public async Task ResolverClassifiesSurfaceAndMode(string host, string path, TenantSurface surface, TenantMode mode, string siteKey)
    {
        var options = Options.Create(new EcomAeOptions
        {
            PlatformHost = "www.ecomae.com",
            SeedTenants =
            [
                new TenantSeedOptions { Host = "www.ecomae.com", SiteKey = "platform", DatabaseName = "ecomae", Mode = TenantMode.Platform, BosEnabled = true },
                new TenantSeedOptions { Host = "tenant.example", SiteKey = "tenant_example", DatabaseName = "tenant_example", Mode = TenantMode.LiveTenant },
                new TenantSeedOptions { Host = "erp-only.example", SiteKey = "erp_only_example", DatabaseName = "erp_only_example", Mode = TenantMode.ErpOnlyTenant, StorefrontEnabled = false, ControlPanelEnabled = false }
            ]
        });
        var registry = new ConfigurationTenantRegistry(options);
        var resolver = new RouteTenantResolver(options, registry);
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        context.Request.Path = path;

        var tenant = await resolver.ResolveAsync(context);

        Assert.Equal(surface, tenant.Surface);
        Assert.Equal(mode, tenant.Mode);
        Assert.Equal(host, tenant.Host);
        Assert.Equal(siteKey, tenant.SiteKey);
        Assert.False(string.IsNullOrWhiteSpace(tenant.DatabaseName));
    }
}
