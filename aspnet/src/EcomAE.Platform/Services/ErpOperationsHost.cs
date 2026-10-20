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

    public static bool TryParse(string? host, out string siteKey, out string country)
    {
        siteKey = string.Empty;
        country = string.Empty;
        var match = HostPattern.Match(host ?? string.Empty);
        if (!match.Success)
        {
            return false;
        }

        siteKey = ClientErpPath.NormalizeSiteKey(match.Groups["slug"].Value);
        if (siteKey.Length == 0)
        {
            return false;
        }

        country = match.Groups["country"].Value.ToLowerInvariant();
        return true;
    }

    public static bool IsOperationsHost(string? host)
        => HostPattern.IsMatch(host ?? string.Empty);

    /// <summary>Canonical operations host for a tenant site key.</summary>
    public static string HostFor(string siteKey, string? country = null)
    {
        var key = ClientErpPath.NormalizeSiteKey(siteKey);
        var shard = string.IsNullOrWhiteSpace(country) ? DefaultCountry : country.Trim().ToLowerInvariant();
        return $"{key}.operations.{shard}.ecomae.com";
    }

    /// <summary>Canonical absolute URL for a tenant path, e.g. https://indj.operations.uae.ecomae.com/erp.</summary>
    public static string CanonicalUrl(string siteKey, string? country, string path)
    {
        var bare = ClientErpPath.NormalizeRequestPath(path);
        if (bare == "/")
        {
            bare = "/erp";
        }

        return $"https://{HostFor(siteKey, country)}{bare}";
    }
}
