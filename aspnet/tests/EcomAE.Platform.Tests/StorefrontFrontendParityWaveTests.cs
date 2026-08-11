using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
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
        Assert.Contains("StorefrontSurfaceLinks.BulkUpload", chrome, StringComparison.Ordinal);
        Assert.DoesNotContain("title=\"Compare\" href=\"@StorefrontSurfaceLinks.PartSearch\"", chrome, StringComparison.Ordinal);
        Assert.DoesNotContain("title=\"Bookmarks\" href=\"@StorefrontSurfaceLinks.PartSearch\"", chrome, StringComparison.Ordinal);
        Assert.DoesNotContain("title=\"Quotes\" href=\"@StorefrontSurfaceLinks.Orders\"", chrome, StringComparison.Ordinal);
    }

    [Fact]
    public void AspNetCanonical_MapsVinVehicleQuotesWishlistProductBulkApps()
    {
        Assert.Equal("/storefront/vin-app", StorefrontAspNetCanonical.LaximoVin);
        Assert.Equal("/storefront/vehicle-catalog-app", StorefrontAspNetCanonical.VehicleCatalog);
        Assert.Equal("/storefront/quotes-app", StorefrontAspNetCanonical.Quotes);
        Assert.Equal("/storefront/wishlist-app", StorefrontAspNetCanonical.Wishlist);
        Assert.Equal("/storefront/compare-app", StorefrontAspNetCanonical.Compare);
        Assert.Equal("/storefront/product-app", StorefrontAspNetCanonical.Product);
        Assert.Equal("/storefront/bulk-upload-app", StorefrontAspNetCanonical.BulkUpload);
    }

    [Fact]
    public void PhpCanonical_HasQuotesWishlistCompareBalanceBulk()
    {
        Assert.Equal("/en/shop/quotes", StorefrontPhpCanonical.Quotes);
        Assert.Equal("/en/shop/zakladki", StorefrontPhpCanonical.Wishlist);
        Assert.Equal("/en/shop/sravneniya", StorefrontPhpCanonical.Compare);
        Assert.Equal("/en/shop/balans", StorefrontPhpCanonical.Balance);
        Assert.Equal("/en/shop/bulk-upload", StorefrontPhpCanonical.BulkUpload);
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
        Assert.True(File.Exists(RepoPath("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontBulkUploadApp.razor")));
    }

    [Fact]
    public void SearchApp_HasGenuineSplitAndProgressivePoll()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor");
        Assert.Contains("<th>Actions</th>", text, StringComparison.Ordinal);
        Assert.Contains("Genuine (OE)", text, StringComparison.Ordinal);
        Assert.Contains("Aftermarket", text, StringComparison.Ordinal);
        Assert.Contains("epc-part-type-row--genuine", text, StringComparison.Ordinal);
        Assert.Contains("/storefront/products-of-bunch", text, StringComparison.Ordinal);
        Assert.Contains("/storefront/search-bunches", text, StringComparison.Ordinal);
        Assert.Contains("ListStorefrontGenuineBrandsAsync", text, StringComparison.Ordinal);
        Assert.Contains("ajax_getProductsOfBunch", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductApp_RendersMediaAndSpecs()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontProductApp.razor");
        Assert.Contains("epc-sf-pd-gallery", text, StringComparison.Ordinal);
        Assert.Contains("Specifications", text, StringComparison.Ordinal);
        Assert.Contains("_product.Images", text, StringComparison.Ordinal);
        Assert.Contains("_product.Specs", text, StringComparison.Ordinal);
        Assert.Contains("epc_sku_media", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BulkUploadApp_HostsClassicProcessAndHistory()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontBulkUploadApp.razor");
        Assert.Contains("@page \"/storefront/bulk-upload-app\"", text, StringComparison.Ordinal);
        Assert.Contains("ListStorefrontBulkUploadHistoryAsync", text, StringComparison.Ordinal);
        Assert.Contains("ajax_process", text, StringComparison.Ordinal);
        Assert.Contains("/php-reference", text, StringComparison.Ordinal);
        Assert.Contains("StorefrontPhpCanonical.BulkUpload", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Routes_ExposeBunchPollGenuineBulkDigests()
    {
        Assert.Equal("/storefront/genuine-brands", EcomAeRoutes.StorefrontGenuineBrands);
        Assert.Equal("/storefront/search-bunches", EcomAeRoutes.StorefrontSearchBunches);
        Assert.Equal("/storefront/products-of-bunch", EcomAeRoutes.StorefrontProductsOfBunch);
        Assert.Equal("/storefront/bulk-upload-app", EcomAeRoutes.StorefrontBulkUploadApp);
        Assert.Equal("/storefront/bulk-upload/history", EcomAeRoutes.StorefrontBulkUploadHistory);
    }

    [Fact]
    public void Sql_CoversMediaSpecsGenuineBunchesBulkHistory()
    {
        Assert.Contains("epc_sku_profiles", LegacySurfaceDashboardSql.SelectStorefrontProductById, StringComparison.Ordinal);
        Assert.Contains("shop_products_images", LegacySurfaceDashboardSql.SelectStorefrontProductImages, StringComparison.Ordinal);
        Assert.Contains("epc_sku_photos", LegacySurfaceDashboardSql.SelectStorefrontSkuPhotos, StringComparison.Ordinal);
        Assert.Contains("epc_sku_spec_rows", LegacySurfaceDashboardSql.SelectStorefrontSkuSpecs, StringComparison.Ordinal);
        Assert.Contains("epc_umapi_manufacturers", LegacySurfaceDashboardSql.SelectStorefrontGenuineManufacturerNames, StringComparison.Ordinal);
        Assert.Contains("shop_offices_storages_map", LegacySurfaceDashboardSql.SelectStorefrontOfficeStorageBunches, StringComparison.Ordinal);
        Assert.Contains("epc_bulk_upload_history", LegacySurfaceDashboardSql.SelectStorefrontBulkUploadHistory, StringComparison.Ordinal);
    }

    [Fact]
    public void Bridge_SupportsProductsOfBunchProxy()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Migration/PhpWarehouseSearchBridge.cs");
        Assert.Contains("TryLoadProductsOfBunchAsync", text, StringComparison.Ordinal);
        Assert.Contains("ajax_getProductsOfBunch.php", text, StringComparison.Ordinal);
        Assert.Contains("ForwardBrowserCookies", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CartApp_WiresDryRunQtyCheckDelete()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontCartApp.razor");
        Assert.Contains("/storefront/cart/change-count-need", text, StringComparison.Ordinal);
        Assert.Contains("/storefront/cart/check-for-order", text, StringComparison.Ordinal);
        Assert.Contains("/storefront/cart/delete", text, StringComparison.Ordinal);
        Assert.Contains("StorefrontPhpCanonical.Cart", text, StringComparison.Ordinal);
        Assert.Contains("/php-reference", text, StringComparison.Ordinal);
        Assert.Contains("Compare PHP reference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"@StorefrontPhpCanonical.Cart\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void VehicleCatalogApp_HostsPhpWidget()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontVehicleCatalogApp.razor");
        Assert.Contains("PhpHomeWidgetHtml.VehicleCatalog()", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/en/vehicle-catalog\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void VinApp_EmbedsPhpReferenceLaximoNotProductEn()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontVinApp.razor");
        Assert.Contains("identString=", text, StringComparison.Ordinal);
        Assert.Contains("StorefrontPhpCanonical.LaximoVin", text, StringComparison.Ordinal);
        Assert.Contains("/php-reference", text, StringComparison.Ordinal);
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
    public void GapBoard_ReflectsAspNetProductPrimary()
    {
        var json = Read("docs/migration/evidence/storefront/epartscart-php-aspnet-gap-board.json");
        Assert.Contains("\"status\": \"complete\"", json, StringComparison.Ordinal);
        Assert.Contains("\"productBasedOn\": \"aspnet-core\"", json, StringComparison.Ordinal);
        Assert.Contains("preferAspNetStorefrontApps", json, StringComparison.Ordinal);
        Assert.Contains("phpSourceDeletionAllowed", json, StringComparison.Ordinal);
        Assert.Contains("bulk-upload-app", json, StringComparison.Ordinal);
        Assert.Contains("php-runtime-dependency-board.json", json, StringComparison.Ordinal);
        Assert.Contains("cursor/aspnet-primary-no-php-product-7b3b", json, StringComparison.Ordinal);
    }

    [Fact]
    public void PhpRuntimeDependencyBoard_BlocksDeletionUntilPortsDone()
    {
        var json = Read("docs/migration/evidence/storefront/php-runtime-dependency-board.json");
        Assert.Contains("\"productBasedOn\": \"aspnet-core\"", json, StringComparison.Ordinal);
        Assert.Contains("\"phpSourceDeletionAllowed\": false", json, StringComparison.Ordinal);
        Assert.Contains("\"readyForPhpRemoval\": false", json, StringComparison.Ordinal);
        Assert.Contains("aspnet-warehouse", json, StringComparison.Ordinal);
        Assert.Contains("ReadyToRemovePhp", json, StringComparison.Ordinal);
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
