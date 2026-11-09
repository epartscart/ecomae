using EcomAE.Platform.Middleware;
using EcomAE.Platform.Packages;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace EcomAE.Customer.Auto247;

/// <summary>
/// Auto247 customer-package skin: per-country storefront palette, body class and
/// display currency. The tenant runs one DB/CP/ERP across many country domains
/// (auto247.ae, auto247.pk, auto247.com.pk, auto247.sa, …) — each domain gets
/// its own flag-inspired skin. Customer-owned; Standard Core holds no palettes.
/// </summary>
public sealed class Auto247SkinContributor : ICustomerSkinContributor
{
    public string SiteKey => Auto247Package.OwnerSiteKey;

    public string PackageId => Auto247Package.PackageId;

    public string StylesheetHref => "/platform-assets/epc-storefront-theme-auto247.css?v=20261109f";

    public string BodyClass => "epc-storefront--auto247";

    public string SeoHomeDescription =>
        "Buy and sell genuine and aftermarket automotive parts online at AUTO247 — UAE and GCC warehouse, fast delivery worldwide.";

    public string SeoHomeKeywords =>
        "AUTO247, automotive parts, auto parts UAE, spare parts, OE parts, aftermarket, buy sell vehicles parts GCC";

    /// <summary>
    /// Tenant match + package activation: the skin applies only while
    /// <c>customer.auto247</c> is installed+enabled for the auto247 tenant.
    /// Fail-closed — an unverifiable package state means no skin.
    /// </summary>
    public async Task<bool> AppliesToAsync(HttpContext? context, CancellationToken cancellationToken = default)
    {
        if (context?.Items[TenantResolutionMiddleware.HttpContextItemKey]
            is not TenantContext { SiteKey: Auto247Package.OwnerSiteKey })
        {
            return false;
        }

        var gate = context.RequestServices?.GetService<IPackageGate>();
        if (gate is null)
        {
            return false;
        }

        try
        {
            return await gate.IsEnabledAsync(Auto247Package.OwnerSiteKey, Auto247Package.PackageId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Lowercase 2-letter country code for an auto247 host. The registry domain
    /// map pins the country (.pk/.com.pk → PK, GCC TLDs → their cc); unknown
    /// hosts (auto247.com) fall back to the tenant home country "ae".
    /// </summary>
    public static string CountryCodeForHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return "ae";
        }

        var h = host.Trim().ToLowerInvariant();
        if (h.StartsWith("www.", StringComparison.Ordinal))
        {
            h = h[4..];
        }

        foreach (var cc in new[] { "ae", "sa", "qa", "kw", "bh", "om", "pk" })
        {
            if (h.EndsWith("." + cc, StringComparison.Ordinal))
            {
                return cc;
            }
        }

        return "ae";
    }

    public string CountryCodeFor(HttpContext? context)
        => CountryCodeForHost(context?.Request.Host.Host);

    public string CurrencyFor(string countryCode)
        => countryCode.ToLowerInvariant() switch
        {
            "pk" => "PKR",
            "sa" => "SAR",
            "qa" => "QAR",
            "kw" => "KWD",
            "bh" => "BHD",
            "om" => "OMR",
            _ => "AED"
        };
}
