namespace EcomAE.Platform.Presentation;

/// <summary>
/// Host/tenant-aware storefront industry attributes mirrored from PHP
/// <c>epc_industry_seo_host_map()</c> + <c>epc_portal_resolve_storefront_package()</c>.
/// </summary>
public static class StorefrontIndustryHostResolver
{
    public sealed record IndustryAttrs(string IndustryCode, string StorefrontPackage);

    /// <summary>Host slug → portal industry_code (reverse of epc_industry_seo_host_map).</summary>
    private static readonly IReadOnlyDictionary<string, string> SlugToIndustryCode =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["automotive"] = "automotive",
            ["healthcare"] = "healthcare",
            ["food"] = "food_beverage",
            ["fashion"] = "fashion",
            ["jewellery"] = "jewellery",
            ["electronics"] = "electronics",
            ["construction"] = "construction",
            ["manufacturing"] = "manufacturing",
            ["professional"] = "professional",
            ["education"] = "education",
            ["hospitality"] = "hospitality",
            ["beauty"] = "beauty",
            ["retail"] = "retail",
            ["agriculture"] = "agriculture",
            ["logistics"] = "logistics",
            ["energy"] = "energy",
            ["finance"] = "finance",
            ["technology"] = "it_software",
            ["media"] = "media",
            ["sports"] = "sports",
            ["homeliving"] = "home_living",
            ["wholesale"] = "wholesale",
            ["rental"] = "rental",
            ["nonprofit"] = "nonprofit",
            ["cleaning"] = "cleaning",
            ["pet"] = "pet",
            ["printing"] = "printing",
            ["security"] = "security",
        };

    public static IndustryAttrs Resolve(string? host)
    {
        var industry = ResolveIndustryCode(host);
        var package = ResolveStorefrontPackage(industry);
        return new IndustryAttrs(industry, package);
    }

    public static string ResolveIndustryCode(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return "auto_parts";
        }

        var normalized = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (normalized.StartsWith("www.", StringComparison.Ordinal))
        {
            normalized = normalized[4..];
        }

        if (normalized is "ecomae.com" or "epartscart.com" or "localhost" or "127.0.0.1")
        {
            return "auto_parts";
        }

        if (normalized.EndsWith(".ecomae.com", StringComparison.Ordinal))
        {
            var slug = normalized[..^".ecomae.com".Length];
            if (SlugToIndustryCode.TryGetValue(slug, out var code))
            {
                return code;
            }
        }

        return "auto_parts";
    }

    public static string ResolveStorefrontPackage(string industryCode)
    {
        return industryCode switch
        {
            "auto_parts" or "automotive" => "automotive_spareparts_pro",
            "electronics" => "electronics_retail_virgin",
            "tax_advisory" or "consultancy" => "consulting_primeinvest",
            "fashion" => "fashion_retail_namshi",
            "jewellery" => "jewellery_retail_kiyasha",
            _ => "default",
        };
    }

    public static string ResolveStorefrontTitle(string? host)
    {
        var industry = ResolveIndustryCode(host);
        if (industry is "auto_parts" or "automotive")
        {
            return "eParts Cart (Autoparts)";
        }

        var slug = ExtractIndustrySlug(host);
        if (!string.IsNullOrEmpty(slug))
        {
            var showcase = EcomaeIndustryShowcaseHosts.All.FirstOrDefault(h =>
                h.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
            if (showcase is not null)
            {
                return $"{showcase.Title} · Storefront";
            }
        }

        return "ECOM AE Storefront";
    }

    private static string? ExtractIndustrySlug(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var normalized = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (normalized.StartsWith("www.", StringComparison.Ordinal))
        {
            normalized = normalized[4..];
        }

        if (!normalized.EndsWith(".ecomae.com", StringComparison.Ordinal))
        {
            return null;
        }

        return normalized[..^".ecomae.com".Length];
    }
}
