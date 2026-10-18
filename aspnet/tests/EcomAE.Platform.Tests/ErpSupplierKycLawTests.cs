using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpSupplierKycLawTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(54999.99, false)]
    [InlineData(55000, true)]
    [InlineData(100000, true)]
    [InlineData(250000, true)]
    public void CoveredBuyStartsAtFiftyFiveThousand(decimal amount, bool required)
        => Assert.Equal(required, ErpSupplierKycLaw.RequiresCompleteFile(amount));

    [Fact]
    public void CompleteLegalPersonFilePassesAndABuyAboveOneHundredThousandUsesTheSameFile()
    {
        Assert.True(ErpSupplierKycLaw.FileComplete(Complete()));
        Assert.Equal(string.Empty, ErpSupplierKycLaw.MissingSummary(Complete()));
        Assert.True(ErpSupplierKycLaw.RequiresCompleteFile(ErpSupplierKycLaw.NamedBuyAed));
        Assert.Equal(55_000m, ErpSupplierKycLaw.CoveredAmountAed);
        Assert.Equal(100_000m, ErpSupplierKycLaw.NamedBuyAed);
        Assert.Equal(25m, ErpSupplierKycLaw.BeneficialOwnerShare);
        Assert.Equal(5, ErpSupplierKycLaw.RetentionYears);
        Assert.Contains("Memorandum of association", ErpSupplierKycLaw.FileMarks, StringComparer.Ordinal);
        Assert.Contains("Authority to act", ErpSupplierKycLaw.FileMarks, StringComparer.Ordinal);
    }

    [Fact]
    public void ApprovalPackDoesNotRequireTheCoveredBuyFile()
    {
        var ready = Complete() with
        {
            Owners = [new("Fatima Ali", 100m, "AE", "784-1990-1234567-1", "owner")],
            Documents = [ErpSupplierOnboard.DocTradeLicense],
        };
        Assert.True(ErpSupplierOnboard.CanApprove(ErpSupplierOnboard.Evaluate(ready)));
        Assert.False(ErpSupplierKycLaw.FileComplete(ready));
        var missing = ErpSupplierKycLaw.MissingSummary(ready);
        Assert.Contains("Memorandum of association", missing, StringComparison.Ordinal);
        Assert.Contains("Articles of association", missing, StringComparison.Ordinal);
        Assert.Contains("Authorized signatory identity", missing, StringComparison.Ordinal);
        Assert.Contains("Authority to act", missing, StringComparison.Ordinal);
    }

    [Fact]
    public void UnregisteredSupplierDoesNotNeedATaxNumber()
    {
        var state = Complete() with { VatRegistered = false, Trn = "" };
        Assert.Contains(ErpSupplierKycLaw.Assess(state), item => item.Label == "Tax registration number when registered" && item.Met);
    }

    [Fact]
    public void SuppliersPageShowsTheLawAndTheDocumentFile()
    {
        var page = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpSuppliersApp.razor"));
        Assert.Contains("Supplier AML and KYC", page, StringComparison.Ordinal);
        Assert.Contains("ErpSupplierKycLaw.Decree", page, StringComparison.Ordinal);
        Assert.Contains("Cabinet Resolution No. 134 of 2025", page, StringComparison.Ordinal);
        Assert.Equal("Federal Decree-Law No. 10 of 2025", ErpSupplierKycLaw.Decree);
        Assert.Equal("Cabinet Resolution No. 134 of 2025", ErpSupplierKycLaw.Resolution);
        Assert.Contains("AED 55,000", page, StringComparison.Ordinal);
        Assert.Contains("AED 100,000", page, StringComparison.Ordinal);
        Assert.Contains("KycFindings", page, StringComparison.Ordinal);
        Assert.Contains("value=\"memorandum\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"articles\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"senior_management\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"beneficial_owner_id\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"signatory_id\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"signatory_authority\"", page, StringComparison.Ordinal);
        Assert.Contains("trade_license", page, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", page, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", page, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Classic", page, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", page, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", page, StringComparison.Ordinal);

        var aml = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpAmlComplianceApp.razor"));
        Assert.Contains("Save a KYC record", aml, StringComparison.Ordinal);
        Assert.Contains("AED 55,000", aml, StringComparison.Ordinal);
        Assert.Contains("/erp/suppliers-app", aml, StringComparison.Ordinal);

        Assert.Contains("KYC", ErpAreaUseGuide.Find("purchasing")!.Use, StringComparison.Ordinal);
        Assert.Contains("RequireKycForCoveredPurchaseAsync", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPurchaseOrderWriteService.cs")), StringComparison.Ordinal);
        Assert.Contains("RequireKycForCoveredPurchaseAsync", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPurchaseInvoiceWriteService.cs")), StringComparison.Ordinal);
        Assert.Contains("RequireKycForCoveredPurchaseAsync", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpJwBarcodePurchaseWriteService.cs")), StringComparison.Ordinal);
        Assert.Contains("RequireKycForJewelPurchaseAsync", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpJwVoucherWriteService.cs")), StringComparison.Ordinal);
        Assert.Contains("RequireKycForCoveredPurchaseAsync", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpOrderFulfillmentWriteService.cs")), StringComparison.Ordinal);
    }

    private static ErpSupplierOnboardState Complete() => new(
        "Gold House LLC",
        "V-0004",
        21,
        "AED",
        "Warehouse 4",
        "Sheikh Zayed Rd",
        "Dubai",
        "Fatima Ali",
        "+971501234567",
        "Local",
        "Jewellery",
        "AE",
        true,
        "100123456789003",
        [new("Fatima Ali", 60m, "AE", "784-1990-1234567-1", "ubo")],
        [
            ErpSupplierOnboard.DocTradeLicense,
            ErpSupplierKycLaw.DocMemorandum,
            ErpSupplierKycLaw.DocArticles,
            ErpSupplierKycLaw.DocBeneficialOwnerId,
            ErpSupplierKycLaw.DocSignatoryId,
            ErpSupplierKycLaw.DocSignatoryAuthority,
        ]);

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir!.FullName, relative);
    }
}
