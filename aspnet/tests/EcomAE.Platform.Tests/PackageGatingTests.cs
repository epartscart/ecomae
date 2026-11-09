using System.Text.Json;
using EcomAE.Package.AutoParts;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Packages;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Slice 1c gating tests: package-owned APIs, menus, events and jobs execute only
/// while the package is enabled for the resolving tenant — fail closed everywhere.
/// </summary>
public class PackageGatingTests
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

    private sealed class FaultingGate : IPackageGate
    {
        public Task<bool> IsEnabledAsync(HttpContext? context, string packageId, CancellationToken cancellationToken = default)
            => Task.FromException<bool>(new InvalidOperationException("db down"));
        public Task<bool> IsEnabledAsync(string siteKey, string packageId, CancellationToken cancellationToken = default)
            => Task.FromException<bool>(new InvalidOperationException("db down"));
    }

    private static DefaultHttpContext Ctx(string? siteKey, IPackageGate gate, string path = "/storefront/vin/decode")
    {
        var services = new ServiceCollection();
        services.AddSingleton(gate);
        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        ctx.Request.Path = path;
        if (siteKey is not null)
        {
            ctx.Items[TenantResolutionMiddleware.HttpContextItemKey] =
                TenantContext.ForKnownTenant(siteKey, "shop.test", TenantMode.LiveTenant, TenantSurface.Storefront, "/");
        }

        return ctx;
    }

    private static PackageGateMiddleware Middleware(IPackageGate gate, out Func<bool> nextCalled)
    {
        var called = false;
        var mw = new PackageGateMiddleware(
            _ => { called = true; return Task.CompletedTask; },
            [new AutoPartsRouteContributor()],
            NullLogger<PackageGateMiddleware>.Instance);
        nextCalled = () => called;
        return mw;
    }

    [Fact]
    public async Task Route_gate_allows_enabled_package()
    {
        var mw = Middleware(new StubGate(["auto247|industry.auto_parts"]), out var next);
        var ctx = Ctx("auto247", new StubGate(["auto247|industry.auto_parts"]));
        await mw.InvokeAsync(ctx);
        Assert.True(next());
        Assert.NotEqual(403, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Route_gate_blocks_disabled_package_403()
    {
        var gate = new StubGate([]);
        var mw = Middleware(gate, out var next);
        var ctx = Ctx("auto247", gate);
        ctx.Response.Body = new MemoryStream();
        await mw.InvokeAsync(ctx);
        Assert.False(next());
        Assert.Equal(403, ctx.Response.StatusCode);
        ctx.Response.Body.Position = 0;
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        Assert.Contains("package_disabled", body);
        Assert.Contains("industry.auto_parts", body);
    }

    [Fact]
    public async Task Route_gate_blocks_tenant_without_package()
    {
        // auto_parts enabled for auto247 only — ecomae tenant hits the same URL.
        var gate = new StubGate(["auto247|industry.auto_parts"]);
        var mw = Middleware(gate, out var next);
        var ctx = Ctx("ecomae", gate);
        ctx.Response.Body = new MemoryStream();
        await mw.InvokeAsync(ctx);
        Assert.False(next());
        Assert.Equal(403, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Route_gate_fails_closed_when_gate_throws()
    {
        var gate = new FaultingGate();
        var mw = Middleware(gate, out var next);
        var ctx = Ctx("auto247", gate);
        ctx.Response.Body = new MemoryStream();
        await mw.InvokeAsync(ctx);
        Assert.False(next());
        Assert.Equal(403, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task Route_gate_ignores_unowned_paths()
    {
        var gate = new StubGate([]);
        var mw = Middleware(gate, out var next);
        var ctx = Ctx("auto247", gate, "/storefront/cart");
        await mw.InvokeAsync(ctx);
        Assert.True(next());
    }

    private sealed class RecordingHandler : IPackageEventHandler
    {
        public string PackageId { get; }
        public string EventName => "order.placed";
        public int Calls;
        public RecordingHandler(string packageId) => PackageId = packageId;
        public Task HandleAsync(JsonElement payload, string siteKey, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private static IPackageEventBus Bus(IPackageGate gate, params IPackageEventHandler[] handlers)
    {
        var services = new ServiceCollection();
        services.AddSingleton(gate);
        return new PackageEventBus(handlers, services.BuildServiceProvider(), NullLogger<PackageEventBus>.Instance);
    }

    [Fact]
    public async Task Event_bus_invokes_handler_only_when_enabled_for_tenant()
    {
        var handler = new RecordingHandler("industry.auto_parts");
        var bus = Bus(new StubGate(["auto247|industry.auto_parts"]), handler);

        await bus.PublishAsync("order.placed", "auto247", new { order = 1 });
        Assert.Equal(1, handler.Calls);

        // Same handler, tenant without the package — gated out.
        await bus.PublishAsync("order.placed", "ecomae", new { order = 2 });
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Event_bus_skips_unrelated_events()
    {
        var handler = new RecordingHandler("industry.auto_parts");
        var bus = Bus(new StubGate(["auto247|industry.auto_parts"]), handler);
        await bus.PublishAsync("catalog.synced", "auto247", new { });
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Menu_items_appear_only_when_package_enabled()
    {
        var menus = new PackageMenuService(
            [new AutoPartsMenuContributor()],
            new StubGate(["auto247|industry.auto_parts"]),
            NullLogger<PackageMenuService>.Instance);

        var on = await menus.GetMenuAsync(null, "auto247", "storefront");
        Assert.Single(on);
        Assert.Equal("/storefront/vin-app", on[0].Href);

        var off = await menus.GetMenuAsync(null, "ecomae", "storefront");
        Assert.Empty(off);
    }

    [Fact]
    public async Task Job_guard_runs_work_only_when_enabled()
    {
        var guard = new PackageJobGuard(new StubGate(["auto247|industry.auto_parts"]));
        var ran = 0;

        Assert.True(await guard.RunIfEnabledAsync("auto247", "industry.auto_parts", _ => { ran++; return Task.CompletedTask; }));
        Assert.False(await guard.RunIfEnabledAsync("ecomae", "industry.auto_parts", _ => { ran++; return Task.CompletedTask; }));
        Assert.Equal(1, ran);
    }
}
