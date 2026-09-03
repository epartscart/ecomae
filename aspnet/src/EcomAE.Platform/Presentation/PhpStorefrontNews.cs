namespace EcomAE.Platform.Presentation;

/// <summary>
/// Customer news twins for PHP <c>content/general_pages/news.php</c> (<c>/en/novosti</c>).
/// Seed is industry-scoped so jewellery never appears on epartscart.
/// </summary>
public static class PhpStorefrontNews
{
    public sealed record Article(string Url, string Title, string Lead, string Body, string Date);

    public static IReadOnlyList<Article> ForIndustry(string? industryCode)
    {
        var industry = string.IsNullOrWhiteSpace(industryCode) ? "auto_parts" : industryCode.Trim().ToLowerInvariant();
        return industry switch
        {
            "electronics" => Electronics,
            "fashion" => Fashion,
            "jewellery" => Jewellery,
            "tax_advisory" or "consultancy" => Consulting,
            _ => AutoParts,
        };
    }

    public static bool TryFind(string? industryCode, string? url, out Article article)
    {
        var key = (url ?? string.Empty).Trim().Trim('/');
        article = ForIndustry(industryCode).FirstOrDefault(a =>
            string.Equals(a.Url, key, StringComparison.OrdinalIgnoreCase))!;
        return article is not null;
    }

    public static bool IsNewsPath(string? only)
    {
        var path = (only ?? string.Empty).Trim().Trim('/');
        return path.Equals("novosti", StringComparison.OrdinalIgnoreCase)
               || path.Equals("news", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("novosti/", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("news/", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly Article[] AutoParts =
    [
        new("novosti/ramadan-parts-hours", "Ramadan parts desk hours",
            "VIN search and warehouse pick-up stay open with shorter evening hours.",
            "The storefront VIN desk and warehouse counter keep the same part-search tools during Ramadan. Evening cut-off moves earlier; next-day courier still covers Dubai and Abu Dhabi.",
            "2026-03-02"),
        new("novosti/bulk-upload-excel", "Excel bulk upload for workshops",
            "Upload a parts list from the garage notepad or a workshop job card.",
            "Workshops can send an Excel list from the bulk-upload page. The seller request form still accepts VIN plus photos when the article is unknown.",
            "2026-02-18"),
        new("novosti/ucats-tires-wheels", "Tires and wheels catalogs",
            "Size pickers for tires, wheels, oil, and batteries are on the service catalogs hub.",
            "Open the Epart service catalogs, pick a size or vehicle field, then search live stock. Deep UCats trees remain on the classic pickers.",
            "2026-01-22"),
    ];

    private static readonly Article[] Electronics =
    [
        new("novosti/iphone-trade-in", "Trade-in week for phones",
            "Bring a working handset when you collect a new phone from the store.",
            "Electronicae accepts working phones against a new device. Gift cards and warranty questions stay on the contact page.",
            "2026-03-01"),
        new("novosti/gaming-stock", "Gaming laptops back in stock",
            "New gaming notebooks landed in the Dubai warehouse.",
            "Browse the gaming category for current AED prices. Store pickup is available the same day when the listing shows stock.",
            "2026-02-10"),
    ];

    private static readonly Article[] Fashion =
    [
        new("novosti/eid-abaya-edit", "Eid abaya edit",
            "New abayas and modest sets for the holiday week.",
            "Style N Look lists the Eid edit under Women. Sizing help stays on the contact page.",
            "2026-03-04"),
        new("novosti/beauty-delivery", "Beauty delivery windows",
            "Perfume and beauty orders ship in insulated packs.",
            "Same-city delivery stays next day when you order before the afternoon cut-off.",
            "2026-02-14"),
    ];

    private static readonly Article[] Jewellery =
    [
        new("novosti/gold-hallmark", "Gold hallmark week",
            "22K pieces ship with a hallmark card.",
            "The Jewellery Trend includes hallmark paperwork on gold rings and sets. Ring sizing stays on the contact page.",
            "2026-03-03"),
        new("novosti/bridal-appointments", "Bridal set appointments",
            "Book a boutique slot for bridal sets.",
            "Bridal collections stay under Bridal. Insured delivery questions use the contact form.",
            "2026-02-08"),
    ];

    private static readonly Article[] Consulting =
    [
        new("novosti/corporate-tax-deadline", "Corporate tax filing window",
            "UAE corporate tax filings for the current period.",
            "TaxoFinca lists corporate tax and VAT services under Services. Client ERP stays on the signed-in desk.",
            "2026-03-01"),
        new("novosti/vat-registration", "VAT registration pack",
            "New VAT registrations for UAE mainland and free-zone firms.",
            "Open Services for the VAT pack. Bookkeeping questions go through the contact page.",
            "2026-02-12"),
    ];
}
