using System.Globalization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpLandlordBookTests
{
    [Fact]
    public void SplitEvenKeepsTheOriginalAmount()
    {
        var parts = ErpLandlordCalc.SplitEven(100m, 3);
        Assert.Equal(new[] { 33.34m, 33.33m, 33.33m }, parts);
        Assert.Equal(100m, parts.Sum());
        Assert.Empty(ErpLandlordCalc.SplitEven(10m, 0));
    }

    [Fact]
    public void December2025LinesMatchThePrintedSheet()
    {
        var lines = ErpLandlordOpening.Lines;
        Assert.Equal(lines.Select(line => line.Code).Distinct().Count(), lines.Count);
        Assert.All(lines, line => Assert.InRange(line.Code.Length, 1, 40));

        var income = lines.Where(line => line.Kind == "income").Sum(line => line.Amount);
        Assert.Equal(8_076_457m, income);

        Assert.Equal(115_183m, Expense("LC-126"));
        Assert.Equal(247_888m, Expense("DIC-135"));
        Assert.Equal(22_806m, Expense("RITAJ-530"));
        Assert.Equal(223_213m, Expense("MEY-B172"));
        Assert.Equal(27_834m, Expense("DH-V68"));
        Assert.Equal(24_902m, Expense("DH-V69"));
        Assert.Equal(6_352m, Expense("PEARL-1001"));
        Assert.Equal(22_472m, Expense("RITAJ-531"));
        Assert.Equal(885_492m, Expense("V6869"));

        Assert.Equal(126, Planned("LC-126"));
        Assert.Equal(38, Planned("DIC-135"));
        Assert.Equal(99, Planned("DIC-178"));
        Assert.DoesNotContain(lines, line => line.Kind is "investment" or "through_2024");

        var recovered = lines.Where(line => line.Kind == "recovery").ToArray();
        Assert.Equal(2, recovered.Length);
        Assert.All(recovered, line => Assert.Equal(0m, line.Amount));
        Assert.Contains(recovered, line => line.PropertyCode == "LC-126");
        Assert.Contains(recovered, line => line.PropertyCode == "PEARL-1001");
    }

    [Fact]
    public void FlatViewCountsALineOnce()
    {
        var building = Line(1, 0, 0, "expense", "building", 100m);
        var oneFlat = Line(1, 7, 0, "expense", "flat", 100m);
        var parent = Line(2, 0, 0, "expense", "all_flats", 100m);
        var left = Line(3, 7, 2, "expense", "flat", 50m);
        var right = Line(4, 8, 2, "expense", "flat", 50m);
        var recovery = Line(5, 0, 0, "recovery", "building", 0m);

        var property = ErpLandlordCalc.Summarize([oneFlat, parent, left, right, recovery], 2025, 0, 0);
        Assert.Equal(200m, property.Expense);
        Assert.True(property.RecoveryMarked);
        Assert.Null(property.RecoveryPercent);
        Assert.Null(property.ReturnOnInvestment);

        var flat = ErpLandlordCalc.Summarize([oneFlat, parent, left, right, building], 2025, 0, 7);
        Assert.Equal(150m, flat.Expense);

        var withInvestment = ErpLandlordCalc.Summarize(
            [oneFlat, Line(6, 0, 0, "investment", "building", 1000m), Line(7, 0, 0, "through_2024", "building", 400m)],
            2025,
            0,
            0);
        Assert.Equal(1000m, withInvestment.Investment);
        Assert.Equal(400m, withInvestment.Through2024);
        Assert.Equal(-100m, withInvestment.ReturnAmount);
        Assert.Equal(-10.00m, withInvestment.ReturnOnInvestment);
    }

    [Fact]
    public void EachExpenseHasItsOwnAccountAndNewOnesStayOnTheOtherAccount()
    {
        var reserved = ErpRealEstateIfrs.Accounts.Select(account => account.Code).ToHashSet(StringComparer.Ordinal);
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in ErpLandlordOpening.Lines.Where(line => line.Kind == "expense"))
        {
            var code = ErpLandlordCoa.CodeFor(line.Kind, line.Category);
            Assert.StartsWith("62", code, StringComparison.Ordinal);
            Assert.DoesNotContain(code, reserved);
            codes.Add(code);
            Assert.Equal(ErpLandlordCoa.Label(line.Category), ErpLandlordCoa.Expenses.Single(row => row.Category == line.Category).Name);
        }

        Assert.Equal(ErpLandlordOpening.Lines.Count(line => line.Kind == "expense"), 
            ErpLandlordOpening.Lines.Where(line => line.Kind == "expense").Select(line => line.PropertyCode + ":" + line.Category).Distinct().Count());
        Assert.Equal("4100", ErpLandlordCoa.CodeFor("income", "rental"));
        Assert.Equal("1510", ErpLandlordCoa.CodeFor("investment", "cost"));
        Assert.Equal("6295", ErpLandlordCoa.CodeFor("through_2024", "combined"));
        Assert.Equal("6299", ErpLandlordCoa.CodeFor("expense", "pool cleaning"));
        Assert.Equal("dewa", ErpLandlordCoa.Normalize("DEWA"));
        Assert.Equal("camp_head_lease", ErpLandlordCoa.Normalize("Camp head lease"));
        Assert.True(codes.Count >= 10);
    }

    [Fact]
    public void WorkbookAcceptsYearLinesAndOlderPropertySheets()
    {
        var year = ErpRealEstateWorkbook.Parse(
            "Year_Lines",
            "Code*,Year*,Kind*,PropertyCode*,Amount*,Category,Spread\nY2027-INC-BLD-A,2027,income,BLD-A,120000,rental,building\nY2027-REC-BLD-A,2027,recovery,BLD-A,0,recovered,building\n");
        Assert.Empty(year.Errors);
        Assert.True(year.Applies);
        Assert.Equal("Year_Lines", year.Name);

        var bad = ErpRealEstateWorkbook.Parse(
            "Year_Lines",
            "Code*,Year*,Kind*,PropertyCode*,Amount*\nY2027-INC-BLD-A,2027,income,BLD-A,0\n");
        Assert.Contains(bad.Errors, error => error.Contains("amount", StringComparison.OrdinalIgnoreCase));

        var older = ErpRealEstateWorkbook.Parse(
            "Properties",
            "Code*,Name*\nBLD-A,Building A\n");
        Assert.Empty(older.Errors);

        var expense = ErpRealEstateWorkbook.Parse(
            "Expenses",
            "Code*,PropertyCode*,Category*,Amount*\nEXP-1,BLD-A,repairs,1500\n");
        Assert.Empty(expense.Errors);

        using var stream = new MemoryStream(ErpRealEstateWorkbook.Template());
        var csv = ErpRealEstateWorkbook.SheetToCsv(stream, "Year_Lines");
        Assert.Contains("Y2027-INC-BLD-A", csv, StringComparison.Ordinal);
        stream.Position = 0;
        Assert.Contains("RentStartYear", ErpRealEstateWorkbook.SheetToCsv(stream, "Properties"), StringComparison.Ordinal);
        Assert.Equal("real_estate", IndustrySpecificRoutePolicy.RequiredIndustry("/erp/landlord-app"));
        Assert.Equal("real_estate", IndustrySpecificRoutePolicy.RequiredIndustry("/erp/landlord/save"));
        Assert.Equal("real_estate", IndustrySpecificRoutePolicy.RequiredIndustry("/erp/landlord/report"));
        var catalog = ErpGlobalSearchCatalog.Build(jewellery: false, fitOut: false, realEstate: true);
        Assert.Contains(catalog, entry => entry.Href == "/erp/landlord-app?year=2025");
    }

    private static decimal Expense(string property)
        => ErpLandlordOpening.Lines.Where(line => line.PropertyCode == property && line.Kind == "expense").Sum(line => line.Amount);

    private static int Planned(string code)
        => ErpLandlordOpening.Properties.Single(property => property.Code == code).PlannedUnits;

    private static ErpLandlordLine Line(long id, long unitId, long parentId, string kind, string spread, decimal amount)
        => new(id, "C" + id.ToString(CultureInfo.InvariantCulture), 1, unitId, kind == "through_2024" ? 2024 : 2025, kind, kind, amount, spread, parentId, "2025-12-31", "", "recorded", "BLD", "Building", unitId == 0 ? "" : "U" + unitId.ToString(CultureInfo.InvariantCulture), "");
}
