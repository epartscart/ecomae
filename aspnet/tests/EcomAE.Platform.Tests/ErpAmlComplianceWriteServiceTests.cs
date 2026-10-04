using Xunit;

namespace EcomAE.Platform.Tests;

public class ErpAmlComplianceWriteServiceTests
{
    [Fact]
    public void DefaultRulesMatchPhpAmlCatalogue()
    {
        var rules = EcomAE.Platform.Erp.ErpAmlShared.DefaultRules;
        Assert.Equal(5, rules.Length);
        Assert.Equal(("Cash transaction ≥ 55,000 AED (DPMS / CTR)", "threshold", 55000m, "AED", "report", 0, 1), rules[0]);
        Assert.Equal(("Wire transfer ≥ 100,000 AED", "threshold", 100000m, "AED", "flag", 0, 1), rules[1]);
        Assert.Equal(("More than 5 cash transactions in 7 days", "frequency", 0m, "AED", "flag", 5, 7), rules[2]);
        Assert.Equal(("Structuring — 3+ payments in 24h near cash threshold", "frequency", 0m, "AED", "flag", 3, 1), rules[3]);
        Assert.Equal(("High-risk country origin", "country", 0m, "AED", "flag", 0, 1), rules[4]);
    }

    [Fact]
    public void CheckRequestDefaultsMatchPhpPostFields()
    {
        var request = new EcomAE.Platform.Erp.ErpAmlCheckWriteRequest();
        Assert.Equal(0m, request.Amount);
        Assert.Null(request.Currency);
        Assert.Null(request.TransactionType);
        Assert.Null(request.CustomerName);
        Assert.Null(request.Reference);
    }

    [Fact]
    public void SettingsInputDefaultsMatchPhpPostFields()
    {
        var input = new EcomAE.Platform.Erp.ErpAmlSettingsWriteInput();
        Assert.Null(input.CashThreshold);
        Assert.False(input.StructuringEnabled);
        Assert.False(input.PepScreening);
        Assert.Null(input.Authority);
        Assert.Null(input.MlroName);
        Assert.Null(input.GoamlReg);
    }

    [Fact]
    public void ServiceTypeImplementsContract()
    {
        Assert.True(typeof(EcomAE.Platform.Erp.IErpAmlCheckWriteService).IsAssignableFrom(typeof(EcomAE.Platform.Erp.ErpAmlCheckWriteService)));
        Assert.True(typeof(EcomAE.Platform.Erp.IErpAmlSeedRulesWriteService).IsAssignableFrom(typeof(EcomAE.Platform.Erp.ErpAmlSeedRulesWriteService)));
        Assert.True(typeof(EcomAE.Platform.Erp.IErpAmlSettingsSaveWriteService).IsAssignableFrom(typeof(EcomAE.Platform.Erp.ErpAmlSettingsSaveWriteService)));
        Assert.True(typeof(EcomAE.Platform.Erp.IErpAmlReportGenerateWriteService).IsAssignableFrom(typeof(EcomAE.Platform.Erp.ErpAmlReportGenerateWriteService)));
    }
}
