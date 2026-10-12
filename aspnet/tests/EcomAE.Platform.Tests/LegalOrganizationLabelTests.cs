using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegalOrganizationLabelTests
{
    [Fact]
    public void Resolve_ReplacesMainCompanyWithLegalName()
    {
        Assert.Equal("Jewellery Trend LLC", LegalOrganizationLabel.Resolve("Main Company", "Jewellery Trend LLC", "ECOM AE"));
        Assert.Equal("ECOM AE", LegalOrganizationLabel.Resolve("Main Company", null, "ECOM AE"));
        Assert.Equal("Jewellery Trend LLC", LegalOrganizationLabel.Display("MAIN", "Main Company", "Jewellery Trend LLC", "ECOM AE"));
        Assert.True(LegalOrganizationLabel.IsGeneric("Main Company"));
        Assert.False(LegalOrganizationLabel.IsGeneric("Jewellery Trend LLC"));
    }

    [Fact]
    public void Resolve_KeepsRealEntityName()
    {
        Assert.Equal("Jewellery Division", LegalOrganizationLabel.Resolve("Jewellery Division", "Jewellery Trend LLC"));
    }
}
