namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP <c>content/shop/ucats/catalogues.php</c> — eight UCats service catalogs.
/// Deep picker trees stay PHP; this catalog is the ASP.NET hub + category landing.
/// </summary>
public static class StorefrontUcatsCatalog
{
    public static readonly IReadOnlyList<StorefrontUcatsCard> Cards =
    [
        new("shiny", "Tires", "fa-circle-o", "Tire size and brand picker.",
            "Tire size groups and product cards. Use warehouse search for live stock.",
            [
                ("width", "Width"),
                ("profile", "Profile"),
                ("diameter", "Diameter"),
                ("season", "Season"),
            ]),
        new("kolesnye-diski", "Wheels", "fa-sun-o", "Alloy and steel wheel catalog.",
            "Wheel size groups. Use name search for a manufacturer + size.",
            [
                ("width", "Width"),
                ("diameter", "Diameter"),
                ("pcd", "PCD"),
                ("et", "ET"),
            ]),
        new("avtoaksessuary", "UCats accessories", "fa-shopping-bag", "Accessory groups by vehicle.",
            "Accessory groups by vehicle. The accessories marketplace is a separate dedicated app.",
            [
                ("make", "Make"),
                ("model", "Model"),
                ("group", "Group"),
            ]),
        new("katalog-texnicheskogo-obsluzhivaniya", "Service / TO", "fa-wrench", "Maintenance kits by vehicle.",
            "Make → model → type → parts list. VIN search is the live twin.",
            [
                ("make", "Make"),
                ("model", "Model"),
                ("year", "Year"),
            ]),
        new("avtoximiya", "Oil & chemicals", "fa-tint", "Oils, fluids, and auto chemistry.",
            "Oils, fluids, and auto chemistry. Warehouse attribute search covers live stock.",
            [
                ("viscosity", "Viscosity"),
                ("spec", "Spec"),
                ("brand", "Brand"),
            ]),
        new("akkumulyatory", "Batteries", "fa-bolt", "Starter batteries by group.",
            "Starter batteries by group. Search article or brand for live stock.",
            [
                ("capacity", "Ah"),
                ("polarity", "Polarity"),
                ("brand", "Brand"),
            ]),
        new("kolpaki", "Hubcaps", "fa-circle-thin", "Wheel covers and hubcaps.",
            "Wheel covers and hubcaps. Accessories marketplace also lists related trim.",
            [
                ("diameter", "Diameter"),
                ("brand", "Brand"),
            ]),
        new("kolesnye-gajki-bolty-prostavki", "Bolts & spacers", "fa-cog", "Wheel nuts, bolts, and spacers.",
            "Wheel nuts, bolts, and spacers. Use name search for thread/size.",
            [
                ("thread", "Thread"),
                ("length", "Length"),
                ("seat", "Seat"),
            ]),
    ];

    public static StorefrontUcatsCard? Find(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var key = slug.Trim().Trim('/');
        key = key.ToLowerInvariant() switch
        {
            "disky" or "disks" => "kolesnye-diski",
            "accessories" or "aksessuary" => "avtoaksessuary",
            "to" or "texnicheskoe-obsluzhivanie" => "katalog-texnicheskogo-obsluzhivaniya",
            "oil" or "masla" or "masla-i-avtoximiya" => "avtoximiya",
            "akb" or "batteries" => "akkumulyatory",
            "caps" or "hubcaps" => "kolpaki",
            "bolty" or "bolty-gayki-prostavki" => "kolesnye-gajki-bolty-prostavki",
            "tires" or "tyres" => "shiny",
            _ => key,
        };
        return Cards.FirstOrDefault(c => string.Equals(c.Slug, key, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record StorefrontUcatsCard(
    string Slug,
    string Title,
    string Icon,
    string Blurb,
    string Detail,
    IReadOnlyList<(string Name, string Label)> PickerFields);
