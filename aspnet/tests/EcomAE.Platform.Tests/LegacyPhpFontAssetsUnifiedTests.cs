using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// CP / ERP / BOS / storefront must share one PHP-parity body font + size
/// (Open Sans 14px from bootstrap_admin style.css).
/// </summary>
public sealed class LegacyPhpFontAssetsUnifiedTests
{
    [Theory]
    [InlineData("cp")]
    [InlineData("erp")]
    [InlineData("bos")]
    [InlineData("storefront")]
    public void ProductSurfacesShareOpenSansStack(string surface)
    {
        Assert.Equal(LegacyPhpFontAssets.ProductStack, LegacyPhpFontAssets.StackFor(surface));
        Assert.Contains("Open Sans", LegacyPhpFontAssets.StackFor(surface), StringComparison.Ordinal);
        Assert.DoesNotContain("Inter", LegacyPhpFontAssets.StackFor(surface), StringComparison.Ordinal);
        Assert.DoesNotContain("PT Sans", LegacyPhpFontAssets.StackFor(surface), StringComparison.Ordinal);
        Assert.DoesNotContain("Sora", LegacyPhpFontAssets.StackFor(surface), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("cp")]
    [InlineData("erp")]
    [InlineData("bos")]
    [InlineData("storefront")]
    public void ProductSurfacesShareBaseFontSize14px(string surface)
    {
        Assert.Equal("14px", LegacyPhpFontAssets.FontSizeFor(surface));
        Assert.Equal(LegacyPhpFontAssets.BaseFontSize, LegacyPhpFontAssets.FontSizeFor(surface));
    }

    [Theory]
    [InlineData("cp")]
    [InlineData("erp")]
    [InlineData("bos")]
    [InlineData("storefront")]
    public void ProductSurfacesLoadOpenSansWebfont(string surface)
    {
        var hrefs = LegacyPhpFontAssets.FontHrefsFor(surface);
        Assert.Contains(LegacyPhpFontAssets.OpenSans, hrefs);
        Assert.All(hrefs, href => Assert.DoesNotContain("family=Inter", href, StringComparison.Ordinal));
    }

    [Fact]
    public void ChromeComponentsApplyUnifiedStackAndSize()
    {
        var root = FindRepoRoot();
        foreach (var rel in new[]
                 {
                     "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor",
                     "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor",
                     "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpBosDesktopChrome.razor",
                     "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor",
                     "aspnet/src/EcomAE.Platform/Components/Shared/PhpChromeStyles.razor"
                 })
        {
            var src = File.ReadAllText(Path.Combine(root, rel));
            Assert.Contains("LegacyPhpFontAssets.StackFor", src);
            Assert.True(
                src.Contains("LegacyPhpFontAssets.BaseFontSize", StringComparison.Ordinal)
                || src.Contains("LegacyPhpFontAssets.FontSizeFor", StringComparison.Ordinal),
                $"{rel} must apply unified base font size");
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
