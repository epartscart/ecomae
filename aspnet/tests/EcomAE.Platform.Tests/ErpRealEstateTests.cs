using System.Globalization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpRealEstateTests
{
    [Fact]
    public void CalcUsesDecimalMoneyAndDocumentedKpis()
    {
        Assert.Equal(1_050_000.00m, ErpRealEstateCalc.AcquisitionCost(1_000_000m, 50_000m));
        Assert.Equal(1_150_000.00m, ErpRealEstateCalc.CarryingValue(1_050_000m, 100_000m, 0m));
        Assert.Equal(50_000.00m, ErpRealEstateCalc.Unrealized(1_200_000m, 1_150_000m));
        Assert.Equal(80_000.00m, ErpRealEstateCalc.RealizedGain(1_230_000m, 1_150_000m));
        Assert.Equal(1_230_000.00m, ErpRealEstateCalc.NetSaleProceeds(1_300_000m, 20_000m, 50_000m));
        Assert.Equal(0.08m, ErpRealEstateCalc.GrossYield(80_000m, 1_000_000m));
        Assert.Equal(55_000.00m, ErpRealEstateCalc.NetOperatingIncome(80_000m, 25_000m));
        Assert.Equal(0.055m, ErpRealEstateCalc.NetYield(55_000m, 1_000_000m));
        Assert.Equal(0.10m, ErpRealEstateCalc.CashOnCash(80_000m, 30_000m, 500_000m));
        Assert.Equal(0.75m, ErpRealEstateCalc.Occupancy(3, 4));
        Assert.Equal(0.90m, ErpRealEstateCalc.CollectionEfficiency(90_000m, 100_000m));
        Assert.Equal(0.50m, ErpRealEstateCalc.LoanToValue(500_000m, 1_000_000m));
        Assert.Equal(2.00m, ErpRealEstateCalc.DebtServiceCoverage(80_000m, 40_000m));
        Assert.Equal(0.20m, ErpRealEstateCalc.ProjectMargin(1_000_000m, 800_000m));
        Assert.Equal(250_000.00m, ErpRealEstateCalc.CostToComplete(1_000_000m, 750_000m));
        Assert.Contains(ErpRealEstateCalc.Definitions, d => d.Key == "unrealized" && d.Formula.Contains("IAS 40 through P&L", StringComparison.Ordinal));
        Assert.Contains(ErpRealEstateCalc.Definitions, d => d.Currency == "AED");
    }

    [Fact]
    public void CertificateNetAppliesAdvanceRetentionAndVat()
    {
        var net = ErpRealEstateCalc.CertificateNet(
            previousCertified: 100_000m,
            currentMeasured: 50_000m,
            materialsOnSite: 10_000m,
            approvedVariations: 5_000m,
            advanceRecovery: 8_000m,
            retention: 10_000m,
            otherDeductions: 2_000m,
            vatAmount: 7_500m);
        Assert.Equal(152_500.00m, net);
    }

    [Fact]
    public void XirrUsesDatedCashFlows()
    {
        var rate = ErpRealEstateCalc.Xirr(
        [
            new(new DateOnly(2024, 1, 1), -1_000_000m),
            new(new DateOnly(2025, 1, 1), 1_100_000m),
        ]);
        Assert.InRange(rate, 0.099m, 0.101m);
        Assert.Equal(0m, ErpRealEstateCalc.Xirr([new(new DateOnly(2024, 1, 1), 100m)]));
    }

    [Fact]
    public void RentScheduleSplitsAnnualRentByFrequency()
    {
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 12, 31);
        Assert.Equal(12, ErpRealEstateCalc.PeriodCount("monthly", start, end));
        Assert.Equal(4, ErpRealEstateCalc.PeriodCount("quarterly", start, end));
        Assert.Equal(10_000.00m, ErpRealEstateCalc.InstalmentAmount(120_000m, "monthly"));
        Assert.Equal(30_000.00m, ErpRealEstateCalc.InstalmentAmount(120_000m, "quarterly"));
    }

    [Fact]
    public void OpportunityRowsAreNeverOwnedAssets()
    {
        var opportunity = new ErpRealEstateRow(1, "opportunity", "OPP-1", "Marina tower", "screening", 2_000_000m, 1_900_000m, "Dubai", false);
        var owned = new ErpRealEstateRow(2, "property", "PR-1", "Marina tower", "active", 1_950_000m, 2_100_000m, "Dubai", true);
        Assert.False(opportunity.OwnedAsset);
        Assert.True(owned.OwnedAsset);
        Assert.Equal(150_000.00m, ErpRealEstateCalc.Unrealized(owned.Amount2, owned.Amount));
    }

    [Fact]
    public void RoutesAndNavOpenForRealEstateIndustryOnly()
    {
        Assert.Equal("real_estate", IndustrySpecificRoutePolicy.RequiredIndustry("/erp/real-estate-app"));
        Assert.Equal("real_estate", IndustrySpecificRoutePolicy.RequiredIndustry("/erp/real-estate/save"));
        Assert.True(IndustrySpecificRoutePolicy.Allows("real_estate", null, null, "real_estate"));
        Assert.False(IndustrySpecificRoutePolicy.Allows("real_estate", "auto_parts", null, "jewellery"));
        var company = new ErpCompanyDigest(4, "REAL", "Real Estate Company", "AED", "AE", "real-estate-development", true);
        Assert.True(ErpIndustryNav.IsRealEstateCompany(company));
        Assert.True(IndustrySpecificRoutePolicy.Allows("real_estate", "auto_parts", company));
        Assert.True(ErpIndustryNav.ShowRealEstateModules("core", company));
        Assert.False(ErpIndustryNav.ShowRealEstateModules("core", new ErpCompanyDigest(1, "MAIN", "Main", "AED", "AE", "", true)));
    }

    [Fact]
    public void SearchAndCapabilitySurfaceTheWorkspace()
    {
        var catalog = ErpGlobalSearchCatalog.Build(jewellery: false, fitOut: false, realEstate: true);
        Assert.Equal("Real estate workspace", catalog[0].Label);
        Assert.Contains(catalog, e => e.Href == "/erp/real-estate-app?tab=opportunities");
        Assert.True(ErpPhpTabRouteMap.TryMapTab("real_estate", out var href));
        Assert.Equal("/erp/real-estate-app", href);
        Assert.Contains(ErpCapabilityCatalog.Groups, g => g.Key == "real_estate" && g.Href == "/erp/real-estate-app");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "projects/real_estate");
        Assert.Contains("IErpRealEstateWriteService", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Program.cs")), StringComparison.Ordinal);
        Assert.Contains("HandleRealEstateSaveAsync", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs")), StringComparison.Ordinal);
        var razor = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpRealEstateApp.razor"));
        Assert.Contains("@((_snap.Kpis.GrossYield * 100).ToString(\"0.00\", CultureInfo.InvariantCulture))%", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@(_snap.Kpis.GrossYield * 100).ToString", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteServiceKeepsPostedHistoryAndChequeRules()
    {
        var src = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpRealEstateWriteService.cs"));
        Assert.Contains("It is not cash until bank clearance", src, StringComparison.Ordinal);
        Assert.Contains("Acquisition cost was not overwritten", src, StringComparison.Ordinal);
        Assert.Contains("Ejari stays unregistered", src, StringComparison.Ordinal);
        Assert.Contains("Prior lease", src, StringComparison.Ordinal);
        Assert.Contains("History preserved", src, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM `epc_erp_re_leases`", src, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM `epc_erp_re_acquisitions`", src, StringComparison.Ordinal);
        Assert.Contains("status` IN ('deposited','held','due_for_deposit')", src, StringComparison.Ordinal);
        Assert.Contains("ErpPostedTransactionPolicy", src, StringComparison.Ordinal);
        Assert.Contains("voided", src, StringComparison.Ordinal);
        var razor = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpRealEstateApp.razor"));
        Assert.Contains("void (keep on file) or delete", razor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("value=\"void\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"reverse\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"delete\"", razor, StringComparison.Ordinal);
        Assert.Contains(">Delete<", razor, StringComparison.Ordinal);
        Assert.Contains(">Void<", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("else if (ErpPostedTransactionPolicy.CanVoid", razor, StringComparison.Ordinal);
        Assert.Contains("ErpRealEstateIfrs", src, StringComparison.Ordinal);
        Assert.Contains("PostIfrsAsync", src, StringComparison.Ordinal);
        Assert.Contains("IAS 40 investment property", src, StringComparison.Ordinal);
        Assert.DoesNotContain("Fair-value changes stay off the ledger", src, StringComparison.Ordinal);
        Assert.Contains("income statement", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpRealEstateApp.razor")), StringComparison.Ordinal);
    }

    [Fact]
    public void IfrsRecipesSplitBalanceSheetAndIncomeStatement()
    {
        Assert.True(ErpRealEstateIfrs.IsOffStatement("opportunity"));
        Assert.False(ErpRealEstateIfrs.IsOffStatement("acquisition"));
        Assert.Equal("balance_sheet", ErpRealEstateIfrs.StatementFor(ErpRealEstateIfrs.InvestmentProperty));
        Assert.Equal("balance_sheet", ErpRealEstateIfrs.StatementFor(ErpRealEstateIfrs.TenantDeposits));
        Assert.Equal("balance_sheet", ErpRealEstateIfrs.StatementFor(ErpRealEstateIfrs.ChequesUnderCollection));
        Assert.Equal("balance_sheet", ErpRealEstateIfrs.StatementFor(ErpRealEstateIfrs.PropertyUnderConstruction));
        Assert.Equal("income_statement", ErpRealEstateIfrs.StatementFor(ErpRealEstateIfrs.RentalIncome));
        Assert.Equal("income_statement", ErpRealEstateIfrs.StatementFor(ErpRealEstateIfrs.FairValueGain));
        Assert.Equal("income_statement", ErpRealEstateIfrs.StatementFor(ErpRealEstateIfrs.DevelopmentRevenue));
        Assert.Equal("income_statement", ErpRealEstateIfrs.StatementFor(ErpRealEstateIfrs.PropertyOpex));

        var acq = ErpRealEstateIfrs.Acquisition(1_050_000m);
        Assert.Equal(2, acq.Count);
        Assert.Equal(1_050_000m, acq.Sum(l => l.Debit));
        Assert.Equal(acq.Sum(l => l.Debit), acq.Sum(l => l.Credit));
        Assert.Contains(acq, l => l.Code == ErpRealEstateIfrs.InvestmentProperty && l.Debit == 1_050_000m);
        Assert.Contains(acq, l => l.Code == ErpRealEstateIfrs.Bank && l.Credit == 1_050_000m);

        var fvUp = ErpRealEstateIfrs.FairValueMovement(1_000_000m, 1_200_000m);
        Assert.Contains(fvUp, l => l.Code == ErpRealEstateIfrs.FairValueGain && l.Credit == 200_000m);
        var fvDown = ErpRealEstateIfrs.FairValueMovement(1_000_000m, 900_000m);
        Assert.Contains(fvDown, l => l.Code == ErpRealEstateIfrs.FairValueLoss && l.Debit == 100_000m);
        Assert.Empty(ErpRealEstateIfrs.FairValueMovement(1_000_000m, 1_000_000m));

        var disposal = ErpRealEstateIfrs.Disposal(1_230_000m, 1_150_000m);
        Assert.Equal(disposal.Sum(l => l.Debit), disposal.Sum(l => l.Credit));
        Assert.Contains(disposal, l => l.Code == ErpRealEstateIfrs.DisposalGain && l.Credit == 80_000m);

        var deposit = ErpRealEstateIfrs.DepositHeld(20_000m);
        Assert.Contains(deposit, l => l.Code == ErpRealEstateIfrs.TenantDeposits && l.Credit == 20_000m);
        Assert.Equal("balance_sheet", ErpRealEstateIfrs.StatementFor(deposit[1].Code));

        var cheque = ErpRealEstateIfrs.ChequeReceived(12_000m);
        Assert.Contains(cheque, l => l.Code == ErpRealEstateIfrs.ChequesUnderCollection && l.Debit == 12_000m);
        Assert.Contains(cheque, l => l.Code == ErpRealEstateIfrs.RentalIncome && l.Credit == 12_000m);
        var cleared = ErpRealEstateIfrs.ChequeCleared(12_000m);
        Assert.Contains(cleared, l => l.Code == ErpRealEstateIfrs.Bank && l.Debit == 12_000m);

        var opex = ErpRealEstateIfrs.OperatingExpense(5_000m);
        Assert.Equal("income_statement", ErpRealEstateIfrs.StatementFor(opex[0].Code));
        var capex = ErpRealEstateIfrs.CapitalExpenditure(5_000m);
        Assert.Equal("balance_sheet", ErpRealEstateIfrs.StatementFor(capex[0].Code));

        var cert = ErpRealEstateIfrs.Certificate(152_500m);
        Assert.Contains(cert, l => l.Code == ErpRealEstateIfrs.PropertyUnderConstruction && l.Debit == 152_500m);
        Assert.Contains(cert, l => l.Code == ErpRealEstateIfrs.ContractorPayable && l.Credit == 152_500m);

        var sale = ErpRealEstateIfrs.DevelopmentSale(800_000m, 500_000m);
        Assert.Contains(sale, l => l.Code == ErpRealEstateIfrs.DevelopmentRevenue && l.Credit == 800_000m);
        Assert.Equal(sale.Sum(l => l.Debit), sale.Sum(l => l.Credit));
        Assert.All(ErpRealEstateIfrs.Accounts, a => Assert.Contains(a.Type, new[] { "asset", "liability", "revenue", "expense" }));
        Assert.Contains(ErpRealEstateIfrs.Accounts, a => a.Type is "asset" or "liability" && a.Statement == "balance_sheet");
        Assert.Contains(ErpRealEstateIfrs.Accounts, a => a.Type is "revenue" or "expense" && a.Statement == "income_statement");
    }

    [Fact]
    public void PostedTransactionsCannotBeEdited_VoidAndDeleteStayAvailable()
    {
        Assert.True(ErpPostedTransactionPolicy.IsPosted("acquisition", "completed"));
        Assert.False(ErpPostedTransactionPolicy.CanModify("acquisition", "completed"));
        Assert.True(ErpPostedTransactionPolicy.CanDelete("acquisition", "completed"));
        Assert.True(ErpPostedTransactionPolicy.CanVoid("acquisition", "completed"));
        Assert.True(ErpPostedTransactionPolicy.CanReverse("acquisition", "completed"));
        Assert.True(ErpPostedTransactionPolicy.CanDelete("acquisition", "draft"));
        Assert.True(ErpPostedTransactionPolicy.CanVoid("acquisition", "draft"));
        Assert.False(ErpPostedTransactionPolicy.CanDelete("acquisition", "voided"));
        Assert.False(ErpPostedTransactionPolicy.CanVoid("property", "active"));
        Assert.False(ErpPostedTransactionPolicy.CanModify("cheque", "cleared"));
        Assert.True(ErpPostedTransactionPolicy.CanModify("cheque", "deposited"));
        Assert.True(ErpPostedTransactionPolicy.IsVoided("voided"));
        Assert.Equal(ErpPostedTransactionPolicy.RefuseModify, "Posted transactions cannot be modified. Void or delete them.");
    }

    private static string Find(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
