namespace EcomAE.Platform.Presentation;

/// <summary>
/// Authoritative catalog of industry showcase frontends (<c>*.ecomae.com</c>).
/// Mirrored from PHP <c>epc_industry_seo_host_map()</c> + live <c>/platform/industries</c> links.
/// Live product chrome on these hosts stays PHP until dual-sample + approval.
/// ASP.NET compare surface: <c>/marketing/industries</c> on www only.
/// </summary>
public static class EcomaeIndustryShowcaseHosts
{
    public sealed record Host(
        string Slug,
        string Title,
        string HomeUrl,
        string CpUrl,
        string ErpUrl);

    /// <summary>28 primary industry showcase hosts (not client DB tenants).</summary>
    public static readonly IReadOnlyList<Host> All =
    [
        new("agriculture", "Agriculture & Farming", "https://agriculture.ecomae.com/", "https://agriculture.ecomae.com/CP/", "https://agriculture.ecomae.com/ERP/"),
        new("automotive", "Automotive & Vehicles", "https://automotive.ecomae.com/", "https://automotive.ecomae.com/CP/", "https://automotive.ecomae.com/ERP/"),
        new("beauty", "Beauty & Personal Care", "https://beauty.ecomae.com/", "https://beauty.ecomae.com/CP/", "https://beauty.ecomae.com/ERP/"),
        new("cleaning", "Cleaning Services", "https://cleaning.ecomae.com/", "https://cleaning.ecomae.com/CP/", "https://cleaning.ecomae.com/ERP/"),
        new("construction", "Construction & Real Estate", "https://construction.ecomae.com/", "https://construction.ecomae.com/CP/", "https://construction.ecomae.com/ERP/"),
        new("education", "Education & Training", "https://education.ecomae.com/", "https://education.ecomae.com/CP/", "https://education.ecomae.com/ERP/"),
        new("electronics", "Electronics & Technology", "https://electronics.ecomae.com/", "https://electronics.ecomae.com/CP/", "https://electronics.ecomae.com/ERP/"),
        new("energy", "Energy & Utilities", "https://energy.ecomae.com/", "https://energy.ecomae.com/CP/", "https://energy.ecomae.com/ERP/"),
        new("fashion", "Fashion & Apparel", "https://fashion.ecomae.com/", "https://fashion.ecomae.com/CP/", "https://fashion.ecomae.com/ERP/"),
        new("finance", "Financial Services & Insurance", "https://finance.ecomae.com/", "https://finance.ecomae.com/CP/", "https://finance.ecomae.com/ERP/"),
        new("food", "Food & Beverage", "https://food.ecomae.com/", "https://food.ecomae.com/CP/", "https://food.ecomae.com/ERP/"),
        new("healthcare", "Healthcare & Medical", "https://healthcare.ecomae.com/", "https://healthcare.ecomae.com/CP/", "https://healthcare.ecomae.com/ERP/"),
        new("homeliving", "Home & Living", "https://homeliving.ecomae.com/", "https://homeliving.ecomae.com/CP/", "https://homeliving.ecomae.com/ERP/"),
        new("hospitality", "Hospitality & Travel", "https://hospitality.ecomae.com/", "https://hospitality.ecomae.com/CP/", "https://hospitality.ecomae.com/ERP/"),
        new("jewellery", "Jewellery & Luxury Goods", "https://jewellery.ecomae.com/", "https://jewellery.ecomae.com/CP/", "https://jewellery.ecomae.com/ERP/"),
        new("logistics", "Logistics & Transport", "https://logistics.ecomae.com/", "https://logistics.ecomae.com/CP/", "https://logistics.ecomae.com/ERP/"),
        new("manufacturing", "Manufacturing & Industrial", "https://manufacturing.ecomae.com/", "https://manufacturing.ecomae.com/CP/", "https://manufacturing.ecomae.com/ERP/"),
        new("media", "Media & Entertainment", "https://media.ecomae.com/", "https://media.ecomae.com/CP/", "https://media.ecomae.com/ERP/"),
        new("nonprofit", "Nonprofit & Government", "https://nonprofit.ecomae.com/", "https://nonprofit.ecomae.com/CP/", "https://nonprofit.ecomae.com/ERP/"),
        new("pet", "Pet & Animal Services", "https://pet.ecomae.com/", "https://pet.ecomae.com/CP/", "https://pet.ecomae.com/ERP/"),
        new("printing", "Printing & Publishing", "https://printing.ecomae.com/", "https://printing.ecomae.com/CP/", "https://printing.ecomae.com/ERP/"),
        new("professional", "Professional & Business Services", "https://professional.ecomae.com/", "https://professional.ecomae.com/CP/", "https://professional.ecomae.com/ERP/"),
        new("rental", "Rental & Leasing", "https://rental.ecomae.com/", "https://rental.ecomae.com/CP/", "https://rental.ecomae.com/ERP/"),
        new("retail", "Retail & E-commerce", "https://retail.ecomae.com/", "https://retail.ecomae.com/CP/", "https://retail.ecomae.com/ERP/"),
        new("security", "Security Services", "https://security.ecomae.com/", "https://security.ecomae.com/CP/", "https://security.ecomae.com/ERP/"),
        new("sports", "Sports & Fitness", "https://sports.ecomae.com/", "https://sports.ecomae.com/CP/", "https://sports.ecomae.com/ERP/"),
        new("technology", "IT & Software", "https://technology.ecomae.com/", "https://technology.ecomae.com/CP/", "https://technology.ecomae.com/ERP/"),
        new("wholesale", "Wholesale & Trading", "https://wholesale.ecomae.com/", "https://wholesale.ecomae.com/CP/", "https://wholesale.ecomae.com/ERP/"),
    ];

    public static int Count => All.Count;

    public static IEnumerable<string> Slugs => All.Select(h => h.Slug);
}
