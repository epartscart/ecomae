using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards storefront search-app PHP part_search brand→warehouse→cross flow parity.
/// Canonical ASP.NET result URL matches PHP CHPU: <c>/en/parts/{BRAND}/{ARTICLE}</c>.
/// Query <c>/storefront/search-app?article=&amp;brand=</c> remaps into that CHPU (same digest).
/// PHP legacy <c>brend</c> still accepted on the query entry.
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
        Assert.Contains("@page \"/en/parts/{PathBrand}/{PathArticle}\"", text, StringComparison.Ordinal);
        Assert.Contains("ListStorefrontArticleBrandsAsync", text, StringComparison.Ordinal);
        Assert.Contains("epc-brand-picker-table", text, StringComparison.Ordinal);
        Assert.Contains("epc-brand-picker-mode", text, StringComparison.Ordinal);
        // SSR-seed local warehouse rows (≤350ms) so first HTML already shows offers (faster than PHP AJAX-only).
        Assert.Contains("ProbeStorefrontPartStockAsync", text, StringComparison.Ordinal);
        Assert.Contains("ssr-seed-fast-path", text, StringComparison.Ordinal);
        Assert.Contains("SSR-seed local warehouse rows", text, StringComparison.Ordinal);
        Assert.Contains("runChpuPriceSearch", text, StringComparison.Ordinal);
        Assert.Contains("Promise.all([pricePromise, crossPromise])", text, StringComparison.Ordinal);
        // First paint: protocol-3 poll fires immediately (no search-bunches RTT before rows).
        Assert.Contains("Immediate protocol-3 poll", text, StringComparison.Ordinal);
        Assert.Contains("AbortSignal.timeout(3000)", text, StringComparison.Ordinal);
        Assert.Contains("loadGenuineBrandsBackground", text, StringComparison.Ordinal);
        Assert.Contains("/storefront/cross-search?", text, StringComparison.Ordinal);
        Assert.Contains("data-enhance-nav=\"false\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("await Task.WhenAll(genuineTask, stockTask)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("chain = chain.then", text, StringComparison.Ordinal);
        Assert.Contains("epc-seo-cross-refs", text, StringComparison.Ordinal);
        Assert.Contains("all_table_products", text, StringComparison.Ordinal);
        Assert.Contains("Availability", text, StringComparison.Ordinal);
        Assert.Contains("filter_div", text, StringComparison.Ordinal);
        Assert.Contains("epc-fitment-panel", text, StringComparison.Ordinal);
        Assert.Contains("epc-cross-search-btn", text, StringComparison.Ordinal);
        Assert.Contains("Add to Cart", text, StringComparison.Ordinal);
        Assert.Contains("Add to Quote", text, StringComparison.Ordinal);
        Assert.Contains("Open prices", text, StringComparison.Ordinal);
        Assert.Contains("In warehouse", text, StringComparison.Ordinal);
        Assert.Contains("info_box", text, StringComparison.Ordinal);
        Assert.Contains("epc-search-row-photo__btn--load", text, StringComparison.Ordinal);
        Assert.Contains("bread_crumbs_a", text, StringComparison.Ordinal);
        Assert.Contains("epc-ssr-warehouse-banner", text, StringComparison.Ordinal);
        Assert.Contains("data-epc-base-price", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ajax_epc_cross_search.php", text, StringComparison.Ordinal);
        Assert.Contains("loadAspNetCrossSearch", text, StringComparison.Ordinal);
        Assert.Contains("/storefront/cross-search?", text, StringComparison.Ordinal);
        Assert.Contains("epc-cross-ref-list", text, StringComparison.Ordinal);
        Assert.Contains("/en/parts/", text, StringComparison.Ordinal);
        Assert.Contains("/en/parts/brands/", text, StringComparison.Ordinal);
        Assert.Contains("include_crossbase=1", text, StringComparison.Ordinal);
        Assert.Contains("No manufacturers found for this article", text, StringComparison.Ordinal);
        Assert.Contains("warehouse offers", text, StringComparison.OrdinalIgnoreCase);
        // PHP CHPU: header search only on brand/warehouse results (no second in-page search window).
        Assert.Contains("_hideInPageSearch", text, StringComparison.Ordinal);
        Assert.Contains("epc-chpu-direct-part-search", text, StringComparison.Ordinal);
        Assert.Contains("php-chpu", text, StringComparison.Ordinal);
        // CHPU loads digests in place; query search-app remaps INTO CHPU (not the reverse).
        Assert.Contains("onChpuBrandArticle", text, StringComparison.Ordinal);
        Assert.Contains("never bounce CHPU", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LegacySql_HasWarehouseBrandsCrossPairsAndArticleExprPartSearch()
    {
        Assert.Contains("shop_docpart_prices_data", LegacySurfaceDashboardSql.SelectStorefrontPartSearch, StringComparison.Ordinal);
        Assert.Contains("{ARTICLE_MATCH}", LegacySurfaceDashboardSql.SelectStorefrontPartSearch, StringComparison.Ordinal);
        Assert.Contains("@brand", LegacySurfaceDashboardSql.SelectStorefrontPartSearch, StringComparison.Ordinal);
        Assert.Contains("time_to_exe", LegacySurfaceDashboardSql.SelectStorefrontPartSearch, StringComparison.Ordinal);
        // PHP prices_enclosure / CHPU brand query do not require price>0 or storefront_temp_disabled on WHERE.
        Assert.DoesNotContain("AND IFNULL(d.`price`, 0) > 0", LegacySurfaceDashboardSql.SelectStorefrontPartSearch, StringComparison.Ordinal);
        Assert.DoesNotContain("AND IFNULL(`price`, 0) > 0", LegacySurfaceDashboardSql.SelectStorefrontPartSearch, StringComparison.Ordinal);
        Assert.DoesNotContain("storefront_temp_disabled", LegacySurfaceDashboardSql.SelectStorefrontPartSearch, StringComparison.Ordinal);
        Assert.Contains("shop_docpart_prices_data", LegacySurfaceDashboardSql.SelectStorefrontArticleWarehouseBrands, StringComparison.Ordinal);
        Assert.Contains("{ARTICLE_MATCH}", LegacySurfaceDashboardSql.SelectStorefrontArticleWarehouseBrands, StringComparison.Ordinal);
        Assert.Contains("exist_sum", LegacySurfaceDashboardSql.SelectStorefrontArticleWarehouseBrands, StringComparison.Ordinal);
        Assert.Contains("part_name", LegacySurfaceDashboardSql.SelectStorefrontArticleWarehouseBrands, StringComparison.Ordinal);
        Assert.Contains("min_price", LegacySurfaceDashboardSql.SelectStorefrontArticleWarehouseBrands, StringComparison.Ordinal);
        Assert.Contains("HAVING SUM", LegacySurfaceDashboardSql.SelectStorefrontArticleWarehouseBrands, StringComparison.Ordinal);
        Assert.DoesNotContain("storefront_temp_disabled", LegacySurfaceDashboardSql.SelectStorefrontArticleWarehouseBrands, StringComparison.Ordinal);
        Assert.DoesNotContain("LEFT JOIN", LegacySurfaceDashboardSql.SelectStorefrontArticleWarehouseBrands, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("shop_docpart_articles_analogs_list", LegacySurfaceDashboardSql.SelectStorefrontArticleCrossPairs, StringComparison.Ordinal);
        Assert.Contains("{CROSS_MATCH}", LegacySurfaceDashboardSql.SelectStorefrontArticleCrossPairs, StringComparison.Ordinal);
        // min_price may use CASE WHEN price>0; that is not a WHERE filter excluding free rows.
        Assert.DoesNotContain("AND IFNULL(d.`price`, 0) > 0", LegacySurfaceDashboardSql.SelectStorefrontArticleWarehouseBrands, StringComparison.Ordinal);
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
    public void PhpSurfaceLinkMap_MapsPartsBrandArticleToSameUrlChpu()
    {
        var previous = StorefrontSurfaceLinks.PreferAspNetApps;
        StorefrontSurfaceLinks.PreferAspNetApps = true;
        try
        {
            var href = PhpSurfaceLinkMap.AspNetPrimaryHref("/en/parts/ROCKY/DA320");
            Assert.Equal("/en/parts/ROCKY/DA320", href);

            var brands = PhpSurfaceLinkMap.AspNetPrimaryHref("/parts/brands/DA320");
            Assert.Equal("/en/parts/brands/DA320", brands);
        }
        finally
        {
            StorefrontSurfaceLinks.PreferAspNetApps = previous;
        }
    }

    [Fact]
    public void PhpSurfaceLinkMap_DoesNotRedirectIncomingPartsChpuAwayFromBlazor()
    {
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/en/parts/TOYOTA/1310154101", out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/parts/ROCKY/DA320", out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/en/shop/part_search?article=DA320", out _));
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/en/shop/warehouse-search", out _));
    }

    [Fact]
    public void ReporterInterface_HasBrandWarehouseCrossApis()
    {
        var path = FindRepoFile("aspnet/src/EcomAE.Platform/Migration/ISurfaceDashboardSummaryReporter.cs");
        var text = File.ReadAllText(path);

        Assert.Contains("ListStorefrontArticleBrandsAsync", text, StringComparison.Ordinal);
        Assert.Contains("ListStorefrontCrossRefsAsync", text, StringComparison.Ordinal);
        Assert.Contains("SearchStorefrontPartsAsync(string article, string? brand, int limit", text, StringComparison.Ordinal);
        Assert.Contains("ProbeStorefrontPartStockAsync", text, StringComparison.Ordinal);
        Assert.Contains("ListStorefrontCrossRefsAsync(string article, string? brand, int limit", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ReporterImplementation_HasBrandWarehouseCrossApis()
    {
        var path = FindRepoFile("aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs");
        var text = File.ReadAllText(path);

        Assert.Contains("ListStorefrontArticleBrandsAsync", text, StringComparison.Ordinal);
        Assert.Contains("ListStorefrontCrossRefsAsync", text, StringComparison.Ordinal);
        Assert.Contains("ProbeStorefrontPartStockAsync", text, StringComparison.Ordinal);
        Assert.Contains("TryLoadCrossSearchAsync", text, StringComparison.Ordinal);
        Assert.Contains("ECOMAE_SSR_PHP_CROSS", text, StringComparison.Ordinal);
        Assert.Contains("SelectStorefrontArticleWarehouseBrands", text, StringComparison.Ordinal);
        Assert.Contains("SelectStorefrontArticleCrossPairs", text, StringComparison.Ordinal);
        Assert.Contains("StorefrontArticleMatchMode.ExactTrim", text, StringComparison.Ordinal);
        Assert.Contains("QueryStorefrontPartOffersCascadeAsync", text, StringComparison.Ordinal);
        Assert.Contains("QueryStorefrontPartOffersBrandedFastAsync", text, StringComparison.Ordinal);
        Assert.Contains("brandedFastPath", text, StringComparison.Ordinal);
        Assert.Contains("LoadManufacturerBrandAliasesAsync", text, StringComparison.Ordinal);
        Assert.Contains("ManufacturerMatchesBrand", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchApp_BrandArticleChpuSkipsBlockingWarehouseSsr()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));

        // Cap SSR offer seed so first paint stays in the 1–3s budget (rows visible in HTML).
        Assert.Contains("CancellationTokenSource(TimeSpan.FromMilliseconds(350))", text, StringComparison.Ordinal);
        Assert.Contains("SearchStorefrontPartsAsync(", text, StringComparison.Ordinal);
        Assert.Contains("_articleInput, _brandInput, 40, seedCts.Token", text, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ListStorefrontCrossRefsAsync(_articleInput, _brandInput, ssrCrossLimit)",
            text,
            StringComparison.Ordinal);
        Assert.DoesNotContain("ListStorefrontGenuineBrandsAsync()", text, StringComparison.Ordinal);
        Assert.Contains("ONE protocol-3", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("search-bunches is diagnostic only", text, StringComparison.Ordinal);
        Assert.Contains("Never leave \"Polling suppliers…\" visible", text, StringComparison.Ordinal);
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
    public void SearchApp_NameModeUsesCatalogueSearchString()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        Assert.Contains("ListStorefrontCatalogueProductsAsync", text, StringComparison.Ordinal);
        Assert.Contains("[SupplyParameterFromQuery(Name = \"search_string\")]", text, StringComparison.Ordinal);
        Assert.Contains("name=\"search_string\"", text, StringComparison.Ordinal);
        Assert.Contains("StorefrontSurfaceLinks.NameSearch", text, StringComparison.Ordinal);
        Assert.Contains("_mode is \"name\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@page \"/en/{alias}\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchApp_AttrModeUsesWarehouseAttrIndex()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontSearchApp.razor"));
        Assert.Contains("ListStorefrontWarehouseAttrAsync", text, StringComparison.Ordinal);
        Assert.Contains("name=\"field\"", text, StringComparison.Ordinal);
        Assert.Contains("PhpWarehouseAttrSearch.Fields", text, StringComparison.Ordinal);
        Assert.Contains("epc_price_attr_index", LegacySurfaceDashboardSql.SelectStorefrontWarehouseAttrIndex, StringComparison.Ordinal);
        Assert.Contains("@field", LegacySurfaceDashboardSql.SelectStorefrontWarehouseAttrIndex, StringComparison.Ordinal);
        Assert.Contains("@norm", LegacySurfaceDashboardSql.SelectStorefrontWarehouseAttrIndex, StringComparison.Ordinal);
    }

    [Fact]
    public void WarehouseAttr_NormalizeMatchesPhp()
    {
        Assert.Equal("all", PhpWarehouseAttrSearch.NormalizeField(null));
        Assert.Equal("engine_code", PhpWarehouseAttrSearch.NormalizeField("engine_code"));
        Assert.Equal("all", PhpWarehouseAttrSearch.NormalizeField("engine code!"));
        Assert.Equal("2JZGE", PhpWarehouseAttrSearch.NormalizeValue("2JZ-GE"));
        Assert.Equal("Engine code", PhpWarehouseAttrSearch.LabelFor("engine_code"));
    }

    [Fact]
    public void CompareApp_HasNoTenantVisiblePhpLink()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontCompareApp.razor"));
        Assert.DoesNotContain("Open PHP", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Open classic compare", text, StringComparison.Ordinal);
        Assert.DoesNotContain("StorefrontPhpCanonical.Compare", text, StringComparison.Ordinal);
        Assert.Contains("StorefrontSurfaceLinks.OwnCatalog", text, StringComparison.Ordinal);
    }

    [Fact]
    public void WishlistApp_HasGetAddToCartForm()
    {
        var text = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Pages/StorefrontWishlistApp.razor"));
        Assert.Contains("StorefrontSurfaceLinks.Cart", text, StringComparison.Ordinal);
        Assert.Contains("name=\"product_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"count_need\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ajax_add_to_basket", text, StringComparison.Ordinal);
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
