using EcomAE.Platform.Configuration;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// D365-style tenant hosts <c>{site_key}.operations.{country3}.ecomae.com</c> must
/// bind the same ERP-only tenant the <c>/cp/client-erp/{key}</c> path binds —
/// and every other host must keep resolving exactly as before (no link breakage).
/// </summary>
public sealed class ErpOperationsHostTests
{
    [Theory]
    [InlineData("indj.operations.uae.ecomae.com", "indj", "uae")]
    [InlineData("asap.operations.uae.ecomae.com", "asap", "uae")]
    [InlineData("fito.operations.uae.ecomae.com", "fito", "uae")]
    [InlineData("real.operations.ksa.ecomae.com", "real", "ksa")]
    [InlineData("xyz.operations.gbr.ecomae.com", "xyz", "gbr")]
    [InlineData("INDJ.OPERATIONS.UAE.ECOMAE.COM", "indj", "uae")]
    public void TryParse_OperationsHost(string host, string siteKey, string country)
    {
        Assert.True(ErpOperationsHost.TryParse(host, out var key, out var shard));
        Assert.Equal(siteKey, key);
        Assert.Equal(country, shard);
    }

    [Theory]
    [InlineData("www.ecomae.com")]
    [InlineData("ecomae.com")]
    [InlineData("cp.ecomae.com")]
    [InlineData("operations.ecomae.com")]
    [InlineData("indj.ecomae.com")]
    [InlineData("indj.operations.ecomae.com")]
    [InlineData("indj.operations.uae.ecomae.com.evil.test")]
    [InlineData("indj.ops.uae.ecomae.com")]
    [InlineData("indj.operations.ae.ecomae.com")]
    [InlineData("localhost")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_RejectsNonOperationsHosts(string? host)
    {
        Assert.False(ErpOperationsHost.TryParse(host, out _, out _));
    }

    [Fact]
    public void HostFor_BuildsCanonicalHost()
    {
        Assert.Equal("indj.operations.uae.ecomae.com", ErpOperationsHost.HostFor("indj"));
        Assert.Equal("asap.operations.ksa.ecomae.com", ErpOperationsHost.HostFor("asap", "ksa"));
        Assert.Equal("indj.operations.uae.ecomae.com", ErpOperationsHost.HostFor("INDJ", null));
    }

    [Fact]
    public void CanonicalUrl_MapsRootToErp()
    {
        Assert.Equal(
            "https://indj.operations.uae.ecomae.com/erp",
            ErpOperationsHost.CanonicalUrl("indj", null, "/"));
        Assert.Equal(
            "https://asap.operations.uae.ecomae.com/erp/finance-app",
            ErpOperationsHost.CanonicalUrl("asap", "uae", "/erp/finance-app"));
    }

    [Fact]
    public async Task Resolver_BindsOperationsHostToErpOnlyTenant()
    {
        var options = EmptyOptions();
        var lookup = new StubClientErpLookup(new ClientErpTenantRecord(
            SiteKey: "indj",
            TradeName: "INDJ Jewellery",
            IndustryCode: "jewellery",
            DatabaseName: "indj_shop",
            DbUser: null,
            DbPassword: null,
            DedicatedDb: false,
            IsActive: true,
            IsDemo: false));
        var resolver = new RouteTenantResolver(options, new ConfigurationTenantRegistry(options), lookup);
        var http = new DefaultHttpContext();
        http.Request.Host = new HostString("indj.operations.uae.ecomae.com");
        http.Request.Path = "/erp";

        var tenant = await resolver.ResolveAsync(http);

        Assert.Equal(TenantMode.ErpOnlyTenant, tenant.Mode);
        Assert.Equal(TenantSurface.Erp, tenant.Surface);
        Assert.Equal("indj", tenant.SiteKey);
        Assert.Equal("indj_shop", tenant.DatabaseName);
        Assert.Equal("jewellery", tenant.IndustryCode);
    }

    [Fact]
    public async Task Resolver_OperationsHostRoot_StaysErpSurface()
    {
        var options = EmptyOptions();
        var lookup = new StubClientErpLookup(new ClientErpTenantRecord(
            SiteKey: "asap",
            TradeName: "ASAP Autoparts",
            IndustryCode: "auto_parts",
            DatabaseName: "asap_shop",
            DbUser: null,
            DbPassword: null,
            DedicatedDb: false,
            IsActive: true,
            IsDemo: false));
        var resolver = new RouteTenantResolver(options, new ConfigurationTenantRegistry(options), lookup);
        var http = new DefaultHttpContext();
        http.Request.Host = new HostString("asap.operations.uae.ecomae.com");
        http.Request.Path = "/";

        var tenant = await resolver.ResolveAsync(http);

        Assert.Equal(TenantMode.ErpOnlyTenant, tenant.Mode);
        Assert.Equal("asap", tenant.SiteKey);
        Assert.Equal("asap_shop", tenant.DatabaseName);
        Assert.Equal("auto_parts", tenant.IndustryCode);
    }

    [Fact]
    public async Task Resolver_UnknownOperationsSlug_FallsThroughSafely()
    {
        var options = EmptyOptions();
        var lookup = new StubClientErpLookup(null);
        var resolver = new RouteTenantResolver(options, new ConfigurationTenantRegistry(options), lookup);
        var http = new DefaultHttpContext();
        http.Request.Host = new HostString("nope.operations.uae.ecomae.com");
        http.Request.Path = "/erp";

        var tenant = await resolver.ResolveAsync(http);

        // No tenant DB leak: unknown slug resolves like any other unbound host's
        // /erp request — no database is bound, so nothing can read a wrong tenant.
        Assert.NotEqual("nope", tenant.SiteKey);
        Assert.True(string.IsNullOrWhiteSpace(tenant.DatabaseName));
    }

    [Fact]
    public void ActiveCompanyFromQuery_CmpMatchesEntityCode()
    {
        var companies = new List<ErpCompanyDigest>
        {
            new(1, "MAIN", "Main Co", "AED", "AE", "", true),
            new(2, "INDJ", "Jewellery Division", "AED", "AE", "jewellery_diamond", true),
        };
        var http = new DefaultHttpContext();
        http.Request.QueryString = new QueryString("?cmp=indj");

        var active = ErpHostContext.ActiveCompanyFromQuery(companies, http.Request);

        Assert.NotNull(active);
        Assert.Equal(2, active!.Id);
    }

    [Fact]
    public void ActiveCompanyFromQuery_NumericCmpMatchesEntityId()
    {
        var companies = new List<ErpCompanyDigest>
        {
            new(1, "MAIN", "Main Co", "AED", "AE", "", true),
            new(2, "INDJ", "Jewellery Division", "AED", "AE", "jewellery_diamond", true),
        };
        var http = new DefaultHttpContext();
        http.Request.QueryString = new QueryString("?cmp=2");

        var active = ErpHostContext.ActiveCompanyFromQuery(companies, http.Request);

        Assert.Equal(2, active!.Id);
    }

    [Fact]
    public void ActiveCompanyFromQuery_NoSelector_FallsBackToFirst()
    {
        var companies = new List<ErpCompanyDigest>
        {
            new(1, "MAIN", "Main Co", "AED", "AE", "", true),
        };
        var http = new DefaultHttpContext();

        var active = ErpHostContext.ActiveCompanyFromQuery(companies, http.Request);

        Assert.Equal(1, active!.Id);
    }

    private static IOptions<EcomAeOptions> EmptyOptions()
        => Options.Create(new EcomAeOptions
        {
            PlatformHost = "www.ecomae.com",
            SeedTenants =
            [
                new TenantSeedOptions
                {
                    Host = "www.ecomae.com",
                    SiteKey = "platform",
                    DatabaseName = "ecomae",
                    Mode = TenantMode.Platform,
                },
            ],
        });

    private sealed class StubClientErpLookup(ClientErpTenantRecord? record) : IClientErpTenantLookup
    {
        public ValueTask<ClientErpTenantRecord?> FindActiveErpOnlyAsync(string siteKey, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(record);

        public ValueTask<IReadOnlyList<ClientErpTenantRecord>> ListActiveErpOnlyAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<ClientErpTenantRecord>>(record is null ? [] : [record]);
    }
}
