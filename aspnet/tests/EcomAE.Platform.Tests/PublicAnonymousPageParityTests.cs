using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PublicAnonymousPageParityTests
{
    [Fact]
    public void ErpDemoKpis_MatchPhpSampleMath()
    {
        var jewellery = PublicErpDemoPage.Compute("jewellery");
        Assert.Equal("Al Noor Jewellers LLC", jewellery.Company);
        Assert.Equal(10400m, jewellery.Revenue);
        Assert.Equal(8150m, jewellery.Cogs);
        Assert.Equal(2250m, jewellery.GrossMargin);
        Assert.Equal(21.6m, jewellery.GrossMarginPct);
        Assert.Equal(5200m, jewellery.ArOutstanding);
        Assert.Equal(40700m, jewellery.StockValue);
        Assert.Equal(2, jewellery.PaidOrders);
        Assert.Equal(1, jewellery.UnpaidOrders);

        var trading = PublicErpDemoPage.Compute("trading");
        Assert.Equal(11240m, trading.Revenue);
        Assert.Equal(4500m, trading.ArOutstanding);
        Assert.Equal(58000m, trading.StockValue);

        var retail = PublicErpDemoPage.Compute("retail");
        Assert.Equal(2100m, retail.Revenue);
        Assert.Equal(1214m, retail.Cogs);
        Assert.Equal(0m, retail.ArOutstanding);
        Assert.Equal(38000m, retail.StockValue);
    }

    [Theory]
    [InlineData(null, "jewellery")]
    [InlineData("", "jewellery")]
    [InlineData("nope", "jewellery")]
    [InlineData("Retail", "retail")]
    [InlineData("manufacturing", "manufacturing")]
    public void ErpDemoIndustry_FallsBackLikeDashboard(string? raw, string expected)
        => Assert.Equal(expected, PublicErpDemoPage.NormalizeIndustry(raw));

    [Fact]
    public void ErpDemoHtml_IsSampleDashboardNotAWrite()
    {
        var html = PublicErpDemoPage.Html("jewellery");
        Assert.Contains("Live ERP demo", html, StringComparison.Ordinal);
        Assert.Contains("Al Noor Jewellers LLC", html, StringComparison.Ordinal);
        Assert.Contains("AED 10,400", html, StringComparison.Ordinal);
        Assert.Contains("demo@ecomae.com", html, StringComparison.Ordinal);
        Assert.Contains("/erp-demo?demo=1&amp;industry=trading", html, StringComparison.Ordinal);
        Assert.DoesNotContain("confirmWrites", html, StringComparison.Ordinal);
        Assert.DoesNotContain("demo_seed", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/erp-demo", true)]
    [InlineData("/erp-demo/", true)]
    [InlineData("/shop/erp-demo", true)]
    [InlineData("/en/erp-demo", true)]
    [InlineData("/en/shop/erp-demo/", true)]
    [InlineData("/erp", false)]
    [InlineData("/erp/login", false)]
    [InlineData("/shop/erp", false)]
    public void ErpDemoPath_MatchesPhpRouter(string path, bool expected)
        => Assert.Equal(expected, PublicAnonymousPages.IsErpDemoPath(path));

    [Theory]
    [InlineData("www.ecomae.com", "/shop", true)]
    [InlineData("www.ecomae.com", "/shop/", true)]
    [InlineData("ecomae.com", "/shop", true)]
    [InlineData("www.ecomae.com", "/shop/cart", false)]
    [InlineData("www.epartscart.com", "/shop", false)]
    public void MarketingShop_IsStorefrontDemoOnly(string host, string path, bool expected)
        => Assert.Equal(expected, PublicAnonymousPages.IsMarketingStorefrontDemo(host, path));

    [Theory]
    [InlineData("www.epartscart.com", "/brochure-cp", true)]
    [InlineData("www.epartscart.com", "/brochure/cp", true)]
    [InlineData("www.epartscart.com", "/en/brochure-cp", true)]
    [InlineData("www.epartscart.com", "/ru/brochure/cp/", true)]
    [InlineData("www.ecomae.com", "/brochure-cp", false)]
    [InlineData("www.epartscart.com", "/brochure", false)]
    public void TenantBrochurePath_MatchesPhpIndex(string host, string path, bool expected)
        => Assert.Equal(expected, PublicAnonymousPages.IsTenantCpBrochurePath(host, path));

    [Fact]
    public void TenantBrochure_IsEpartscartRenderNotEcomaeSnapshot()
    {
        Assert.Equal("client", TenantCpBrochure.NormalizeScope("client"));
        Assert.Equal("client", TenantCpBrochure.NormalizeScope(""));
        Assert.Equal("super", TenantCpBrochure.NormalizeScope("SUPER"));
        Assert.Equal("catalog", TenantCpBrochure.NormalizeView("catalog"));
        Assert.Equal("deck", TenantCpBrochure.NormalizeView("nope"));

        var html = TenantCpBrochure.HtmlFor("client", "deck", print: false);
        Assert.Contains("<title>eParts Cart — Full Control Panel brochure</title>", html, StringComparison.Ordinal);
        Assert.Contains("hello@epartscart.com", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<title>ECOM AE — Full Control Panel brochure</title>", html, StringComparison.Ordinal);

        var printed = TenantCpBrochure.HtmlFor("client", "deck", print: true);
        Assert.Contains("window.print()", printed, StringComparison.Ordinal);
    }

    [Fact]
    public void IndustrySitemap_ListsHubAndSubpathFromSnapshots()
    {
        var xml = PublicSeoSitemaps.IndustryUrlset();
        Assert.Contains("https://agriculture.ecomae.com/", xml, StringComparison.Ordinal);
        Assert.Contains("https://agriculture.ecomae.com/crop-farming-cultivation", xml, StringComparison.Ordinal);
        Assert.Contains("<priority>0.85</priority>", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("agriculture__", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void MarketingSitemap_ListsPlatformAndIndustryHosts()
    {
        var xml = PublicSeoSitemaps.MarketingUrlset();
        Assert.Contains("https://www.ecomae.com/platform", xml, StringComparison.Ordinal);
        Assert.Contains("https://www.ecomae.com/privacy", xml, StringComparison.Ordinal);
        Assert.Contains("https://agriculture.ecomae.com/", xml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("www.ecomae.com", "/akciya", true)]
    [InlineData("www.ecomae.com", "/en/akciya", true)]
    [InlineData("www.ecomae.com", "/ru/promotions", true)]
    [InlineData("ecomae.com", "/promotion/", true)]
    [InlineData("www.ecomae.com", "/en/terms", false)]
    [InlineData("www.ecomae.com", "/en", false)]
    [InlineData("www.ecomae.com", "/platform", false)]
    [InlineData("www.epartscart.com", "/akciya", false)]
    public void LegacyPromo_IsMarketingHostOnly(string host, string path, bool expected)
        => Assert.Equal(expected, PublicSeoSitemaps.IsLegacyPromoRedirect(host, path));

    [Fact]
    public void SitemapIndex_FollowsHostBranches()
    {
        var marketing = PublicSeoSitemaps.SitemapIndex(
            "https://www.ecomae.com",
            PublicSeoSitemaps.IndexChildren("www.ecomae.com"));
        Assert.Contains("https://www.ecomae.com/sitemap-industries.php", marketing, StringComparison.Ordinal);
        Assert.Contains("https://www.ecomae.com/sitemap-marketing.php", marketing, StringComparison.Ordinal);
        Assert.DoesNotContain("sitemap-products.php", marketing, StringComparison.Ordinal);

        var tenant = PublicSeoSitemaps.SitemapIndex(
            "https://www.epartscart.com",
            PublicSeoSitemaps.IndexChildren("www.epartscart.com"));
        Assert.Contains("https://www.epartscart.com/sitemap-products.php", tenant, StringComparison.Ordinal);
        Assert.Contains("https://www.epartscart.com/sitemap-pages.php", tenant, StringComparison.Ordinal);
        Assert.DoesNotContain("sitemap-industries.php", tenant, StringComparison.Ordinal);

        var industry = PublicSeoSitemaps.IndexChildren("agriculture.ecomae.com");
        Assert.Equal(["sitemap.xml"], industry);
        Assert.True(PublicSeoSitemaps.ShouldRedirectIndustriesSitemap("energy.ecomae.com"));
        Assert.False(PublicSeoSitemaps.ShouldRedirectIndustriesSitemap("www.ecomae.com"));
    }

    [Fact]
    public void MarketingRobots_AdvertisesIndustryAndIndexMaps()
    {
        var robots = StorefrontPublicSeo.RobotsTxt("www.ecomae.com");
        Assert.Contains("Sitemap: https://www.ecomae.com/sitemap-industries.php", robots, StringComparison.Ordinal);
        Assert.Contains("Sitemap: https://www.ecomae.com/sitemap-index.php", robots, StringComparison.Ordinal);
        Assert.DoesNotContain("Disallow: /en/parts/brands/", robots, StringComparison.Ordinal);

        var shop = StorefrontPublicSeo.RobotsTxt("www.epartscart.com");
        Assert.Contains("Sitemap: https://www.epartscart.com/sitemap-index.php", shop, StringComparison.Ordinal);
        Assert.Contains("Sitemap: https://www.epartscart.com/sitemap-products.php", shop, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductHubUrlset_KeepsHubsWhenBrandsAreMissing()
    {
        var xml = PublicSeoSitemaps.ProductHubUrlset("https://www.epartscart.com", ["bosch"]);
        Assert.Contains("https://www.epartscart.com/en/parts<", xml, StringComparison.Ordinal);
        Assert.Contains("https://www.epartscart.com/en/available-brands", xml, StringComparison.Ordinal);
        Assert.Contains("https://www.epartscart.com/en/parts/BOSCH", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("/parts/brands/", xml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/sitemap-wh-0.php", 0, true)]
    [InlineData("/sitemap-wh-79.php", 79, true)]
    [InlineData("/sitemap-wh-80.php", 80, false)]
    [InlineData("/sitemap-products.php", -1, false)]
    public void WarehouseShardPath_MatchesPhpFiles(string path, int shard, bool served)
    {
        var parsed = PublicSeoSitemaps.TryParseWarehouseShard(path, out var value);
        if (shard < 0)
        {
            Assert.False(parsed);
            return;
        }

        Assert.True(parsed);
        Assert.Equal(shard, value);
        Assert.Equal(served, PublicSeoSitemaps.IsServedWarehouseShard(value));
    }

    [Fact]
    public void WarehouseShardXml_UsesPartChpu()
    {
        var xml = PublicSeoSitemaps.WarehouseShardUrlset(
            "https://www.epartscart.com",
            [("bosch", "0 986 494 527")]);
        Assert.Contains("https://www.epartscart.com/en/parts/BOSCH/0986494527", xml, StringComparison.Ordinal);
        Assert.Contains("<priority>0.6</priority>", xml, StringComparison.Ordinal);
        Assert.Equal(
            ["sitemap-wh-0.php"],
            PublicSeoSitemaps.WarehouseShardChildren(1).ToArray());
    }

    [Fact]
    public void CatalogEntry_RewritesKnownActionsAndKeepsMissingAction()
    {
        Assert.True(PublicCatalogApiEntry.IsEntryPath("/api/v1/catalog.php"));
        Assert.True(PublicCatalogApiEntry.TryMapAction("engine_search", out var slug));
        Assert.Equal("engine-search", slug);
        Assert.False(PublicCatalogApiEntry.TryMapAction("", out _));
        Assert.False(PublicCatalogApiEntry.TryMapAction("not-a-real-action", out _));
        Assert.Contains("missing_action", PublicCatalogApiEntry.ErrorJson(
            PublicCatalogApiEntry.MissingActionCode,
            PublicCatalogApiEntry.MissingActionMessage), StringComparison.Ordinal);
    }

    [Fact]
    public void EpcApiV1_AnonymousRoutesMatchPhp()
    {
        Assert.Equal("", EpcPublicApiV1.RouteOf("/epc-api/v1"));
        Assert.Equal("health", EpcPublicApiV1.RouteOf("/epc-api/v1/health"));
        Assert.Equal("tenant/info", EpcPublicApiV1.RouteOf("/epc-api/v1/tenant/info/"));
        Assert.False(EpcPublicApiV1.IsKeyedRoute("health"));
        Assert.True(EpcPublicApiV1.IsKeyedRoute("orders"));
        Assert.Equal("read:orders", EpcPublicApiV1.RequiredScope("orders"));

        var health = EpcPublicApiV1.HealthJson(new DateTimeOffset(2026, 10, 4, 18, 0, 0, TimeSpan.Zero));
        Assert.Contains("\"service\": \"epc-api\"", health, StringComparison.Ordinal);
        Assert.Contains("/epc-api/v1/health", health, StringComparison.Ordinal);
        Assert.Contains("2026-10-04T18:00:00+00:00", health, StringComparison.Ordinal);

        var areas = EpcPublicApiV1.CapabilityAreas();
        Assert.Contains(areas, area => area.Area == "Platform & Super CP" && area.Count > 0);
        var missing = EpcPublicApiV1.ErrorJson("missing_api_key", EpcPublicApiV1.MissingKeyMessage);
        Assert.Contains("missing_api_key", missing, StringComparison.Ordinal);
        Assert.Contains("Send X-API-Key header", missing, StringComparison.Ordinal);
        Assert.NotNull(EpcPublicApiV1.OpenApiPath());
        Assert.Equal("read:erp", EpcPublicApiV1.RequiredScope("erp/dashboard-summary"));
        Assert.True(EpcPublicApiV1.IsKeyedRoute("erp/dashboard-summary"));
    }

    [Fact]
    public void EpcApiV1_DashboardSummaryJsonMatchesPhpEnvelope()
    {
        var json = EpcPublicApiV1.DashboardSummaryJson("tenant_demo", new Dictionary<string, object?>
        {
            ["date_from"] = 1759276800L,
            ["date_to"] = 1759363199L,
            ["order_count"] = 3L,
            ["revenue_ex_vat"] = 12.5m,
            ["profit_ex_vat"] = 2.25m,
            ["receivable_due_orders"] = 1m,
            ["customer_ledger_balance"] = 4.5m,
            ["payable_balance"] = 0m,
            ["cash_bank_total"] = 100.1m,
            ["vat_net_payable"] = -0.5m,
            ["vat_net_status"] = "recoverable_from_fta",
            ["kpi_tiles"] = new[] { "omit-me" },
            ["approval_queue"] = new[] { "omit-me" },
        });
        Assert.Contains("\"ok\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"tenant_site_key\": \"tenant_demo\"", json, StringComparison.Ordinal);
        Assert.Contains("\"from\": \"2025-10-01T00:00:00+00:00\"", json, StringComparison.Ordinal);
        Assert.Contains("\"to\": \"2025-10-01T23:59:59+00:00\"", json, StringComparison.Ordinal);
        Assert.Contains("\"order_count\": 3", json, StringComparison.Ordinal);
        Assert.Contains("\"revenue_ex_vat\": 12.5", json, StringComparison.Ordinal);
        Assert.Contains("\"profit_ex_vat\": 2.25", json, StringComparison.Ordinal);
        Assert.Contains("\"payable_balance\": 0", json, StringComparison.Ordinal);
        Assert.Contains("\"cash_bank_total\": 100.1", json, StringComparison.Ordinal);
        Assert.Contains("\"vat_net_payable\": -0.5", json, StringComparison.Ordinal);
        Assert.Contains("\"vat_net_status\": \"recoverable_from_fta\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("kpi_tiles", json, StringComparison.Ordinal);
        Assert.DoesNotContain("approval_queue", json, StringComparison.Ordinal);
        Assert.DoesNotContain("omit-me", json, StringComparison.Ordinal);
    }
}
