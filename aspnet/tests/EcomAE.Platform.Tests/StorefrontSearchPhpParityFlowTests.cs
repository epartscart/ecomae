using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards storefront search-app PHP part_search brand→warehouse→cross flow parity.
/// Canonical ASP.NET URL: <c>/storefront/search-app?article=…&amp;brand=…</c>
/// (PHP legacy <c>brend</c> still accepted).
/// </summary>
public sealed class StorefrontSearchPhpParityFlowTests
{
    [Fact]
    public void SearchApp_UsesBrandArticleFormatAndPhpResultShape()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));

        Assert.Contains("[SupplyParameterFromQuery(Name = \"brand\")]", text, StringComparison.Ordinal);
        Assert.Contains("[SupplyParameterFromQuery(Name = \"brend\")]", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/parts/{PathBrand}/{PathArticle}\"", text, StringComparison.Ordinal);
        Assert.Contains("ListStorefrontArticleBrandsAsync", text, StringComparison.Ordinal);
        Assert.Contains("epc-sf-brand-picker", text, StringComparison.Ordinal);
        Assert.Contains("SearchStorefrontPartsAsync(_articleInput, _brandInput", text, StringComparison.Ordinal);
        Assert.Contains("ListStorefrontCrossRefsAsync(_articleInput, _brandInput", text, StringComparison.Ordinal);
        Assert.Contains("epc-sf-cross-refs", text, StringComparison.Ordinal);
        Assert.Contains("all_table_products", text, StringComparison.Ordinal);
        Assert.Contains("Availability", text, StringComparison.Ordinal);
        Assert.Contains("&brand=", text, StringComparison.Ordinal);
        Assert.Contains("/storefront/search-app?article=", text, StringComparison.Ordinal);
        Assert.Contains("No manufacturers found for this article", text, StringComparison.Ordinal);
        Assert.Contains("warehouse offers", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LegacySql_HasWarehouseBrandsCrossPairsAndArticleExprPartSearch()
    {
        Assert.Contains("shop_docpart_prices_data", LegacySurfaceDashboardSql.SelectStorefrontPartSearch, StringComparison.Ordinal);
        Assert.Contains("{ARTICLE_MATCH}", LegacySurfaceDashboardSql.SelectStorefrontPartSearch, StringComparison.Ordinal);
        Assert.Contains("@brand", LegacySurfaceDashboardSql.SelectStorefrontPartSearch, StringComparison.Ordinal);
        Assert.Contains("time_to_exe", LegacySurfaceDashboardSql.SelectStorefrontPartSearch, StringComparison.Ordinal);
        // PHP prices_enclosure / CHPU brand query do not require price>0 or storefront_temp_disabled.
        Assert.DoesNotContain("price`, 0) > 0", LegacySurfaceDashboardSql.SelectStorefrontPartSearch, StringComparison.Ordinal);
        Assert.DoesNotContain("storefront_temp_disabled", LegacySurfaceDashboardSql.SelectStorefrontPartSearch, StringComparison.Ordinal);
        Assert.Contains("shop_docpart_prices_data", LegacySurfaceDashboardSql.SelectStorefrontArticleWarehouseBrands, StringComparison.Ordinal);
        Assert.Contains("{ARTICLE_MATCH}", LegacySurfaceDashboardSql.SelectStorefrontArticleWarehouseBrands, StringComparison.Ordinal);
        Assert.DoesNotContain("storefront_temp_disabled", LegacySurfaceDashboardSql.SelectStorefrontArticleWarehouseBrands, StringComparison.Ordinal);
        Assert.DoesNotContain("LEFT JOIN", LegacySurfaceDashboardSql.SelectStorefrontArticleWarehouseBrands, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("shop_docpart_articles_analogs_list", LegacySurfaceDashboardSql.SelectStorefrontArticleCrossPairs, StringComparison.Ordinal);
        Assert.Contains("{CROSS_MATCH}", LegacySurfaceDashboardSql.SelectStorefrontArticleCrossPairs, StringComparison.Ordinal);
        Assert.DoesNotContain("price`, 0) > 0", LegacySurfaceDashboardSql.SelectStorefrontArticleWarehouseBrands, StringComparison.Ordinal);
        Assert.Contains("UPPER(TRIM(IFNULL(d.`article`", LegacySurfaceDashboardSql.StorefrontPriceArticleExactInSql(1), StringComparison.Ordinal);
    }

    [Fact]
    public void StorefrontModule_AllowsGuestSearchAndAcceptsBrandOrBrend()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs"));
        Assert.Contains("EcomAeRoutes.StorefrontSearch", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.StorefrontSearchBrands", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Customer session required for storefront search digest.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Customer session required for storefront search brands digest.", text, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_article_brands", text, StringComparison.Ordinal);
        Assert.Contains("string? brand", text, StringComparison.Ordinal);
        Assert.Contains("string? brend", text, StringComparison.Ordinal);
        Assert.Contains("Prefer brand=", text, StringComparison.Ordinal);
    }

    [Fact]
    public void PhpSurfaceLinkMap_MapsPartsBrandArticleToSearchApp()
    {
        var previous = StorefrontSurfaceLinks.PreferAspNetApps;
        StorefrontSurfaceLinks.PreferAspNetApps = true;
        try
        {
            var href = PhpSurfaceLinkMap.AspNetPrimaryHref("/en/parts/ROCKY/DA320");
            Assert.Contains("/storefront/search-app", href, StringComparison.Ordinal);
            Assert.Contains("article=DA320", href, StringComparison.Ordinal);
            Assert.Contains("brand=ROCKY", href, StringComparison.Ordinal);

            var brands = PhpSurfaceLinkMap.AspNetPrimaryHref("/parts/brands/DA320");
            Assert.Contains("/storefront/search-app", brands, StringComparison.Ordinal);
            Assert.Contains("article=DA320", brands, StringComparison.Ordinal);
            Assert.DoesNotContain("brand=", brands, StringComparison.Ordinal);
        }
        finally
        {
            StorefrontSurfaceLinks.PreferAspNetApps = previous;
        }
    }

    [Fact]
    public void ReporterInterface_HasBrandWarehouseCrossApis()
    {
        var path = FindRepoFile("aspnet/src/EcomAE.Platform/Migration/ISurfaceDashboardSummaryReporter.cs");
        var text = File.ReadAllText(path);

        Assert.Contains("ListStorefrontArticleBrandsAsync", text, StringComparison.Ordinal);
        Assert.Contains("ListStorefrontCrossRefsAsync", text, StringComparison.Ordinal);
        Assert.Contains("SearchStorefrontPartsAsync(string article, string? brand, int limit", text, StringComparison.Ordinal);
        Assert.Contains("ListStorefrontCrossRefsAsync(string article, string? brand, int limit", text, StringComparison.Ordinal);
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
        Assert.Contains("StorefrontArticleMatchMode.ExactTrim", text, StringComparison.Ordinal);
        Assert.Contains("QueryStorefrontPartOffersCascadeAsync", text, StringComparison.Ordinal);
        Assert.Contains("LoadManufacturerBrandAliasesAsync", text, StringComparison.Ordinal);
        Assert.Contains("ManufacturerMatchesBrand", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AccountSummaryApp_AliasesAccountAppRoute()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontAccountSummaryApp.razor"));
        Assert.Contains("@page \"/storefront/account-app\"", text, StringComparison.Ordinal);
        Assert.Contains("@page \"/storefront/account-summary-app\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchApp_VinModeUsesIdentStringField()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        Assert.Contains("name=\"identString\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"mode\" value=\"vin\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void GapBoard_DocumentsEpartscartStorefrontSurfaces()
    {
        var json = File.ReadAllText(FindRepoFile(
            "docs/migration/evidence/storefront/epartscart-php-aspnet-gap-board.json"));
        Assert.Contains("\"tenant\": \"epartscart.com\"", json, StringComparison.Ordinal);
        Assert.Contains("part_search", json, StringComparison.Ordinal);
        Assert.Contains("priorityBuildOrder", json, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed", json, StringComparison.Ordinal);
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
