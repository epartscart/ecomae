using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Auth;

public static partial class LegacyApiClientKeyParser
{
    public static LegacyApiClientKey? Parse(string? raw)
    {
        var key = raw?.Trim() ?? string.Empty;
        if (key.Length == 0)
        {
            return null;
        }

        var product = ProductForKey(key);
        if (product is null)
        {
            return null;
        }

        var prefixLength = Math.Min(32, key.Length);
        return new LegacyApiClientKey(key, product, key[..prefixLength], Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant());
    }

    public static string? ProductForKey(string raw)
    {
        // Match PHP epc_api_clients_product_for_key(): ^epc_(catalog|pricepro)_[a-z0-9_]+$
        if (CatalogKeyPattern().IsMatch(raw))
        {
            return "catalog";
        }

        if (PriceProKeyPattern().IsMatch(raw))
        {
            return "price_pro";
        }

        return null;
    }

    public static string? ExtractFromRequest(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader))
        {
            var apiKey = apiKeyHeader.ToString().Trim();
            if (apiKey.Length > 0)
            {
                return apiKey;
            }
        }

        if (request.Headers.TryGetValue("Authorization", out var authorization))
        {
            return ExtractFromAuthorizationHeader(authorization.ToString());
        }

        return null;
    }

    public static string? ExtractFromAuthorizationHeader(string? authorization)
    {
        const string bearer = "Bearer ";
        if (authorization is not null && authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
        {
            return authorization[bearer.Length..].Trim();
        }

        return null;
    }

    [GeneratedRegex("^epc_catalog_[a-z0-9_]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CatalogKeyPattern();

    [GeneratedRegex("^epc_pricepro_[a-z0-9_]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PriceProKeyPattern();
}
