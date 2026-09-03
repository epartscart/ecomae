namespace EcomAE.Platform.Presentation;

/// <summary>
/// Whitelist of own-catalogue category URLs (PHP <c>shop_catalogue_categories.url</c>).
/// Never a catch-all — only these aliases rewrite, and only on auto_parts hosts.
/// </summary>
public static class PhpOwnCatalogSlugs
{
    public static readonly string[] Aliases =
    [
        "dvigatel", "masla", "filtry", "tormoznaya-sistema", "podveska",
        "transmissiya", "ohlazhdenie", "elektrika", "kuzov", "rul-i-hodovaya",
        "stekla-i-optika", "vyhlopnaya-sistema", "kondicioner", "salon",
        "stseplenie", "podshipniki", "tormoza", "zhidkosti", "instrumenty",
        "filtry-vozdushnye", "filtry-toplivnye", "filtry-salonnye",
    ];

    public static bool IsAlias(string? path)
    {
        var alias = Normalize(path);
        return alias.Length > 0 && Aliases.Contains(alias, StringComparer.OrdinalIgnoreCase);
    }

    public static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var value = path.Trim().Trim('/');
        var q = value.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
        {
            value = value[..q];
        }

        if (value.StartsWith("en/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("ar/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("me/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("ru/", StringComparison.OrdinalIgnoreCase))
        {
            value = value[3..];
        }

        var slash = value.IndexOf('/', StringComparison.Ordinal);
        return (slash > 0 ? value[..slash] : value).ToLowerInvariant();
    }
}
