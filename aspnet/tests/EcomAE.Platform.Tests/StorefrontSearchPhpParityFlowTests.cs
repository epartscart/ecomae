using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards storefront search-app PHP part_search brand→warehouse→cross flow parity.
/// </summary>
public sealed class StorefrontSearchPhpParityFlowTests
{
    [Fact]
    public void SearchApp_HasBrendQueryBrandPickerAndCrossRefs()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));

        Assert.Contains("[SupplyParameterFromQuery(Name = \"brend\")]", text, StringComparison.Ordinal);
        Assert.Contains("ListStorefrontArticleBrandsAsync", text, StringComparison.Ordinal);
        Assert.Contains("epc-sf-brand-picker", text, StringComparison.Ordinal);
        Assert.Contains("SearchStorefrontPartsAsync(_articleInput, _brandInput", text, StringComparison.Ordinal);
        Assert.Contains("ListStorefrontCrossRefsAsync", text, StringComparison.Ordinal);
        Assert.Contains("epc-sf-cross-refs", text, StringComparison.Ordinal);
        Assert.Contains("epc-sf-cross-stock", text, StringComparison.Ordinal);
        Assert.Contains("&brend=", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacySql_HasWarehouseBrandsCrossPairsAndArticleExprPartSearch()
    {
        Assert.Contains("shop_docpart_prices_data", LegacySurfaceDashboardSql.SelectStorefrontPartSearch, StringComparison.Ordinal);
        Assert.Contains("{ARTICLE_MATCH}", LegacySurfaceDashboardSql.SelectStorefrontPartSearch, StringComparison.Ordinal);
        Assert.Contains("@brand", LegacySurfaceDashboardSql.SelectStorefrontPartSearch, StringComparison.Ordinal);
        Assert.Contains("shop_docpart_prices_data", LegacySurfaceDashboardSql.SelectStorefrontArticleWarehouseBrands, StringComparison.Ordinal);
        Assert.Contains("{ARTICLE_MATCH}", LegacySurfaceDashboardSql.SelectStorefrontArticleWarehouseBrands, StringComparison.Ordinal);
        Assert.Contains("shop_docpart_articles_analogs_list", LegacySurfaceDashboardSql.SelectStorefrontArticleCrossPairs, StringComparison.Ordinal);
        Assert.Contains("{CROSS_MATCH}", LegacySurfaceDashboardSql.SelectStorefrontArticleCrossPairs, StringComparison.Ordinal);
        Assert.DoesNotContain("price`, 0) > 0", LegacySurfaceDashboardSql.SelectStorefrontArticleWarehouseBrands, StringComparison.Ordinal);
    }

    [Fact]
    public void StorefrontModule_AllowsGuestSearchAndSearchBrands()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs"));
        Assert.Contains("EcomAeRoutes.StorefrontSearch", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.StorefrontSearchBrands", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Customer session required for storefront search digest.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Customer session required for storefront search brands digest.", text, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_article_brands", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ReporterInterface_HasBrandWarehouseCrossApis()
    {
        var path = FindRepoFile("aspnet/src/EcomAE.Platform/Migration/ISurfaceDashboardSummaryReporter.cs");
        var text = File.ReadAllText(path);

        Assert.Contains("ListStorefrontArticleBrandsAsync", text, StringComparison.Ordinal);
        Assert.Contains("ListStorefrontCrossRefsAsync", text, StringComparison.Ordinal);
        Assert.Contains("SearchStorefrontPartsAsync(string article, string? brand, int limit", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ReporterImplementation_HasBrandWarehouseCrossApis()
    {
        var path = FindRepoFile("aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs");
        var text = File.ReadAllText(path);

        Assert.Contains("ListStorefrontArticleBrandsAsync", text, StringComparison.Ordinal);
        Assert.Contains("ListStorefrontCrossRefsAsync", text, StringComparison.Ordinal);
        Assert.Contains("SelectStorefrontArticleWarehouseBrands", text, StringComparison.Ordinal);
        Assert.Contains("SelectStorefrontArticleCrossPairs", text, StringComparison.Ordinal);
    }

    [Fact]
    public void StorefrontModuleMapsSearchBrandsRoute()
    {
        Assert.Equal("/storefront/search-brands", EcomAeRoutes.StorefrontSearchBrands);

        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs"));
        Assert.Contains("EcomAeRoutes.StorefrontSearchBrands", text, StringComparison.Ordinal);
        Assert.Contains("ListStorefrontArticleBrandsAsync", text, StringComparison.Ordinal);
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
