namespace EcomAE.Platform.LifeOs.Legal;

/// <summary>
/// Canonical ECOM AE legal &amp; security policies — same set as PHP
/// <c>epc_ecomae_platform_layout_close()</c> / marketing chrome footer.
/// Linked from lifeos.ecomae.com to www.ecomae.com (PHP-primary policy pages).
/// </summary>
public static class LifeOsLegalCatalog
{
    public const string CanonicalHost = "https://www.ecomae.com";

    public sealed record LegalLink(string Key, string Label, string Path)
    {
        public string Href => CanonicalHost + Path;
    }

    /// <summary>Exact Legal &amp; security strip from ecomae.com PHP footer.</summary>
    public static IReadOnlyList<LegalLink> FooterLegalLinks { get; } =
    [
        new("legal", "All policies", "/legal"),
        new("privacy", "Privacy", "/privacy"),
        new("terms", "Terms", "/terms"),
        new("cookie-policy", "Cookies", "/cookie-policy"),
        new("security-policy", "Security", "/security-policy"),
        new("right-to-use", "Right to use", "/right-to-use"),
        new("trademark", "Trademark", "/trademark"),
        new("copyright", "Copyright", "/copyright"),
        new("data-protection", "Data protection", "/data-protection"),
        new("acceptable-use", "Acceptable use", "/acceptable-use"),
        new("confidentiality", "Confidentiality", "/confidentiality"),
        new("intellectual-property", "Intellectual property", "/intellectual-property"),
        new("blockchain-disclaimer", "Blockchain disclaimer", "/blockchain-disclaimer"),
        new("dmca", "IP notice", "/dmca"),
    ];

    public static string LegalHubHref => CanonicalHost + "/legal";

    public static string CopyrightLine(int year) =>
        $"© {year} Electronic World Group · Dubai, UAE";
}
