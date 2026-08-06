using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontFrontendParityWaveTests
{
    [Fact]
    public void Chrome_MenuBox_PointsToQuotesWishlistCompare()
    {
        var chrome = Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor");
        Assert.Contains("StorefrontSurfaceLinks.Compare", chrome, StringComparison.Ordinal);
        Assert.Contains("StorefrontSurfaceLinks.Wishlist", chrome, StringComparison.Ordinal);
        Assert.Contains("StorefrontSurfaceLinks.Quotes", chrome, StringComparison.Ordinal);
        Assert.Contains("StorefrontSurfaceLinks.Balance", chrome, StringComparison.Ordinal);
        Assert.DoesNotContain("title=\"Compare\" href=\"@StorefrontSurfaceLinks.PartSearch\"", chrome, StringComparison.Ordinal);
        Assert.DoesNotContain("title=\"Bookmarks\" href=\"@StorefrontSurfaceLinks.PartSearch\"", chrome, StringComparison.Ordinal);
        Assert.DoesNotContain("title=\"Quotes\" href=\"@StorefrontSurfaceLinks.Orders\"", chrome, StringComparison.Ordinal);
    }

    [Fact]
    public void AspNetCanonical_MapsVinVehicleQuotesWishlistProductApps()
    {
        Assert.Equal("/storefront/vin-app", StorefrontAspNetCanonical.LaximoVin);
        Assert.Equal("/storefront/vehicle-catalog-app", StorefrontAspNetCanonical.VehicleCatalog);
        Assert.Equal("/storefront/quotes-app", StorefrontAspNetCanonical.Quotes);
        Assert.Equal("/storefront/wishlist-app", StorefrontAspNetCanonical.Wishlist);
        Assert.Equal("/storefront/compare-app", StorefrontAspNetCanonical.Compare);
        Assert.Equal("/storefront/product-app", StorefrontAspNetCanonical.Product);
    }

    [Fact]
    public void PhpCanonical_HasQuotesWishlistCompareBalance()
    {
        Assert.Equal("/en/shop/quotes", StorefrontPhpCanonical.Quotes);
        Assert.Equal("/en/shop/zakladki", StorefrontPhpCanonical.Wishlist);
        Assert.Equal("/en/shop/sravneniya", StorefrontPhpCanonical.Compare);
        Assert.Equal("/en/shop/balans", StorefrontPhpCanonical.Balance);
    }

    [Fact]
    public void WaveApps_ExistOnDisk()
    {
        Assert.True(File.Exists(RepoPath("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontVinApp.razor")));
        Assert.True(File.Exists(RepoPath("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontVehicleCatalogApp.razor")));
        Assert.True(File.Exists(RepoPath("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontQuotesApp.razor")));
        Assert.True(File.Exists(RepoPath("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontWishlistApp.razor")));
        Assert.True(File.Exists(RepoPath("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontCompareApp.razor")));
        Assert.True(File.Exists(RepoPath("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontProductApp.razor")));
    }

    [Fact]
    public void SearchApp_HasActionsColumnAndPhpSupplierDeepLink()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor");
        Assert.Contains("<th>Actions</th>", text, StringComparison.Ordinal);
        Assert.Contains("PhpPartSearchHref", text, StringComparison.Ordinal);
        Assert.Contains("ajax_getProductsOfBunch", text, StringComparison.Ordinal);
        Assert.Contains("StorefrontSurfaceLinks.ForVin", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CartApp_WiresDryRunQtyCheckDelete()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontCartApp.razor");
        Assert.Contains("/storefront/cart/change-count-need", text, StringComparison.Ordinal);
        Assert.Contains("/storefront/cart/check-for-order", text, StringComparison.Ordinal);
        Assert.Contains("/storefront/cart/delete", text, StringComparison.Ordinal);
        Assert.Contains("StorefrontPhpCanonical.Cart", text, StringComparison.Ordinal);
    }

    [Fact]
    public void VehicleCatalogApp_HostsPhpWidget()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontVehicleCatalogApp.razor");
        Assert.Contains("PhpHomeWidgetHtml.VehicleCatalog()", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/en/vehicle-catalog\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void VinApp_PassesIdentStringToPhpLaximo()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontVinApp.razor");
        Assert.Contains("identString=", text, StringComparison.Ordinal);
        Assert.Contains("StorefrontPhpCanonical.LaximoVin", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/en/katalog-laximo\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void QuoteAndProductSql_AreCustomerScoped()
    {
        Assert.Contains("`user_id` = @userId", LegacySurfaceDashboardSql.SelectStorefrontCustomerQuotes, StringComparison.Ordinal);
        Assert.Contains("product_object_json", LegacySurfaceDashboardSql.SelectStorefrontCustomerQuoteItems, StringComparison.Ordinal);
        Assert.Contains("shop_catalogue_products", LegacySurfaceDashboardSql.SelectStorefrontProductById, StringComparison.Ordinal);
        Assert.Contains("{IDS}", LegacySurfaceDashboardSql.SelectStorefrontProductsByIds, StringComparison.Ordinal);
    }

    [Fact]
    public void GapBoard_ReflectsFrontendWave()
    {
        var json = Read("docs/migration/evidence/storefront/epartscart-php-aspnet-gap-board.json");
        Assert.Contains("vin-app", json, StringComparison.Ordinal);
        Assert.Contains("vehicle-catalog-app", json, StringComparison.Ordinal);
        Assert.Contains("quotes-app", json, StringComparison.Ordinal);
        Assert.Contains("wishlist-app", json, StringComparison.Ordinal);
        Assert.Contains("product-app", json, StringComparison.Ordinal);
        Assert.Contains("Actions column", json, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string relative)
        => File.ReadAllText(RepoPath(relative));

    private static string RepoPath(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate) || Directory.Exists(Path.Combine(dir.FullName, "aspnet")))
            {
                var full = Path.Combine(dir.FullName, relative);
                if (File.Exists(full))
                {
                    return full;
                }
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found for " + relative);
    }
}
