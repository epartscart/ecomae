namespace EcomAE.Platform.Presentation;

/// <summary>
/// Customer special-search twins for PHP <c>special_searches_handler.php</c>.
/// Seed is auto_parts only — never resolve on jewellery / fashion / electronics / tax.
/// </summary>
public static class PhpSpecialSearches
{
    public sealed record Search(string Alias, string Title, string Lead, IReadOnlyList<Option> Options);

    public sealed record Option(string Label, string Query);

    public static IReadOnlyList<Search> All => Catalog;

    public static bool IsAlias(string? path)
    {
        var alias = Normalize(path);
        return alias.Length > 0 && Catalog.Any(s => s.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryFind(string? path, out Search search)
    {
        var alias = Normalize(path);
        search = Catalog.FirstOrDefault(s => s.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase))!;
        return search is not null;
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

    private static readonly Search[] Catalog =
    [
        new("tormoznye-kolodki", "Brake pads",
            "Front and rear brake pads by size and vehicle.",
            [
                new("Front pads", "brake pads front"),
                new("Rear pads", "brake pads rear"),
                new("Ceramic pads", "ceramic brake pads"),
            ]),
        new("tormoznye-diski", "Brake discs",
            "Discs and rotors for passenger cars.",
            [
                new("Front discs", "brake disc front"),
                new("Rear discs", "brake disc rear"),
                new("Ventilated discs", "ventilated brake disc"),
            ]),
        new("filtry-maslyanye", "Oil filters",
            "Engine oil filters by brand and thread.",
            [
                new("Spin-on filters", "oil filter"),
                new("Cartridge filters", "oil filter cartridge"),
                new("OEM filters", "oil filter oem"),
            ]),
        new("sveci-zazhiganiya", "Spark plugs",
            "Iridium, platinum, and standard spark plugs.",
            [
                new("Iridium", "iridium spark plug"),
                new("Platinum", "platinum spark plug"),
                new("Standard", "spark plug"),
            ]),
        new("ammortizatory", "Shock absorbers",
            "Front and rear dampers for passenger cars.",
            [
                new("Front shocks", "shock absorber front"),
                new("Rear shocks", "shock absorber rear"),
                new("Gas shocks", "gas shock absorber"),
            ]),
        new("remni-grm", "Timing belts",
            "Timing belts and kits with tensioners.",
            [
                new("Timing belt", "timing belt"),
                new("Timing kit", "timing belt kit"),
                new("Tensioner", "timing tensioner"),
            ]),
        new("lampy-avtomobilnye", "Automotive lamps",
            "Headlamp, indicator, and interior bulbs.",
            [
                new("H7 headlamp", "H7 bulb"),
                new("H4 headlamp", "H4 bulb"),
                new("LED", "LED automotive lamp"),
            ]),
        new("salniki-i-prokladki", "Seals and gaskets",
            "Engine and axle seals for warehouse search.",
            [
                new("Oil seal", "oil seal"),
                new("Head gasket", "head gasket"),
                new("Valve cover", "valve cover gasket"),
            ]),
    ];
}
