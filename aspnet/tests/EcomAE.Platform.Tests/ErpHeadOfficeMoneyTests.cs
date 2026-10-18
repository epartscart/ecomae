using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpHeadOfficeMoneyTests
{
    [Fact]
    public void Each_country_currency_converts_before_the_head_office_total()
    {
        var result = ErpHeadOfficeMoney.Consolidate(
            [
                new("4000", "Sales", "revenue", "AED", 100m),
                new("5000", "Cost", "expense", "EUR", 40m),
                new("1000", "Cash", "asset", "AED", 100m),
                new("2000", "Payables", "liability", "EUR", 40m),
            ],
            "USD",
            [
                new("AED", "USD", 0.27m),
                new("EUR", "USD", 1.10m),
            ],
            profitAndLoss: true);

        Assert.True(result.Applied);
        Assert.Equal("USD", result.Currency);
        Assert.Equal(27m, Line(result, "4000"));
        Assert.Equal(44m, Line(result, "5000"));
        Assert.Contains("AED to USD at 0.27", result.Note, StringComparison.Ordinal);
        Assert.Contains("EUR to USD at 1.1", result.Note, StringComparison.Ordinal);
        Assert.Contains("head office reporting currency USD", result.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void Profit_uses_the_average_rate_and_the_balance_sheet_uses_the_closing_rate()
    {
        var rates = new ErpHeadOfficeMoney.Rate[]
        {
            new("AED", "USD", 0.25m),
            new("AED", "USD", 0.27m),
        };
        var slices = new ErpHeadOfficeMoney.Slice[]
        {
            new("4000", "Sales", "revenue", "AED", 100m),
            new("1000", "Cash", "asset", "AED", 100m),
        };

        var profit = ErpHeadOfficeMoney.Consolidate(slices, "usd", rates, profitAndLoss: true);
        Assert.Equal(26m, Line(profit, "4000"));

        var sheet = ErpHeadOfficeMoney.Consolidate(slices, "USD", rates, profitAndLoss: false);
        Assert.Equal(27m, Line(sheet, "1000"));
        Assert.Equal(26m, Line(sheet, "4000"));
        Assert.Equal(1m, Line(sheet, "CTA"));
        Assert.Equal(1m, sheet.Translation);

        var occupied = ErpHeadOfficeMoney.Consolidate(
            [
                new("CTA", "Cash trust account", "asset", "AED", 100m),
                new("4000", "Sales", "revenue", "AED", 100m),
            ],
            "USD",
            rates,
            profitAndLoss: false);
        Assert.Equal(27m, Line(occupied, "CTA"));
        Assert.Equal(1m, Line(occupied, "FX-CTA"));
    }

    [Fact]
    public void A_missing_rate_does_not_mix_the_currencies()
    {
        var result = ErpHeadOfficeMoney.Consolidate(
            [new("4000", "Sales", "revenue", "AED", 100m)],
            "EUR",
            [],
            profitAndLoss: true);
        Assert.False(result.Applied);
        Assert.Contains("No exchange rate from AED to EUR", result.Note, StringComparison.Ordinal);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void The_same_currency_needs_no_rate()
    {
        var result = ErpHeadOfficeMoney.Consolidate(
            [new("4000", "Sales", "revenue", "AED", 80m)],
            "AED",
            [],
            profitAndLoss: false);
        Assert.True(result.Applied);
        Assert.Equal(80m, Line(result, "4000"));
        Assert.Equal(0m, result.Translation);
        Assert.Contains("AED stays AED", result.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void The_report_page_names_the_head_office_currency()
    {
        var page = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpReportCenterApp.razor"));
        Assert.Contains("head office reporting currency", page, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", page, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", page, StringComparison.Ordinal);
    }

    private static decimal Line(ErpHeadOfficeMoney.Result result, string code)
        => Assert.Single(result.Lines, line => line.Code == code).Amount;

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

        throw new FileNotFoundException(relative);
    }
}
