using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpIndustryNavTests
{
    [Fact]
    public void MainCompany_HidesJewelleryTabs_JewelleryCompany_ShowsThem()
    {
        var all = LegacyDesktopChromeCatalog.ErpTopnav();
        var main = ErpIndustryNav.FilterTopnav(all, jewelleryCompany: false);
        var jw = ErpIndustryNav.FilterTopnav(all, jewelleryCompany: true);

        Assert.True(all.Count > 0);
        Assert.True(jw.Sum(g => g.Links.Count) >= main.Sum(g => g.Links.Count));
        Assert.DoesNotContain(main.SelectMany(g => g.Links), ErpIndustryNav.IsJewelleryTab);
        Assert.Contains(jw.SelectMany(g => g.Links), ErpIndustryNav.IsJewelleryTab);
    }

    [Theory]
    [InlineData("jewellery_diamond", true)]
    [InlineData("jewellery", true)]
    [InlineData("", false)]
    [InlineData("auto_parts", false)]
    public void IsJewelleryCompany_UsesIndustryPack(string pack, bool expected)
    {
        var co = new ErpCompanyDigest(1, "X", "Test", "AED", "AE", pack, true);
        Assert.Equal(expected, ErpIndustryNav.IsJewelleryCompany(co));
    }
}
