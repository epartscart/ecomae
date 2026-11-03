using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontCountryTests
{
    [Theory]
    [InlineData("ae.epartscart.com", "ae")]
    [InlineData("qa.epartscart.com", "qa")]
    [InlineData("SA.epartscart.com", "sa")]
    [InlineData("www.epartscart.com", null)]
    [InlineData("epartscart.com", null)]
    [InlineData("de.epartscart.com", null)] // not in the GCC set — aliases still resolve, not a storefront country
    [InlineData("ae", null)]
    [InlineData(null, null)]
    public void SubdomainOf_detects_country_hosts(string? host, string? expected)
        => Assert.Equal(expected, StorefrontCountryDefaults.SubdomainOf(host));

    [Theory]
    [InlineData("ae.epartscart.com", "epartscart.com")]
    [InlineData("kw.epartscart.com", "epartscart.com")]
    [InlineData("www.epartscart.com", null)]
    [InlineData("ae", null)]
    public void BaseDomainOf_strips_country_label(string? host, string? expected)
        => Assert.Equal(expected, StorefrontCountryDefaults.BaseDomainOf(host));

    [Fact]
    public void Default_falls_back_to_default_country()
    {
        var unknown = StorefrontCountryDefaults.Default("de");
        Assert.True(unknown.IsDefault);
        Assert.Equal("AE", unknown.CountryCode);

        var sa = StorefrontCountryDefaults.Default("sa");
        Assert.Equal("SA", sa.CountryCode);
        Assert.Equal("ar", sa.LanguageCode);
        Assert.Equal("rtl", sa.Direction);
        Assert.Equal("SAR", sa.CurrencyCode);
    }

    [Fact]
    public void ConvertPrice_applies_fx_markup_and_rounding()
    {
        var c = StorefrontCountryDefaults.Default("sa") with { FxRate = 2m, PriceMarkupPercent = 10m, PriceRoundTo = 0.05m };
        // 100 * 2 * 1.10 = 220.003 → rounds to nearest 0.05 → 220.00
        Assert.Equal(220.00m, c.ConvertPrice(100m));
    }

    [Fact]
    public void Split_math_advance_is_thirty_percent_of_order_total()
    {
        var total = 1000m;
        var advance = Math.Round(total * StorefrontSplitPaymentService.DefaultAdvancePercent / 100m, 2);
        Assert.Equal(300m, advance);
    }
}
