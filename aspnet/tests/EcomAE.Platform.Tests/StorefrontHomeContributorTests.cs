using EcomAE.Customer.Auto247;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Packages;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Package-contributed storefront home: the auto247 home renders only for the
/// auto247 tenant while customer.auto247 is enabled — fail-closed everywhere else.
/// </summary>
public class StorefrontHomeContributorTests
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

    private static DefaultHttpContext Ctx(string? siteKey, IPackageGate gate)
    {
        var services = new ServiceCollection();
        services.AddSingleton(gate);
        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        if (siteKey is not null)
        {
            ctx.Items[TenantResolutionMiddleware.HttpContextItemKey] =
                TenantContext.ForKnownTenant(siteKey, "shop.test", TenantMode.LiveTenant, TenantSurface.Storefront, "/");
        }

        return ctx;
    }

    [Fact]
    public async Task Auto247_home_applies_only_for_owner_tenant_with_package_enabled()
    {
        var contributor = new Auto247HomeContributor();
        var enabled = new StubGate(["auto247|customer.auto247"]);

        Assert.True(await contributor.AppliesToAsync(Ctx("auto247", enabled)));
        Assert.False(await contributor.AppliesToAsync(Ctx("ecomae", enabled)));
        Assert.False(await contributor.AppliesToAsync(Ctx("auto247", new StubGate([]))));
        Assert.False(await contributor.AppliesToAsync(Ctx(null, enabled)));
    }

    [Fact]
    public async Task Resolver_picks_contributor_only_when_enabled()
    {
        var resolver = new StorefrontHomeResolver([new Auto247HomeContributor()]);
        Assert.NotNull(await resolver.ResolveAsync(Ctx("auto247", new StubGate(["auto247|customer.auto247"]))));
        Assert.Null(await resolver.ResolveAsync(Ctx("auto247", new StubGate([]))));
        Assert.Null(await resolver.ResolveAsync(Ctx("docpart", new StubGate(["docpart|customer.auto247"]))));
    }

    [Fact]
    public async Task Rendered_home_links_existing_routes_and_logo()
    {
        var contributor = new Auto247HomeContributor();
        var ctx = Ctx("auto247", new StubGate(["auto247|customer.auto247"]));
        var html = await contributor.RenderHomeHtmlAsync(ctx);

        Assert.Contains("/platform-assets/auto247-logo.png", html);
        Assert.Contains("/platform-assets/epc-storefront-home-auto247.css", html);
        Assert.Contains("/storefront/vin-app", html);
        Assert.Contains("/storefront/garage-manager-app", html);
        Assert.Contains("/storefront/ai-parts-expert-app", html);
        Assert.Contains("/en/shop/search", html);
        Assert.Contains("/en/available-brands", html);
        Assert.Contains("/en/umapi_catalog", html);
        Assert.Contains("/en/shop/bulk-upload", html);
        Assert.DoesNotContain("services garage", html, StringComparison.OrdinalIgnoreCase);
    }
}
