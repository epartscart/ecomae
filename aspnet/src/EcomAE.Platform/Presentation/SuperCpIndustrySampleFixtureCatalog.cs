namespace EcomAE.Platform.Presentation;

/// <summary>
/// Deterministic, clearly labelled sample-data contract for the Super CP ERP preview.
/// This catalog describes isolated fixtures; it never writes to a tenant database.
/// </summary>
public sealed record SuperCpIndustrySampleFixture(
    string IndustryCode,
    string IndustryName,
    string Profile,
    string LabelPrefix,
    IReadOnlyList<string> ProductCodes,
    IReadOnlyList<string> CustomerCodes,
    IReadOnlyList<string> SupplierCodes,
    IReadOnlyList<string> WorkflowCodes,
    bool StorefrontEnabled);

public static class SuperCpIndustrySampleFixtureCatalog
{
    private static readonly IReadOnlyDictionary<string, string> ProfileByIndustry =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["auto_parts"] = "auto-parts",
            ["automotive_full_vehicles"] = "vehicles",
            ["jewellery"] = "jewellery",
            ["construction_contracting"] = "fit-out-contracting",
            ["furniture_interiors"] = "furniture-interiors",
            ["building_materials"] = "building-materials",
            ["manufacturing_industrial"] = "manufacturing",
            ["erp_standalone"] = "erp-standalone",
            ["platform_host"] = "platform",
        };

    private static readonly IReadOnlyList<SuperCpIndustrySampleFixture> Rows =
        PhpPortalIndustryCatalog.All
            .Select(Build)
            .ToArray();

    private static readonly IReadOnlyDictionary<string, SuperCpIndustrySampleFixture> ByCode =
        Rows.ToDictionary(row => row.IndustryCode, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<SuperCpIndustrySampleFixture> All => Rows;

    public static SuperCpIndustrySampleFixture Resolve(string? industryCode)
        => !string.IsNullOrWhiteSpace(industryCode)
            && ByCode.TryGetValue(industryCode.Trim(), out var fixture)
                ? fixture
                : ByCode["auto_parts"];

    public static bool Covers(string? industryCode)
        => !string.IsNullOrWhiteSpace(industryCode)
            && ByCode.ContainsKey(industryCode.Trim());

    private static SuperCpIndustrySampleFixture Build(PhpPortalIndustry industry)
    {
        var profile = ProfileByIndustry.TryGetValue(industry.Code, out var explicitProfile)
            ? explicitProfile
            : industry.Ecosystem switch
            {
                "commerce" => "commerce",
                "lifestyle_consumer" => "lifestyle",
                "business_services" => "professional-services",
                "asset_sharing" => "rental",
                "digital_technology" => "digital",
                _ => "general-erp",
            };
        var prefix = $"DEMO | {industry.Name}";
        var slug = industry.Code.Replace('_', '-');
        var storefrontEnabled = !string.Equals(industry.Code, "erp_standalone", StringComparison.OrdinalIgnoreCase);

        return new(
            industry.Code,
            industry.Name,
            profile,
            prefix,
            [$"{slug}-sku-001", $"{slug}-sku-002", $"{slug}-sku-003"],
            [$"{slug}-customer-001", $"{slug}-customer-002"],
            [$"{slug}-supplier-001", $"{slug}-supplier-002"],
            ["sales-order", "purchase-order", "receipt", "delivery", "cash-voucher", "gl-journal"],
            storefrontEnabled);
    }
}
