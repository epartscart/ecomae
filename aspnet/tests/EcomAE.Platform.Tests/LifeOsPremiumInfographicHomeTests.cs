using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// lifeos.ecomae.com home must present a premium blue/black/white infographic
/// covering the full Master Spec (Parts 1–10 scaffolds).
/// </summary>
public sealed class LifeOsPremiumInfographicHomeTests
{
    [Fact]
    public void Home_UsesBlueBlackWhiteInfographicPaletteAndBrandHero()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Pages/LifeOsHomeApp.razor");
        Assert.Contains("lifeos-infographic", text, StringComparison.Ordinal);
        Assert.Contains("lifeos-brand", text, StringComparison.Ordinal);
        Assert.Contains("Life<em>OS</em>™", text, StringComparison.Ordinal);
        Assert.Contains("--lo-black", text, StringComparison.Ordinal);
        Assert.Contains("--lo-blue", text, StringComparison.Ordinal);
        Assert.Contains("--lo-white", text, StringComparison.Ordinal);
        Assert.Contains("Fraunces", text, StringComparison.Ordinal);
        Assert.Contains("Sora", text, StringComparison.Ordinal);
        // No teal/amber legacy palette
        Assert.DoesNotContain("#0f766e", text, StringComparison.Ordinal);
        Assert.DoesNotContain("#f59e0b", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Instrument Serif", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Home_EmitsCriticalCssInBodyStreamFallback()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Pages/LifeOsHomeApp.razor");
        Assert.Contains("body-inline", text, StringComparison.Ordinal);
        var close = text.IndexOf("</HeadContent>", StringComparison.Ordinal);
        Assert.True(close > 0);
        var after = text[(close + "</HeadContent>".Length)..];
        Assert.Contains("<style>", after, StringComparison.Ordinal);
        Assert.Contains("--lo-black", after, StringComparison.Ordinal);
    }

    [Fact]
    public void Home_HasFlowLifestyleFinancialMotionsAndFullSpecTimeline()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Pages/LifeOsHomeApp.razor");
        Assert.Contains("lifeos-flow", text, StringComparison.Ordinal);
        Assert.Contains("lifeos-lifestyle", text, StringComparison.Ordinal);
        Assert.Contains("lifeos-hero__spark", text, StringComparison.Ordinal);
        Assert.Contains("lifeos-hero__flow-a", text, StringComparison.Ordinal);
        Assert.Contains("lifeos-parts__timeline", text, StringComparison.Ordinal);
        Assert.Contains("ILifeOsMasterSpec", text, StringComparison.Ordinal);
        Assert.Contains("LifeOsRoadmapApp", text, StringComparison.Ordinal);
        Assert.Contains("Ch.151–168", text, StringComparison.Ordinal);
        Assert.Contains("Wealth", text, StringComparison.Ordinal);
        Assert.Contains("Perceive", text, StringComparison.Ordinal);
        Assert.Contains("marker-end", text, StringComparison.Ordinal);
        // Hero stays brand-led — not an 8-CTA AI template cluster.
        Assert.Contains("lifeos-navstrip", text, StringComparison.Ordinal);
        var ctaBlockStart = text.IndexOf("class=\"lifeos-cta\"", StringComparison.Ordinal);
        var ctaBlockEnd = text.IndexOf("</div>", ctaBlockStart, StringComparison.Ordinal);
        Assert.True(ctaBlockStart > 0 && ctaBlockEnd > ctaBlockStart);
        var ctaBlock = text[ctaBlockStart..ctaBlockEnd];
        Assert.True(ctaBlock.Split("href=", StringSplitOptions.None).Length - 1 <= 3);
    }

    [Fact]
    public void ProductCss_IsAllowlistedAndEditorial()
    {
        var css = Read("aspnet/src/EcomAE.Platform/wwwroot/lifeos/lifeos-product.css");
        Assert.Contains("--lo-black", css, StringComparison.Ordinal);
        Assert.Contains("--lo-blue", css, StringComparison.Ordinal);
        Assert.Contains("lo-chrome", css, StringComparison.Ordinal);
        Assert.DoesNotContain("#0f766e", css, StringComparison.Ordinal);
        Assert.DoesNotContain("#f59e0b", css, StringComparison.Ordinal);
        var pwa = Read("aspnet/src/EcomAE.Platform/LifeOs/Clients/LifeOsPwaAssets.cs");
        Assert.Contains("lifeos-product.css", pwa, StringComparison.Ordinal);
    }

    [Fact]
    public void LifeOsFonts_AreFrauncesSoraPremiumStack()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Presentation/LegacyPhpFontAssets.cs");
        Assert.Contains("Fraunces", text, StringComparison.Ordinal);
        Assert.Contains("Sora", text, StringComparison.Ordinal);
        Assert.Contains("LifeOsFonts", text, StringComparison.Ordinal);
    }

    private static string Read(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
