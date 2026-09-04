using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>modules/shop/bottom_panel/bottom_panel.php</c> JSON int-array cookies
/// (<c>bookmarks</c> / <c>compare</c>). Lifetime matches the classic 15552000s (~180 days).
/// </summary>
public static class StorefrontIntListCookie
{
    public const string BookmarksName = "bookmarks";
    public const string CompareName = "compare";
    public const int BookmarksMax = 80;
    public const int CompareMax = 40;
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(15_552_000);

    public static string? RawValue(HttpRequest request, string name)
    {
        var header = request.Headers.Cookie.ToString();
        var extracted = ExtractFromHeader(header, name);
        if (!string.IsNullOrWhiteSpace(extracted))
        {
            return extracted;
        }

        return request.Cookies.TryGetValue(name, out var typed) ? typed : null;
    }

    public static List<int> Read(HttpRequest request, string name, int maxItems)
        => Parse(RawValue(request, name), maxItems);

    /// <summary>
    /// PHP writes <c>bookmarks=[1,2]</c> without encoding. ASP.NET's cookie map
    /// splits on commas, so the raw header must be scanned for the JSON array.
    /// </summary>
    public static string? ExtractFromHeader(string? cookieHeader, string name)
    {
        if (string.IsNullOrWhiteSpace(cookieHeader) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var key = name + "=";
        var start = 0;
        while (start < cookieHeader.Length)
        {
            var idx = cookieHeader.IndexOf(key, start, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return null;
            }

            if (idx > 0)
            {
                var prev = cookieHeader[idx - 1];
                if (prev is not ';' and not ' ' and not ',')
                {
                    start = idx + key.Length;
                    continue;
                }
            }

            var valueStart = idx + key.Length;
            if (valueStart >= cookieHeader.Length)
            {
                return "";
            }

            if (cookieHeader[valueStart] == '[')
            {
                var close = cookieHeader.IndexOf(']', valueStart);
                return close >= valueStart
                    ? cookieHeader[valueStart..(close + 1)]
                    : cookieHeader[valueStart..];
            }

            var end = cookieHeader.IndexOf(';', valueStart);
            var raw = end < 0 ? cookieHeader[valueStart..] : cookieHeader[valueStart..end];
            try
            {
                return Uri.UnescapeDataString(raw);
            }
            catch (UriFormatException)
            {
                return raw;
            }
        }

        return null;
    }

    public static List<int> Parse(string? raw, int maxItems)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<int>>(raw);
            if (parsed is null)
            {
                return [];
            }

            return parsed.Where(id => id > 0).Distinct().Take(Math.Max(1, maxItems)).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string Serialize(IReadOnlyList<int> ids)
        => JsonSerializer.Serialize(ids.Where(id => id > 0).Distinct().ToArray());

    public static List<int> Add(IReadOnlyList<int> current, int productId, int maxItems)
    {
        var next = (current ?? []).Where(id => id > 0).Distinct().ToList();
        if (productId <= 0)
        {
            return next;
        }

        if (!next.Contains(productId) && next.Count < Math.Max(1, maxItems))
        {
            next.Add(productId);
        }

        return next;
    }

    public static List<int> Remove(IReadOnlyList<int> current, int productId)
        => (current ?? []).Where(id => id > 0 && id != productId).Distinct().ToList();

    public static CookieOptions Options() => new()
    {
        Path = "/",
        Expires = DateTimeOffset.UtcNow.Add(Lifetime),
        IsEssential = true,
    };
}
