using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpEinvoiceCountryProfilePhpParityTests
{
    [Fact]
    public void Resolves_php_country_profiles_without_making_pint_ae_global()
    {
        var uae = ErpEinvoiceCountryProfiles.Resolve("UAE");
        var saudi = ErpEinvoiceCountryProfiles.Resolve("SA");
        var india = ErpEinvoiceCountryProfiles.Resolve("IN");
        var uk = ErpEinvoiceCountryProfiles.Resolve("GB");
        var generic = ErpEinvoiceCountryProfiles.Resolve("ZZ");

        Assert.Equal(("AE", "UAE-FTA", "VAT"), (uae.CountryCode, uae.Scheme, uae.TaxLabel));
        Assert.Equal(("SA", "ZATCA", "VAT"), (saudi.CountryCode, saudi.Scheme, saudi.TaxLabel));
        Assert.Equal(("IN", "GST-IRN", "GST"), (india.CountryCode, india.Scheme, india.TaxLabel));
        Assert.Equal(("GB", "EN16931", "VAT"), (uk.CountryCode, uk.Scheme, uk.TaxLabel));
        Assert.Equal(("ZZ", "GENERIC", "TAX"), (generic.CountryCode, generic.Scheme, generic.TaxLabel));
        Assert.True(saudi.ClearanceRequired);
        Assert.Contains("hsn", india.RequiredFields);
        Assert.DoesNotContain("PINT-AE", generic.Scheme, StringComparison.Ordinal);
    }

    [Fact]
    public void Tenant_country_wins_over_preview_country()
    {
        var profile = ErpEinvoiceCountryProfiles.ResolveForTenant("GB", "AE");

        Assert.Equal("GB", profile.CountryCode);
        Assert.Equal("EN16931", profile.Scheme);
        Assert.DoesNotContain("PINT-AE", profile.Scheme, StringComparison.Ordinal);
    }
}
