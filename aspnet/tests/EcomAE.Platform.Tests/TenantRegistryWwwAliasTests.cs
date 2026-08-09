using EcomAE.Platform.Configuration;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class TenantRegistryWwwAliasTests
{
    [Theory]
    [InlineData("www.epartscart.com", "www.epartscart.com", "epartscart.com")]
    [InlineData("epartscart.com", "epartscart.com", "www.epartscart.com")]
    [InlineData("WWW.EPartsCart.com:443", "www.epartscart.com", "epartscart.com")]
    public void NormalizeHostAliases_PrefersExactThenWwwVariant(string host, string primary, string alias)
    {
        var aliases = PlatformHostPolicy.NormalizeHostAliases(host);
        Assert.Equal(2, aliases.Count);
        Assert.Equal(primary, aliases[0]);
        Assert.Equal(alias, aliases[1]);
    }

    [Fact]
    public void NormalizeHostAliases_EmptyHost_ReturnsEmpty()
    {
        Assert.Empty(PlatformHostPolicy.NormalizeHostAliases(null));
        Assert.Empty(PlatformHostPolicy.NormalizeHostAliases("   "));
    }

    [Fact]
    public async Task ConfigurationTenantRegistry_WwwHostMatchesBareSeedRow()
    {
        var options = Options.Create(new EcomAeOptions
        {
            PlatformHost = "www.ecomae.com",
            SeedTenants =
            [
                new TenantSeedOptions
                {
                    Host = "epartscart.com",
                    SiteKey = "epartscart",
                    DatabaseName = "epartscart_shop",
                    DbUser = "shop_user",
                    Mode = TenantMode.LiveTenant
                }
            ]
        });

        var registry = new ConfigurationTenantRegistry(options);
        var byWww = await registry.FindByHostAsync("www.epartscart.com");
        var byBare = await registry.FindByHostAsync("epartscart.com");

        Assert.NotNull(byWww);
        Assert.NotNull(byBare);
        Assert.Equal("epartscart_shop", byWww!.DatabaseName);
        Assert.Equal("epartscart_shop", byBare!.DatabaseName);
        Assert.Equal(byBare.SiteKey, byWww.SiteKey);
    }

    [Fact]
    public async Task ConfigurationTenantRegistry_BareHostMatchesWwwSeedRow()
    {
        var options = Options.Create(new EcomAeOptions
        {
            SeedTenants =
            [
                new TenantSeedOptions
                {
                    Host = "www.epartscart.com",
                    SiteKey = "epartscart",
                    DatabaseName = "epartscart_shop",
                    Mode = TenantMode.LiveTenant
                }
            ]
        });

        var registry = new ConfigurationTenantRegistry(options);
        var record = await registry.FindByHostAsync("epartscart.com");

        Assert.NotNull(record);
        Assert.Equal("epartscart_shop", record!.DatabaseName);
    }

    [Fact]
    public async Task Resolver_WwwRequestBindsShopDatabaseFromBareSeedHost()
    {
        var options = Options.Create(new EcomAeOptions
        {
            SeedTenants =
            [
                new TenantSeedOptions
                {
                    Host = "epartscart.com",
                    SiteKey = "epartscart",
                    DatabaseName = "epartscart_shop",
                    DbUser = "u",
                    DbPassword = "p",
                    Mode = TenantMode.LiveTenant
                }
            ]
        });
        var registry = new ConfigurationTenantRegistry(options);
        var resolver = new RouteTenantResolver(options, registry);
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("www.epartscart.com");
        context.Request.Path = "/";

        var tenant = await resolver.ResolveAsync(context);

        Assert.Equal(TenantMode.LiveTenant, tenant.Mode);
        Assert.Equal(TenantSurface.Storefront, tenant.Surface);
        Assert.True(tenant.HasTenantDatabase);
        Assert.Equal("epartscart_shop", tenant.DatabaseName);
    }

    [Fact]
    public void PortalTenantSql_SelectActiveTenantByHosts_PrefersShopDbThenExactHost()
    {
        Assert.Contains("`hostname` IN (@h0, @h1)", PortalTenantSql.SelectActiveTenantByHosts, StringComparison.Ordinal);
        Assert.Contains("CASE WHEN IFNULL(TRIM(`db_name`), '') <> '' THEN 0 ELSE 1 END", PortalTenantSql.SelectActiveTenantByHosts, StringComparison.Ordinal);
        Assert.Contains("CASE WHEN `hostname` = @h0 THEN 0 ELSE 1 END", PortalTenantSql.SelectActiveTenantByHosts, StringComparison.Ordinal);
        // erp_only_shared stubs without db_name must not outrank shop tenants.
        Assert.Contains("`erp_only_shared` ASC", PortalTenantSql.SelectActiveTenantByHosts, StringComparison.Ordinal);
        Assert.DoesNotContain("`erp_only_shared` DESC", PortalTenantSql.SelectActiveTenantByHosts, StringComparison.Ordinal);
        // Legacy db_pass column is absent on many registries — referencing it throws and
        // yields false tenant_db_unbound while portal already has db_name=docpart.
        Assert.DoesNotContain("`db_pass`", PortalTenantSql.SelectActiveTenantByHosts, StringComparison.Ordinal);
        Assert.DoesNotContain("`db_pass`", PortalTenantSql.SelectActiveTenantByHostsMinimal, StringComparison.Ordinal);
        Assert.Contains("0 AS dedicated_db", PortalTenantSql.SelectActiveTenantByHostsMinimal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StorefrontSearch_UnboundTenantShop_ReturnsWwwAliasHint()
    {
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        http.HttpContext.Items[TenantResolutionMiddleware.HttpContextItemKey] = new TenantContext(
            Host: "www.epartscart.com",
            Path: "/",
            Surface: TenantSurface.Storefront,
            Mode: TenantMode.LiveTenant,
            SiteKey: "epartscart",
            DatabaseName: null);

        var reporter = new SurfaceDashboardSummaryReporter(new ConfiguredNoopFactory(), http);
        var brands = await reporter.ListStorefrontArticleBrandsAsync("DA320", 20);
        var parts = await reporter.SearchStorefrontPartsAsync("DA320", 20);

        Assert.Equal("migration", brands.Source);
        Assert.Equal("migration", parts.Source);
        Assert.Contains("www alias", brands.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("epc_portal_tenants.hostname", parts.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(brands.Brands);
        Assert.Empty(parts.Rows);
    }

    private sealed class ConfiguredNoopFactory : ITenantDbConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("should not open when tenant shop DB is unbound");

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("should not open when tenant shop DB is unbound");

        public Task<System.Data.Common.DbConnection> OpenForTenantAsync(TenantContext? tenant, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("should not open when tenant shop DB is unbound");

        public Task<System.Data.Common.DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("should not open when tenant shop DB is unbound");
    }
}
