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
        Assert.Equal("SFX", ErpJwVocTypes.Canonical(null, "jw_sales_fixing_save"));
        Assert.Equal("PFX", ErpJwVocTypes.Canonical(null, "jw_purchase_fixing_save"));
        Assert.Equal("MSV", ErpJwVocTypes.Canonical(null, "jw_stock_verification_save"));
        Assert.Equal("VRV", ErpJwVocTypes.Canonical(null, "jw_tourist_vat_save"));
        Assert.Equal("REP", ErpJwVocTypes.Canonical(null, "jw_repair_receipt_save"));
    }

    [Fact]
    public async Task NextVoucherNumberKeepsExplicitValueWithoutOpeningDb()
    {
        var next = await ErpJwVocNumbering.NextNoAsync(
            null!,
            null,
            "epc_jewel_voucher",
            2,
            "RMP",
            "HO",
            17);
        Assert.Equal(17, next);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ErpJwVocNumbering.NextNoAsync(null!, null, "not_a_jewel_table", 2, "RMP", "HO", 0));
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
        Assert.Equal(
            "/erp/jewellery/voucher-save?company=2",
            ErpJewelleryModuleChrome.AppHref("/erp/jewellery/voucher-save", 2));
    }
}
