using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class SuperCpIndustrySampleFixtureCatalogTests
{
    [Fact]
    public void CoversEverySupportedPortalIndustry()
    {
        Assert.Equal(PhpPortalIndustryCatalog.All.Count, SuperCpIndustrySampleFixtureCatalog.All.Count);
        Assert.Equal(
            PhpPortalIndustryCatalog.All.Select(industry => industry.Code).OrderBy(code => code),
            SuperCpIndustrySampleFixtureCatalog.All.Select(fixture => fixture.IndustryCode).OrderBy(code => code));
    }

    [Fact]
    public void FixturesAreExplicitlyDemoLabelledAndTenantSafe()
    {
        Assert.All(SuperCpIndustrySampleFixtureCatalog.All, fixture =>
        {
            Assert.StartsWith("DEMO | ", fixture.LabelPrefix, StringComparison.Ordinal);
            Assert.NotEmpty(fixture.ProductCodes);
            Assert.NotEmpty(fixture.CustomerCodes);
            Assert.NotEmpty(fixture.SupplierCodes);
            Assert.NotEmpty(fixture.WorkflowCodes);
            Assert.DoesNotContain("docpart", fixture.ProductCodes, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("ecomae", fixture.CustomerCodes, StringComparer.OrdinalIgnoreCase);
        });
    }

    [Theory]
    [InlineData("jewellery", "jewellery")]
    [InlineData("construction_contracting", "fit-out-contracting")]
    [InlineData("auto_parts", "auto-parts")]
    [InlineData("manufacturing_industrial", "manufacturing")]
    public void ImportantIndustryPacksUseDedicatedFixtureProfiles(string industryCode, string expectedProfile)
    {
        Assert.Equal(expectedProfile, SuperCpIndustrySampleFixtureCatalog.Resolve(industryCode).Profile);
    }

    [Fact]
    public void TenantScopedFixtureIdsCannotOverlap()
    {
        var first = SuperCpIndustrySampleFixtureCatalog.ForTenant("jewellery", "jewellery-demo");
        var second = SuperCpIndustrySampleFixtureCatalog.ForTenant("jewellery", "fitout-demo");

        Assert.NotEqual(first.TenantKey, second.TenantKey);
        Assert.DoesNotContain(first.ProductCodes, code => second.ProductCodes.Contains(code, StringComparer.Ordinal));
        Assert.All(first.ProductCodes, code => Assert.StartsWith("jewellery-demo:", code, StringComparison.Ordinal));
        Assert.All(second.CustomerCodes, code => Assert.StartsWith("fitout-demo:", code, StringComparison.Ordinal));
    }
}
