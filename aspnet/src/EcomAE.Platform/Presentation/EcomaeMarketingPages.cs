namespace EcomAE.Platform.Presentation;

/// <summary>
/// Authoritative catalog of www.ecomae.com marketing routes.
/// Primary Hrefs are ASP.NET /marketing/* pages (PHP style). PHP compare only via /php-reference/*.
/// </summary>
public static class EcomaeMarketingPages
{
    /// <summary>PHP live host — reference/compare only, never primary product clicks.</summary>
    public const string LiveBase = "https://www.ecomae.com/";
    public const string AspNetHome = "/marketing/app";
    public const string SuperCpUrl = "/cp";
    public const string PlatformErpUrl = "/erp";
    public const string ClientErpDemoUrl = "/erp";
    public const string PhpReferenceHome = "/php-reference/home";
    public const int DemoDays = 3;

    public sealed record PageLink(string Id, string Label, string Href, string Group);

    public static readonly IReadOnlyList<PageLink> All =
    [
        new("home", "Home", AspNetHome, "core"),
        new("platform", "Platform", "/marketing/platform", "core"),
        new("industries", "Industries", "/marketing/industries", "core"),
        new("capabilities", "Capabilities", "/marketing/capabilities", "core"),
        new("free_tools", "Free Tools", "/marketing/free-tools", "core"),
        new("pricing", "Pricing", "/marketing/pricing", "core"),
        new("about", "About", "/marketing/about", "core"),
        new("contact", "Contact", "/marketing/contact", "core"),
        new("demo", "3-day demo", "/marketing/demo", "solutions"),
        new("platform_guides", "Super CP guides", "/marketing/platform-guides", "solutions"),
        new("api_services", "Catalog & Price API", "/marketing/api-services", "solutions"),
        new("api_documentation", "Tenant ERP API", "/marketing/api-documentation", "solutions"),
        new("auto_price_ai", "Auto Price AI", "/marketing/auto-price-ai", "solutions"),
        new("faq", "FAQ", "/marketing/faq", "solutions"),
        new("customer_results", "Customer results", "/marketing/customer-results", "solutions"),
        new("business_continuity", "Continuity", "/marketing/business-continuity", "solutions"),
        new("brochure", "Product brochure", "/marketing/brochure", "resources"),
        new("brochure_cp", "Full CP brochure", "/marketing/brochure-cp", "resources"),
        new("docs", "Documentation", "/marketing/documentation", "resources"),
        new("compare", "Compare", "/marketing/compare", "resources"),
        new("blockchain", "Blockchain BOS", "/marketing/blockchain", "resources"),
        new("bos_marketing", "What is Blockchain BOS", "/marketing/bos", "resources"),
        new("solutions", "Solutions", "/marketing/solutions", "resources"),
        new("legal", "Legal policies", "/marketing/legal", "legal"),
        new("privacy", "Privacy", "/marketing/privacy", "legal"),
        new("terms", "Terms", "/marketing/terms", "legal"),
        new("cookie_policy", "Cookie policy", "/marketing/cookie-policy", "legal"),
        new("security_policy", "Security policy", "/marketing/security-policy", "legal"),
        new("right_to_use", "Right to use", "/marketing/right-to-use", "legal"),
        new("trademark", "Trademark", "/marketing/trademark", "legal"),
        new("copyright", "Copyright", "/marketing/copyright", "legal"),
        new("data_protection", "Data protection", "/marketing/data-protection", "legal"),
        new("acceptable_use", "Acceptable use", "/marketing/acceptable-use", "legal"),
        new("confidentiality", "Confidentiality", "/marketing/confidentiality", "legal"),
        new("intellectual_property", "Intellectual property", "/marketing/intellectual-property", "legal"),
        new("blockchain_disclaimer", "Blockchain disclaimer", "/marketing/blockchain-disclaimer", "legal"),
        new("dmca", "DMCA", "/marketing/dmca", "legal"),
    ];

    public static int Count => All.Count;

    public static IEnumerable<IGrouping<string, PageLink>> ByGroup()
        => All.GroupBy(p => p.Group);

    /// <summary>
    /// Relative or absolute paths that are PHP marketing pages (not ASP.NET apps / operator chrome).
    /// Marketing <c>/bos</c> is distinct from product <c>/BOS/</c>.
    /// </summary>
    public static bool IsMarketingPhpPath(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return false;
        }

        var value = href.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttps || absolute.Scheme == Uri.UriSchemeHttp)
            && absolute.Host.EndsWith("ecomae.com", StringComparison.OrdinalIgnoreCase))
        {
            value = string.IsNullOrEmpty(absolute.AbsolutePath) ? "/" : absolute.AbsolutePath;
        }

        if (!value.StartsWith('/'))
        {
            return false;
        }

        // Operator product chrome / ASP.NET scaffolding — never marketing PHP path.
        if (value.StartsWith("/CP", StringComparison.Ordinal)
            || value.StartsWith("/ERP", StringComparison.Ordinal)
            || value.StartsWith("/BOS", StringComparison.Ordinal)
            || value.StartsWith("/cp/", StringComparison.Ordinal)
            || value.StartsWith("/erp/", StringComparison.Ordinal)
            || value.StartsWith("/marketing/", StringComparison.Ordinal)
            || value.StartsWith("/storefront/", StringComparison.Ordinal)
            || value.StartsWith("/migration/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/auth/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/php-reference/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (value.Equals("/", StringComparison.Ordinal)
            || value.Equals("/index.php", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Case-sensitive for /bos marketing (PHP router); product is /BOS/.
        string[] exactOrPrefix =
        [
            "/platform",
            "/documentation",
            "/compare",
            "/bos",
            "/blockchain",
            "/brochure",
            "/legal",
            "/solutions",
            "/privacy",
            "/terms",
            "/cookie-policy",
            "/security-policy",
            "/right-to-use",
            "/trademark",
            "/copyright",
            "/data-protection",
            "/acceptable-use",
            "/confidentiality",
            "/intellectual-property",
            "/blockchain-disclaimer",
            "/dmca",
        ];

        foreach (var p in exactOrPrefix)
        {
            if (value.Equals(p, StringComparison.Ordinal)
                || value.StartsWith(p + "/", StringComparison.Ordinal)
                || value.StartsWith(p + "?", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
