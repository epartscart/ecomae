using EcomAE.Platform.Presentation;
using Xunit;

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
    public void SuperHost_UsesPlatformAtmosphere(string host, string surface, string theme)
    {
        var brand = LoginHostBrand.Resolve(host, surface);
        Assert.Equal(LoginHostBrand.Kind.Platform, brand.LogoKind);
        Assert.Equal(theme, brand.AtmosphereTheme);
    }
}
