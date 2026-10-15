using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using MySqlConnector;
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
    public void LandlordScheduleChequeAndProfitStayApart()
    {
        var start = new DateOnly(2027, 1, 1);
        var end = new DateOnly(2027, 12, 31);
        var schedule = ErpRealEstateCalc.BuildSchedule(120_000m, "quarterly", start, end);
        Assert.Equal(4, schedule.Count);
        Assert.Equal(new DateOnly(2027, 1, 1), schedule[0].Due);
        Assert.Equal(new DateOnly(2027, 4, 1), schedule[1].Due);
        Assert.Equal(new DateOnly(2027, 7, 1), schedule[2].Due);
        Assert.Equal(new DateOnly(2027, 10, 1), schedule[3].Due);
        Assert.All(schedule, line => Assert.Equal(30_000.00m, line.Amount));
        Assert.Equal(120_000.00m, ErpRealEstateCalc.ScheduleTotal(schedule));

        var uneven = ErpRealEstateCalc.BuildSchedule(100_000m, "monthly", start, end);
        Assert.Equal(12, uneven.Count);
        Assert.Equal(100_000.00m, ErpRealEstateCalc.ScheduleTotal(uneven));

        Assert.Equal(120_000.00m, ErpRealEstateCalc.RecognizedRent(120_000m, start, end, start, end));
        Assert.Equal(29_589.04m, ErpRealEstateCalc.RecognizedRent(120_000m, start, end, start, new DateOnly(2027, 3, 31)));
        Assert.Equal(0m, ErpRealEstateCalc.RecognizedRent(120_000m, start, end, new DateOnly(2028, 1, 1), new DateOnly(2028, 12, 31)));
        Assert.False(ErpRealEstateCalc.CountsAsCash("received"));
        Assert.False(ErpRealEstateCalc.CountsAsCash("deposited"));
        Assert.True(ErpRealEstateCalc.CountsAsCash("cleared"));

        var today = new DateOnly(2027, 1, 1);
        Assert.Equal("due_today", ErpRealEstateCalc.ChequeBucket("received", today, today));
        Assert.Equal("due_tomorrow", ErpRealEstateCalc.ChequeBucket("in_custody", today.AddDays(1), today));
        Assert.Equal("due_7", ErpRealEstateCalc.ChequeBucket("received", today.AddDays(5), today));
        Assert.Equal("due_15", ErpRealEstateCalc.ChequeBucket("received", today.AddDays(10), today));
        Assert.Equal("due_30", ErpRealEstateCalc.ChequeBucket("received", today.AddDays(20), today));
        Assert.Equal("overdue_not_deposited", ErpRealEstateCalc.ChequeBucket("received", today.AddDays(-3), today));
        Assert.Equal("awaiting_clear", ErpRealEstateCalc.ChequeBucket("deposited", today, today));
        Assert.Equal("returned", ErpRealEstateCalc.ChequeBucket("bounced", today, today));
        Assert.Equal("cleared", ErpRealEstateCalc.ChequeBucket("cleared", today, today));
        Assert.Equal("current", ErpRealEstateCalc.AgeingBucket(today, today));
        Assert.Equal("1_30", ErpRealEstateCalc.AgeingBucket(today, today.AddDays(15)));
        Assert.Equal("over_90", ErpRealEstateCalc.AgeingBucket(today, today.AddDays(120)));
        Assert.True(ErpRealEstateCalc.TenanciesOverlap(start, end, new DateOnly(2027, 6, 1), new DateOnly(2028, 5, 31)));
        Assert.False(ErpRealEstateCalc.TenanciesOverlap(start, end, new DateOnly(2028, 1, 1), new DateOnly(2028, 12, 31)));
        Assert.False(ErpRealEstateCalc.ReducesOperatingProfit("painting", capital: true));
        Assert.False(ErpRealEstateCalc.ReducesOperatingProfit("mortgage_interest", capital: false));
        Assert.False(ErpRealEstateCalc.ReducesOperatingProfit("depreciation", capital: false));
        Assert.True(ErpRealEstateCalc.ReducesOperatingProfit("repairs", capital: false));
        Assert.Equal(100_000.00m, ErpRealEstateCalc.LandlordNoi(120_000m, 20_000m));
        Assert.Equal(90_000.00m, ErpRealEstateCalc.NetCashFlow(120_000m, 30_000m));
        Assert.Equal(20_000.00m, ErpRealEstateCalc.MissingChequeAmount(30_000m, 10_000m));
        Assert.Equal(0m, ErpRealEstateCalc.MissingChequeAmount(30_000m, 30_000m));
        var codes = ErpRealEstateCalc.BulkUnitCodes("A", 101, 100);
        Assert.Equal(100, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("A-101", codes[0]);
        Assert.Equal("A-200", codes[99]);
        Assert.Equal(500, ErpRealEstateCalc.BulkUnitCodes("U", 1, 800).Count);
    }

    [Fact]
    public void WorkbookRejectsClearedChequesAndRoundTripsSheets()
    {
        var cleared = ErpRealEstateWorkbook.Parse(
            "PDC_Cheques",
            "ChequeNo*,Bank*,Amount*,ChequeDate*,Status\n000456,ENBD,30000,2027-01-01,cleared\n");
        Assert.Contains(cleared.Errors, e => e.Contains("not mark cash", StringComparison.OrdinalIgnoreCase));

        var units = ErpRealEstateWorkbook.Parse("Units", "PropertyCode*,UnitCode*\n,U1\nBLD,U1\nBLD,U1\n");
        Assert.Contains(units.Errors, e => e.Contains("required", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(units.Errors, e => e.Contains("duplicate", StringComparison.OrdinalIgnoreCase));

        var agreement = ErpRealEstateWorkbook.Parse(
            "Agreements",
            "Code*,PropertyCode*,TenantCode*,Start*,End*,AnnualRent*,Frequency*\nAGR-101,BLD-A,TEN-1,2027-01-01,2027-12-31,120000,quarterly\n");
        Assert.Empty(agreement.Errors);
        Assert.True(agreement.Applies);

        var schedule = ErpRealEstateWorkbook.Parse("Rent_Schedule", "AgreementCode*,DueDate*,Amount*\nAGR-101,2027-01-01,30000\n");
        Assert.Empty(schedule.Errors);
        Assert.False(schedule.Applies);
        Assert.Contains("not imported twice", schedule.HoldReason, StringComparison.OrdinalIgnoreCase);

        var file = ErpRealEstateWorkbook.IsInlineFile("data:image/jpeg;base64,AAAA");
        Assert.True(file);
        Assert.False(ErpRealEstateWorkbook.IsInlineFile("cheques/000456/front.jpg"));

        var bytes = ErpRealEstateWorkbook.Template();
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
        using var stream = new MemoryStream(bytes);
        var csv = ErpRealEstateWorkbook.SheetToCsv(stream, "PDC_Cheques");
        Assert.Contains("000456", csv, StringComparison.Ordinal);
        Assert.Contains("ENBD", csv, StringComparison.Ordinal);
        foreach (var sheet in ErpRealEstateWorkbook.TemplateSheets)
        {
            stream.Position = 0;
            var text = ErpRealEstateWorkbook.SheetToCsv(stream, sheet);
            Assert.False(string.IsNullOrWhiteSpace(text), sheet);
        }

        var snap = SampleBoardSnap();
        var package = ErpRealEstateWorkbook.ExportPackage(snap);
        Assert.Equal((byte)'P', package[0]);
        Assert.Equal((byte)'K', package[1]);
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(package), System.IO.Compression.ZipArchiveMode.Read);
        Assert.Contains(zip.Entries, e => e.Name == "RealEstate_Registers.xlsx");
        Assert.Contains(zip.Entries, e => e.Name == "document-manifest.csv");
        using var readme = zip.GetEntry("README.txt")!.Open();
        using var reader = new StreamReader(readme);
        Assert.Contains("not a full restoration archive", reader.ReadToEnd(), StringComparison.OrdinalIgnoreCase);

        var src = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpRealEstateWriteService.cs"));
        Assert.Contains("bulk_units", src, StringComparison.Ordinal);
        Assert.Contains("ALLOW_OVERLAP", src, StringComparison.Ordinal);
        Assert.Contains("does not store the file", src, StringComparison.Ordinal);
        Assert.Contains("import_sheet", src, StringComparison.Ordinal);
        Assert.Contains("Rollback removes only draft rows with no journal", src, StringComparison.Ordinal);
        var razor = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpRealEstateApp.razor"));
        Assert.Contains("PDCs in hand", razor, StringComparison.Ordinal);
        Assert.Contains("Generate units", razor, StringComparison.Ordinal);
        Assert.Contains("Cheques in hand are not cash", razor, StringComparison.Ordinal);
        Assert.Contains("Download import template", razor, StringComparison.Ordinal);
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
        Assert.Equal("real_estate", IndustrySpecificRoutePolicy.RequiredIndustry("/erp/real-estate/report"));
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
        Assert.Contains(catalog, e => e.Href == "/erp/real-estate/report?format=html");
        Assert.True(ErpPhpTabRouteMap.TryMapTab("real_estate", out var href));
        Assert.Equal("/erp/real-estate-app", href);
        Assert.Contains(ErpCapabilityCatalog.Groups, g => g.Key == "real_estate" && g.Href == "/erp/real-estate-app");
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "projects/real_estate");
        Assert.Contains("IErpRealEstateWriteService", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Program.cs")), StringComparison.Ordinal);
        Assert.Contains("HandleRealEstateSaveAsync", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs")), StringComparison.Ordinal);
        Assert.Contains("HandleRealEstateReportAsync", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs")), StringComparison.Ordinal);
        Assert.Contains("re-board-dock", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpRealEstateApp.razor")), StringComparison.Ordinal);
        Assert.Contains("/erp/real-estate/report?format=", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpRealEstateApp.razor")), StringComparison.Ordinal);
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
        Assert.Contains("Prior cleared allocation and cash journal were left intact", src, StringComparison.Ordinal);
        Assert.Contains("save_boq_line", src, StringComparison.Ordinal);
        Assert.Contains("save_escrow_receipt", src, StringComparison.Ordinal);
        Assert.Contains("generate_instalments", src, StringComparison.Ordinal);
        Assert.Contains("allocate_shared_cost", src, StringComparison.Ordinal);
        Assert.Contains("reset_portfolio", src, StringComparison.Ordinal);
        Assert.Contains("ErpRealEstatePortfolioReset", src, StringComparison.Ordinal);
        Assert.Contains("income statement", razor, StringComparison.Ordinal);
        Assert.Contains("save_boq_line", razor, StringComparison.Ordinal);
        Assert.Contains("Cheque maturity calendar", razor, StringComparison.Ordinal);
        Assert.Contains("\"calendar\", \"Calendars\"", razor, StringComparison.Ordinal);
        Assert.Contains("reset_portfolio", razor, StringComparison.Ordinal);
        Assert.Contains("Empty portfolio for new entry", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void PortfolioResetClearsOperationalRowsNotUsers()
    {
        var src = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpRealEstatePortfolioReset.cs"));
        Assert.Contains("epc_erp_properties", src, StringComparison.Ordinal);
        Assert.Contains("epc_erp_re_leases", src, StringComparison.Ordinal);
        Assert.Contains("epc_erp_re_projects", src, StringComparison.Ordinal);
        Assert.Contains("`reference` LIKE 'RE-%'", src, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM `users`", src, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", src, StringComparison.Ordinal);
    }

    [Fact]
    public void BoardPackEmitsColouredPdfWordAndHtml()
    {
        var snap = SampleBoardSnap();
        var utc = new DateTime(2026, 10, 13, 4, 0, 0, DateTimeKind.Utc);
        var pdf = ErpRealEstateReportPack.Pdf(snap, "REAL investment real estate", utc);
        Assert.True(pdf.Length > 20_000);
        Assert.Equal((byte)'%', pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);

        var docx = ErpRealEstateReportPack.Docx(snap, "REAL investment real estate", utc);
        Assert.Equal((byte)'P', docx[0]);
        Assert.Equal((byte)'K', docx[1]);
        Assert.Contains("word/document.xml", System.Text.Encoding.UTF8.GetString(docx), StringComparison.Ordinal);

        var html = ErpRealEstateReportPack.Html(snap, "REAL investment real estate", "/erp/real-estate/report?format=pdf", "/erp/real-estate/report?format=docx", utc);
        Assert.Contains("Executive summary", html, StringComparison.Ordinal);
        Assert.Contains("Investment real estate", html, StringComparison.Ordinal);
        Assert.Contains("Gross yield", html, StringComparison.Ordinal);
        Assert.Contains("Annual rent", html, StringComparison.Ordinal);
        Assert.Contains("data:image/jpeg;base64,", html, StringComparison.Ordinal);
        Assert.Contains("#c4a35a", html, StringComparison.Ordinal);
        Assert.Contains("Download PDF", html, StringComparison.Ordinal);
        Assert.Contains("Download Word", html, StringComparison.Ordinal);
        Assert.Contains("SLICE-2026", html, StringComparison.Ordinal);

        var sky = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpRealEstateReportVisual.cs"));
        Assert.Contains("PaintSkyline", sky, StringComparison.Ordinal);
        Assert.Contains("INVESTMENT REAL ESTATE", sky, StringComparison.Ordinal);
        Assert.Contains("C4A35A", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpRealEstateReportPack.cs")), StringComparison.Ordinal);
        Assert.Equal("ecomae-investment-real-estate-real-20261013", ErpRealEstateReportPack.FileStem("REAL", utc));
    }

    private static ErpRealEstateSnapshot SampleBoardSnap()
    {
        static ErpRealEstateRow Row(long id, string kind, string code, string name, string status, decimal amount, decimal amount2 = 0, string detail = "", bool owned = false)
            => new(id, kind, code, name, status, amount, amount2, detail, owned);

        return new ErpRealEstateSnapshot(
            new ErpRealEstateKpis(2, 2, 4, 1, 3, 1, 1_850_000m, 2_200_000m, 350_000m, 180_000m, 0.0818m, 0.75m, 0.88m, 420_000m, 320_000m),
            [Row(1, "opportunity", "OPP-1", "Marina screening", "screening", 2_000_000m, 1_900_000m, "Dubai")],
            [Row(1, "property", "PR-1", "Marina tower", "active", 1_850_000m, 2_200_000m, "Dubai", true)],
            [Row(3, "unit", "U-3", "Tower A 1204", "sold", 600_000m, 0, "project 3", true), Row(4, "unit", "U-4", "Tower A 1205", "available", 400_000m, 0, "project 3", true)],
            [Row(1, "acquisition", "ACQ-1", "Seller LLC", "posted", 1_850_000m, 0, "completed")],
            [],
            [Row(1, "valuation", "VAL-1", "Marina tower", "approved", 2_200_000m, 2_150_000m, "IAS 40")],
            [Row(1, "tenant", "TN-1", "Retail tenant", "active", 0, 0, "Ejari")],
            [Row(1, "lease", "LS-1", "Retail tenant", "active", 180_000m, 0, "annual")],
            [Row(6, "cheque", "CHQ-6", "Retail tenant", "bounced", 45_000m, 0, "prior clear journal kept")],
            [Row(1, "deposit", "DEP-1", "Retail tenant", "held", 20_000m, 0, "2200")],
            [],
            [Row(3, "project", "SLICE-2026", "Off-plan tower", "active", 1_000_000m, 0, "escrow required")],
            [Row(1, "sale", "SAL-1", "Buyer One", "reserved", 500_000m, 200_000m, "off-plan")],
            [],
            [Row(1, "contract", "CTR-1", "Main contractor", "active", 800_000m, 0, "BOQ linked")],
            [Row(1, "certificate", "IPC-1", "Main contractor", "certified", 80_000m, 0, "escrow out")],
            [Row(1, "boq", "BOQ-1", "Structure package", "awarded", 25_000m, 0, "qty × rate")],
            [Row(1, "escrow", "ESC-IN", "Buyer collection", "posted", 200_000m, 0, "1140 / 2220"), Row(2, "escrow", "ESC-OUT", "Permitted works", "posted", 80_000m, 0, "disbursement")],
            [Row(1, "milestone", "MS-1", "Foundation", "complete", 40, 0, "%")],
            [Row(1, "instalment", "INS-1", "Sale 1", "collected", 200_000m, 200_000m, "milestone 1"), Row(2, "instalment", "INS-2", "Sale 1", "due", 300_000m, 0, "milestone 2")],
            [Row(1, "allocation", "ALLOC-1", "U-3", "posted", 600_000m, 0, "GIA"), Row(2, "allocation", "ALLOC-2", "U-4", "posted", 400_000m, 0, "GIA")],
            [],
            [],
            ErpRealEstateCalc.Definitions,
            "Sample board pack");
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

        Assert.Equal("balance_sheet", ErpRealEstateIfrs.StatementFor(ErpRealEstateIfrs.EscrowCash));
        Assert.Equal("balance_sheet", ErpRealEstateIfrs.StatementFor(ErpRealEstateIfrs.BuyerAdvances));
        var escrowIn = ErpRealEstateIfrs.EscrowReceipt(250_000m);
        Assert.Contains(escrowIn, l => l.Code == ErpRealEstateIfrs.EscrowCash && l.Debit == 250_000m);
        Assert.Contains(escrowIn, l => l.Code == ErpRealEstateIfrs.BuyerAdvances && l.Credit == 250_000m);
        var escrowOut = ErpRealEstateIfrs.EscrowDisbursement(80_000m, againstCertificate: true);
        Assert.Contains(escrowOut, l => l.Code == ErpRealEstateIfrs.ContractorPayable && l.Debit == 80_000m);
        Assert.Contains(escrowOut, l => l.Code == ErpRealEstateIfrs.EscrowCash && l.Credit == 80_000m);
        var advance = ErpRealEstateIfrs.OffPlanCollection(40_000m, intoEscrow: true, revenueAlreadyRecognized: false);
        Assert.Contains(advance, l => l.Code == ErpRealEstateIfrs.BuyerAdvances && l.Credit == 40_000m);
        Assert.DoesNotContain(advance, l => l.Code == ErpRealEstateIfrs.DevelopmentRevenue);
    }

    [Fact]
    public void ConstructionSliceFormulasAndGuards()
    {
        Assert.Equal(12_500.00m, ErpRealEstateCalc.BoqAmount(25m, 500m));
        Assert.Equal(150_000.00m, ErpRealEstateCalc.EscrowBalance(200_000m, 50_000m));
        Assert.True(ErpRealEstateCalc.EscrowDisbursementPermitted(200_000m, 50_000m, 50_000m));
        Assert.False(ErpRealEstateCalc.EscrowDisbursementPermitted(200_000m, 50_000m, 160_000m));
        Assert.Equal(200_000.00m, ErpRealEstateCalc.OffPlanInstalment(500_000m, 40m));
        Assert.Equal(80_000.00m, ErpRealEstateCalc.UnitCostOfSale(80_000m, 100_000m));
        Assert.Equal(100_000.00m, ErpRealEstateCalc.UnitCostOfSale(0m, 100_000m));

        var byArea = ErpRealEstateCalc.AllocateSharedCost(1_000_000m, [60m, 40m]);
        Assert.Equal(2, byArea.Count);
        Assert.Equal(600_000.00m, byArea[0]);
        Assert.Equal(400_000.00m, byArea[1]);
        Assert.Equal(1_000_000.00m, byArea.Sum());

        var equal = ErpRealEstateCalc.AllocateSharedCost(100_000m, [0m, 0m, 0m]);
        Assert.Equal(3, equal.Count);
        Assert.Equal(100_000.00m, equal.Sum());
        Assert.Equal(33_333.33m, equal[0]);
        Assert.Equal(33_333.34m, equal[2]);

        Assert.Contains(ErpRealEstateCalc.Definitions, d => d.Key == "escrow_balance");
        Assert.Contains(ErpRealEstateCalc.Definitions, d => d.Key == "boq_amount");
        Assert.True(ErpPostedTransactionPolicy.IsDraft("instalment", "due"));
        Assert.True(ErpPostedTransactionPolicy.IsPosted("escrow", "received"));
        Assert.True(ErpPostedTransactionPolicy.CanReverse("instalment", "collected"));
        Assert.False(ErpPostedTransactionPolicy.CanModify("allocation", "allocated"));
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

    [Fact]
    public async Task CompletedDocumentsPostToBalanceSheetAndIncomeStatement()
    {
        var (admin, cs) = ThrowawayCs();
        if (admin is null)
        {
            return;
        }

        await Exec(admin, "CREATE DATABASE `" + DbName(cs) + "`");
        try
        {
            var writes = new ErpRealEstateWriteService(
                new ThrowawayConnections(cs),
                new ErpAuditLogWriter(),
                new ErpGlPostingService(new ErpVoucherNumberService()));

            var opp = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "save_opportunity",
                Code: "OPP-IFRS",
                Name: "Marina tower",
                Amount: 1_000_000m,
                Amount2: 1_050_000m,
                LegalEntityId: 1,
                ConfirmWrites: true));
            Assert.True(opp.Succeeded, opp.Message);

            var bought = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "convert_opportunity",
                Id: opp.Id,
                Amount: 50_000m,
                LegalEntityId: 1,
                ActorId: 1,
                ConfirmWrites: true));
            Assert.True(bought.Succeeded, bought.Message);
            Assert.Contains("balance sheet", bought.Message, StringComparison.OrdinalIgnoreCase);

            var valuation = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "save_valuation",
                PropertyId: bought.Id,
                Code: "VAL-IFRS",
                Amount: 1_100_000m,
                Amount2: 1_200_000m,
                Amount3: 1_250_000m,
                LegalEntityId: 1,
                ActorId: 1,
                ConfirmWrites: true));
            Assert.True(valuation.Succeeded, valuation.Message);
            Assert.Contains("income statement", valuation.Message, StringComparison.OrdinalIgnoreCase);

            var deposit = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "save_deposit",
                LeaseId: 1,
                Amount: 20_000m,
                LegalEntityId: 1,
                ActorId: 1,
                ConfirmWrites: true));
            Assert.True(deposit.Succeeded, deposit.Message);

            var cheque = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "save_cheque",
                ChequeNo: "PDC-IFRS-1",
                Amount: 12_000m,
                PropertyId: bought.Id,
                LegalEntityId: 1,
                ActorId: 1,
                ConfirmWrites: true));
            Assert.True(cheque.Succeeded, cheque.Message);

            var opex = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "save_maintenance",
                PropertyId: bought.Id,
                Name: "Chiller service",
                Amount: 5_000m,
                VatCode: "opex",
                LegalEntityId: 1,
                ActorId: 1,
                ConfirmWrites: true));
            Assert.True(opex.Succeeded, opex.Message);

            var project = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "save_project",
                Code: "PRJ-IFRS",
                Name: "Escrow tower",
                Amount: 400_000m,
                Amount2: 900_000m,
                Amount3: 1_200_000m,
                Count: 1,
                LegalEntityId: 1,
                ConfirmWrites: true));
            Assert.True(project.Succeeded, project.Message);

            var unitA = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "save_project_unit",
                ProjectId: project.Id,
                Code: "A-01",
                Amount: 600_000m,
                Area: 60m,
                ConfirmWrites: true));
            var unitB = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "save_project_unit",
                ProjectId: project.Id,
                Code: "B-01",
                Amount: 400_000m,
                Area: 40m,
                ConfirmWrites: true));
            Assert.True(unitA.Succeeded && unitB.Succeeded, unitA.Message + " " + unitB.Message);

            var contractor = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "save_contractor", Code: "CTR-1", Name: "Main contractor", ConfirmWrites: true));
            var contract = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "save_contract",
                Code: "CON-1",
                ProjectId: project.Id,
                RelatedId: contractor.Id,
                Amount: 500_000m,
                Percent: 10m,
                ConfirmWrites: true));
            Assert.True(contract.Succeeded, contract.Message);

            var boq = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "save_boq_line",
                ContractId: contract.Id,
                Code: "BOQ-01",
                Name: "Blockwork",
                Category: "masonry",
                Amount: 100m,
                Amount2: 250m,
                ConfirmWrites: true));
            Assert.True(boq.Succeeded, boq.Message);
            Assert.Equal(25_000m, await ScalarDec(cs, "SELECT `amount` FROM `epc_erp_re_boq_lines` WHERE `id`=" + boq.Id));

            var blockedEscrow = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "save_escrow_disbursement",
                ProjectId: project.Id,
                Amount: 10_000m,
                ConfirmWrites: true));
            Assert.False(blockedEscrow.Succeeded);

            var receipt = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "save_escrow_receipt",
                ProjectId: project.Id,
                Amount: 200_000m,
                Code: "ESC-1",
                Date1: "2026-10-13",
                LegalEntityId: 1,
                ActorId: 1,
                ConfirmWrites: true));
            Assert.True(receipt.Succeeded, receipt.Message);

            var over = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "save_escrow_disbursement",
                ProjectId: project.Id,
                Amount: 250_000m,
                ConfirmWrites: true));
            Assert.False(over.Succeeded);

            var paid = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "save_escrow_disbursement",
                ProjectId: project.Id,
                Amount: 80_000m,
                Date1: "2026-10-13",
                LegalEntityId: 1,
                ActorId: 1,
                ConfirmWrites: true));
            Assert.True(paid.Succeeded, paid.Message);

            var ms1 = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "save_milestone", ProjectId: project.Id, Code: "MS-1", Name: "Booking", Percent: 40m, Date1: "2026-11-01", ConfirmWrites: true));
            var ms2 = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "save_milestone", ProjectId: project.Id, Code: "MS-2", Name: "Structure", Percent: 60m, Date1: "2027-03-01", ConfirmWrites: true));
            Assert.True(ms1.Succeeded && ms2.Succeeded, ms1.Message);

            var offplan = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "save_sale",
                UnitId: unitA.Id,
                ProjectId: project.Id,
                Name: "Buyer B",
                Amount: 500_000m,
                Status: "reserved",
                LegalEntityId: 1,
                ActorId: 1,
                ConfirmWrites: true));
            Assert.True(offplan.Succeeded, offplan.Message);
            Assert.Contains("off the statements", offplan.Message, StringComparison.OrdinalIgnoreCase);

            var calendar = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "generate_instalments",
                SaleId: offplan.Id,
                ConfirmWrites: true));
            Assert.True(calendar.Succeeded, calendar.Message);
            Assert.Equal(2L, await Scalar(cs, "SELECT COUNT(*) FROM `epc_erp_re_instalments` WHERE `sale_id`=" + offplan.Id));
            Assert.Equal(200_000m, await ScalarDec(cs, "SELECT `amount` FROM `epc_erp_re_instalments` WHERE `sale_id`=" + offplan.Id + " ORDER BY `id` LIMIT 1"));

            var instId = await Scalar(cs, "SELECT `id` FROM `epc_erp_re_instalments` WHERE `sale_id`=" + offplan.Id + " ORDER BY `id` LIMIT 1");
            var collected = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "collect_instalment",
                Id: instId,
                LegalEntityId: 1,
                ActorId: 1,
                ConfirmWrites: true));
            Assert.True(collected.Succeeded, collected.Message);
            Assert.Contains("contract liability", collected.Message, StringComparison.OrdinalIgnoreCase);

            var alloc = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "allocate_shared_cost",
                ProjectId: project.Id,
                Amount: 1_000_000m,
                Category: "area",
                ConfirmWrites: true));
            Assert.True(alloc.Succeeded, alloc.Message);
            Assert.Equal(600_000m, await ScalarDec(cs, "SELECT `allocated_cost` FROM `epc_erp_re_units` WHERE `id`=" + unitA.Id));
            Assert.Equal(400_000m, await ScalarDec(cs, "SELECT `allocated_cost` FROM `epc_erp_re_units` WHERE `id`=" + unitB.Id));

            var cleared = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "cheque_status",
                Id: cheque.Id,
                Status: "deposited",
                ConfirmWrites: true));
            Assert.True(cleared.Succeeded, cleared.Message);
            var banked = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "cheque_status",
                Id: cheque.Id,
                Status: "cleared",
                LegalEntityId: 1,
                ActorId: 1,
                ConfirmWrites: true));
            Assert.True(banked.Succeeded, banked.Message);
            var bounce = await writes.SaveAsync(new ErpRealEstateWriteRequest(
                "cheque_status",
                Id: cheque.Id,
                Status: "bounced",
                ConfirmWrites: true));
            Assert.True(bounce.Succeeded, bounce.Message);
            Assert.Contains("Prior cleared allocation", bounce.Message, StringComparison.Ordinal);
            Assert.True(await Scalar(cs, "SELECT `gl_clear_journal_id` FROM `epc_erp_re_cheques` WHERE `id`=" + cheque.Id) > 0);
            Assert.Equal("bounced", await ScalarText(cs, "SELECT `status` FROM `epc_erp_re_cheques` WHERE `id`=" + cheque.Id));

            var journals = await Scalar(cs, "SELECT COUNT(*) FROM `epc_erp_gl_journals` WHERE `uae_tax_legislation_ref`='IAS40/IFRS15/IFRS18' AND `active`=1");
            Assert.True(journals >= 7, "expected IAS 40 / IFRS journals, got " + journals);

            Assert.Equal(1L, await Scalar(cs, "SELECT COUNT(*) FROM `epc_erp_re_acquisitions` WHERE `gl_journal_id`>0"));
            Assert.Equal(1L, await Scalar(cs, "SELECT COUNT(*) FROM `epc_erp_re_valuations` WHERE `gl_journal_id`>0"));
            Assert.Equal(1L, await Scalar(cs, "SELECT COUNT(*) FROM `epc_erp_re_deposits` WHERE `gl_journal_id`>0"));
            Assert.Equal(1L, await Scalar(cs, "SELECT COUNT(*) FROM `epc_erp_re_cheques` WHERE `gl_journal_id`>0"));
            Assert.Equal(0L, await Scalar(cs, "SELECT COUNT(*) FROM `epc_erp_re_sales` WHERE `gl_journal_id`>0"));

            Assert.True(await Scalar(cs, "SELECT COUNT(*) FROM `epc_erp_gl_lines` l JOIN `epc_erp_coa_accounts` a ON a.id=l.coa_id WHERE a.code='1510' AND l.debit>0") > 0);
            Assert.True(await Scalar(cs, "SELECT COUNT(*) FROM `epc_erp_gl_lines` l JOIN `epc_erp_coa_accounts` a ON a.id=l.coa_id WHERE a.code='2200' AND a.account_type='liability' AND l.credit>0") > 0);
            Assert.True(await Scalar(cs, "SELECT COUNT(*) FROM `epc_erp_gl_lines` l JOIN `epc_erp_coa_accounts` a ON a.id=l.coa_id WHERE a.code='4200' AND a.account_type='revenue' AND l.credit>0") > 0);
            Assert.True(await Scalar(cs, "SELECT COUNT(*) FROM `epc_erp_gl_lines` l JOIN `epc_erp_coa_accounts` a ON a.id=l.coa_id WHERE a.code='4100' AND a.account_type='revenue' AND l.credit>0") > 0);
            Assert.True(await Scalar(cs, "SELECT COUNT(*) FROM `epc_erp_gl_lines` l JOIN `epc_erp_coa_accounts` a ON a.id=l.coa_id WHERE a.code='6210' AND a.account_type='expense' AND l.debit>0") > 0);
            Assert.True(await Scalar(cs, "SELECT COUNT(*) FROM `epc_erp_gl_lines` l JOIN `epc_erp_coa_accounts` a ON a.id=l.coa_id WHERE a.code='1140' AND a.account_type='asset' AND l.debit>0") > 0);
            Assert.True(await Scalar(cs, "SELECT COUNT(*) FROM `epc_erp_gl_lines` l JOIN `epc_erp_coa_accounts` a ON a.id=l.coa_id WHERE a.code='2220' AND a.account_type='liability' AND l.credit>0") > 0);
            Assert.True(await Scalar(cs, "SELECT COUNT(*) FROM `epc_erp_gl_lines` l JOIN `epc_erp_coa_accounts` a ON a.id=l.coa_id WHERE a.code='1520' AND a.account_type='asset' AND l.debit>0") > 0);
            Assert.Equal(0L, await Scalar(cs, "SELECT COUNT(*) FROM `epc_erp_gl_lines` l JOIN `epc_erp_coa_accounts` a ON a.id=l.coa_id WHERE a.code='4110'"));

            var bs = await Scalar(
                cs,
                "SELECT COUNT(*) FROM `epc_erp_gl_lines` l JOIN `epc_erp_coa_accounts` a ON a.id=l.coa_id WHERE a.account_type IN ('asset','liability','equity')");
            var pl = await Scalar(
                cs,
                "SELECT COUNT(*) FROM `epc_erp_gl_lines` l JOIN `epc_erp_coa_accounts` a ON a.id=l.coa_id WHERE a.account_type IN ('revenue','income','expense')");
            Assert.True(bs >= 10, "balance-sheet lines " + bs);
            Assert.True(pl >= 3, "income-statement lines " + pl);
        }
        finally
        {
            await Exec(admin, "DROP DATABASE IF EXISTS `" + DbName(cs) + "`");
        }
    }

    private static (string? Admin, string Cs) ThrowawayCs()
    {
        var password = Environment.GetEnvironmentVariable("ECOMAE_LOCAL_MARIADB_E2E_DSN");
        if (string.IsNullOrWhiteSpace(password))
        {
            return (null, string.Empty);
        }

        var database = "ecomae_re_ifrs_" + Guid.NewGuid().ToString("N")[..12];
        return (
            "Server=127.0.0.1;Port=3306;Database=mysql;User ID=ecomae;Password=" + password + ";AllowUserVariables=true;",
            "Server=127.0.0.1;Port=3306;Database=" + database + ";User ID=ecomae;Password=" + password + ";AllowUserVariables=true;");
    }

    private static string DbName(string cs) => cs.Split("Database=")[1].Split(';')[0];

    private static async Task Exec(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> Scalar(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task<decimal> ScalarDec(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        return Convert.ToDecimal(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task<string> ScalarText(string connectionString, string sql)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new MySqlCommand(sql, connection);
        return Convert.ToString(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) ?? "";
    }

    private sealed class ThrowawayConnections : IErpWriteConnectionFactory
    {
        private readonly string _connectionString;

        public ThrowawayConnections(string connectionString) => _connectionString = connectionString;

        public bool IsConfigured => true;

        public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
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
