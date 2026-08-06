using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards the non-epartscart tenant storefront homes same-to-same vs the PHP
/// custom-storefront packages (templates/nero/desktop.php custom branch).
/// </summary>
public sealed class TenantHomePhpParityTests
{
    [Theory]
    [InlineData("www.electronicae.com", "electronics", "electronics_retail_virgin")]
    [InlineData("electronicae.com", "electronics", "electronics_retail_virgin")]
    [InlineData("www.stylenlook.com", "fashion", "fashion_retail_namshi")]
    [InlineData("www.thejewellerytrend.com", "jewellery", "jewellery_retail_kiyasha")]
    [InlineData("www.taxofinca.com", "tax_advisory", "consulting_primeinvest")]
    [InlineData("www.epartscart.com", "auto_parts", "automotive_spareparts_pro")]
    public void ProductTenantHostsResolveToPhpPortalPackages(string host, string industry, string package)
    {
        // Mirrors PHP epc_portal_sites() + epc_portal_active_storefront_package().
        var attrs = StorefrontIndustryHostResolver.Resolve(host);
        Assert.Equal(industry, attrs.IndustryCode);
        Assert.Equal(package, attrs.StorefrontPackage);
    }

    [Theory]
    [InlineData("www.electronicae.com", "Electronicae — Tech, Gaming & Electronics UAE")]
    [InlineData("www.stylenlook.com", "Stylenlook — Fashion & Beauty UAE")]
    [InlineData("www.thejewellerytrend.com", "The Jewellery Trend — Fine Jewellery UAE")]
    [InlineData("www.taxofinca.com", "Taxofinca — Tax, Accounting & Advisory UAE")]
    public void TenantTitlesMatchPhpPackageSeo(string host, string title)
    {
        Assert.Equal(title, StorefrontIndustryHostResolver.ResolveStorefrontTitle(host));
    }

    [Theory]
    [InlineData("electronics_retail_virgin", "epc-er-home", "epc_electronics_retail.css")]
    [InlineData("fashion_retail_namshi", "epc-frn-home", "epc_fashion_retail_namshi.css")]
    [InlineData("jewellery_retail_kiyasha", "epc-jrk-home", "epc_jewellery_retail_kiyasha.css")]
    [InlineData("consulting_primeinvest", "epc-cpi-home", "epc_consulting_primeinvest.css")]
    public void SnapshotsContainPhpHomeRootAndPackageCss(string package, string rootClass, string css)
    {
        Assert.True(PhpTenantHomeSnapshots.IsCustomPackage(package));
        var html = PhpTenantHomeSnapshots.HtmlFor(package);
        Assert.False(string.IsNullOrWhiteSpace(html), $"snapshot missing for {package} — run scripts/render_php_home_snapshots.php");
        Assert.Contains(rootClass, html, StringComparison.Ordinal);
        Assert.Contains(css, html, StringComparison.Ordinal);
        Assert.DoesNotContain("<?php", html, StringComparison.Ordinal);
    }

    [Fact]
    public void EpartscartStaysOnNeroAutomotivePath()
    {
        Assert.False(PhpTenantHomeSnapshots.IsCustomPackage("automotive_spareparts_pro"));
        Assert.False(PhpTenantHomeSnapshots.IsCustomPackage("default"));
        Assert.Equal(string.Empty, PhpTenantHomeSnapshots.HtmlFor("automotive_spareparts_pro"));
    }

    [Fact]
    public void HomeRendersSnapshotForCustomPackages()
    {
        var path = Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontPreviewApp.razor");
        var text = File.ReadAllText(path);
        Assert.Contains("PhpTenantHomeSnapshots.IsCustomPackage", text, StringComparison.Ordinal);
        Assert.Contains("_tenantSnapshotHtml", text, StringComparison.Ordinal);
    }

    private static string Find(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
