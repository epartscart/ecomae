using System.Linq;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// All five locked live tenants: package chrome, industry slugs, CMS twins,
/// and host isolation (no jewellery on epartscart).
/// </summary>
public sealed class LiveTenantIndustryParityTests
{
    [Fact]
    public void LockedLiveTenantsAreTheFiveProductHosts()
    {
        Assert.Equal(5, LiveTenantPresentationLock.Tenants.Count);
        Assert.Contains(LiveTenantPresentationLock.Tenants, t => t.Id == "epartscart");
        Assert.Contains(LiveTenantPresentationLock.Tenants, t => t.Id == "electronicae");
        Assert.Contains(LiveTenantPresentationLock.Tenants, t => t.Id == "stylenlook");
        Assert.Contains(LiveTenantPresentationLock.Tenants, t => t.Id == "thejewellerytrend");
        Assert.Contains(LiveTenantPresentationLock.Tenants, t => t.Id == "taxofinca");
    }

    [Theory]
    [InlineData("www.electronicae.com", "electronics", "gaming", "catalog:gaming")]
    [InlineData("electronicae.com", "electronics", "/en/smartphones", "catalog:smartphones")]
    [InlineData("www.stylenlook.com", "fashion", "/en/women", "catalog:women")]
    [InlineData("www.stylenlook.com", "fashion", "/beauty/perfumes", "catalog:beauty/perfumes")]
    [InlineData("www.stylenlook.com", "fashion", "/accessories", "catalog:accessories")]
    [InlineData("www.thejewellerytrend.com", "jewellery", "/gold/rings", "catalog:gold/rings")]
    [InlineData("www.thejewellerytrend.com", "jewellery", "/bridal", "catalog:bridal")]
    [InlineData("www.taxofinca.com", "tax_advisory", "/services/tax", "catalog:services/tax")]
    [InlineData("www.taxofinca.com", "tax_advisory", "/en/shop/erp", "client-erp")]
    [InlineData("www.epartscart.com", "auto_parts", "/en/kontakty", "cms:kontakty")]
    [InlineData("www.electronicae.com", "electronics", "/o-dostavke", "cms:o-dostavke")]
    public void IndustrySlugsRewriteOnOwningHost(string host, string industry, string path, string kind)
    {
        Assert.Equal(industry, StorefrontIndustryHostResolver.ResolveIndustryCode(host));
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch(host, path, out var rewrite, out var matched));
        Assert.Equal(kind, matched);
        Assert.False(string.IsNullOrWhiteSpace(rewrite));
        if (kind.StartsWith("catalog:", StringComparison.Ordinal))
        {
            Assert.StartsWith(StorefrontAspNetCanonical.IndustryCatalog, rewrite, StringComparison.Ordinal);
        }
        else if (kind.StartsWith("cms:", StringComparison.Ordinal))
        {
            Assert.StartsWith(StorefrontAspNetCanonical.IndustryCms, rewrite, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal("/erp", rewrite);
        }
    }

    [Theory]
    [InlineData("www.epartscart.com", "/gaming")]
    [InlineData("www.epartscart.com", "/gold")]
    [InlineData("www.epartscart.com", "/women")]
    [InlineData("www.epartscart.com", "/accessories")]
    [InlineData("www.epartscart.com", "/services/tax")]
    [InlineData("www.electronicae.com", "/gold")]
    [InlineData("www.stylenlook.com", "/gaming")]
    [InlineData("www.thejewellerytrend.com", "/women")]
    [InlineData("www.taxofinca.com", "/gaming")]
    public void ForeignIndustrySlugsDoNotRewrite(string host, string path)
    {
        Assert.False(IndustryStorefrontSlugMiddleware.TryMatch(host, path, out _, out _));
    }

    [Theory]
    [InlineData("www.epartscart.com", "/en/shop/cart")]
    [InlineData("www.electronicae.com", "/shop/orders")]
    [InlineData("www.stylenlook.com", "/users/login")]
    [InlineData("www.thejewellerytrend.com", "/parts/BOSCH/123")]
    [InlineData("www.taxofinca.com", "/cp")]
    public void ReservedProductPathsStayUntouched(string host, string path)
    {
        Assert.False(IndustryStorefrontSlugMiddleware.TryMatch(host, path, out _, out _));
    }

