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

    [Fact]
    public void DedicatedIndustryAppsExist()
    {
        var catalog = Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontIndustryCatalogApp.razor");
        var cms = Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontIndustryCmsPageApp.razor");
        Assert.Contains("PhpIndustryStorefrontCatalog", File.ReadAllText(catalog), StringComparison.Ordinal);
        Assert.Contains("PhpIndustryCmsPages", File.ReadAllText(cms), StringComparison.Ordinal);
        Assert.Contains("IndustryStorefrontSlugMiddleware", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Program.cs")), StringComparison.Ordinal);
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
