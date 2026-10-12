using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpJwVocTypesTests
{
    [Fact]
    public void CanonicalWritesUsePhpVoucherCodes()
    {
        Assert.Equal("RMP", ErpJwVocTypes.Canonical(null, "jw_metal_purchase_save"));
        Assert.Equal("RDP", ErpJwVocTypes.Canonical("DMP", null));
        Assert.Equal("RIN", ErpJwVocTypes.Canonical("RSI", "jw_retail_sale_save"));
        Assert.Equal("ADV", ErpJwVocTypes.Canonical("PAD", null));
        Assert.Equal("JVL", ErpJwVocTypes.Canonical("JVG", null));
        Assert.Equal("MFG", ErpJwVocTypes.Canonical(null, "jw_manufacture_save"));
        Assert.Equal("BTR", ErpJwVocTypes.Canonical(null, "jw_branch_transfer_save"));
    }

    [Fact]
    public void CompanyTwoJewelleryReadsPhpAndLegacyAliases()
    {
        Assert.True(ErpJwVocTypes.Matches("RMP", ErpJwVocTypes.MetalPurchaseAliases));
        Assert.True(ErpJwVocTypes.Matches("MMP", ErpJwVocTypes.MetalPurchaseAliases));
        Assert.True(ErpJwVocTypes.Matches("RIN", ErpJwVocTypes.AliasesForTab("jw_retail_sales")));
        Assert.True(ErpJwVocTypes.Matches("RSI", ErpJwVocTypes.AliasesForTab("jw_retail_sales")));
        Assert.True(ErpJwVocTypes.IsSale("RIN"));
        Assert.True(ErpJwVocTypes.IsReturn("SRT"));
        Assert.False(ErpJwVocTypes.IsSale("RMP"));

        var companyTwo = ErpIndustryNav.FallbackCompanies("ECOM AE").Single(c => c.Id == 2);
        Assert.Equal("JW", companyTwo.Code);
        Assert.True(ErpIndustryNav.IsJewelleryCompany(companyTwo));
        Assert.Equal(
            "/erp/jewellery-fixing-app?company=2&tab=jw_metal_purchase",
            ErpJewelleryModuleChrome.AppHref("/erp/jewellery-fixing-app", 2, ("tab", "jw_metal_purchase")));
    }
}
