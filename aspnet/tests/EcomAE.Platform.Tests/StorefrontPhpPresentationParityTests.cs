using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Storefront ASP.NET apps must present like PHP nero pages — light page headers, no invented gradient heroes.
/// </summary>
public sealed class StorefrontPhpPresentationParityTests
{
    [Fact]
    public void StorefrontStylesheetsIncludeModuleParityCss()
    {
        Assert.Contains(
            LegacyPresentationAssets.StorefrontStylesheets,
            s => s.Contains("epc_storefront_aspnet_module_parity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ParityCssNeutralizesInventedHeroGradients()
    {
        var css = File.ReadAllText(Find("content/general_pages/epc_storefront_aspnet_module_parity.css"));
        Assert.Contains("epc-sf-page-hd", css, StringComparison.Ordinal);
        Assert.Contains("[class*=\"-hero\"]", css, StringComparison.Ordinal);
        Assert.Contains("background: #fff !important", css, StringComparison.Ordinal);
        Assert.Contains("background-image: none !important", css, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedPhpStorefrontPageHeaderExists()
    {
        var path = Find("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontPageHeader.razor");
        var text = File.ReadAllText(path);
        Assert.Contains("epc-sf-page-hd", text, StringComparison.Ordinal);
        Assert.Contains("epc-sf-page-title", text, StringComparison.Ordinal);
        Assert.DoesNotContain("linear-gradient", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StorefrontApps_DoNotUseMarketingHeroGradients()
    {
        var dir = Path.GetDirectoryName(Find("aspnet/src/EcomAE.Platform/Components/Pages/StorefrontCartApp.razor"))!;
        var offenders = new List<string>();
        foreach (var file in Directory.GetFiles(dir, "Storefront*App.razor"))
        {
            var text = File.ReadAllText(file);
            if (text.Contains("linear-gradient(135deg", StringComparison.OrdinalIgnoreCase))
            {
                offenders.Add(Path.GetFileName(file));
            }
        }

        Assert.True(offenders.Count == 0, "Marketing hero leftovers: " + string.Join(", ", offenders));
    }

    [Theory]
    [InlineData("StorefrontCartApp.razor")]
    [InlineData("StorefrontSearchApp.razor")]
    [InlineData("StorefrontCheckoutApp.razor")]
    [InlineData("StorefrontVinApp.razor")]
    [InlineData("StorefrontOrdersApp.razor")]
    [InlineData("StorefrontGarageApp.razor")]
    [InlineData("StorefrontAccountSummaryApp.razor")]
    public void StorefrontApps_UsePhpPageHeader(string fileName)
    {
        var text = File.ReadAllText(Find($"aspnet/src/EcomAE.Platform/Components/Pages/{fileName}"));
        Assert.Contains("PhpStorefrontPageHeader", text, StringComparison.Ordinal);
        Assert.DoesNotContain("linear-gradient(135deg", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OwnCatalogCss_UsesLightPhpHeaderNotDarkHero()
    {
        var css = File.ReadAllText(Find("content/general_pages/epc_own_catalog.css"));
        Assert.Contains(".epc-own-cat__hero", css, StringComparison.Ordinal);
        Assert.Contains("background: #fff", css, StringComparison.Ordinal);
        Assert.DoesNotContain("linear-gradient(135deg, #111827", css, StringComparison.Ordinal);
    }

    private static string Find(string relative)
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
