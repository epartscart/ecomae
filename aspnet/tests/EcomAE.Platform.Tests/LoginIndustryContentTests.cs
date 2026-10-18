using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LoginIndustryContentTests
{
    [Fact]
    public void EpartsCartErp_IsPartsIndustryNotSuperFleet()
    {
        var brand = LoginHostBrand.Resolve("www.epartscart.com", "erp");
        var content = LoginIndustryContent.For(brand, "erp");

        Assert.Equal("auto_parts", content.IndustryCode);
        Assert.Contains("Parts", content.FormTitle, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(content.Capabilities, c => c.Title.Contains("Parts", StringComparison.OrdinalIgnoreCase)
            || c.Title.Contains("Warehouse", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(content.Stats, s => s.Label.Equals("Tenants", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(content.Stats, s => s.Label.Equals("Industries", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(content.PanelFeatures, f => f.Contains("Workforce Management", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(content.PanelFeatures, f => f.Contains("VIN", StringComparison.OrdinalIgnoreCase)
            || f.Contains("Parts", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void JewelleryErp_UsesGoldAndTagsCopy()
    {
        var brand = LoginHostBrand.Resolve("thejewellerytrend.com", "erp");
        var content = LoginIndustryContent.For(brand, "erp");
        Assert.Equal("jewellery", content.IndustryCode);
        Assert.Contains(content.Capabilities, c => c.Title.Contains("Gold", StringComparison.OrdinalIgnoreCase)
            || c.Title.Contains("Jewellery", StringComparison.OrdinalIgnoreCase)
            || c.Title.Contains("tags", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ErpOnlyJewellery_UsesSameJewelleryCopyAsTrend()
    {
        var brand = LoginHostBrand.ForErpOnly("indj", "INDJ Jewellery", "jewellery");
        var trend = LoginIndustryContent.For(LoginHostBrand.Resolve("thejewellerytrend.com", "erp"), "erp");
        var content = LoginIndustryContent.For(brand, "erp");
        Assert.Equal(trend.IndustryCode, content.IndustryCode);
        Assert.Equal(trend.FormTitle, content.FormTitle);
        Assert.Equal(trend.HeroTagline, content.HeroTagline);
        Assert.Equal(trend.SubmitLabel, content.SubmitLabel);
        Assert.Equal(trend.Capabilities.Count, content.Capabilities.Count);
        Assert.Equal("INDJ Jewellery", content.HeroTitle);
    }

    [Fact]
    public void ErpOnlyRealEstate_UsesPropertyNotFleetCopy()
    {
        var brand = LoginHostBrand.ForErpOnly("real", "REAL Real Estate", "real_estate");
        var content = LoginIndustryContent.For(brand, "erp");

        Assert.Equal("real_estate", content.IndustryCode);
        Assert.Equal("REAL Real Estate", content.HeroTitle);
        Assert.Equal("skyline", brand.AtmosphereTheme);
        Assert.Contains("bos-login--tenant-realestate", brand.RootModifierClass, StringComparison.Ordinal);
        Assert.Contains("Real Estate", content.FormTitle, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(content.Capabilities, c => c.Title.Contains("Property", StringComparison.OrdinalIgnoreCase)
            || c.Title.Contains("Leasing", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(content.PanelFeatures, f => f.Contains("cheques", StringComparison.OrdinalIgnoreCase)
            || f.Contains("certificates", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(content.Stats, s => s.Label.Equals("Tenants", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(content.Stats, s => s.Label.Equals("Industries", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(content.Capabilities, c => c.Title.Contains("Gold", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(content.Capabilities, c => c.Title.Contains("Parts", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(content.PanelFeatures, f => f.Contains("Workforce Management", StringComparison.OrdinalIgnoreCase));

        var live = LoginIndustryContent.WithStats(
            content,
            [
                new LoginIndustryContent.Stat(4, "Properties"),
                new LoginIndustryContent.Stat(2, "Active leases"),
                new LoginIndustryContent.Stat(1, "Projects"),
                new LoginIndustryContent.Stat(3, "Opportunities"),
            ]);
        Assert.Equal("real_estate", live.IndustryCode);
        Assert.Contains(live.Stats, s => s.Label == "Properties" && s.Value == 4);
        Assert.DoesNotContain(live.Stats, s => s.Label.Equals("Workspaces", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(live.Stats, s => s.Label.Equals("Tenants", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SuperErp_KeepsFleetStats()
    {
        var brand = LoginHostBrand.Resolve("www.ecomae.com", "erp");
        var content = LoginIndustryContent.For(brand, "erp");
        Assert.Equal("platform", content.IndustryCode);
        Assert.Contains(content.Stats, s => s.Label.Equals("Tenants", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ErpLoginApp_WiresIndustryContent()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        string? path = null;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "aspnet/src/EcomAE.Platform/Components/Pages/ErpLoginApp.razor");
            if (File.Exists(candidate))
            {
                path = candidate;
                break;
            }

            dir = dir.Parent;
        }

        Assert.NotNull(path);
        var text = File.ReadAllText(path!);
        Assert.Contains("LoginIndustryContent.For", text, StringComparison.Ordinal);
        Assert.Contains("data-login-industry", text, StringComparison.Ordinal);
        Assert.Contains("_content.Capabilities", text, StringComparison.Ordinal);
        Assert.Contains("OverlayRealEstateStatsAsync", text, StringComparison.Ordinal);
        Assert.Contains("IErpRealEstateReadService", text, StringComparison.Ordinal);
        Assert.Contains("OwnedProperties", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Worldwide Compliance", text, StringComparison.Ordinal);
        Assert.DoesNotContain("225", text, StringComparison.Ordinal);
    }
}
