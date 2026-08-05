namespace EcomAE.Platform.Presentation;

/// <summary>
/// Authoritative catalog of industry showcase frontends (<c>*.ecomae.com</c>).
/// Mirrored from PHP <c>epc_industry_seo_host_map()</c> + live <c>/platform/industries</c> links.
/// Marketing grid links use same-host ASP.NET /cp and /erp shells (not uppercase PHP /CP /ERP).
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
        new("agriculture", "Agriculture & Farming", "https://agriculture.ecomae.com/", "https://agriculture.ecomae.com/cp", "https://agriculture.ecomae.com/erp"),
        new("automotive", "Automotive & Vehicles", "https://automotive.ecomae.com/", "https://automotive.ecomae.com/cp", "https://automotive.ecomae.com/erp"),
        new("beauty", "Beauty & Personal Care", "https://beauty.ecomae.com/", "https://beauty.ecomae.com/cp", "https://beauty.ecomae.com/erp"),
        new("cleaning", "Cleaning Services", "https://cleaning.ecomae.com/", "https://cleaning.ecomae.com/cp", "https://cleaning.ecomae.com/erp"),
        new("construction", "Construction & Real Estate", "https://construction.ecomae.com/", "https://construction.ecomae.com/cp", "https://construction.ecomae.com/erp"),
        new("education", "Education & Training", "https://education.ecomae.com/", "https://education.ecomae.com/cp", "https://education.ecomae.com/erp"),
        new("electronics", "Electronics & Technology", "https://electronics.ecomae.com/", "https://electronics.ecomae.com/cp", "https://electronics.ecomae.com/erp"),
        new("energy", "Energy & Utilities", "https://energy.ecomae.com/", "https://energy.ecomae.com/cp", "https://energy.ecomae.com/erp"),
        new("fashion", "Fashion & Apparel", "https://fashion.ecomae.com/", "https://fashion.ecomae.com/cp", "https://fashion.ecomae.com/erp"),
        new("finance", "Financial Services & Insurance", "https://finance.ecomae.com/", "https://finance.ecomae.com/cp", "https://finance.ecomae.com/erp"),
        new("food", "Food & Beverage", "https://food.ecomae.com/", "https://food.ecomae.com/cp", "https://food.ecomae.com/erp"),
        new("healthcare", "Healthcare & Medical", "https://healthcare.ecomae.com/", "https://healthcare.ecomae.com/cp", "https://healthcare.ecomae.com/erp"),
        new("homeliving", "Home & Living", "https://homeliving.ecomae.com/", "https://homeliving.ecomae.com/cp", "https://homeliving.ecomae.com/erp"),
        new("hospitality", "Hospitality & Travel", "https://hospitality.ecomae.com/", "https://hospitality.ecomae.com/cp", "https://hospitality.ecomae.com/erp"),
        new("jewellery", "Jewellery & Luxury Goods", "https://jewellery.ecomae.com/", "https://jewellery.ecomae.com/cp", "https://jewellery.ecomae.com/erp"),
        new("logistics", "Logistics & Transport", "https://logistics.ecomae.com/", "https://logistics.ecomae.com/cp", "https://logistics.ecomae.com/erp"),
        new("manufacturing", "Manufacturing & Industrial", "https://manufacturing.ecomae.com/", "https://manufacturing.ecomae.com/cp", "https://manufacturing.ecomae.com/erp"),
        new("media", "Media & Entertainment", "https://media.ecomae.com/", "https://media.ecomae.com/cp", "https://media.ecomae.com/erp"),
        new("nonprofit", "Non-Profit & Government", "https://nonprofit.ecomae.com/", "https://nonprofit.ecomae.com/cp", "https://nonprofit.ecomae.com/erp"),
        new("pet", "Pet & Animal Services", "https://pet.ecomae.com/", "https://pet.ecomae.com/cp", "https://pet.ecomae.com/erp"),
        new("printing", "Printing & Publishing", "https://printing.ecomae.com/", "https://printing.ecomae.com/cp", "https://printing.ecomae.com/erp"),
        new("professional", "Professional & Business Services", "https://professional.ecomae.com/", "https://professional.ecomae.com/cp", "https://professional.ecomae.com/erp"),
        new("rental", "Rental & Leasing", "https://rental.ecomae.com/", "https://rental.ecomae.com/cp", "https://rental.ecomae.com/erp"),
        new("retail", "Retail & E-commerce", "https://retail.ecomae.com/", "https://retail.ecomae.com/cp", "https://retail.ecomae.com/erp"),
        new("security", "Security Services", "https://security.ecomae.com/", "https://security.ecomae.com/cp", "https://security.ecomae.com/erp"),
        new("sports", "Sports & Fitness", "https://sports.ecomae.com/", "https://sports.ecomae.com/cp", "https://sports.ecomae.com/erp"),
        new("technology", "IT & Software", "https://technology.ecomae.com/", "https://technology.ecomae.com/cp", "https://technology.ecomae.com/erp"),
        new("wholesale", "Wholesale & Trading", "https://wholesale.ecomae.com/", "https://wholesale.ecomae.com/cp", "https://wholesale.ecomae.com/erp"),
    ];

    public static int Count => All.Count;

    public static IEnumerable<string> Slugs => All.Select(h => h.Slug);
}
