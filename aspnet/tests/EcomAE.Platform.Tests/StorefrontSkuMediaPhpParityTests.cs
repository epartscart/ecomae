using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards PHP CHPU Spec + Photos (sku_media) parity on ASP.NET.
/// </summary>
public sealed class StorefrontSkuMediaPhpParityTests
{
    [Fact]
    public void Routes_ExposeSkuMediaAndProductImage()
    {
        Assert.Equal("/storefront/sku-media", EcomAeRoutes.StorefrontSkuMedia);
        Assert.Equal("/storefront/product-image", EcomAeRoutes.StorefrontProductImage);
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs"));
        Assert.Contains("EcomAeRoutes.StorefrontSkuMedia", module, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.StorefrontProductImage", module, StringComparison.Ordinal);
        Assert.Contains("IStorefrontSkuMediaService", module, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchApp_RendersSpecAndPhotosChrome()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        Assert.Contains("epc-spec-check-btn", text, StringComparison.Ordinal);
        Assert.Contains("epc-spec-panel", text, StringComparison.Ordinal);
        Assert.Contains("epc-sku-media-part-page", text, StringComparison.Ordinal);
        Assert.Contains("epc_sku_media.css?v=20260812-fitment-sku", text, StringComparison.Ordinal);
        Assert.Contains("epc_warehouse_search_parity.js?v=20260812-fitment-sku", text, StringComparison.Ordinal);
        Assert.Contains("IStorefrontSkuMediaService", text, StringComparison.Ordinal);
        Assert.Contains("ApplySkuMedia", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ParityJs_WiresSpecSplashLightboxAndSkuMediaLookup()
    {
        var text = File.ReadAllText(FindRepoFile("content/general_pages/epc_warehouse_search_parity.js"));
        Assert.Contains("/storefront/sku-media?", text, StringComparison.Ordinal);
        Assert.Contains("window.epcOpenSpecSplash", text, StringComparison.Ordinal);
        Assert.Contains("window.epcCloseSpecSplash", text, StringComparison.Ordinal);
        Assert.Contains("window.epcOpenImageLightbox", text, StringComparison.Ordinal);
        Assert.Contains("window.epcFetchSkuMediaLookup", text, StringComparison.Ordinal);
        Assert.Contains("epc-spec-check-btn", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Sql_ResolvesSkuProfileByBrandArticle()
    {
        var sql = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Migration/LegacySurfaceDashboardSql.cs"));
        Assert.Contains("SelectStorefrontSkuProfileByBrandArticle", sql, StringComparison.Ordinal);
        Assert.Contains("epc_sku_profiles", sql, StringComparison.Ordinal);
        Assert.Contains("article_key", sql, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
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
