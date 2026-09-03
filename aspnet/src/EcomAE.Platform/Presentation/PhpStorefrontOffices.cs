namespace EcomAE.Platform.Presentation;

/// <summary>
/// Customer pickup / office list twin for PHP <c>shop_offices</c> shown at checkout.
/// Seed is industry-scoped so jewellery boutiques never appear on epartscart.
/// </summary>
public static class PhpStorefrontOffices
{
    public sealed record Office(string Name, string City, string Address, string Hours, string Phone);

    public static IReadOnlyList<Office> ForIndustry(string? industryCode)
    {
        var industry = string.IsNullOrWhiteSpace(industryCode) ? "auto_parts" : industryCode.Trim().ToLowerInvariant();
        return industry switch
        {
            "electronics" =>
            [
                new("Dubai Mall counter", "Dubai", "Lower ground, electronics wing", "10:00–22:00", "+971 4 000 2100"),
                new("Abu Dhabi warehouse", "Abu Dhabi", "Mussafah industrial, gate 2", "09:00–18:00", "+971 2 000 2100"),
            ],
            "fashion" =>
            [
                new("Dubai Marina store", "Dubai", "Marina Walk, Style N Look", "10:00–22:00", "+971 4 000 3100"),
                new("Sharjah City Centre", "Sharjah", "Level 1, fashion court", "10:00–22:00", "+971 6 000 3100"),
            ],
            "jewellery" =>
            [
                new("Gold Souk boutique", "Dubai", "Deira Gold Souk, The Jewellery Trend", "10:00–21:00", "+971 4 000 4100"),
                new("Abu Dhabi boutique", "Abu Dhabi", "World Trade Center mall", "10:00–22:00", "+971 2 000 4100"),
            ],
            "tax_advisory" or "consultancy" =>
            [
                new("DIFC advisory desk", "Dubai", "Gate Village, Taxofinca", "09:00–18:00", "+971 4 000 5100"),
                new("Abu Dhabi Global Market", "Abu Dhabi", "Al Maryah Island", "09:00–18:00", "+971 2 000 5100"),
            ],
            _ =>
            [
                new("Dubai warehouse counter", "Dubai", "Al Quoz industrial, eParts Cart", "08:30–18:00", "+971 4 000 1100"),
                new("Abu Dhabi pickup", "Abu Dhabi", "Mussafah M14, warehouse 4", "08:30–17:30", "+971 2 000 1100"),
            ],
        };
    }
}
