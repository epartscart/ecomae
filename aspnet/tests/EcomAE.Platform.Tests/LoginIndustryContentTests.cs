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
        Assert.DoesNotContain("Worldwide Compliance", text, StringComparison.Ordinal);
        Assert.DoesNotContain("225", text, StringComparison.Ordinal);
    }
}
