using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpTaxToolkitCountryProfilePhpParityTests
{
    [Fact]
    public void Resolves_php_worldwide_vat_and_corporate_tax_fixture()
    {
        var uae = ErpTaxToolkitCountryProfiles.Resolve("UAE");
        var saudi = ErpTaxToolkitCountryProfiles.Resolve("SA");
        var india = ErpTaxToolkitCountryProfiles.Resolve("IN");
        var germany = ErpTaxToolkitCountryProfiles.Resolve("DE");
        var australia = ErpTaxToolkitCountryProfiles.Resolve("AU");

        Assert.Equal(("AE", "AE-UAE-VAT", "vat", 5m, "AED"), (uae.CountryCode, uae.KitCode, uae.TaxType, uae.StandardRate, uae.Currency));
        Assert.Equal(9m, uae.CorporateTaxRate);
        Assert.Equal(375000m, uae.CorporateTaxThreshold);
        Assert.Equal(("SA", "SA-KSA-VAT", 15m, 20m), (saudi.CountryCode, saudi.KitCode, saudi.StandardRate, saudi.CorporateTaxRate));
        Assert.Equal(("IN", "IN-INDIA-GST", "gst", 18m, 30m), (india.CountryCode, india.KitCode, india.TaxType, india.StandardRate, india.CorporateTaxRate));
        Assert.Equal(("DE", "DE-GERMANY-VAT", 19m, 15m), (germany.CountryCode, germany.KitCode, germany.StandardRate, germany.CorporateTaxRate));
        Assert.Equal(("AU", "AU-AUSTRALIA-GST", "gst", 10m, "GST", "ABN", "AUD"), (australia.CountryCode, australia.KitCode, australia.TaxType, australia.StandardRate, australia.TaxLabel, australia.RegistrationLabel, australia.Currency));
    }

    [Fact]
    public void Unknown_country_uses_php_manual_configuration_fallback()
    {
        var profile = ErpTaxToolkitCountryProfiles.Resolve("ZZ");

        Assert.Equal("ZZ", profile.CountryCode);
        Assert.Equal("ZZ-ZZ-VAT", profile.KitCode);
        Assert.Equal(15m, profile.StandardRate);
        Assert.Equal("USD", profile.Currency);
        Assert.Null(profile.CorporateTaxRate);
        Assert.Contains("Configure corporate tax manually", profile.CorporateTaxNotes, StringComparison.Ordinal);
    }
}
