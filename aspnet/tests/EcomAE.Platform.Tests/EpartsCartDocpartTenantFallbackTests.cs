using EcomAE.Platform.Configuration;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class EpartsCartDocpartTenantFallbackTests
{
    [Theory]
    [InlineData("www.epartscart.com", null, true)]
    [InlineData("epartscart.com", null, true)]
    [InlineData("www.epartscart.com", "epartscart", true)]
    [InlineData("www.taxofinca.com", null, false)]
    [InlineData("www.ecomae.com", "platform", false)]
    public void IsEpartsCartHost_DetectsShopHost(string host, string? siteKey, bool expected)
    {
        Assert.Equal(expected, RouteTenantResolver.IsEpartsCartHost(host, siteKey));
    }

    [Fact]
    public async Task Resolver_FillsDocpart_WhenPortalDbNameMissing()
    {
        var options = Options.Create(new EcomAeOptions { PlatformHost = "www.ecomae.com" });
        var registry = new StubRegistry(new TenantRegistryRecord(
            Host: "www.epartscart.com",
            Mode: TenantMode.LiveTenant,
            SiteKey: "epartscart",
            DatabaseName: null,
            StorefrontEnabled: true,
            ErpEnabled: true,
            ControlPanelEnabled: true,
            BosEnabled: false));
        var resolver = new RouteTenantResolver(options, registry);
        var http = new DefaultHttpContext();
        http.Request.Host = new HostString("www.epartscart.com");
        http.Request.Path = "/cp/login";

        var tenant = await resolver.ResolveAsync(http);

        Assert.True(tenant.HasTenantDatabase);
        Assert.Equal("docpart", tenant.DatabaseName);
        Assert.Equal("epartscart", tenant.SiteKey);
        Assert.Equal(TenantMode.LiveTenant, tenant.Mode);
        Assert.Equal(TenantSurface.ControlPanel, tenant.Surface);
    }

    [Fact]
    public async Task Resolver_UsesSeedDocpart_WhenNoRegistryRow()
    {
        var options = Options.Create(new EcomAeOptions
        {
            PlatformHost = "www.ecomae.com",
            SeedTenants =
            [
                new TenantSeedOptions
                {
                    Host = "www.epartscart.com",
                    SiteKey = "epartscart",
                    DatabaseName = "docpart",
                    Mode = TenantMode.LiveTenant,
                }
            ]
        });
        var resolver = new RouteTenantResolver(options, new ConfigurationTenantRegistry(options));
        var http = new DefaultHttpContext();
        http.Request.Host = new HostString("www.epartscart.com");
        http.Request.Path = "/";

        var tenant = await resolver.ResolveAsync(http);

        Assert.Equal("docpart", tenant.DatabaseName);
        Assert.True(tenant.HasTenantDatabase);
    }

    private sealed class StubRegistry : ITenantRegistry
    {
        private readonly TenantRegistryRecord? _record;

        public StubRegistry(TenantRegistryRecord? record) => _record = record;

        public ValueTask<TenantRegistryRecord?> FindByHostAsync(string host, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_record);
    }
}
