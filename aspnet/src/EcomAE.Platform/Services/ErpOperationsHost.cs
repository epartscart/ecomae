using System.Linq;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Services;

/// <summary>
/// D365-style dedicated tenant hosts:
/// <c>{site_key}.operations.{country3}.ecomae.com</c> — e.g.
/// <c>indj.operations.uae.ecomae.com/?cmp=indj</c>.
/// The host binds the tenant exactly like Super-CP <c>/cp/client-erp/{key}</c>
/// does; country is the ISO 3166-1 alpha-3 shard the tenant is homed in.
/// </summary>
public static class ErpOperationsHost
{
    public const string DefaultCountry = "uae";

    private static readonly Regex HostPattern = new(
        @"^(?<slug>[a-z0-9][a-z0-9-]*)\.operations\.(?<country>[a-z]{3})\.ecomae\.com$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // Flat alias — {key}-operations-{country3}.ecomae.com is a SINGLE-label host,
    // covered by the zone's free Universal SSL and the existing *.ecomae.com DNS
    // record; the deep form needs paid multi-level certs (ACM/Total TLS). A
    // dotted flat host ({key}-{c}.operations.ecomae.com) is still two labels and
    // NOT covered — the country must live inside the one label. Slug carries no
    // hyphen (site keys are [a-z0-9]), so "-operations-{ccc}" is unambiguous.
    private static readonly Regex FlatHostPattern = new(
        @"^(?<slug>[a-z0-9]+)-operations-(?<country>[a-z]{3})\.ecomae\.com$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryParse(string? host, out string siteKey, out string country)
    {
        siteKey = string.Empty;
        country = string.Empty;
        var value = host ?? string.Empty;
        var flat = FlatHostPattern.Match(value);
        var match = flat.Success ? flat : HostPattern.Match(value);
        if (!match.Success)
        {
            return false;
        }

        var slug = match.Groups["slug"].Value.ToLowerInvariant();
        siteKey = ClientErpPath.NormalizeSiteKey(slug);
        // Site keys never contain '-' (NormalizeSiteKey strips them): a hyphenated
        // slug would silently bind a *different* tenant (acme-uk → acmeuk), so
        // canonical hosts only ever use [a-z0-9_]. Anything else is not ours.
        if (siteKey.Length == 0 || siteKey != slug)
        {
            siteKey = string.Empty;
            return false;
        }

        country = match.Groups["country"].Value.ToLowerInvariant();
        return true;
    }

    public static bool IsOperationsHost(string? host)
        => HostPattern.IsMatch(host ?? string.Empty) || FlatHostPattern.IsMatch(host ?? string.Empty);

    /// <summary>
    /// True when a normalized site key can live in a DNS host label — only
    /// [a-z0-9]. Keys containing '_' (e.g. fit_out) can never be an operations
    /// host: DNS would not resolve them and TryParse rightly rejects them.
    /// </summary>
    public static bool IsValidSlug(string siteKey)
    {
        var key = ClientErpPath.NormalizeSiteKey(siteKey);
        return key.Length > 0 && key.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9');
    }

    /// <summary>Canonical operations host for a tenant site key, or empty when the key is not host-safe.</summary>
    public static string HostFor(string siteKey, string? country = null)
    {
        if (!IsValidSlug(siteKey))
        {
            return string.Empty;
        }

        var key = ClientErpPath.NormalizeSiteKey(siteKey);
        var shard = string.IsNullOrWhiteSpace(country) ? DefaultCountry : country.Trim().ToLowerInvariant();
        // Canonical is the single-label form: free Universal SSL + *.ecomae.com
        // DNS cover it; the deep {key}.operations.{country} form still resolves.
        return $"{key}-operations-{shard}.ecomae.com";
    }

    /// <summary>Canonical absolute URL for a tenant path, e.g. https://indj.operations.uae.ecomae.com/erp. Empty when the key is not host-safe.</summary>
    public static string CanonicalUrl(string siteKey, string? country, string path)
    {
        var host = HostFor(siteKey, country);
        if (host.Length == 0)
        {
            return string.Empty;
        }

        var bare = ClientErpPath.NormalizeRequestPath(path);
        if (bare == "/")
        {
            bare = "/erp";
        }

        return $"https://{host}{bare}";
    }
}
