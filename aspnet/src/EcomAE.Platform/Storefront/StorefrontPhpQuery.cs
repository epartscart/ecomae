using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP request-key rules for <c>$_GET</c>: <c>.</c>, space and <c>+</c> become <c>_</c>, the last assignment wins,
/// and a key that ends with <c>[]</c> or contains <c>[...]</c> is an array. Used by
/// <c>content/shop/catalogue/set_cookie_products_style.php</c>.
/// </summary>
public static class StorefrontPhpQuery
{
    public sealed record Value(bool Present, bool IsArray, bool ArrayEmpty, string? Scalar);

    public static Value Get(HttpRequest request, string name)
    {
        var raw = request.QueryString.Value;
        if (string.IsNullOrEmpty(raw))
        {
            return new Value(false, false, false, null);
        }

        var parsed = QueryHelpers.ParseQuery(raw);
        Value? found = null;
        foreach (var pair in parsed)
        {
            if (!SameKey(pair.Key, name))
            {
                continue;
            }

            var array = IsArrayKey(pair.Key);
            var values = pair.Value;
            found = array
                ? new Value(true, true, values.Count == 0, null)
                : new Value(true, false, false, values.Count == 0 ? string.Empty : values[^1]);
        }

        return found ?? new Value(false, false, false, null);
    }

    private static bool SameKey(string rawKey, string name)
    {
        var normalized = Normalize(rawKey);
        if (string.Equals(normalized, name, StringComparison.Ordinal))
        {
            return true;
        }

        var bracket = normalized.IndexOf('[', StringComparison.Ordinal);
        return bracket > 0
            && normalized.EndsWith(']')
            && string.Equals(normalized[..bracket], name, StringComparison.Ordinal);
    }

    private static bool IsArrayKey(string rawKey)
    {
        var normalized = Normalize(rawKey);
        return normalized.Contains('[', StringComparison.Ordinal) && normalized.EndsWith(']');
    }

    /// <summary>PHP replaces <c>.</c>, space and <c>+</c> with <c>_</c> in request names.</summary>
    public static string Normalize(string key)
    {
        key = key.Trim();
        var chars = key.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is '.' or ' ' or '+')
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }
}
