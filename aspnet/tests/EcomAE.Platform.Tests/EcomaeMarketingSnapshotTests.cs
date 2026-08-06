using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards the full www.ecomae.com marketing site parity: every PHP marketing
/// page is served as a PHP-rendered snapshot at its canonical URL.
/// </summary>
public sealed class EcomaeMarketingSnapshotTests
{
    [Theory]
    [InlineData("/platform", "platform")]
    [InlineData("/platform/pricing", "platform__pricing")]
    [InlineData("/platform/pricing/", "platform__pricing")]
    [InlineData("/platform/pricing?x=1", "platform__pricing")]
    [InlineData("/platform/industry/auto_parts", "platform__industry__auto_parts")]
    [InlineData("/platform/free-tools/vat", "platform__free-tools__vat")]
    [InlineData("/platform/brochure", "brochure")]
    [InlineData("/platform/brochure/cp", "brochure__cp")]
    [InlineData("/brochure/cp", "brochure__cp")]
    [InlineData("/documentation/erp-modules", "documentation__erp-modules")]
    [InlineData("/compare/ecomae-vs-odoo", "compare__ecomae-vs-odoo")]
    [InlineData("/bos/bos-vs-erp", "bos__bos-vs-erp")]
    [InlineData("/solutions", "solutions")]
    [InlineData("/legal", "legal")]
    [InlineData("/privacy", "privacy")]
    [InlineData("/terms", "terms")]
    public void CanonicalPathsMapToSnapshotSlugs(string path, string slug)
    {
        Assert.Equal(slug, EcomaeMarketingSnapshots.SlugFor(path));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/bos")]
    [InlineData(null)]
    [InlineData("/platform/../etc/passwd")]
    public void NonSnapshotPathsReturnNull(string? path)
    {
        Assert.Null(EcomaeMarketingSnapshots.SlugFor(path));
    }

    [Theory]
    [InlineData("www.ecomae.com", true)]
    [InlineData("ecomae.com", true)]
    [InlineData("www.epartscart.com", false)]
    [InlineData("electronics.ecomae.com", false)]
    [InlineData("cp.ecomae.com", false)]
    public void MarketingHostGate(string host, bool expected)
    {
        Assert.Equal(expected, EcomaeMarketingSnapshots.IsMarketingHost(host));
    }

    [Theory]
    [InlineData("/platform")]
    [InlineData("/platform/pricing")]
    [InlineData("/platform/faq")]
    [InlineData("/platform/industries")]
    [InlineData("/platform/capabilities")]
    [InlineData("/platform/free-tools")]
    [InlineData("/platform/about")]
    [InlineData("/platform/contact")]
    [InlineData("/platform/demo")]
    [InlineData("/platform/industry/auto_parts")]
    [InlineData("/brochure")]
    [InlineData("/brochure/cp")]
    [InlineData("/documentation")]
    [InlineData("/compare")]
    [InlineData("/compare/ecomae-vs-odoo")]
    [InlineData("/bos/what-is-a-business-operating-system")]
    [InlineData("/blockchain")]
    [InlineData("/solutions")]
    [InlineData("/legal")]
    [InlineData("/privacy")]
    [InlineData("/terms")]
    [InlineData("/dmca")]
    public void SnapshotsExistAndAreCompletePhpPages(string path)
    {
        var html = EcomaeMarketingSnapshots.HtmlFor(path);
        Assert.False(string.IsNullOrWhiteSpace(html),
            $"snapshot missing for {path} — run scripts/render_ecomae_marketing_snapshots.php");
        Assert.Contains("<!DOCTYPE html>", html, StringComparison.OrdinalIgnoreCase);
        if (!path.Contains("/brochure", StringComparison.Ordinal))
        {
            // Brochures are standalone print-style documents in PHP too (no epm chrome).
            Assert.Contains("epm-", html, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("<?php", html, StringComparison.Ordinal);
    }

    [Fact]
    public void SnapshotCorpusCoversTheWholePhpMarketingRouter()
    {
        // The PHP router enumerates ~136 pages (platform pages, 14 free tools,
        // docs/compare/bos/solutions/legal catalogs, 13 legal aliases, industries).
        var dir = FindDir("content/general_pages/epc_rendered_marketing");
        var count = Directory.GetFiles(dir, "*.html").Length;
        Assert.True(count >= 130, $"expected ≥130 marketing snapshots, found {count}");
    }

    private static string FindDir(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(relative);
    }
}
