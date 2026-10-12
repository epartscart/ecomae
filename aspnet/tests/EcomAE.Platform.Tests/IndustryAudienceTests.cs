using EcomAE.Platform.Cp;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Industry updates follow the bound tenant: hybrid host, ERP-only site key,
/// Super CP company pack. Jewellery must reach INDJ on www.ecomae.com the same
/// way thejewellerytrend.com receives it.
/// </summary>
public sealed class IndustryAudienceTests
{
    [Theory]
    [InlineData("indj", "", "www.ecomae.com", "jewellery")]
    [InlineData("indj", "jewellery", "www.ecomae.com", "jewellery")]
    [InlineData("fito", "", "www.ecomae.com", "fit_out")]
    [InlineData("asap", "", "www.ecomae.com", "auto_parts")]
    [InlineData("real", "", "www.ecomae.com", "real_estate")]
    [InlineData("thejewellerytrend", "", "www.thejewellerytrend.com", "jewellery")]
    [InlineData("thejewellerytrend", "", "thejewellerytrend.com", "jewellery")]
    [InlineData("epartscart", "", "www.epartscart.com", "auto_parts")]
    [InlineData(null, "jewellery", "www.ecomae.com", "jewellery")]
    public void ResolveIndustryCode_UsesPortalThenLaunchCatalogThenHost(
        string? siteKey,
        string portalIndustry,
        string host,
        string expected)
    {
        Assert.Equal(expected, RouteTenantResolver.ResolveIndustryCode(siteKey, portalIndustry, host));
    }

    [Fact]
    public void JewelleryRoutesOpenOnHybridHostAndErpOnlyTenant()
    {
        var main = new ErpCompanyDigest(1, "MAIN", "Main", "AED", "AE", "", true);

        Assert.True(IndustrySpecificRoutePolicy.Allows("jewellery", "jewellery", main));
        Assert.True(IndustrySpecificRoutePolicy.Allows("jewellery", "auto_parts", main, "jewellery"));
        Assert.True(IndustrySpecificRoutePolicy.Allows("jewellery", "", null, "jewellery"));
        Assert.True(IndustrySpecificRoutePolicy.Matches("jewellery", "jewellery_diamond"));
        Assert.True(IndustrySpecificRoutePolicy.Matches("jewellery", "jewelry"));
        Assert.False(IndustrySpecificRoutePolicy.Allows("jewellery", "auto_parts", main, "auto_parts"));
        Assert.False(IndustrySpecificRoutePolicy.Allows("jewellery", "", main, "fit_out"));
    }

    [Fact]
    public void FitOutAndGenericIndustryRoutesFollowTenantIndustry()
    {
        Assert.True(IndustrySpecificRoutePolicy.Allows("fitout", "auto_parts", null, "fit_out"));
        Assert.True(IndustrySpecificRoutePolicy.Allows("fitout", "", null, "construction_contracting"));
        Assert.False(IndustrySpecificRoutePolicy.Allows("fitout", "auto_parts", null, "jewellery"));
        Assert.True(IndustrySpecificRoutePolicy.Matches("auto_parts", "auto_parts"));
        Assert.True(IndustrySpecificRoutePolicy.Matches("real_estate", "real_estate"));
        Assert.False(IndustrySpecificRoutePolicy.Matches("real_estate", "jewellery"));
    }

    [Fact]
    public void ChromeShowsIndustryModulesFromTenantIndustryOnSuperHost()
    {
        var main = new ErpCompanyDigest(1, "MAIN", "Main", "AED", "AE", "", true);
        var super = ErpHostContext.Resolve("www.ecomae.com").IndustryCode;

        Assert.True(ErpIndustryNav.ShowJewelleryModules(super, main, "jewellery"));
        Assert.True(ErpIndustryNav.ShowFitOutModules(super, main, "fit_out"));
        Assert.False(ErpIndustryNav.ShowJewelleryModules(super, main, "auto_parts"));
        Assert.False(ErpIndustryNav.ShowFitOutModules(super, main, "jewellery"));
    }

    [Theory]
    [InlineData("jewellery", "jewellery_diamond", "jewellery")]
    [InlineData("jewelry", "jewellery_diamond", "jewellery")]
    [InlineData("jewellery_diamond", "jewellery_diamond", "jewellery")]
    [InlineData("fit_out", "fitout_contracting", "building_construction")]
    [InlineData("auto_parts", "auto_parts", "auto_parts")]
    [InlineData("real_estate", "real_estate", "real_estate")]
    public void ShopBootstrapMapsIndustryToPhpPackAndProfile(string industry, string pack, string profile)
    {
        Assert.Equal(pack, ErpOnlyShopBootstrap.PackForIndustry(industry));
        Assert.Equal(profile, ErpOnlyShopBootstrap.ProfileForIndustry(industry));
    }

    [Fact]
    public void LaunchClientsCoverEachIndustryOnSuperHost()
    {
        Assert.Contains(ErpOnlyLaunchClients.All, c => c.SiteKey == "indj" && c.IndustryCode == "jewellery");
        Assert.Contains(ErpOnlyLaunchClients.All, c => c.SiteKey == "fito" && c.IndustryCode == "fit_out");
        Assert.Contains(ErpOnlyLaunchClients.All, c => c.SiteKey == "asap" && c.IndustryCode == "auto_parts");
        Assert.Contains(ErpOnlyLaunchClients.All, c => c.SiteKey == "real" && c.IndustryCode == "real_estate");
    }
}
