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
            "Tire size groups and product cards. Use warehouse search for live stock."),
        new("kolesnye-diski", "Wheels", "fa-sun-o", "Alloy and steel wheel catalog.",
            "Wheel size groups. Use name search for a manufacturer + size."),
        new("avtoaksessuary", "UCats accessories", "fa-shopping-bag", "Accessory groups by vehicle.",
            "Accessory groups by vehicle. The accessories marketplace is a separate dedicated app."),
        new("katalog-texnicheskogo-obsluzhivaniya", "Service / TO", "fa-wrench", "Maintenance kits by vehicle.",
            "Make → model → type → parts list. VIN search is the live twin."),
        new("avtoximiya", "Oil & chemicals", "fa-tint", "Oils, fluids, and auto chemistry.",
            "Oils, fluids, and auto chemistry. Warehouse attribute search covers live stock."),
        new("akkumulyatory", "Batteries", "fa-bolt", "Starter batteries by group.",
            "Starter batteries by group. Search article or brand for live stock."),
        new("kolpaki", "Hubcaps", "fa-circle-thin", "Wheel covers and hubcaps.",
            "Wheel covers and hubcaps. Accessories marketplace also lists related trim."),
        new("kolesnye-gajki-bolty-prostavki", "Bolts & spacers", "fa-cog", "Wheel nuts, bolts, and spacers.",
            "Wheel nuts, bolts, and spacers. Use name search for thread/size."),
    ];

    public static StorefrontUcatsCard? Find(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        return Cards.FirstOrDefault(c => string.Equals(c.Slug, slug, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record StorefrontUcatsCard(string Slug, string Title, string Icon, string Blurb, string Detail);
