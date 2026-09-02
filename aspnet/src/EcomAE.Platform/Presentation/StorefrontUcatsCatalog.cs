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
            "PHP ucats/shiny — size groups and product cards. Use warehouse search for live stock."),
        new("kolesnye-diski", "Wheels", "fa-sun-o", "Alloy and steel wheel catalog.",
            "PHP ucats/disky — wheel size groups. Use name search for a manufacturer + size."),
        new("avtoaksessuary", "UCats accessories", "fa-shopping-bag", "Accessory groups by vehicle.",
            "PHP ucats/accessories. The storefront accessories marketplace is a separate dedicated app."),
        new("katalog-texnicheskogo-obsluzhivaniya", "Service / TO", "fa-wrench", "Maintenance kits by vehicle.",
            "PHP ucats/to — make → model → type → parts list. VIN search is the live ASP.NET twin."),
        new("avtoximiya", "Oil & chemicals", "fa-tint", "Oils, fluids, and auto chemistry.",
            "PHP ucats/oil. Warehouse attribute search covers live oil/fluid stock."),
        new("akkumulyatory", "Batteries", "fa-bolt", "Starter batteries by group.",
            "PHP ucats/akb. Search article or brand for live battery stock."),
        new("kolpaki", "Hubcaps", "fa-circle-thin", "Wheel covers and hubcaps.",
            "PHP ucats/kolpaki. Accessories marketplace also lists related trim."),
        new("kolesnye-gajki-bolty-prostavki", "Bolts & spacers", "fa-cog", "Wheel nuts, bolts, and spacers.",
            "PHP ucats/bolty_gayki_prostavki. Use name search for thread/size."),
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
