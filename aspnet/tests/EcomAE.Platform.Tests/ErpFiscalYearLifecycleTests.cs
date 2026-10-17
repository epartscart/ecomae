using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpFiscalYearLifecycleTests
{
    [Fact]
    public void CloseProfit_DebitsIncome_CreditsExpense_AndRetainsTheProfit()
    {
        var entry = ErpFiscalYearBooks.CloseProfitAndLoss(
        [
            new ErpFiscalYearBooks.Movement("4000", "revenue", 0m, 1000m),
            new ErpFiscalYearBooks.Movement("5000", "expense", 400m, 0m),
        ]);

        Assert.Equal(600m, entry.NetProfit);
        Assert.Equal(2000m, entry.Lines.Sum(line => line.Debit));
        Assert.Equal(2000m, entry.Lines.Sum(line => line.Credit));
        Assert.Equal(0m, Net(entry.Lines, "3900"));
        var retained = Assert.Single(entry.Lines, line => line.Account == "3200");
        Assert.Equal((0m, 600m), (retained.Debit, retained.Credit));
    }

    [Fact]
    public void CloseLoss_DebitsRetainedEarnings_AndClears3900()
    {
        var entry = ErpFiscalYearBooks.CloseProfitAndLoss(
        [
            new ErpFiscalYearBooks.Movement("4000", "revenue", 0m, 100m),
            new ErpFiscalYearBooks.Movement("5000", "expense", 250m, 0m),
        ]);

        Assert.Equal(-150m, entry.NetProfit);
        Assert.Equal(entry.Lines.Sum(line => line.Debit), entry.Lines.Sum(line => line.Credit));
        Assert.Equal(0m, Net(entry.Lines, "3900"));
        var retained = Assert.Single(entry.Lines, line => line.Account == "3200");
        Assert.Equal((150m, 0m), (retained.Debit, retained.Credit));
    }

    [Fact]
    public void CloseWithNoProfitOrLoss_PostsNothing()
    {
        var entry = ErpFiscalYearBooks.CloseProfitAndLoss([]);
        Assert.Equal(0m, entry.NetProfit);
        Assert.Empty(entry.Lines);
    }

    [Fact]
    public void OpeningBalances_CarryTheBalanceSheet_AndDropIncome()
    {
        var lines = ErpFiscalYearBooks.OpeningBalances(
        [
            new ErpFiscalYearBooks.Movement("1000", "asset", 1000m, 0m),
            new ErpFiscalYearBooks.Movement("2000", "liability", 0m, 400m),
            new ErpFiscalYearBooks.Movement("3200", "equity", 0m, 600m),
            new ErpFiscalYearBooks.Movement("4000", "revenue", 0m, 1000m),
        ]);

        Assert.Equal(lines.Sum(line => line.Debit), lines.Sum(line => line.Credit));
        Assert.DoesNotContain(lines, line => line.Account == "4000");
        Assert.Contains(lines, line => line.Account == "1000" && line.Debit == 1000m);
        Assert.Contains(lines, line => line.Account == "2000" && line.Credit == 400m);
        Assert.Contains(lines, line => line.Account == "3200" && line.Credit == 600m);
        Assert.Empty(ErpFiscalYearBooks.OpeningBalances([]));
    }

    [Fact]
    public void NextYear_StartsTheNextSecond_AndIncrementsTheLabel()
    {
        Assert.Equal((201L, 301L), ErpFiscalYearBooks.NextWindow(100, 200));
        Assert.Equal("FY2027", ErpFiscalYearBooks.NextLabel("FY2026"));
        Assert.Equal("Books next", ErpFiscalYearBooks.NextLabel("Books"));
        Assert.Equal("Next year", ErpFiscalYearBooks.NextLabel(""));
    }

    [Fact]
    public void HoldAndClose_BlockPosting_OpenDoesNot()
    {
        Assert.True(ErpFiscalYearBooks.AllowsPosting(null, null));
        Assert.True(ErpFiscalYearBooks.AllowsPosting("", ""));
        Assert.True(ErpFiscalYearBooks.AllowsPosting("open", "open"));
        Assert.True(ErpFiscalYearBooks.AllowsPosting("open", ""));
        Assert.False(ErpFiscalYearBooks.AllowsPosting("hold", "open"));
        Assert.False(ErpFiscalYearBooks.AllowsPosting("closed", ""));
        Assert.False(ErpFiscalYearBooks.AllowsPosting("open", "locked"));
        Assert.False(ErpFiscalYearBooks.AllowsPosting("open", "closed"));
        Assert.Contains("hold", ErpFiscalYearBooks.BlockedMessage("hold", "open"), StringComparison.Ordinal);
        Assert.True(ErpFiscalYearBooks.IsAction(" Close "));
        Assert.False(ErpFiscalYearBooks.IsAction("lock"));
    }

    [Fact]
    public void PeriodClosePage_OffersHoldOpenAndClose()
    {
        var razor = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpPeriodCloseApp.razor"));
        Assert.Contains("/erp/fiscal-years/action", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"hold\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"open\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"close\"", razor, StringComparison.Ordinal);
        Assert.Contains("3900", razor, StringComparison.Ordinal);
        Assert.Contains("3200", razor, StringComparison.Ordinal);
        Assert.Contains("6900", razor, StringComparison.Ordinal);
        Assert.Contains("6910", razor, StringComparison.Ordinal);
        Assert.Contains("5100", razor, StringComparison.Ordinal);
        Assert.Contains("1550", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@section", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void PostingGuard_RefusesAHeldOrClosedFiscalYear()
    {
        var posting = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpGlPostingService.cs"));
        Assert.Contains("AssertFiscalYearAllowsPostingAsync", posting, StringComparison.Ordinal);
        Assert.Contains("ErpFiscalYearBooks.AllowsPosting", posting, StringComparison.Ordinal);

        var lifecycle = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFiscalYearLifecycleService.cs"));
        Assert.Contains("SourceType = \"adjustment\"", lifecycle, StringComparison.Ordinal);
        Assert.Contains("SourceType = \"opening\"", lifecycle, StringComparison.Ordinal);
        Assert.Contains("\"FY-CLOSE\"", lifecycle, StringComparison.Ordinal);
        Assert.Contains("\"FY-OPEN\"", lifecycle, StringComparison.Ordinal);
        Assert.Contains("\"FY-FX\"", lifecycle, StringComparison.Ordinal);
        Assert.Contains("\"FY-FRAC\"", lifecycle, StringComparison.Ordinal);
        Assert.Contains("\"FY-DEPR\"", lifecycle, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", lifecycle, StringComparison.Ordinal);

        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpFiscalYearLifecycleService", program, StringComparison.Ordinal);

        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpFiscalYearAction", module, StringComparison.Ordinal);
        Assert.Contains("HandleFyActionAsync", module, StringComparison.Ordinal);

        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/fiscal-years/action");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("retained earnings", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
    }

    private static decimal Net(IReadOnlyList<ErpFyCloseEntryLine> lines, string account)
        => lines.Where(line => line.Account == account).Sum(line => line.Credit - line.Debit);

    private static string FindRepoFile(string relative)
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

        throw new FileNotFoundException("Could not locate " + relative);
    }
}
