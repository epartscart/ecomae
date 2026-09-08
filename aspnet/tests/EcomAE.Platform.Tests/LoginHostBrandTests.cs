using EcomAE.Platform.Presentation;
using Xunit;
using System.IO;

namespace EcomAE.Platform.Tests;

public sealed class LoginHostBrandTests
{
    [Theory]
    [InlineData("www.epartscart.com")]
    [InlineData("epartscart.com")]
    public void EpartsCart_UsesAnimatedLogoAndAutopartsTheme(string host)
    {
        var brand = LoginHostBrand.Resolve(host, "cp");
        Assert.Equal(LoginHostBrand.Kind.AnimatedEparts, brand.LogoKind);
        Assert.Equal("epartscart", brand.SiteKey);
        Assert.Equal("autoparts", brand.AtmosphereTheme);
        Assert.Contains("bos-login--tenant-epartscart", brand.RootModifierClass);
        Assert.Contains(brand.ParticleColors, c => c.Contains("220,38,38") || c.Contains("dc2626"));
    }

    [Theory]
    [InlineData("www.electronicae.com", "electronicae", "circuit")]
    [InlineData("stylenlook.com", "stylenlook", "fashion")]
    [InlineData("thejewellerytrend.com", "thejewellerytrend", "sparkle")]
    [InlineData("taxofinca.com", "taxofinca", "advisory")]
    public void OtherTenants_UseCatalogLogoAndDistinctThemes(string host, string siteKey, string theme)
    {
        var brand = LoginHostBrand.Resolve(host, "cp");
        Assert.Equal(LoginHostBrand.Kind.TenantImage, brand.LogoKind);
        Assert.Equal(siteKey, brand.SiteKey);
        Assert.Equal(theme, brand.AtmosphereTheme);
        Assert.False(string.IsNullOrWhiteSpace(brand.LogoUrl));
    }

    [Theory]
    [InlineData("www.ecomae.com", "cp", "crimson-stars")]
    [InlineData("ecomae.com", "erp", "teal-moon")]
    [InlineData("cp.ecomae.com", "bos", "cyan-stars")]
    public void SuperHost_UsesPlatformAtmosphere(string host, string surface, string theme)
    {
        var brand = LoginHostBrand.Resolve(host, surface);
        Assert.Equal(LoginHostBrand.Kind.Platform, brand.LogoKind);
        Assert.Equal("platform", brand.SiteKey);
        Assert.Equal(theme, brand.AtmosphereTheme);
        Assert.NotEqual(LoginHostBrand.Kind.AnimatedEparts, brand.LogoKind);
    }

    [Fact]
    public void SuperHost_NeverBorrowsTenantLogoUrl()
    {
        var brand = LoginHostBrand.Resolve("www.ecomae.com", "erp");
        Assert.Equal(LoginHostBrand.Kind.Platform, brand.LogoKind);
        Assert.True(string.IsNullOrWhiteSpace(brand.LogoUrl));
        Assert.Contains("ERP", brand.Label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SurfaceHostLogo_ResolvesFromHostNotCompanyQuery()
    {
        var logo = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpSurfaceHostLogo.razor"));
        Assert.Contains("LoginHostBrand.Resolve", logo, StringComparison.Ordinal);
        Assert.Contains("Request.Host.Host", logo, StringComparison.Ordinal);
        Assert.DoesNotContain("company", logo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Never reads ?company=", logo, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cp", "content", "shop", "finance", "erp", "ajax_erp.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root.");
    }
}
