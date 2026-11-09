using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EcomAE.Platform.Packages;

/// <summary>
/// Slice 1c extension contracts: packages declare the surface they own —
/// route prefixes (API gating), menu entries (menu gating) and event handlers
/// (extension events). Every contribution is dispatched through <see cref="IPackageGate"/>,
/// fail-closed: a disabled/uninstalled package cannot execute through any path.
/// </summary>
public sealed record PackageMenuItem(string Surface, string Label, string Href, int Order);

/// <summary>Package-owned route prefixes enforced by <see cref="PackageGateMiddleware"/>.</summary>
public interface IPackageRouteContributor
{
    string PackageId { get; }
    IReadOnlyList<string> RoutePrefixes { get; }
}

/// <summary>Package-owned menu entries filtered per tenant by <see cref="PackageMenuService"/>.</summary>
public interface IPackageMenuContributor
{
    string PackageId { get; }
    IReadOnlyList<PackageMenuItem> MenuItems { get; }
}

/// <summary>Package-owned event handler: invoked only while the package is enabled for the tenant.</summary>
public interface IPackageEventHandler
{
    string PackageId { get; }
    string EventName { get; }
    Task HandleAsync(JsonElement payload, string siteKey, CancellationToken cancellationToken = default);
}

/// <summary>Tenant-aware package event bus. Fan-out is gated per handler's package.</summary>
public interface IPackageEventBus
{
    Task PublishAsync(string eventName, string siteKey, object payload, CancellationToken cancellationToken = default);
}

public sealed class PackageEventBus : IPackageEventBus
{
    private readonly IEnumerable<IPackageEventHandler> _handlers;
    private readonly IServiceProvider _services;
    private readonly ILogger<PackageEventBus> _log;

    public PackageEventBus(
        IEnumerable<IPackageEventHandler> handlers,
        IServiceProvider services,
        ILogger<PackageEventBus> log)
    {
        _handlers = handlers;
        _services = services;
        _log = log;
    }

    public async Task PublishAsync(string eventName, string siteKey, object payload, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.SerializeToElement(payload);
        var gate = _services.GetService<IPackageGate>();
        foreach (var handler in _handlers)
        {
            if (!string.Equals(handler.EventName, eventName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                // Fail closed: unverifiable activation state never runs package code.
                if (gate is not null && !await gate.IsEnabledAsync(siteKey, handler.PackageId, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                await handler.HandleAsync(json, siteKey, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "package event handler {PackageId} for {Event} failed (site {SiteKey})", handler.PackageId, eventName, siteKey);
            }
        }
    }
}

/// <summary>Menu gating: contributed entries only appear while the owning package is enabled.</summary>
public sealed class PackageMenuService
{
    private readonly IEnumerable<IPackageMenuContributor> _contributors;
    private readonly IPackageGate _gate;
    private readonly ILogger<PackageMenuService> _log;

    public PackageMenuService(
        IEnumerable<IPackageMenuContributor> contributors,
        IPackageGate gate,
        ILogger<PackageMenuService> log)
    {
        _contributors = contributors;
        _gate = gate;
        _log = log;
    }

    public async Task<IReadOnlyList<PackageMenuItem>> GetMenuAsync(HttpContext? context, string siteKey, string surface, CancellationToken cancellationToken = default)
    {
        var items = new List<PackageMenuItem>();
        foreach (var contributor in _contributors)
        {
            try
            {
                if (!await _gate.IsEnabledAsync(siteKey, contributor.PackageId, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                items.AddRange(contributor.MenuItems.Where(m => string.Equals(m.Surface, surface, StringComparison.OrdinalIgnoreCase)));
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "package menu contribution {PackageId} failed closed", contributor.PackageId);
            }
        }

        return items.OrderBy(m => m.Order).ToList();
    }
}

/// <summary>Background-job / scheduled-task gating: package work runs only while enabled.</summary>
public sealed class PackageJobGuard
{
    private readonly IPackageGate _gate;

    public PackageJobGuard(IPackageGate gate)
    {
        _gate = gate;
    }

    /// <summary>Runs <paramref name="work"/> only when the package is enabled; false = skipped (fail closed).</summary>
    public async Task<bool> RunIfEnabledAsync(string siteKey, string packageId, Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        if (!await _gate.IsEnabledAsync(siteKey, packageId, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await work(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
