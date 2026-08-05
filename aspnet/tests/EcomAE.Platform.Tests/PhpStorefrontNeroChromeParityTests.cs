using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards epartscart storefront chrome same-to-same vs PHP templates/nero/desktop.php.
/// </summary>
public sealed class PhpStorefrontNeroChromeParityTests
{
    [Fact]
    public void StorefrontAssetsPointAtNeroNotModex()
    {
        Assert.All(
            LegacyPresentationAssets.StorefrontStylesheets,
            href => Assert.DoesNotContain("templates/modex/", href, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            LegacyPresentationAssets.StorefrontStylesheets,
            href => href.Contains("templates/nero/assets/css/style_all.css", StringComparison.Ordinal));
        Assert.Contains(
            LegacyPresentationAssets.StorefrontStylesheets,
            href => href.Contains("epc_storefront_professional_shell_css.php", StringComparison.Ordinal));
        Assert.Contains(
            LegacyPresentationAssets.StorefrontStylesheets,
            href => href.Contains("eparts-animated-logo.css", StringComparison.Ordinal));
        Assert.Equal("templates/nero/desktop.php", LegacyPresentationAssets.LegacyChromeSourceFor("storefront"));
    }

    [Fact]
    public void ChromeMarkupMatchesPhpProfessionalHeaderLook()
    {
        var path = Find("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor");
        var text = File.ReadAllText(path);
        Assert.Contains("ERP Login", text, StringComparison.Ordinal);
        Assert.Contains("Catalog <span class=\"hidden-sm\">of products</span>", text, StringComparison.Ordinal);
        Assert.Contains("Vendor login", text, StringComparison.Ordinal);
        Assert.Contains("Customer login", text, StringComparison.Ordinal);
        Assert.Contains("header-call-box", text, StringComparison.Ordinal);
        Assert.Contains("header-whatsapp-box", text, StringComparison.Ordinal);
        Assert.Contains("header-bulk-upload-box", text, StringComparison.Ordinal);
        Assert.Contains("Mon-Fri from 9:00 to 18:00, Sat from 9:00 to 16:00, Sun - day off.", text, StringComparison.Ordinal);
        // Must not fight PHP professional shell with flat gray search bar / white menu tiles.
        Assert.DoesNotContain(".schearch-line { background:#f3f4f6", text, StringComparison.Ordinal);
        Assert.DoesNotContain("background:#fff; border:1px solid #e5e7eb; color:#111", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProfessionalShellCssEndpointExtractsStyleBlocks()
    {
        var shell = Find("content/general_pages/site_professional_shell.php");
        var css = PhpLegacyAssetBridge.ExtractStyleBlocks(File.ReadAllText(shell));
        Assert.Contains(".header-call-box a", css, StringComparison.Ordinal);
        Assert.Contains(".schearch-line", css, StringComparison.Ordinal);
        Assert.Contains("epc-animated-logo__text", css, StringComparison.Ordinal);
        Assert.Contains("#ef4444", css, StringComparison.Ordinal);
        Assert.True(File.Exists(Find("content/general_pages/epc_storefront_professional_shell_css.php")));
    }

    [Fact]
    public void StorefrontStructuralSelectorsCoverNeroChrome()
    {
        var selectors = LegacyDesktopChromeCatalog.RequiredStructuralSelectors("storefront");
        Assert.Contains(".top-menu-line", selectors);
        Assert.Contains(".logo-line", selectors);
        Assert.Contains(".schearch-line", selectors);
        Assert.Contains(".header_search_form_1", selectors);
        Assert.Contains(".header_search_form_attr", selectors);
        Assert.Contains("#footer-widgets", selectors);
        Assert.DoesNotContain("#header-full-top", selectors);
    }

    [Fact]
    public void WarehouseAndCatalogPathsMapToSearchAppModes()
    {
        Assert.Equal("/storefront/search-app?mode=attr", PhpSurfaceLinkMap.AspNetPrimaryHref("/en/shop/warehouse-search"));
        Assert.Equal("/storefront/search-app?mode=vin", PhpSurfaceLinkMap.AspNetPrimaryHref("/katalog-laximo"));
        Assert.Equal("/storefront/search-app?mode=car", PhpSurfaceLinkMap.AspNetPrimaryHref("/vehicle-catalog"));
        Assert.True(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/en/shop/warehouse-search", out var mapped));
        Assert.Equal("/storefront/search-app?mode=attr", mapped);
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