    [Fact]
    public void ElectronicsSeedMatchesPhpCategoryAndProductCounts()
    {
        Assert.Equal(21, PhpIndustryStorefrontCatalog.CategoriesFor("electronics").Count);
        Assert.Equal(26, PhpIndustryStorefrontCatalog.ProductsFor("electronics").Count);
        Assert.True(PhpIndustryStorefrontCatalog.TryResolve("electronics", "gaming", out var gaming));
        Assert.Equal(3, PhpIndustryStorefrontCatalog.ProductsIn("electronics", gaming).Count);
        Assert.Equal("AED 3,499", PhpIndustryStorefrontCatalog.FormatAed(3499));
    }

    [Fact]
    public void FashionSeedKeepsGccAbayaAndDoesNotLeakJewelleryCollections()
    {
        Assert.Contains(PhpIndustryStorefrontCatalog.CategoriesFor("fashion"), c => c.Url == "women/abayas");
        Assert.False(PhpIndustryStorefrontCatalog.OwnsUrl("fashion", "gold"));
        Assert.True(PhpIndustryStorefrontCatalog.OwnsUrl("jewellery", "gold"));
        Assert.Equal(24, PhpIndustryStorefrontCatalog.ProductsFor("jewellery").Count);
    }

    [Fact]
    public void ConsultingSeedIsTaxServicesNotRetail()
    {
        Assert.True(PhpIndustryStorefrontCatalog.OwnsUrl("tax_advisory", "services/corporate-tax"));
        Assert.True(PhpIndustryStorefrontCatalog.OwnsUrl("consultancy", "services/bookkeeping"));
        Assert.False(PhpIndustryStorefrontCatalog.OwnsUrl("tax_advisory", "smartphones"));
        Assert.Equal(23, PhpIndustryStorefrontCatalog.ProductsFor("tax_advisory").Count);
    }

    [Theory]
    [InlineData("electronics_retail_virgin", "epc-er-home")]
    [InlineData("fashion_retail_namshi", "epc-frn-home")]
    [InlineData("jewellery_retail_kiyasha", "epc-jrk-home")]
    [InlineData("consulting_primeinvest", "epc-cpi-home")]
    public void SnapshotWrapKeepsPackageHeaderAndFooter(string package, string homeClass)
    {
        var inner = "<section id=\"epc-ind-test\">inner-page</section>";
        var html = PhpTenantHomeSnapshots.WrapInner(package, inner);
        Assert.Contains("inner-page", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<div class=\"" + homeClass, html, StringComparison.Ordinal);
        Assert.Contains("<header", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<footer", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CmsCopyIsIndustryScoped()
    {
        var jewellery = PhpIndustryCmsPages.Resolve("kontakty", "jewellery");
        Assert.Contains("Jewellery", jewellery.Lead, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VIN", jewellery.Lead, StringComparison.OrdinalIgnoreCase);
        var auto = PhpIndustryCmsPages.Resolve("kontakty", "auto_parts");
        Assert.Contains("VIN", auto.Lead, StringComparison.Ordinal);
        var tax = PhpIndustryCmsPages.Resolve("o-dostavke", "tax_advisory");
        Assert.Contains("ERP", tax.Paragraphs[1], StringComparison.Ordinal);
        Assert.True(PhpIndustryCmsPages.IsSlug("polzovatelskoe-soglashenie"));
        Assert.Equal("User agreement", PhpIndustryCmsPages.Resolve("polzovatelskoe-soglashenie", "jewellery").Title);
    }

    [Theory]
    [InlineData("/en/cp", true)]
    [InlineData("/en/cp/", true)]
    [InlineData("/ar/erp", true)]
    [InlineData("/en/erp/sales-orders-app", true)]
    [InlineData("/en/bos", true)]
    [InlineData("/en/shop/cart", false)]
    [InlineData("/en/kontakty", false)]
    public void LangPrefixedAdminShellsAreGated(string path, bool required)
    {
        Assert.Equal(required, AdminSurfaceAuthGateMiddleware.RequiresAdmin(path));
    }

    [Fact]
    public void ShopErpIncomingMapsToErpNotHome()
    {
        Assert.Equal("/erp", PhpSurfaceLinkMap.AspNetPrimaryHref("/shop/erp"));
        Assert.Equal("/erp", PhpSurfaceLinkMap.AspNetPrimaryHref("/en/shop/erp"));
    }

    [Fact]
    public void UsersRegisterIsBlazorOwnedSameUrl()
    {
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/en/users/register", out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/users/register", out _));
    }

    [Theory]
    [InlineData("www.electronicae.com", "Electronicae")]
    [InlineData("www.stylenlook.com", "StyleNLook")]
    [InlineData("www.thejewellerytrend.com", "The Jewellery Trend")]
    [InlineData("www.taxofinca.com", "TaxoFinca")]
    [InlineData("www.epartscart.com", "eParts Cart")]
    public void BrandLabelsMatchTenantNotEpartsOnIndustryHosts(string host, string label)
    {
        Assert.Equal(label, StorefrontIndustryHostResolver.ResolveBrandLabel(host));
    }

    [Theory]
    [InlineData("www.electronicae.com", "/en/shop/search?search_string=iPhone", "industry-search")]
    [InlineData("www.stylenlook.com", "/shop/search", "industry-search")]
    [InlineData("www.thejewellerytrend.com", "/p/JWL-GN-22K-15G", "product:JWL-GN-22K-15G")]
    [InlineData("www.taxofinca.com", "/product/CNS-VAT-REG-NEW", "product:CNS-VAT-REG-NEW")]
    [InlineData("www.electronicae.com", "/en/vendor", "vendor")]
    [InlineData("www.stylenlook.com", "/vendor/register", "vendor-register")]
    [InlineData("www.thejewellerytrend.com", "/vendor/upload", "vendor-upload")]
    [InlineData("www.taxofinca.com", "/en/users/forgot_password", "forgot-password")]
    [InlineData("www.epartscart.com", "/users/confirm", "confirm-contact")]
    [InlineData("www.electronicae.com", "/en/shop/returns", "customer-returns")]
    [InlineData("www.stylenlook.com", "/polzovatelskoe-soglashenie", "cms:polzovatelskoe-soglashenie")]
    public void CustomerParitySlugsRewrite(string host, string path, string kind)
    {
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch(host, path, out var rewrite, out var matched));
        Assert.Equal(kind, matched);
        Assert.False(string.IsNullOrWhiteSpace(rewrite));
    }

