using EcomAE.Platform.LifeOs.Legal;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LifeOsLegalFooterTests
{
    [Fact]
    public void Footer_legal_links_match_php_ecomae_marketing_strip()
    {
        var keys = LifeOsLegalCatalog.FooterLegalLinks.Select(l => l.Key).ToArray();
        Assert.Equal(
        [
            "legal",
            "privacy",
            "terms",
            "cookie-policy",
            "security-policy",
            "right-to-use",
            "trademark",
            "copyright",
            "data-protection",
            "acceptable-use",
            "confidentiality",
            "intellectual-property",
            "blockchain-disclaimer",
            "dmca",
        ], keys);

        Assert.Equal(14, LifeOsLegalCatalog.FooterLegalLinks.Count);
        Assert.Equal("All policies", LifeOsLegalCatalog.FooterLegalLinks[0].Label);
        Assert.Equal("IP notice", LifeOsLegalCatalog.FooterLegalLinks[^1].Label);
        Assert.All(LifeOsLegalCatalog.FooterLegalLinks, l =>
        {
            Assert.StartsWith("https://www.ecomae.com/", l.Href, StringComparison.Ordinal);
            Assert.StartsWith("/", l.Path, StringComparison.Ordinal);
        });
        Assert.Equal("https://www.ecomae.com/privacy", LifeOsLegalCatalog.FooterLegalLinks.Single(l => l.Key == "privacy").Href);
        Assert.Equal("https://www.ecomae.com/copyright", LifeOsLegalCatalog.FooterLegalLinks.Single(l => l.Key == "copyright").Href);
        Assert.Equal("https://www.ecomae.com/legal", LifeOsLegalCatalog.LegalHubHref);
        Assert.Contains("Electronic World Group", LifeOsLegalCatalog.CopyrightLine(2026), StringComparison.Ordinal);
        Assert.Contains("Dubai, UAE", LifeOsLegalCatalog.CopyrightLine(2026), StringComparison.Ordinal);
    }

    [Fact]
    public void LifeOs_product_layout_and_footer_are_wired()
    {
        var layout = ReadRepoFile("Components/Layout/LifeOsProductLayout.razor");
        Assert.Contains("LifeOsSiteFooter", layout, StringComparison.Ordinal);
        Assert.Contains("PhpChromeLayout", layout, StringComparison.Ordinal);

        var home = ReadRepoFile("Components/Pages/LifeOsHomeApp.razor");
        Assert.Contains("LifeOsProductLayout", home, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"lifeos-footer\"", home, StringComparison.Ordinal);

        var footer = ReadRepoFile("Components/Shared/LifeOs/LifeOsSiteFooter.razor");
        Assert.Contains("Legal &amp; security", footer, StringComparison.Ordinal);
        Assert.Contains("LifeOsLegalCatalog.FooterLegalLinks", footer, StringComparison.Ordinal);
        Assert.Contains("Legal policies", footer, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(string underPlatform)
    {
        var probe = new DirectoryInfo(AppContext.BaseDirectory);
        while (probe is not null)
        {
            var candidates = new[]
            {
                Path.Combine(probe.FullName, "src", "EcomAE.Platform", underPlatform),
                Path.Combine(probe.FullName, "aspnet", "src", "EcomAE.Platform", underPlatform),
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c))
                {
                    return File.ReadAllText(c);
                }
            }

            probe = probe.Parent;
        }

        throw new FileNotFoundException(underPlatform);
    }
}
