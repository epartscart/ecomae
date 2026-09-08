using System.IO;
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

    [Fact]
    public void EnsureSwitchableCompanies_SuperCpAlwaysHasMainAndJewellery()
    {
        var onlyMain = new[] { new ErpCompanyDigest(1, "MAIN", "Main", "AED", "AE", "", true) };
        var merged = ErpIndustryNav.EnsureSwitchableCompanies(onlyMain, isSuperCpHost: true, "ECOM AE");
        Assert.Contains(merged, c => c.Id == 1 && c.Code == "MAIN");
        Assert.Contains(merged, c => c.Id == 2 && c.Code == "JW");

        var empty = ErpIndustryNav.EnsureSwitchableCompanies([], isSuperCpHost: true, "ECOM AE");
        Assert.Equal(2, empty.Count);

        var emptyTenant = ErpIndustryNav.EnsureSwitchableCompanies([], isSuperCpHost: false, "eParts");
        Assert.Contains(emptyTenant, c => c.Id == 2 && c.Code == "JW");

        var tenantOnly = ErpIndustryNav.EnsureSwitchableCompanies(onlyMain, isSuperCpHost: false, "eParts");
        Assert.Single(tenantOnly);
        Assert.Equal(1, tenantOnly[0].Id);
    }

    [Fact]
    public void JewelleryTenantHost_ShowsModulesEvenWhenCompanyIsMain()
    {
        var main = new ErpCompanyDigest(1, "MAIN", "Main", "AED", "AE", "", true);
        var host = ErpHostContext.Resolve("www.thejewellerytrend.com");
        Assert.Equal("jewellery", host.IndustryCode);
        Assert.True(ErpIndustryNav.ShowJewelleryModules(host.IndustryCode, main));
        Assert.True(ErpIndustryNav.ShowJewelleryModules("jewellery", null));

        var auto = ErpHostContext.Resolve("www.epartscart.com");
        Assert.False(ErpIndustryNav.ShowJewelleryModules(auto.IndustryCode, main));

        var super = ErpHostContext.Resolve("www.ecomae.com");
        Assert.False(ErpIndustryNav.ShowJewelleryModules(super.IndustryCode, main));
        Assert.True(ErpIndustryNav.ShowJewelleryModules(super.IndustryCode,
            new ErpCompanyDigest(2, "JW", "Jewellery Division", "AED", "AE", "jewellery_diamond", true)));
    }

    [Fact]
    public void ErpChromeAndDashboard_UseHostAwareJewelleryGate()
    {
        foreach (var relative in new[]
        {
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor",
            "aspnet/src/EcomAE.Platform/Components/Pages/ErpBosDashboardApp.razor",
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpCompanyIndustryBar.razor",
        })
        {
            var text = File.ReadAllText(FindRepoFile(relative));
            Assert.Contains("ShowJewelleryModules", text, StringComparison.Ordinal);
            Assert.DoesNotContain("_jewellery = ErpIndustryNav.IsJewelleryCompany(", text, StringComparison.Ordinal);
        }
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