    [Fact]
    public void EpartscartKeepsAutomotiveSearchAndRejectsForeignSkus()
    {
        Assert.False(IndustryStorefrontSlugMiddleware.TryMatch("www.epartscart.com", "/en/shop/search", out _, out _));
        Assert.False(IndustryStorefrontSlugMiddleware.TryMatch("www.epartscart.com", "/en/shop/warehouse-search", out _, out _));
        Assert.False(IndustryStorefrontSlugMiddleware.TryMatch("www.epartscart.com", "/p/JWL-GN-22K-15G", out _, out _));
        Assert.False(PhpIndustryStorefrontCatalog.TryFindProduct("auto_parts", "ELC-IP16-128", out _));
        Assert.True(PhpIndustryStorefrontCatalog.TryFindProduct("electronics", "ELC-IP16-128", out var phone));
        Assert.Equal("ELC-IP16-128", phone.Alias);
        Assert.Contains(PhpIndustryStorefrontCatalog.Search("electronics", "iPhone"), p => p.Alias == "ELC-IP16-128");
        Assert.Empty(PhpIndustryStorefrontCatalog.Search("jewellery", "iPhone"));
    }

    [Fact]
    public void VendorAndForgotStayOnSameUrl()
    {
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/en/vendor", out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/vendor/register", out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/en/users/forgot_password", out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/en/shop/returns", out _));
        Assert.Equal(StorefrontAspNetCanonical.VendorPortal, PhpSurfaceLinkMap.AspNetPrimaryHref("/en/vendor"));
        Assert.Equal(StorefrontAspNetCanonical.ForgotPassword, PhpSurfaceLinkMap.AspNetPrimaryHref("/users/forgot"));
        Assert.Equal(StorefrontAspNetCanonical.CustomerReturns, PhpSurfaceLinkMap.AspNetPrimaryHref("/shop/returns"));
        Assert.Equal("Dubai Economy and Tourism", PhpVendorPortal.AuthorityFor("Dubai"));
    }

    [Theory]
    [InlineData("www.epartscart.com", "/en/auto-workshop", "auto-workshop")]
    [InlineData("www.epartscart.com", "/garage/manager", "garage-manager")]
    [InlineData("www.epartscart.com", "/en/garazh", "customer-garage")]
    [InlineData("www.epartscart.com", "/garazh/avtomobil", "customer-garage")]
    [InlineData("www.electronicae.com", "/en/newsletter", "newsletter")]
    public void AutomotiveWorkshopAndNewsletterRewrite(string host, string path, string kind)
    {
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch(host, path, out var rewrite, out var matched));
        Assert.Equal(kind, matched);
        Assert.False(string.IsNullOrWhiteSpace(rewrite));
    }

    [Fact]
    public void WorkshopDoesNotLeakOntoIndustryHosts()
    {
        Assert.False(IndustryStorefrontSlugMiddleware.TryMatch("www.thejewellerytrend.com", "/auto-workshop", out _, out _));
        Assert.False(IndustryStorefrontSlugMiddleware.TryMatch("www.stylenlook.com", "/garage/manager", out _, out _));
        Assert.Equal("Tires", StorefrontUcatsCatalog.Find("tires")!.Title);
        Assert.Equal("Wheels", StorefrontUcatsCatalog.Find("disky")!.Title);
        Assert.Equal("Oil & chemicals", StorefrontUcatsCatalog.Find("masla-i-avtoximiya")!.Title);
        Assert.Equal(StorefrontAspNetCanonical.AutoWorkshop, PhpSurfaceLinkMap.AspNetPrimaryHref("/en/auto-workshop"));
        Assert.Equal(StorefrontAspNetCanonical.GarageManager, PhpSurfaceLinkMap.AspNetPrimaryHref("/garage/manager"));
    }

    [Theory]
    [InlineData("www.epartscart.com", "/en/zapros-prodavczu", "seller-request")]
    [InlineData("www.electronicae.com", "/requests", "customer-requests")]
    [InlineData("www.stylenlook.com", "/en/requests/request?id=12", "customer-requests")]
    [InlineData("www.thejewellerytrend.com", "/en/shop/print?order_id=20", "customer-print")]
    [InlineData("www.taxofinca.com", "/shop/print_docs", "customer-print")]
    public void SellerRequestsAndPrintRewrite(string host, string path, string kind)
    {
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch(host, path, out var rewrite, out var matched));
        Assert.Equal(kind, matched);
        Assert.False(string.IsNullOrWhiteSpace(rewrite));
    }

    [Fact]
    public void StorefrontRequestsDoNotMapToControlPanel()
    {
        Assert.Equal(StorefrontAspNetCanonical.CustomerRequests, PhpSurfaceLinkMap.AspNetPrimaryHref("/en/requests"));
        Assert.Equal(StorefrontAspNetCanonical.CustomerRequests, PhpSurfaceLinkMap.AspNetPrimaryHref("/requests"));
        Assert.Equal(StorefrontAspNetCanonical.SellerRequest, PhpSurfaceLinkMap.AspNetPrimaryHref("/en/zapros-prodavczu"));
        Assert.Equal(StorefrontAspNetCanonical.CustomerPrint, PhpSurfaceLinkMap.AspNetPrimaryHref("/shop/print"));
        Assert.Equal("/cp/system-requests-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/requests"));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/en/requests", out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/en/zapros-prodavczu", out _));
        Assert.Equal("/storefront/seller-request-app", StorefrontAspNetCanonical.SellerRequest);
        Assert.Contains("client_vin", PhpSellerRequest.Fields.Select(f => f.Name));
        Assert.Equal(4, StorefrontUcatsCatalog.Find("shiny")!.PickerFields.Count);
    }

    [Theory]
    [InlineData("www.epartscart.com", "/en/novosti", "news")]
    [InlineData("www.electronicae.com", "/novosti/iphone-trade-in", "news")]
    [InlineData("www.stylenlook.com", "/en/shop/orders/guest", "guest-order")]
    [InlineData("www.thejewellerytrend.com", "/shop/pay", "customer-pay")]
    public void NewsGuestOrderAndPayRewrite(string host, string path, string kind)
    {
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch(host, path, out var rewrite, out var matched));
        Assert.Equal(kind, matched);
        Assert.False(string.IsNullOrWhiteSpace(rewrite));
    }

    [Fact]
    public void NewsIsIndustryScopedAndRequestsStayOffControlPanel()
    {
        Assert.True(PhpStorefrontNews.TryFind("auto_parts", "novosti/ucats-tires-wheels", out var auto));
        Assert.Contains("Tires", auto.Title, StringComparison.Ordinal);
        Assert.False(PhpStorefrontNews.TryFind("auto_parts", "novosti/gold-hallmark", out _));
        Assert.True(PhpStorefrontNews.TryFind("jewellery", "novosti/gold-hallmark", out var gold));
        Assert.Contains("Gold", gold.Title, StringComparison.Ordinal);
        Assert.Equal(StorefrontAspNetCanonical.News, PhpSurfaceLinkMap.AspNetPrimaryHref("/en/novosti"));
        Assert.Equal(StorefrontAspNetCanonical.GuestOrder, PhpSurfaceLinkMap.AspNetPrimaryHref("/shop/orders/guest"));
        Assert.Equal(StorefrontAspNetCanonical.Payment, PhpSurfaceLinkMap.AspNetPrimaryHref("/en/shop/pay"));
        Assert.Equal("/cp/system-requests-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/requests"));
        Assert.Equal("/php-reference/en/users/profile", PhpCustomerWrites.ProfileWriteHref);
    }

    [Fact]
    public void Sitemap_is_industry_scoped_and_rewritten()
    {
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch("www.epartscart.com", "/en/sitemap", out var rewrite, out var kind));
        Assert.Equal("sitemap", kind);
        Assert.StartsWith(StorefrontAspNetCanonical.Sitemap, rewrite, StringComparison.Ordinal);
        Assert.Equal(StorefrontAspNetCanonical.Sitemap, PhpSurfaceLinkMap.AspNetPrimaryHref("/en/sitemap"));
        Assert.Equal(StorefrontAspNetCanonical.Sitemap, PhpSurfaceLinkMap.AspNetPrimaryHref("/en/shop/sitemap"));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/en/sitemap", out _));

        var auto = PhpStorefrontSitemap.ForIndustry("auto_parts");
        Assert.Contains(auto, l => l.Href == StorefrontAspNetCanonical.SellerRequest);
        Assert.Contains(auto, l => l.Href == StorefrontAspNetCanonical.AutoWorkshop);
        Assert.Contains(auto, l => l.Href == StorefrontAspNetCanonical.UcatsService);
        Assert.DoesNotContain(auto, l => l.Href.Contains("gold", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(auto, l => l.Href.Contains("jewellery", StringComparison.OrdinalIgnoreCase));

        var jewellery = PhpStorefrontSitemap.ForIndustry("jewellery");
        Assert.Contains(jewellery, l => l.Href == "/bridal");
        Assert.Contains(jewellery, l => l.Href == "/gold");
        Assert.DoesNotContain(jewellery, l => l.Href == StorefrontAspNetCanonical.SellerRequest);
        Assert.DoesNotContain(jewellery, l => l.Href == StorefrontAspNetCanonical.AutoWorkshop);

        var tax = PhpStorefrontSitemap.ForIndustry("tax_advisory");
        Assert.Contains(tax, l => l.Href == "/erp");
        Assert.Contains(tax, l => l.Href == "/services/tax");
        Assert.DoesNotContain(tax, l => l.Href == StorefrontAspNetCanonical.AutoWorkshop);
    }

    [Fact]
    public void Own_catalogue_product_brochure_and_manufacturer_same_urls()
    {
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch("www.epartscart.com", "/en/shop/catalogue", out var catalog, out var catalogKind));
        Assert.Equal("own-catalog", catalogKind);
        Assert.StartsWith(StorefrontAspNetCanonical.OwnCatalog, catalog, StringComparison.Ordinal);
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch("www.electronicae.com", "/en/shop/product", out var product, out var productKind));
        Assert.Equal("catalogue-product", productKind);
        Assert.StartsWith(StorefrontAspNetCanonical.Product, product, StringComparison.Ordinal);
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch("www.epartscart.com", "/brochure", out var brochure, out var brochureKind));
        Assert.Equal("brochure", brochureKind);
        Assert.Equal(StorefrontAspNetCanonical.Brochure, brochure);
        Assert.False(IndustryStorefrontSlugMiddleware.TryMatch("www.ecomae.com", "/brochure", out _, out _));
        Assert.Equal(StorefrontAspNetCanonical.OwnCatalog, PhpSurfaceLinkMap.AspNetPrimaryHref("/en/shop/catalogue"));
        Assert.Equal(StorefrontAspNetCanonical.Product, PhpSurfaceLinkMap.AspNetPrimaryHref("/en/shop/product"));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/en/shop/catalogue", out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/en/parts/BOSCH", out _));
    }

    [Fact]
    public void Checkout_returns_and_review_write_hrefs_stay_on_php()
    {
        Assert.StartsWith("/php-reference/", PhpCustomerWrites.CheckoutHowGetWriteHref);
        Assert.StartsWith("/php-reference/", PhpCustomerWrites.CheckoutConfirmWriteHref);
        Assert.StartsWith("/php-reference/", PhpCustomerWrites.CartAddHref);
        Assert.StartsWith("/php-reference/", PhpCustomerWrites.ReturnsMessageHref);
        Assert.StartsWith("/php-reference/", PhpCustomerWrites.EvaluationWriteHref);
        Assert.Equal(StorefrontAspNetCanonical.CustomerReturns, PhpSurfaceLinkMap.AspNetPrimaryHref("/en/shop/returns_list"));
        Assert.True(PhpIndustryCmsPages.IsSlug("o-kompanii"));
        Assert.Contains("eParts Cart", PhpIndustryCmsPages.Resolve("o-kompanii", "auto_parts").Title, StringComparison.Ordinal);
        Assert.Contains("Jewellery", PhpIndustryCmsPages.Resolve("o-kompanii", "jewellery").Title, StringComparison.Ordinal);
        Assert.True(PhpIndustryCmsPages.IsSlug("chastye-voprosy"));
        Assert.Contains("VIN", PhpIndustryCmsPages.Resolve("chastye-voprosy", "auto_parts").Lead, StringComparison.Ordinal);
        Assert.DoesNotContain("gold", PhpIndustryCmsPages.Resolve("chastye-voprosy", "auto_parts").Lead, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Pickup_offices_are_industry_scoped()
    {
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch("www.epartscart.com", "/en/ofisy", out var rewrite, out var kind));
        Assert.Equal("offices", kind);
        Assert.StartsWith(StorefrontAspNetCanonical.Offices, rewrite, StringComparison.Ordinal);
        Assert.Equal(StorefrontAspNetCanonical.Offices, PhpSurfaceLinkMap.AspNetPrimaryHref("/en/shop/offices"));
        Assert.Contains(PhpStorefrontOffices.ForIndustry("auto_parts"), o => o.Name.Contains("warehouse", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(PhpStorefrontOffices.ForIndustry("auto_parts"), o => o.Name.Contains("boutique", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(PhpStorefrontOffices.ForIndustry("jewellery"), o => o.Name.Contains("boutique", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(PhpStorefrontOffices.ForIndustry("jewellery"), o => o.Name.Contains("eParts", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Special_search_and_ai_expert_stay_on_auto_parts()
    {
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch("www.epartscart.com", "/en/tormoznye-kolodki", out var search, out var searchKind));
        Assert.Equal("special-search", searchKind);
        Assert.Contains("alias=tormoznye-kolodki", search, StringComparison.Ordinal);
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch("www.epartscart.com", "/ai-parts-expert", out var expert, out var expertKind));
        Assert.Equal("ai-parts-expert", expertKind);
        Assert.Equal(StorefrontAspNetCanonical.AiPartsExpert, expert);
        Assert.False(IndustryStorefrontSlugMiddleware.TryMatch("www.thejewellerytrend.com", "/tormoznye-kolodki", out _, out _));
        Assert.False(IndustryStorefrontSlugMiddleware.TryMatch("www.stylenlook.com", "/ai-parts-expert", out _, out _));
        Assert.True(PhpSpecialSearches.TryFind("filtry-maslyanye", out var oil));
        Assert.Equal("Oil filters", oil.Title);
        Assert.Equal(StorefrontAspNetCanonical.SpecialSearch + "?alias=tormoznye-kolodki", PhpSurfaceLinkMap.AspNetPrimaryHref("/en/tormoznye-kolodki"));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/en/tormoznye-kolodki", out _));
        Assert.Equal("/storefront/account-summary-app", StorefrontAspNetCanonical.Balance);
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/en/users/account", out _));
        Assert.True(PhpProductReviews.ForProduct(1).Count > 0);
        Assert.DoesNotContain(PhpStorefrontSitemap.ForIndustry("jewellery"), l => l.Href.Contains("tormoznye", StringComparison.Ordinal));
        Assert.Contains(PhpStorefrontSitemap.ForIndustry("auto_parts"), l => l.Href == "/tormoznye-kolodki");
    }

    [Fact]
    public void Own_catalog_vin_and_how_to_order_are_host_gated()
    {
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch("www.epartscart.com", "/en/masla", out var catalog, out var catalogKind));
        Assert.Equal("own-catalog-slug", catalogKind);
        Assert.Contains("url=masla", catalog, StringComparison.Ordinal);
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch("www.epartscart.com", "/vin", out var vin, out var vinKind));
        Assert.Equal("laximo-vin", vinKind);
        Assert.Equal(StorefrontAspNetCanonical.LaximoVin, vin);
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch("www.epartscart.com", "/katalog-laximo", out var laximo, out var laximoKind));
        Assert.Equal("laximo-vin", laximoKind);
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch("www.epartscart.com", "/vin-zapros", out var zapros, out var zaprosKind));
        Assert.Equal("seller-request", zaprosKind);
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch("www.epartscart.com", "/en/shop/ucats/shiny", out var ucats, out var ucatsKind));
        Assert.Equal("ucats", ucatsKind);
        Assert.Contains("/shiny", ucats, StringComparison.Ordinal);
        Assert.False(IndustryStorefrontSlugMiddleware.TryMatch("www.thejewellerytrend.com", "/masla", out _, out _));
        Assert.False(IndustryStorefrontSlugMiddleware.TryMatch("www.stylenlook.com", "/vin", out _, out _));
        Assert.False(IndustryStorefrontSlugMiddleware.TryMatch("www.thejewellerytrend.com", "/shop/ucats/shiny", out _, out _));
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch("www.electronicae.com", "/kak-zakazat", out var how, out var howKind));
        Assert.Equal("cms:kak-zakazat", howKind);
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch("www.stylenlook.com", "/dostavka", out var delivery, out var deliveryKind));
        Assert.Equal("cms:dostavka", deliveryKind);
        Assert.True(PhpIndustryCmsPages.IsSlug("garantii"));
        Assert.True(PhpIndustryCmsPages.IsSlug("o-nas"));
        Assert.Contains("article", PhpIndustryCmsPages.Resolve("kak-zakazat", "auto_parts").Lead, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VIN", PhpIndustryCmsPages.Resolve("kak-zakazat", "jewellery").Lead, StringComparison.Ordinal);
        Assert.Equal(2, PhpCustomerReturns.SampleForAccount().Count);
        Assert.Contains("user_id", LegacySurfaceDashboardSql.SelectCustomerReturns, StringComparison.Ordinal);
        Assert.Contains("user_id", LegacySurfaceDashboardSql.SelectCustomerVinRequests, StringComparison.Ordinal);
        Assert.Contains("shop_docpart_garage_notepad", LegacySurfaceDashboardSql.SelectCustomerGarageNotepad, StringComparison.Ordinal);
        Assert.Contains("ListStorefrontReturnsAsync", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontReturnsApp.razor")), StringComparison.Ordinal);
        Assert.Contains("ListStorefrontCustomerRequestsAsync", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontCustomerRequestsApp.razor")), StringComparison.Ordinal);
        Assert.Contains("ListStorefrontGarageNotepadAsync", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontGarageApp.razor")), StringComparison.Ordinal);
        Assert.Contains("_lines", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontCheckoutApp.razor")), StringComparison.Ordinal);
        Assert.True(PhpOwnCatalogSlugs.IsAlias("tormoznaya-sistema"));
        Assert.True(PhpOwnCatalogSlugs.IsAlias("podshipniki"));
        Assert.False(PhpOwnCatalogSlugs.IsAlias("gold"));
        Assert.True(PhpSpecialSearches.IsAlias("generator"));
        Assert.False(PhpSpecialSearches.IsAlias("gold"));
        Assert.Equal(StorefrontAspNetCanonical.SellerRequest, PhpSurfaceLinkMap.AspNetPrimaryHref("/vin-zapros"));
        var editform = PhpSurfaceLinkMap.AspNetPrimaryHref("/users/editform");
        Assert.True(
            editform.Equals(StorefrontAspNetCanonical.Profile, StringComparison.Ordinal)
            || editform.EndsWith("/users/profile", StringComparison.Ordinal));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/en/katalog-laximo", out _));
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch("www.epartscart.com", "/users/logout", out var logout, out var logoutKind));
        Assert.Equal("logout", logoutKind);
        Assert.Equal("/storefront/logout", logout);
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch("www.thejewellerytrend.com", "/en/users/logout", out _, out var jewelleryLogout));
        Assert.Equal("logout", jewelleryLogout);
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch("www.electronicae.com", "/en/shop/prices-download", out var prices, out var pricesKind));
        Assert.Equal("prices-download", pricesKind);
        Assert.Equal(StorefrontAspNetCanonical.PricesDownload, prices);
        Assert.True(IndustryStorefrontSlugMiddleware.TryMatch("www.stylenlook.com", "/users/prices", out _, out var fashionPrices));
        Assert.Equal("prices-download", fashionPrices);
        Assert.True(PhpCustomerPrices.IsPublishedHref(PhpCustomerPrices.FileHref(2)));
        Assert.Equal("/content/files/Documents/prices_tmp/prices_2.csv", PhpCustomerPrices.FileHref(2));
        Assert.Equal(string.Empty, PhpCustomerPrices.FileHref(0));
        Assert.Equal(StorefrontAspNetCanonical.PricesDownload, PhpSurfaceLinkMap.AspNetPrimaryHref("/en/shop/prices-download"));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/en/shop/prices-download", out _));
        Assert.Contains("user_id", LegacySurfaceDashboardSql.SelectCustomerOrderItems, StringComparison.Ordinal);
        Assert.Contains("user_id", LegacySurfaceDashboardSql.SelectCustomerOrderMessages, StringComparison.Ordinal);
        Assert.Contains("shop_orders_messages", LegacySurfaceDashboardSql.SelectCustomerOrderMessages, StringComparison.Ordinal);
        Assert.Contains("users_groups_bind", LegacySurfaceDashboardSql.SelectCustomerPriceGroup, StringComparison.Ordinal);
        Assert.Contains("`user_id` = 0", LegacySurfaceDashboardSql.SelectGuestOrder, StringComparison.Ordinal);
        Assert.Contains("GetStorefrontGuestOrderAsync", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontGuestOrderApp.razor")), StringComparison.Ordinal);
        Assert.Contains("PhpCustomerWrites.GuestOrderWriteHref", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontGuestOrderApp.razor")), StringComparison.Ordinal);
        Assert.Contains("BuildCpOfficesDigestAsync", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontOfficesApp.razor")), StringComparison.Ordinal);
        Assert.Contains("LookupVinAsync", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontVinApp.razor")), StringComparison.Ordinal);
        Assert.DoesNotContain("@page \"/en/katalog-laximo\"", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontVinApp.razor")), StringComparison.Ordinal);
        Assert.Contains("ListStorefrontGenuineBrandsAsync", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontAvailableBrandsApp.razor")), StringComparison.Ordinal);
        Assert.StartsWith("/php-reference/", PhpCustomerWrites.GuestOrderWriteHref);
    }

    [Fact]
    public void DedicatedIndustryAppsExist()
    {
        var catalog = Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontIndustryCatalogApp.razor");
        var cms = Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontIndustryCmsPageApp.razor");
        Assert.Contains("PhpIndustryStorefrontCatalog", File.ReadAllText(catalog), StringComparison.Ordinal);
        Assert.Contains("PhpIndustryCmsPages", File.ReadAllText(cms), StringComparison.Ordinal);
        Assert.Contains("IndustryStorefrontSlugMiddleware", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Program.cs")), StringComparison.Ordinal);
        var orders = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontOrdersApp.razor"));
        Assert.Contains("ListStorefrontOrderMessagesAsync", orders, StringComparison.Ordinal);
        Assert.Contains("PhpCustomerWrites.OrderMessageHref", orders, StringComparison.Ordinal);
        var register = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontRegisterApp.razor"));
        Assert.Contains("@page \"/en/users/regform\"", register, StringComparison.Ordinal);
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/en/users/regform", out _));
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
