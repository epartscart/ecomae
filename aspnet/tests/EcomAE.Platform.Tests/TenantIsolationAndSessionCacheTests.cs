using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Data;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class TenantIsolationAndSessionCacheTests
{
    [Fact]
    public async Task SeedRegistryCarriesDedicatedCredentials()
    {
        var options = Options.Create(new EcomAeOptions
        {
            PlatformHost = "www.ecomae.com",
            SeedTenants =
            [
                new TenantSeedOptions
                {
                    Host = "tenant.example",
                    SiteKey = "tenant_example",
                    DatabaseName = "tenant_db",
                    DbUser = "tenant_user",
                    DbPassword = "secret-not-for-logs",
                    DedicatedDb = true,
                    ScalePolicy = "dedicated_mysql",
                    Mode = TenantMode.LiveTenant
                }
            ]
        });

        var registry = new ConfigurationTenantRegistry(options);
        var record = await registry.FindByHostAsync("tenant.example");

        Assert.NotNull(record);
        Assert.Equal("tenant_db", record!.DatabaseName);
        Assert.Equal("tenant_user", record.DbUser);
        Assert.Equal("secret-not-for-logs", record.DbPassword);
        Assert.True(record.DedicatedDb);
        Assert.Equal("dedicated_mysql", record.ScalePolicy);
    }

    [Fact]
    public async Task ResolverPropagatesCredentialsIntoTenantContext()
    {
        var options = Options.Create(new EcomAeOptions
        {
            PlatformHost = "www.ecomae.com",
            SeedTenants =
            [
                new TenantSeedOptions
                {
                    Host = "tenant.example",
                    SiteKey = "tenant_example",
                    DatabaseName = "tenant_db",
                    DbUser = "u1",
                    DbPassword = "p1",
                    DedicatedDb = true,
                    Mode = TenantMode.LiveTenant
                }
            ]
        });
        var registry = new ConfigurationTenantRegistry(options);
        var resolver = new RouteTenantResolver(options, registry);
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("tenant.example");
        context.Request.Path = "/erp";

        var tenant = await resolver.ResolveAsync(context);

        Assert.Equal("tenant_db", tenant.DatabaseName);
        Assert.Equal("u1", tenant.DbUser);
        Assert.Equal("p1", tenant.DbPassword);
        Assert.True(tenant.DedicatedDb);
        Assert.True(tenant.HasTenantDatabase);
    }

    [Fact]
    public void PortalTenantRowMapsCredentialsWithoutBosDefault()
    {
        var row = new PortalTenantRow(
            SiteKey: "acme",
            Hostname: "acme.example",
            DatabaseName: "acme_db",
            DbUser: "acme_user",
            DbPassword: "acme_pass",
            Status: "live",
            IsDemo: false,
            ErpOnlyShared: false,
            IsActive: true,
            DedicatedDb: true,
            ScalePolicy: "dedicated_mysql");

        var record = row.ToTenantRegistryRecord();

        Assert.Equal("acme_db", record.DatabaseName);
        Assert.Equal("acme_user", record.DbUser);
        Assert.Equal("acme_pass", record.DbPassword);
        Assert.True(record.DedicatedDb);
        Assert.Equal(TenantMode.LiveTenant, record.Mode);
        Assert.False(record.BosEnabled);
    }

    [Fact]
    public void ErpDashboardBatchSqlContainsCoreKpiColumns()
    {
        var sql = LegacySurfaceDashboardSql.SelectErpDashboardSummaryBatch;
        Assert.Contains("cash_position", sql, StringComparison.Ordinal);
        Assert.Contains("stock_value", sql, StringComparison.Ordinal);
        Assert.Contains("overdue_invoices", sql, StringComparison.Ordinal);
        Assert.Contains("@dateFrom", sql, StringComparison.Ordinal);
        Assert.Contains("@periodKey", sql, StringComparison.Ordinal);
        Assert.Contains("@overdueBefore", sql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SessionCacheScopesByHostAndDatabase()
    {
        var inner = new CountingSessionStore();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        http.HttpContext.Request.Host = new HostString("tenant-a.example");
        http.HttpContext.Items[TenantResolutionMiddleware.HttpContextItemKey] =
            TenantContext.ForKnownTenant("a", "tenant-a.example", TenantMode.LiveTenant, TenantSurface.Erp, "/erp", "db_a");

        var store = new CachingLegacySessionStore(
            inner,
            cache,
            Options.Create(new SessionCacheOptions { Enabled = true, SessionExistsTtlSeconds = 60, IdentityTtlSeconds = 60 }),
            http);

        Assert.True(await store.AdminSessionExistsAsync("tok", 7));
        Assert.True(await store.AdminSessionExistsAsync("tok", 7));
        Assert.Equal(1, inner.AdminCalls);

        http.HttpContext.Items[TenantResolutionMiddleware.HttpContextItemKey] =
            TenantContext.ForKnownTenant("b", "tenant-a.example", TenantMode.LiveTenant, TenantSurface.Erp, "/erp", "db_b");
        Assert.True(await store.AdminSessionExistsAsync("tok", 7));
        Assert.Equal(2, inner.AdminCalls);
    }

    private sealed class CountingSessionStore : ILegacySessionStore
    {
        public int AdminCalls { get; private set; }

        public bool IsConfigured => true;

        public Task<bool> AdminSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
        {
            AdminCalls++;
            return Task.FromResult(true);
        }

        public Task<bool> CustomerSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<LegacyAdminIdentity?> GetAdminIdentityAsync(int userId, CancellationToken cancellationToken = default)
            => Task.FromResult<LegacyAdminIdentity?>(null);
    }
}
