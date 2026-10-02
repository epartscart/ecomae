using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCountryObligationProfilePhpParityTests
{
    [Fact]
    public void Mirrors_php_common_and_country_specific_obligations()
    {
        var uae = ErpCountryObligationProfiles.Resolve("UAE");
        var saudi = ErpCountryObligationProfiles.Resolve("SA");
        var generic = ErpCountryObligationProfiles.Resolve("GB");

        Assert.Equal(["einvoice", "payroll", "vat_return", "corporate_tax", "esr"], uae.Select(item => item.Code));
        Assert.Equal(("VAT return (FTA)", "UAE FTA (EmaraTax)", "quarterly", 28), (
            uae.Single(item => item.Code == "vat_return").Title,
            uae.Single(item => item.Code == "vat_return").Authority,
            uae.Single(item => item.Code == "vat_return").Frequency,
            uae.Single(item => item.Code == "vat_return").LeadDays));
        Assert.Equal("ZATCA (Saudi Arabia)", saudi.Single(item => item.Code == "corporate_tax").Authority);
        Assert.Equal("Tax authority", generic.Single(item => item.Code == "corporate_tax").Authority);
        Assert.Equal(4, generic.Count);
    }

    [Fact]
    public void Normalizes_country_aliases_without_using_preview_defaults()
    {
        var uae = ErpCountryObligationProfiles.Resolve("United Arab Emirates");
        var uk = ErpCountryObligationProfiles.Resolve("UK");

        Assert.Contains(uae, item => item.Code == "esr");
        Assert.DoesNotContain(uk, item => item.Code == "esr");
        Assert.Equal("VAT / GST return", uk.Single(item => item.Code == "vat_return").Title);
    }
}
