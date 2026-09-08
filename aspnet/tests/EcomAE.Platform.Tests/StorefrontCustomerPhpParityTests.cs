using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Customer-visible epartscart.com pages must match Classic PHP without stack or twin chrome.
/// </summary>
public sealed class StorefrontCustomerPhpParityTests
{
    [Theory]
    [InlineData("StorefrontVinApp.razor")]
    [InlineData("StorefrontSellerRequestApp.razor")]
    [InlineData("StorefrontPaymentApp.razor")]
    [InlineData("StorefrontProfileApp.razor")]
    [InlineData("StorefrontGarageApp.razor")]
    [InlineData("StorefrontOrdersApp.razor")]
    [InlineData("StorefrontAccountSummaryApp.razor")]
    [InlineData("StorefrontCartApp.razor")]
    [InlineData("StorefrontWishlistApp.razor")]
    [InlineData("StorefrontVehicleCatalogApp.razor")]
    [InlineData("StorefrontIndustryProductApp.razor")]
    [InlineData("StorefrontLoginApp.razor")]
    [InlineData("StorefrontVendorPortalApp.razor")]
    [InlineData("StorefrontDemandIntelligenceApp.razor")]
    public void CustomerPages_HaveNoTwinOrStackChrome(string fileName)
    {
        var text = Read(fileName);
        Assert.DoesNotContain("Classic twin", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Compare PHP reference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Open module", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Nothing to show yet.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VinApp_KeepsDecodeFormAndPhpUrl()
    {
        var text = Read("StorefrontVinApp.razor");
        Assert.Contains("@page \"/en/katalog-laximo\"", text, StringComparison.Ordinal);
        Assert.Contains("action=\"/storefront/vin/decode\"", text, StringComparison.Ordinal);
        Assert.Contains("Decode VIN", text, StringComparison.Ordinal);
        Assert.Contains("StorefrontSurfaceLinks.SellerRequest", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SellerRequest_PostsLiveCreateWithAgreement()
    {
        var text = Read("StorefrontSellerRequestApp.razor");
        Assert.Contains("action=\"@PhpSellerRequest.SellerWriteHref\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"client_parts\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"users_agreement\"", text, StringComparison.Ordinal);
        Assert.Contains("My requests", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Garage_MatchesPhpServiceReadyBanner()
    {
        var text = Read("StorefrontGarageApp.razor");
        Assert.Contains("My Garage · Service ready", text, StringComparison.Ordinal);
        Assert.Contains("No vehicles in the garage yet.", text, StringComparison.Ordinal);
        Assert.Contains("action=\"@PhpCustomerWrites.GarageCarWriteHref\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Orders_UsePhpEmptyCopy()
    {
        var text = Read("StorefrontOrdersApp.razor");
        Assert.Contains("You have no orders yet.", text, StringComparison.Ordinal);
        Assert.Contains("Your orders, receipts, and messages to the store.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void IndustryProduct_DoesNotPostReviewsToPhpReference()
    {
        var text = Read("StorefrontIndustryProductApp.razor");
        Assert.DoesNotContain("ajax_add_evaluation.php", text, StringComparison.Ordinal);
        Assert.Contains("StorefrontSurfaceLinks.Cart", text, StringComparison.Ordinal);
    }

    private static string Read(string fileName)
    {
        var path = Path.Combine(FindRepoRoot(), "aspnet", "src", "EcomAE.Platform", "Components", "Pages", fileName);
        Assert.True(File.Exists(path), path);
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}
