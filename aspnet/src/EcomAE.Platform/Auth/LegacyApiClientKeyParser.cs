using System.Security.Cryptography;
using System.Text;

namespace EcomAE.Platform.Auth;

public static class LegacyApiClientKeyParser
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
        if (raw.StartsWith("epc_catalog_", StringComparison.OrdinalIgnoreCase))
        {
            return "catalog";
        }

        if (raw.StartsWith("epc_pricepro_", StringComparison.OrdinalIgnoreCase))
        {
            return "price_pro";
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
}
