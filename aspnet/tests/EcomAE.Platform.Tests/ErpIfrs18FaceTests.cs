using System.Text;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpIfrs18FaceTests
{
    [Fact]
    public void ProfitOrLoss_UsesTheThreeMandatorySubtotals()
    {
        var statement = Statement(
            ErpFinancialStatementKind.ProfitAndLoss,
            Line("4000", "Sales revenue", "revenue", 1000m),
            Line("5000", "Cost of goods sold", "expense", 400m),
            Line("6100", "General expenses", "expense", 100m),
            Line("7100", "Dividend income", "revenue", 50m),
            Line("7200", "Interest expense", "expense", 30m),
            Line("7300", "Bank interest income", "revenue", 5m),
            Line("8100", "Income tax expense", "expense", 20m));

        var rows = ErpIfrs18Face.ProfitOrLoss(statement);

        Assert.Equal(600m, Amount(rows, ErpIfrs18Face.GrossProfit));
        Assert.Equal(500m, Amount(rows, ErpIfrs18Face.OperatingProfit));
        Assert.Equal(550m, Amount(rows, ErpIfrs18Face.BeforeFinancing));
        Assert.Equal(525m, Amount(rows, ErpIfrs18Face.BeforeTax));
        Assert.Equal(505m, Amount(rows, ErpIfrs18Face.Profit));
        Assert.Contains(rows, row => row.Label == "Dividend income" && row.Accounting == 50m);
        Assert.Contains(rows, row => row.Label == "Interest expense" && row.Accounting == -30m);
        Assert.Contains(rows, row => row.Label == "Bank interest income" && row.Accounting == 5m);
    }

    [Fact]
    public void Classify_PutsCashInterestInFinancing_AndVatOutsideIncomeTax()
    {
        Assert.Equal(ErpIfrs18Category.Financing, ErpIfrs18Face.ClassifyProfit("Bank interest income"));
        Assert.Equal(ErpIfrs18Category.Investing, ErpIfrs18Face.ClassifyProfit("Interest income"));
        Assert.Equal(ErpIfrs18Category.Operating, ErpIfrs18Face.ClassifyProfit("Sales revenue"));
        Assert.Equal(ErpIfrs18Category.IncomeTax, ErpIfrs18Face.ClassifyProfit("Corporate tax"));
        Assert.Equal(ErpIfrs18Category.Operating, ErpIfrs18Face.ClassifyProfit("VAT expense"));
        Assert.Equal(ErpIfrs18Category.Discontinued, ErpIfrs18Face.ClassifyProfit("Discontinued operations"));
        Assert.Equal(ErpIfrs18Liquidity.CurrentAsset, ErpIfrs18Face.ClassifyPosition("1000", "Cash on hand", "asset"));
        Assert.Equal(ErpIfrs18Liquidity.NonCurrentAsset, ErpIfrs18Face.ClassifyPosition("1600", "Equipment", "asset"));
        Assert.Equal(ErpIfrs18Liquidity.CurrentLiability, ErpIfrs18Face.ClassifyPosition("2100", "VAT output (payable)", "liability"));
        Assert.Equal(ErpIfrs18Liquidity.NonCurrentLiability, ErpIfrs18Face.ClassifyPosition("2600", "Bank loan", "liability"));
        Assert.Equal(ErpIfrs18Liquidity.Equity, ErpIfrs18Face.ClassifyPosition("3200", "Retained earnings", "equity"));
    }

    [Fact]
    public void FinancialPosition_TotalsAssetsAndEquity()
    {
        var statement = Statement(
            ErpFinancialStatementKind.BalanceSheet,
            Line("1000", "Cash on hand", "asset", 100m),
            Line("1600", "Equipment", "asset", 500m),
            Line("2000", "Accounts payable", "liability", 40m),
            Line("3000", "Owner's equity", "equity", 60m),
            Line("", "Current year earnings", "equity", 500m),
            new ErpFinancialStatementLine("", "In balance", "total", 0m, 0m));

        var rows = ErpIfrs18Face.FinancialPosition(statement);
        Assert.Equal(600m, Amount(rows, ErpIfrs18Face.TotalAssets));
        Assert.Equal(600m, Amount(rows, ErpIfrs18Face.TotalLiabilitiesAndEquity));
        Assert.Contains(rows, row => row.Label == "In balance");
    }

    [Fact]
    public void Files_OfferPrintPdfAndExcel()
    {
        var statement = Statement(ErpFinancialStatementKind.ProfitAndLoss, Line("4000", "Sales revenue", "revenue", 10m));
        var position = Statement(ErpFinancialStatementKind.BalanceSheet, Line("1000", "Cash on hand", "asset", 10m));
        var document = ErpIfrs18Face.PrimaryStatements(statement, position);
        var html = ErpFinancialReportFile.Html(document);
        Assert.Contains("window.print()", html, StringComparison.Ordinal);
        Assert.Contains("IFRS 18", html, StringComparison.Ordinal);
        Assert.Contains(ErpIfrs18Face.OperatingProfit, html, StringComparison.Ordinal);

        var pdf = ErpFinancialReportFile.Pdf(document);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));

        var xlsx = ErpFinancialReportFile.Xlsx(document);
        Assert.Equal(0x50, xlsx[0]);
        Assert.Equal(0x4B, xlsx[1]);
        var xml = Encoding.UTF8.GetString(xlsx);
        Assert.Contains("Operating profit", xml, StringComparison.Ordinal);

        var razor = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpReportCenterApp.razor"));
        Assert.Contains("Profit and loss", razor, StringComparison.Ordinal);
        Assert.Contains("Total assets", razor, StringComparison.Ordinal);
        Assert.Contains("Total liabilities and equity", razor, StringComparison.Ordinal);
        Assert.Contains("/erp/financial-reports/file", razor, StringComparison.Ordinal);
        Assert.Contains("FileHref(\"print\")", razor, StringComparison.Ordinal);
        Assert.Contains("FileHref(\"pdf\")", razor, StringComparison.Ordinal);
        Assert.Contains("FileHref(\"xlsx\")", razor, StringComparison.Ordinal);
        Assert.Contains("Excel", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP", razor, StringComparison.Ordinal);

        var trial = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpTrialBalanceApp.razor"));
        Assert.Contains("FileHref(\"print\")", trial, StringComparison.Ordinal);
        Assert.Contains("FileHref(\"pdf\")", trial, StringComparison.Ordinal);
        Assert.Contains("FileHref(\"xlsx\")", trial, StringComparison.Ordinal);
        Assert.Contains("Open in Excel", trial, StringComparison.Ordinal);
    }

    private static decimal Amount(IReadOnlyList<ErpIfrs18Row> rows, string label)
        => rows.Single(row => row.Label == label).Accounting ?? 0m;

    private static ErpFinancialStatementLine Line(string code, string name, string type, decimal amount)
        => new(code, name, type, amount, amount);

    private static ErpFinancialStatement Statement(ErpFinancialStatementKind kind, params ErpFinancialStatementLine[] lines)
        => new(
            new ErpFinancialStatementQuery(kind, ErpFinancialStatementLevel.Company, 2, 0, "AED"),
            "AED",
            "AED",
            1m,
            "",
            lines,
            0, 0, 0, 0, 0, 0, 0,
            "Company 2",
            "test",
            "",
            []);

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
