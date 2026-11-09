using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Customer/industry-package extension point: contributes the full storefront
/// home markup (header + hero + sections + footer) for requests resolving to
/// its owning tenant while its package is activated. Implemented by packages
/// (e.g. customer.auto247); Standard Core stores no tenant home layouts.
/// Returning markup replaces the default nero chrome for the home page only —
/// all inner storefront pages keep the standard chrome.
/// </summary>
public interface IStorefrontHomeContributor
{
    /// <summary>Owning tenant site_key ("auto247").</summary>
    string SiteKey { get; }

    /// <summary>Package id this contributor belongs to ("customer.auto247").</summary>
    string PackageId { get; }

    /// <summary>
    /// True when the request resolves to the contributor's tenant AND its package
    /// is installed+enabled for that tenant (fail-closed when unverifiable).
    /// </summary>
    Task<bool> AppliesToAsync(HttpContext? context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Full home markup for the request (links point at the existing storefront
    /// routes — no new backend surface). Empty string declines the contribution.
    /// </summary>
    Task<string> RenderHomeHtmlAsync(HttpContext? context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves the active home contributor for the current request: the first one
/// whose tenant matches AND whose package is activated.
/// </summary>
public interface IStorefrontHomeResolver
{
    Task<IStorefrontHomeContributor?> ResolveAsync(HttpContext? context, CancellationToken cancellationToken = default);
}

public sealed class StorefrontHomeResolver : IStorefrontHomeResolver
{
    private readonly IEnumerable<IStorefrontHomeContributor> _contributors;

    public StorefrontHomeResolver(IEnumerable<IStorefrontHomeContributor> contributors)
        => _contributors = contributors;

    public async Task<IStorefrontHomeContributor?> ResolveAsync(HttpContext? context, CancellationToken cancellationToken = default)
    {
        foreach (var contributor in _contributors)
        {
            try
            {
                if (await contributor.AppliesToAsync(context, cancellationToken).ConfigureAwait(false))
                {
                    return contributor;
                }
            }
            catch
            {
                // A faulted contributor never blocks the storefront — fail-closed.
            }
        }

        return null;
    }
}
