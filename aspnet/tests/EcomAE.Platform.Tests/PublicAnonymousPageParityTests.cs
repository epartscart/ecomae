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
}
