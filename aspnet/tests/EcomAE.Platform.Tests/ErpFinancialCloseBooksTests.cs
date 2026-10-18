using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpFinancialCloseBooksTests
{
    [Fact]
    public void Revaluation_GainOnReceivable_CreditsExchangeDifference()
    {
        var lines = ErpFinancialCloseBooks.Revaluation(
        [
            new ErpFinancialCloseBooks.MonetaryItem("1100", 367.00m, 367.25m, 367.25m, true),
        ]);

        Assert.Equal(lines.Sum(line => line.Debit), lines.Sum(line => line.Credit));
        Assert.Equal(0.25m, ErpFinancialCloseBooks.SignedGain(lines, "6900"));
        Assert.Contains(lines, line => line.Account == "1100" && line.Debit == 0.25m);
    }

    [Fact]
    public void Revaluation_LossOnPayable_DebitsExchangeDifference()
    {
        var lines = ErpFinancialCloseBooks.Revaluation(
        [
            new ErpFinancialCloseBooks.MonetaryItem("2000", 100m, 110m, 110m, false),
        ]);

        Assert.Equal(lines.Sum(line => line.Debit), lines.Sum(line => line.Credit));
        Assert.Equal(-10m, ErpFinancialCloseBooks.SignedGain(lines, "6900"));
        Assert.Contains(lines, line => line.Account == "2000" && line.Credit == 10m);
    }

    [Fact]
    public void Revaluation_DoesNotPostAnAmountAlreadyOnTheBooks()
    {
        var same = ErpFinancialCloseBooks.Revaluation(
            [new ErpFinancialCloseBooks.MonetaryItem("1100", 367m, 367.25m, 367.25m, true)],
            new Dictionary<string, decimal> { ["1100"] = 0.25m });
        Assert.Empty(same);

        var more = ErpFinancialCloseBooks.Revaluation(
            [new ErpFinancialCloseBooks.MonetaryItem("1100", 367m, 367.35m, 367.35m, true)],
            new Dictionary<string, decimal> { ["1100"] = 0.25m });
        Assert.Equal(0.10m, ErpFinancialCloseBooks.SignedGain(more, "6900"));
    }

    [Fact]
    public void Revaluation_ReversesAPriorGainWhenTheInvoiceIsPaid()
    {
        var lines = ErpFinancialCloseBooks.Revaluation(
            [],
            new Dictionary<string, decimal> { ["1100"] = 0.25m });

        Assert.Equal(-0.25m, ErpFinancialCloseBooks.SignedGain(lines, "6900"));
        Assert.Contains(lines, line => line.Account == "1100" && line.Credit == 0.25m);
    }

    [Fact]
    public void Fraction_PostsTheFilLeftByRounding_AndStopsAboveOne()
    {
        var items = new[]
        {
            new ErpFinancialCloseBooks.MonetaryItem("1100", 1.00m, 1.00m, 1.004m, true),
            new ErpFinancialCloseBooks.MonetaryItem("1100", 1.00m, 1.00m, 1.004m, true),
            new ErpFinancialCloseBooks.MonetaryItem("1100", 1.00m, 1.00m, 1.004m, true),
        };
        var lines = ErpFinancialCloseBooks.Fraction(items);
        Assert.Equal(lines.Sum(line => line.Debit), lines.Sum(line => line.Credit));
        Assert.Equal(0.01m, ErpFinancialCloseBooks.SignedGain(lines, "6910"));
        Assert.Contains(lines, line => line.Account == "1100" && line.Debit == 0.01m);

        var tooLarge = ErpFinancialCloseBooks.Fraction(
            [new ErpFinancialCloseBooks.MonetaryItem("1100", 0m, 0m, 1.20m, true)]);
        Assert.Empty(tooLarge);
    }

    [Fact]
    public void Fraction_SkipsFillsAlreadyPosted()
    {
        var items = new[]
        {
            new ErpFinancialCloseBooks.MonetaryItem("1100", 1m, 1m, 1.004m, true),
            new ErpFinancialCloseBooks.MonetaryItem("1100", 1m, 1m, 1.004m, true),
            new ErpFinancialCloseBooks.MonetaryItem("1100", 1m, 1m, 1.004m, true),
        };
        Assert.Empty(ErpFinancialCloseBooks.Fraction(items, 0.01m));
    }

    [Fact]
    public void Depreciation_StraightLineChargesEachOpenMonth()
    {
        var plan = ErpFinancialCloseBooks.Depreciate(
            [new ErpFinancialCloseBooks.AssetRow(7, 12000m, 0m, 0m, 60, "straight_line")],
            Months(12));

        Assert.Equal(2400m, plan.Total);
        Assert.Equal(plan.Lines.Sum(line => line.Debit), plan.Lines.Sum(line => line.Credit));
        Assert.Contains(plan.Lines, line => line.Account == "5100" && line.Debit == 2400m);
        Assert.Contains(plan.Lines, line => line.Account == "1550" && line.Credit == 2400m);
        var asset = Assert.Single(plan.Updates);
        Assert.Equal(2400m, asset.Accumulated);
        Assert.Equal("active", asset.Status);
        Assert.Equal(12, plan.Months.Count);
    }

    [Fact]
    public void Depreciation_DecliningBalanceCompounds_AndStopsAtSalvage()
    {
        var declining = ErpFinancialCloseBooks.Depreciate(
            [new ErpFinancialCloseBooks.AssetRow(1, 1200m, 0m, 0m, 12, "declining_balance")],
            Months(2));
        Assert.Equal(366.67m, declining.Total);

        var capped = ErpFinancialCloseBooks.Depreciate(
            [new ErpFinancialCloseBooks.AssetRow(2, 100m, 80m, 0m, 12, "straight_line")],
            Months(12));
        Assert.Equal(20m, capped.Total);
        var asset = Assert.Single(capped.Updates);
        Assert.Equal(80m, asset.BookValue);
        Assert.Equal("fully_depreciated", asset.Status);
    }

    [Fact]
    public void Months_CoverTheFiscalYear_AndSkipMonthsAlreadyRun()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        var end = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero).ToUnixTimeSeconds();
        var months = ErpFinancialCloseBooks.Months(start, end);
        Assert.Equal(12, months.Count);
        Assert.Equal("2026-01", months[0]);
        Assert.Equal("2026-12", months[11]);
        var open = ErpFinancialCloseBooks.UnpostedMonths(months, ["2026-01", "2026-02"]);
        Assert.Equal(10, open.Count);
        Assert.Equal("2026-03", open[0]);
        Assert.Empty(ErpFinancialCloseBooks.Depreciate([], open).Lines);
    }

    [Fact]
    public void CloseFiles_NameTheThreeJournals_AndDoNotCreateSchema()
    {
        var books = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpFinancialCloseBooks.cs"));
        Assert.Contains("6900", books, StringComparison.Ordinal);
        Assert.Contains("6910", books, StringComparison.Ordinal);
        Assert.Contains("5100", books, StringComparison.Ordinal);
        Assert.Contains("1550", books, StringComparison.Ordinal);
        var data = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Erp/ErpFinancialCloseData.cs"));
        Assert.DoesNotContain("CREATE TABLE", data, StringComparison.Ordinal);
        Assert.Contains("FY-FX-%", data, StringComparison.Ordinal);
        Assert.Contains("FY-FRAC-%", data, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> Months(int count)
        => Enumerable.Range(1, count).Select(month => "2026-" + month.ToString("00")).ToList();

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
