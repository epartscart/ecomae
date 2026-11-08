using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Auto247 storefront theming helpers: per-country skin class + stylesheet selection.
/// The tenant runs one DB/CP/ERP across many country domains (auto247.ae, auto247.pk,
/// auto247.com.pk, auto247.sa, …) — each domain gets its own flag-inspired palette.
/// </summary>
public static class Auto247Theme
{
    /// <summary>True when the current request resolves to the auto247 tenant storefront.</summary>
    public static bool IsAuto247(HttpContext? context)
        => context?.Items[TenantResolutionMiddleware.HttpContextItemKey]
            is TenantContext { SiteKey: "auto247" };

    /// <summary>
    /// Lowercase 2-letter country code for an auto247 host. The registry domain map
    /// pins the country (auto247.pk/com.pk → PK, GCC TLDs → their cc); anything
    /// unknown (auto247.com) falls back to the tenant home country "ae".
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

    /// <summary>Body-class suffix for the current auto247 request ("pk", "ae", …).</summary>
    public static string CountryClass(HttpContext? context)
        => CountryCodeForHost(context?.Request.Host.Host);

    public static string CurrencyForCountry(string countryCode)
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
