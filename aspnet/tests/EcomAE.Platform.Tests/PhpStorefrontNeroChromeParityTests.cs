using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards epartscart storefront chrome same-to-same vs PHP templates/nero/desktop.php.
/// </summary>
[Collection(PreferAspNetAppsCollection.Name)]
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
            href => href.Contains("epc_storefront_professional_shell.css", StringComparison.Ordinal));
        Assert.Contains(
            LegacyPresentationAssets.StorefrontStylesheets,
            href => href.Contains("epc-static.php?f=content/general_pages/epc_storefront_professional_shell.css", StringComparison.Ordinal));
        Assert.Contains(
            LegacyPresentationAssets.StorefrontStylesheets,
            href => href.Contains("eparts-animated-logo.css", StringComparison.Ordinal));
        Assert.Equal("templates/nero/desktop.php", LegacyPresentationAssets.LegacyChromeSourceFor("storefront"));
        Assert.True(File.Exists(Find("content/general_pages/epc_storefront_professional_shell.css")));
    }

    [Fact]
    public void ChromeMarkupMatchesPhpProfessionalHeaderLook()
    {
        var path = Find("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor");
        var text = File.ReadAllText(path);
        Assert.Contains("ERP Login", text, StringComparison.Ordinal);
        Assert.Contains("epc-garage-header-link", text, StringComparison.Ordinal);
        Assert.Contains("epc-erp-header-link", text, StringComparison.Ordinal);
        Assert.Contains("Catalog <span class=\"hidden-sm\">of products</span>", text, StringComparison.Ordinal);
        Assert.Contains("Vendor login", text, StringComparison.Ordinal);
        Assert.Contains("Customer login", text, StringComparison.Ordinal);
        Assert.Contains("header-call-box", text, StringComparison.Ordinal);
        Assert.Contains("header-whatsapp-box", text, StringComparison.Ordinal);
        Assert.Contains("header-bulk-upload-box", text, StringComparison.Ordinal);
        Assert.Contains("Mon-Fri from 9:00 to 18:00, Sat from 9:00 to 16:00, Sun - day off.", text, StringComparison.Ordinal);
        // Inline critical PHP-look CSS (must ship even when www CSS 404s).
        Assert.Contains("header-call-box a { background:#ef4444", text, StringComparison.Ordinal);
        Assert.Contains("background:linear-gradient(135deg,#111827", text, StringComparison.Ordinal);
        Assert.Contains("background:linear-gradient(135deg,#090f1d", text, StringComparison.Ordinal);
        Assert.Contains("color:#a5f3fc !important", text, StringComparison.Ordinal);
        Assert.Contains(".schearch-line", text, StringComparison.Ordinal);
        // Beat nero astself dark-gray nav links on dark bar (invisible top menu bug).
        Assert.Contains("header.epc-nero-header .top-menu-line .navbar-default .navbar-nav > li > a", text, StringComparison.Ordinal);
        Assert.Contains("color:rgba(255,255,255,.88) !important", text, StringComparison.Ordinal);
        Assert.Contains("display:flex !important", text, StringComparison.Ordinal);
        Assert.Contains("navbar-nav > .dropdown > a:before", text, StringComparison.Ordinal);
        // Must not fight PHP professional shell with flat gray search bar / white menu tiles / flat top bar.
        Assert.DoesNotContain(".schearch-line { background:#f3f4f6", text, StringComparison.Ordinal);
        Assert.DoesNotContain("background:#fff; border:1px solid #e5e7eb; color:#111", text, StringComparison.Ordinal);
        Assert.DoesNotContain("top-menu-line\" style=\"background:#1a1a1a", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Mon–Sat 9:00", text, StringComparison.Ordinal);
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
    public void WarehouseAndCatalogPathsMapToPhpCanonicalEnPages()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = false;
        // Interim: live tenant /storefront/* tree 404s — keep PHP /en/… full pages (warehouse, UMAPI, catalogs).
        Assert.Equal("/en/shop/warehouse-search", PhpSurfaceLinkMap.AspNetPrimaryHref("/en/shop/warehouse-search"));
        Assert.Equal("/en/katalog-laximo", PhpSurfaceLinkMap.AspNetPrimaryHref("/katalog-laximo"));
        Assert.Equal("/en/vehicle-catalog", PhpSurfaceLinkMap.AspNetPrimaryHref("/vehicle-catalog"));
        Assert.Equal("/en/product-family", PhpSurfaceLinkMap.AspNetPrimaryHref("/product-family"));
        Assert.Equal("/en/umapi_catalog", PhpSurfaceLinkMap.AspNetPrimaryHref("/umapi_catalog"));
        Assert.True(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/en/shop/warehouse-search", out var mapped));
        Assert.Equal("/en/shop/warehouse-search", mapped);
    }

    [Fact]
    public void ChromeFormsPointAtPhpNeroActions()
    {
        var path = Find("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor");
        var text = File.ReadAllText(path);
        Assert.Contains("StorefrontSurfaceLinks.WarehouseSearch", text, StringComparison.Ordinal);
        Assert.Contains("StorefrontSurfaceLinks.PartSearch", text, StringComparison.Ordinal);
        Assert.Contains("StorefrontSurfaceLinks.UmapiCatalog", text, StringComparison.Ordinal);
        Assert.Contains("StorefrontSurfaceLinks.ProductFamily", text, StringComparison.Ordinal);
        Assert.DoesNotContain("action=\"/storefront/search-app\"", text, StringComparison.Ordinal);
        // Same field vocabulary as PHP templates/nero warehouse-search attr form.
        Assert.Contains("value=\"engine_code\"", text, StringComparison.Ordinal);
        Assert.Contains("value=\"cross_reference\"", text, StringComparison.Ordinal);
        Assert.Contains("value=\"oe_number\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"task\" value=\"vehicles\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"mode\" value=\"attr\"", text, StringComparison.Ordinal);
        // Top nav: Original catalog / Demand intelligence are top-level (PHP nero), not under Selection catalogs.
        Assert.Contains("StorefrontSurfaceLinks.OriginalCatalog", text, StringComparison.Ordinal);
        Assert.Contains("StorefrontSurfaceLinks.DemandIntelligence", text, StringComparison.Ordinal);
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
