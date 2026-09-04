using System.Text.Json;

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
