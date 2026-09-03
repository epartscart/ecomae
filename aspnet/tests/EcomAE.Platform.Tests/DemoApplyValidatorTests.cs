using EcomAE.Platform.Routing;
using EcomAE.Platform.Services;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class DemoApplyValidatorTests
{
    [Fact]
    public void ValidFieldsPass_NeverClaimProvision()
    {
        var result = DemoApplyValidator.ValidateFields(Valid());
        Assert.True(result.Ok);
        var unavailable = DemoApplyValidator.Unavailable();
        Assert.False(unavailable.Ok);
        Assert.Equal(503, unavailable.StatusCode);
        Assert.Equal(DemoApplyValidator.UnavailableMessage, unavailable.Message);
    }

    [Fact]
    public void NameRequired()
    {
        var result = DemoApplyValidator.ValidateFields(Valid() with { ContactName = "  " });
        Assert.False(result.Ok);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Name is required", result.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void EmailRequired(string email)
    {
        var result = DemoApplyValidator.ValidateFields(Valid() with { ContactEmail = email });
        Assert.False(result.Ok);
        Assert.Equal("Valid email is required", result.Message);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("1234567890123456")]
    [InlineData("")]
    public void PhoneMustBeSevenToFifteenDigits(string phone)
    {
        var result = DemoApplyValidator.ValidateFields(Valid() with { ContactPhone = phone });
        Assert.False(result.Ok);
        Assert.Equal("Valid phone number is required (7–15 digits)", result.Message);
    }

    [Fact]
    public void PhoneAcceptsFormattedInternational()
    {
        var result = DemoApplyValidator.ValidateFields(Valid() with { ContactPhone = "+971 50 123 4567" });
        Assert.True(result.Ok);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("++")]
    public void CountryRequired(string country)
    {
        var result = DemoApplyValidator.ValidateFields(Valid() with { CountryCode = country });
        Assert.False(result.Ok);
        Assert.Equal("Please select your country", result.Message);
    }

    [Fact]
    public void CountryNormalizesToIso2()
    {
        Assert.Equal("IN", DemoApplyValidator.NormalizeCountry("in"));
        Assert.Equal("AE", DemoApplyValidator.NormalizeCountry("AE "));
        var result = DemoApplyValidator.ValidateFields(Valid() with { CountryCode = "in" });
        Assert.True(result.Ok);
    }

    [Fact]
    public void TermsRequired()
    {
        var result = DemoApplyValidator.ValidateFields(Valid() with { Terms = false });
        Assert.False(result.Ok);
        Assert.Equal("You must accept the demo terms", result.Message);
    }

    [Fact]
    public void EmptyIndustryMessage()
    {
        var result = DemoApplyValidator.ValidateFields(Valid() with { IndustryCode = "" });
        Assert.False(result.Ok);
        Assert.Equal("Select an industry (auto_parts, fashion, or erp_only)", result.Message);
    }

    [Theory]
    [InlineData("jewellery")]
    [InlineData("electronics")]
    public void UnknownIndustryRejected(string industry)
    {
        var result = DemoApplyValidator.ValidateFields(Valid() with { IndustryCode = industry });
        Assert.False(result.Ok);
        Assert.Equal("Industry not available — choose auto parts, fashion, or ERP only", result.Message);
    }

    [Fact]
    public void ErpStandaloneAliasMapsToErpOnly()
    {
        Assert.Equal("erp_only", DemoApplyValidator.NormalizeIndustry("erp_standalone"));
        var result = DemoApplyValidator.ValidateFields(Valid() with { IndustryCode = "erp_standalone" });
        Assert.True(result.Ok);
    }

    [Fact]
    public void DemoApplyRouteIsNotTheLivePhpProvisioner()
    {
        Assert.Equal("/demo/apply", EcomAeRoutes.DemoApply);
        Assert.NotEqual("/epc-demo-provision-public.php", EcomAeRoutes.DemoApply);
    }

    [Fact]
    public void PublicPhpProvisionForwardsCountryCode()
    {
        var php = File.ReadAllText(Path.Combine(FindRepoRoot(), "epc-demo-provision-public.php"));
        Assert.Contains("'country_code' => $_POST['country_code']", php, StringComparison.Ordinal);
        Assert.Contains("epc_portal_demo_provision", php, StringComparison.Ordinal);
    }

    [Fact]
    public void DemoOverviewCtasPointAtLiveWizard()
    {
        var razor = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpEcomaeDemoOverview.razor"));
        Assert.Contains("href=\"/platform/demo\"", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/marketing/demo\"", razor, StringComparison.Ordinal);
        Assert.Contains("Auto spare parts, fashion retail, or ERP only", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("jewellery", razor, StringComparison.OrdinalIgnoreCase);
    }

    private static DemoApplyRequest Valid() => new(
        ContactName: "Amina Test",
        ContactEmail: "amina@example.com",
        ContactPhone: "+971501234567",
        Company: "Test Co",
        CountryCode: "AE",
        IndustryCode: "auto_parts",
        Terms: true);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "epc-demo-provision-public.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root");
    }
}
