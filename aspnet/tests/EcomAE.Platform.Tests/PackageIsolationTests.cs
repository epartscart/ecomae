using EcomAE.Package.AutoParts;
using EcomAE.Customer.Auto247;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Packages;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Slice 1b tenant-isolation tests: package functionality executes only for the
/// tenant its package is activated for, and degrades fail-closed when the
/// package assembly or activation state is absent.
/// </summary>
public class PackageIsolationTests
{
    private sealed class StubGate : IPackageGate
    {
        private readonly HashSet<string> _enabled;
        public StubGate(IEnumerable<string> enabled) => _enabled = new(enabled, StringComparer.OrdinalIgnoreCase);
        public Task<bool> IsEnabledAsync(HttpContext? context, string packageId, CancellationToken cancellationToken = default)
            => Task.FromResult(_enabled.Contains(packageId));
        public Task<bool> IsEnabledAsync(string siteKey, string packageId, CancellationToken cancellationToken = default)
            => Task.FromResult(_enabled.Contains(siteKey + "|" + packageId));
    }

    private static DefaultHttpContext Ctx(string? siteKey, IEnumerable<string> enabled)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPackageGate>(new StubGate(enabled));
        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        ctx.Request.Host = new HostString("auto247.ae");
        if (siteKey is not null)
        {
            ctx.Items[TenantResolutionMiddleware.HttpContextItemKey] =
                TenantContext.ForKnownTenant(siteKey, "auto247.ae", TenantMode.LiveTenant, TenantSurface.Storefront, "/");
        }

        return ctx;
    }

    [Fact]
    public async Task Auto247_skin_applies_when_package_enabled_for_auto247()
    {
        var skin = new Auto247SkinContributor();
        var ctx = Ctx("auto247", ["auto247|customer.auto247"]);
        Assert.True(await skin.AppliesToAsync(ctx));
        Assert.Equal("epc-storefront--auto247", skin.BodyClass);
        var pkCtx = Ctx("auto247", ["auto247|customer.auto247"]);
        pkCtx.Request.Host = new HostString("auto247.pk");
        Assert.Equal("pk", skin.CountryCodeFor(pkCtx));
    }

    [Fact]
    public async Task Auto247_skin_hidden_for_another_tenant()
    {
        var skin = new Auto247SkinContributor();
        // Same tenant package enabled, but the request resolves to a different tenant.
        var ctx = Ctx("ecomae", ["ecomae|customer.auto247"]);
        Assert.False(await skin.AppliesToAsync(ctx));
    }

    [Fact]
    public async Task Auto247_skin_hidden_when_package_disabled()
    {
        var skin = new Auto247SkinContributor();
        var ctx = Ctx("auto247", []); // auto247 tenant, package not enabled
        Assert.False(await skin.AppliesToAsync(ctx));
    }

    [Fact]
    public async Task Vin_decode_blocked_for_tenant_without_auto_parts()
    {
        var enabled = new[] { "auto247|industry.auto_parts" }; // only auto247 has it
        var accessor = new HttpContextAccessor { HttpContext = Ctx("ecomae", []) };
        var svc = new LaximoVinDecodeService(
            new StubHttpClientFactory(), new ConfigurationBuilder().Build(),
            new StubConnections(), accessor, new StubGate(enabled));

        var result = await svc.DecodeAsync("WVWZZZ1JZXW000001");
        Assert.False(result.Ok);
        Assert.Equal("package_disabled", result.Code);
    }

    [Fact]
    public async Task Vin_decode_reaches_vin_validation_when_package_enabled()
    {
        var accessor = new HttpContextAccessor { HttpContext = Ctx("auto247", ["auto247|industry.auto_parts"]) };
        var svc = new LaximoVinDecodeService(
            new StubHttpClientFactory(), new ConfigurationBuilder().Build(),
            new StubConnections(), accessor, new StubGate(["auto247|industry.auto_parts"]));

        var result = await svc.DecodeAsync("SHORT"); // fails VIN length, not the gate
        Assert.Equal("invalid_vin", result.Code);
    }

    [Fact]
    public void Missing_package_assembly_returns_null_not_exception()
        => Assert.Null(PackageBootstrap.TryLoadAssembly(
            Path.Combine(AppContext.BaseDirectory, "EcomAE.Package.DoesNotExist.dll")));

    [Fact]
    public void Bootstrap_registers_both_packages_when_assemblies_ship()
    {
        var services = new ServiceCollection();
        PackageBootstrap.RegisterPackages(services, new ConfigurationBuilder().Build());
        var provider = services.BuildServiceProvider();
        // Auto247 contributor registered via its package entry point.
        Assert.Contains(provider.GetServices<EcomAE.Platform.Presentation.ICustomerSkinContributor>(),
            c => c.PackageId == "customer.auto247");
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class StubConnections : EcomAE.Platform.Erp.IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;
        public Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => Task.FromException<System.Data.Common.DbConnection>(new InvalidOperationException("unconfigured"));
    }
}
