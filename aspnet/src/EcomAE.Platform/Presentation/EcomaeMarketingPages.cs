namespace EcomAE.Platform.Presentation;

/// <summary>
/// Authoritative catalog of www.ecomae.com marketing routes from PHP
/// <c>epc_ecomae_platform_match_path</c> / nav / legal aliases.
/// Live product pages remain PHP; ASP.NET /marketing/app hybrid-deeplinks here.
/// </summary>
public static class EcomaeMarketingPages
{
    public const string LiveBase = "https://www.ecomae.com/";
    public const string SuperCpUrl = "https://www.ecomae.com/cp";
    public const string PlatformErpUrl = "https://www.ecomae.com/erp/";
    public const string ClientErpDemoUrl = "https://www.ecomae.com/cp/client-erp/asap/";
    public const int DemoDays = 3;

    public sealed record PageLink(string Id, string Label, string Href, string Group);

    public static readonly IReadOnlyList<PageLink> All =
    [
        new("home", "Home", LiveBase, "core"),
        new("platform", "Platform", LiveBase + "platform", "core"),
        new("industries", "Industries", LiveBase + "platform/industries", "core"),
        new("capabilities", "Capabilities", LiveBase + "platform/capabilities", "core"),
        new("free_tools", "Free Tools", LiveBase + "platform/free-tools", "core"),
        new("pricing", "Pricing", LiveBase + "platform/pricing", "core"),
        new("about", "About", LiveBase + "platform/about", "core"),
        new("contact", "Contact", LiveBase + "platform/contact", "core"),
        new("demo", "3-day demo", LiveBase + "platform/demo", "solutions"),
        new("platform_guides", "Super CP guides", LiveBase + "platform/platform-guides", "solutions"),
        new("api_services", "Catalog & Price API", LiveBase + "platform/api-services", "solutions"),
        new("api_documentation", "Tenant ERP API", LiveBase + "platform/api-documentation", "solutions"),
        new("auto_price_ai", "Auto Price AI", LiveBase + "platform/auto-price-ai", "solutions"),
        new("faq", "FAQ", LiveBase + "platform/faq", "solutions"),
        new("customer_results", "Customer results", LiveBase + "platform/customer-results", "solutions"),
        new("business_continuity", "Continuity", LiveBase + "platform/business-continuity", "solutions"),
        new("brochure", "Product brochure", LiveBase + "brochure", "resources"),
        new("brochure_cp", "Full CP brochure", LiveBase + "brochure/cp", "resources"),
        new("docs", "Documentation", LiveBase + "documentation", "resources"),
        new("compare", "Compare", LiveBase + "compare", "resources"),
        new("blockchain", "Blockchain BOS", LiveBase + "blockchain", "resources"),
        new("bos_marketing", "What is Blockchain BOS", LiveBase + "bos", "resources"),
        new("solutions", "Solutions", LiveBase + "solutions", "resources"),
        new("legal", "Legal policies", LiveBase + "legal", "legal"),
        new("privacy", "Privacy", LiveBase + "privacy", "legal"),
        new("terms", "Terms", LiveBase + "terms", "legal"),
        new("cookie_policy", "Cookie policy", LiveBase + "cookie-policy", "legal"),
        new("security_policy", "Security policy", LiveBase + "security-policy", "legal"),
        new("right_to_use", "Right to use", LiveBase + "right-to-use", "legal"),
        new("trademark", "Trademark", LiveBase + "trademark", "legal"),
        new("copyright", "Copyright", LiveBase + "copyright", "legal"),
        new("data_protection", "Data protection", LiveBase + "data-protection", "legal"),
        new("acceptable_use", "Acceptable use", LiveBase + "acceptable-use", "legal"),
        new("confidentiality", "Confidentiality", LiveBase + "confidentiality", "legal"),
        new("intellectual_property", "Intellectual property", LiveBase + "intellectual-property", "legal"),
        new("blockchain_disclaimer", "Blockchain disclaimer", LiveBase + "blockchain-disclaimer", "legal"),
        new("dmca", "DMCA", LiveBase + "dmca", "legal"),
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

        // Operator product chrome / ASP.NET scaffolding — never marketing.
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
            || value.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
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
            "/en/privacy",
            "/en/terms",
        ];

        foreach (var prefix in exactOrPrefix)
        {
            if (value.Equals(prefix, StringComparison.Ordinal)
                || value.StartsWith(prefix + "/", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
