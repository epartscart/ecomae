using EcomAE.Platform.Presentation;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpHostContextTests
{
    [Theory]
    [InlineData("www.epartscart.com", "ePartsCart", "auto_parts")]
    [InlineData("electronicae.com", "Electronicae", "electronics")]
    [InlineData("stylenlook.com", "StyleNLook", "fashion")]
    [InlineData("thejewellerytrend.com", "The Jewellery Trend", "jewellery")]
    [InlineData("taxofinca.com", "Taxofinca", "finance")]
    [InlineData("jewellery.ecomae.com", "Jewellery & Luxury Goods", "jewellery")]
    [InlineData("agriculture.ecomae.com", "Agriculture & Farming", "agriculture")]
    [InlineData("energy.ecomae.com", "Energy & Utilities", "energy")]
    public void Resolve_MapsProductAndIndustryHosts(string host, string brandContains, string industry)
    {
        var ctx = ErpHostContext.Resolve(host);
        Assert.Contains(brandContains, ctx.BrandLabel, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(industry, ctx.IndustryCode);
        Assert.StartsWith("ERP Finance —", ctx.WorkspaceTitle, StringComparison.Ordinal);
        Assert.Equal("/php-reference/erp", ctx.PhpErpShellHref);
    }

    [Fact]
    public void SwitchCompanyHref_ReplacesCompanyQuery()
    {
        var http = new DefaultHttpContext();
        http.Request.Path = "/erp/report-center-app";
        http.Request.QueryString = new QueryString("?key=trial&company=9");
        var href = ErpHostContext.SwitchCompanyHref(http.Request, 3);
        Assert.Contains("/erp/report-center-app?", href, StringComparison.Ordinal);
        Assert.Contains("company=3", href, StringComparison.Ordinal);
        Assert.Contains("key=trial", href, StringComparison.Ordinal);
        Assert.DoesNotContain("company=9", href, StringComparison.Ordinal);
    }

    [Fact]
    public void ChromeExposesCompanyIndustryBar()
    {
        var path = Find("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor");
        var text = File.ReadAllText(path);
        Assert.Contains("PhpErpCompanyIndustryBar", text, StringComparison.Ordinal);
        Assert.Contains("ErpHostContext.Resolve", text, StringComparison.Ordinal);
        Assert.Contains("data-epc-industry=", text, StringComparison.Ordinal);
        Assert.DoesNotContain("<span>Ecom BOS</span>", text, StringComparison.Ordinal);
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
