using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// IFRS 18 face of the primary statements. A trading company keeps the main
/// business in operating. Investment returns are investing. Borrowing costs
/// and interest on cash are financing. Income taxes and discontinued operations
/// are separate. The three mandatory subtotals are always on the profit or loss.
/// </summary>
public static class ErpIfrs18Face
{
    public const string OperatingProfit = "Operating profit or loss";
    public const string BeforeFinancing = "Profit or loss before financing and income taxes";
    public const string BeforeTax = "Profit before income taxes";
    public const string Profit = "Profit or loss";
    public const string GrossProfit = "Gross profit";
    public const string TotalAssets = "Total assets";
    public const string TotalLiabilitiesAndEquity = "Total liabilities and equity";

    public static ErpIfrs18Category ClassifyProfit(string? name)
    {
        var text = Normalize(name);
        if (text.Contains("discontinu", StringComparison.Ordinal))
        {
            return ErpIfrs18Category.Discontinued;
        }

        if (IsIncomeTax(text))
        {
            return ErpIfrs18Category.IncomeTax;
        }

        if (IsFinancing(text))
        {
            return ErpIfrs18Category.Financing;
        }

        if (IsInvesting(text))
        {
            return ErpIfrs18Category.Investing;
        }

        return ErpIfrs18Category.Operating;
    }

    public static ErpIfrs18Liquidity ClassifyPosition(string? code, string? name, string? accountType)
    {
        var type = (accountType ?? string.Empty).Trim().ToLowerInvariant();
        var text = Normalize(name);
        if (type is "equity")
        {
            return ErpIfrs18Liquidity.Equity;
        }

        if (type is "liability")
        {
            if (text.Contains("non-current", StringComparison.Ordinal)
                || text.Contains("noncurrent", StringComparison.Ordinal)
                || text.Contains("long-term", StringComparison.Ordinal)
                || text.Contains("long term", StringComparison.Ordinal)
                || text.Contains("borrow", StringComparison.Ordinal)
                || text.Contains("loan", StringComparison.Ordinal)
                || text.Contains("lease liab", StringComparison.Ordinal))
            {
                return ErpIfrs18Liquidity.NonCurrentLiability;
            }

            return CodeNumber(code) >= 2500
                ? ErpIfrs18Liquidity.NonCurrentLiability
                : ErpIfrs18Liquidity.CurrentLiability;
        }

        if (text.Contains("cash", StringComparison.Ordinal)
            || text.Contains("bank", StringComparison.Ordinal)
            || text.Contains("receiv", StringComparison.Ordinal)
            || text.Contains("inventory", StringComparison.Ordinal)
            || text.Contains("stock", StringComparison.Ordinal)
            || text.Contains("prepay", StringComparison.Ordinal)
            || text.Contains("vat", StringComparison.Ordinal)
            || text.Contains("debtor", StringComparison.Ordinal))
        {
            return ErpIfrs18Liquidity.CurrentAsset;
        }

        if (text.Contains("equipment", StringComparison.Ordinal)
            || text.Contains("building", StringComparison.Ordinal)
            || text.Contains("vehicle", StringComparison.Ordinal)
            || text.Contains("plant", StringComparison.Ordinal)
            || text.Contains("intangible", StringComparison.Ordinal)
            || text.Contains("goodwill", StringComparison.Ordinal)
            || text.Contains("investment", StringComparison.Ordinal))
        {
            return ErpIfrs18Liquidity.NonCurrentAsset;
        }

        return CodeNumber(code) >= 1500
            ? ErpIfrs18Liquidity.NonCurrentAsset
            : ErpIfrs18Liquidity.CurrentAsset;
    }

    public static IReadOnlyList<ErpIfrs18Row> ProfitOrLoss(ErpFinancialStatement statement)
    {
        ArgumentNullException.ThrowIfNull(statement);
        var lines = Detail(statement.Lines, "revenue", "income", "expense");
        var rows = new List<ErpIfrs18Row>();
        var operating = lines.Where(line => ClassifyProfit(line.Name) == ErpIfrs18Category.Operating).ToList();
        var revenue = operating.Where(line => !IsExpense(line)).ToList();
        var cost = operating.Where(line => IsExpense(line) && IsCostOfSales(line.Name)).ToList();
        var other = operating.Where(line => IsExpense(line) && !IsCostOfSales(line.Name)).ToList();

        AddSection(rows, "Operating", revenue.Concat(cost).Concat(other));
        if (revenue.Count > 0 || cost.Count > 0)
        {
            rows.Add(Subtotal(GrossProfit, Sum(revenue) - Sum(cost), SumReporting(revenue) - SumReporting(cost)));
        }

        var operatingProfit = Sum(operating.Where(line => !IsExpense(line))) - Sum(operating.Where(IsExpense));
        var operatingReporting = SumReporting(operating.Where(line => !IsExpense(line))) - SumReporting(operating.Where(IsExpense));
        rows.Add(Subtotal(OperatingProfit, operatingProfit, operatingReporting));

        var investingLines = lines.Where(line => ClassifyProfit(line.Name) == ErpIfrs18Category.Investing).ToList();
        AddSection(rows, "Investing", investingLines);
        var beforeFinancing = operatingProfit + Signed(investingLines);
        var beforeFinancingReporting = operatingReporting + SignedReporting(investingLines);
        rows.Add(Subtotal(BeforeFinancing, beforeFinancing, beforeFinancingReporting));

        var financingLines = lines.Where(line => ClassifyProfit(line.Name) == ErpIfrs18Category.Financing).ToList();
        AddSection(rows, "Financing", financingLines);
        var beforeTax = beforeFinancing + Signed(financingLines);
        var beforeTaxReporting = beforeFinancingReporting + SignedReporting(financingLines);
        rows.Add(Subtotal(BeforeTax, beforeTax, beforeTaxReporting));

        var taxLines = lines.Where(line => ClassifyProfit(line.Name) == ErpIfrs18Category.IncomeTax).ToList();
        AddSection(rows, "Income taxes", taxLines);
        var continuing = beforeTax + Signed(taxLines);
        var continuingReporting = beforeTaxReporting + SignedReporting(taxLines);

        var discontinued = lines.Where(line => ClassifyProfit(line.Name) == ErpIfrs18Category.Discontinued).ToList();
        if (discontinued.Count > 0)
        {
            rows.Add(Subtotal("Profit or loss from continuing operations", continuing, continuingReporting));
            AddSection(rows, "Discontinued operations", discontinued);
            rows.Add(Subtotal(Profit, continuing + Signed(discontinued), continuingReporting + SignedReporting(discontinued)));
        }
        else
        {
            rows.Add(Subtotal(Profit, continuing, continuingReporting));
        }

        rows.Add(new ErpIfrs18Row(
            "note",
            "",
            "IFRS 18 categories and the three mandatory subtotals. No management-defined performance measure is added.",
            null,
            null));
        return rows;
    }

    public static IReadOnlyList<ErpIfrs18Row> FinancialPosition(ErpFinancialStatement statement)
    {
        ArgumentNullException.ThrowIfNull(statement);
        var lines = Detail(statement.Lines, "asset", "liability", "equity");
        var rows = new List<ErpIfrs18Row>();
        var currentAssets = SumGroup(rows, lines, ErpIfrs18Liquidity.CurrentAsset, "Current assets");
        var nonCurrentAssets = SumGroup(rows, lines, ErpIfrs18Liquidity.NonCurrentAsset, "Non-current assets");
        rows.Add(Subtotal(TotalAssets, currentAssets.Accounting + nonCurrentAssets.Accounting, currentAssets.Reporting + nonCurrentAssets.Reporting));

        var currentLiabilities = SumGroup(rows, lines, ErpIfrs18Liquidity.CurrentLiability, "Current liabilities");
        var nonCurrentLiabilities = SumGroup(rows, lines, ErpIfrs18Liquidity.NonCurrentLiability, "Non-current liabilities");
        var equity = SumGroup(rows, lines, ErpIfrs18Liquidity.Equity, "Equity");
        var liabilities = currentLiabilities.Accounting + nonCurrentLiabilities.Accounting;
        var liabilitiesReporting = currentLiabilities.Reporting + nonCurrentLiabilities.Reporting;
        rows.Add(Subtotal(
            TotalLiabilitiesAndEquity,
            liabilities + equity.Accounting,
            liabilitiesReporting + equity.Reporting));

        var check = statement.Lines.FirstOrDefault(line => line.AccountType == "total");
        if (check is not null)
        {
            rows.Add(new ErpIfrs18Row("total", "", check.Name, check.Accounting, check.Reporting));
        }

        rows.Add(new ErpIfrs18Row(
            "note",
            "",
            "Statement of financial position. Current and non-current split follows the account name and code. Profit for the period is included in equity.",
            null,
            null));
        return rows;
    }

    public static ErpFinancialReportDocument PrimaryStatements(ErpFinancialStatement profit, ErpFinancialStatement position)
    {
        ArgumentNullException.ThrowIfNull(profit);
        ArgumentNullException.ThrowIfNull(position);
        var intro = "Presented under IFRS 18 Presentation and Disclosure in Financial Statements. "
            + "Operating is the main business. Investing is returns from assets that earn independently, including dividends and interest receivable. "
            + "Financing is borrowing costs and interest on cash and bank balances. Income taxes and discontinued operations follow. "
            + profit.LevelLabel + ". Accounting currency " + profit.AccountingCurrency
            + ". Reporting currency " + profit.ReportingCurrency + ".";
        return new ErpFinancialReportDocument(
            "Financial statements",
            intro,
            [
                Sheet("Profit or loss", profit, ProfitOrLoss(profit)),
                Sheet("Financial position", position, FinancialPosition(position)),
            ]);
    }

    private static ErpFinancialReportSheet Sheet(string name, ErpFinancialStatement statement, IReadOnlyList<ErpIfrs18Row> rows)
        => new(
            name,
            ["Code", "Line", statement.AccountingCurrency, statement.ReportingCurrency],
            rows);

    private static (decimal Accounting, decimal Reporting) SumGroup(
        List<ErpIfrs18Row> rows,
        IReadOnlyList<ErpFinancialStatementLine> lines,
        ErpIfrs18Liquidity liquidity,
        string title)
    {
        var group = lines
            .Where(line => ClassifyPosition(line.Code, line.Name, line.AccountType) == liquidity)
            .Where(line => line.Accounting != 0m || line.Reporting != 0m)
            .OrderBy(line => line.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (group.Count > 0)
        {
            rows.Add(new ErpIfrs18Row("section", "", title, null, null));
            foreach (var line in group)
            {
                rows.Add(new ErpIfrs18Row("line", line.Code, line.Name, line.Accounting, line.Reporting));
            }
        }

        return (group.Sum(line => line.Accounting), group.Sum(line => line.Reporting));
    }

    private static void AddSection(List<ErpIfrs18Row> rows, string title, IEnumerable<ErpFinancialStatementLine> lines)
    {
        var list = lines
            .Where(line => line.Accounting != 0m || line.Reporting != 0m)
            .OrderBy(line => line.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (list.Count == 0)
        {
            return;
        }

        rows.Add(new ErpIfrs18Row("section", "", title, null, null));
        foreach (var line in list)
        {
            var sign = IsExpense(line) ? -1m : 1m;
            rows.Add(new ErpIfrs18Row("line", line.Code, line.Name, sign * line.Accounting, sign * line.Reporting));
        }
    }

    private static List<ErpFinancialStatementLine> Detail(IReadOnlyList<ErpFinancialStatementLine> lines, params string[] types)
        => lines.Where(line => types.Contains(line.AccountType, StringComparer.OrdinalIgnoreCase)).ToList();

    private static bool IsExpense(ErpFinancialStatementLine line)
        => line.AccountType.Equals("expense", StringComparison.OrdinalIgnoreCase);

    private static decimal Sum(IEnumerable<ErpFinancialStatementLine> lines)
        => lines.Sum(line => line.Accounting);

    private static decimal SumReporting(IEnumerable<ErpFinancialStatementLine> lines)
        => lines.Sum(line => line.Reporting);

    private static decimal Signed(IEnumerable<ErpFinancialStatementLine> lines)
        => lines.Sum(line => IsExpense(line) ? -line.Accounting : line.Accounting);

    private static decimal SignedReporting(IEnumerable<ErpFinancialStatementLine> lines)
        => lines.Sum(line => IsExpense(line) ? -line.Reporting : line.Reporting);

    private static ErpIfrs18Row Subtotal(string label, decimal accounting, decimal reporting)
        => new("subtotal", "", label, Round(accounting), Round(reporting));

    private static decimal Round(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static bool IsCostOfSales(string? name)
    {
        var text = Normalize(name);
        return text.Contains("cost of goods", StringComparison.Ordinal)
            || text.Contains("cost of sales", StringComparison.Ordinal)
            || text.Contains("cogs", StringComparison.Ordinal);
    }

    private static bool IsIncomeTax(string text)
    {
        if (text.Contains("vat", StringComparison.Ordinal) || text.Contains("value added", StringComparison.Ordinal))
        {
            return false;
        }

        return text.Contains("income tax", StringComparison.Ordinal)
            || text.Contains("tax expense", StringComparison.Ordinal)
            || text.Contains("corporate tax", StringComparison.Ordinal)
            || text.Contains("current tax", StringComparison.Ordinal)
            || text.Contains("deferred tax", StringComparison.Ordinal)
            || text.Contains("zakat", StringComparison.Ordinal);
    }

    private static bool IsFinancing(string text)
    {
        if (text.Contains("interest income", StringComparison.Ordinal) || text.Contains("finance income", StringComparison.Ordinal))
        {
            return text.Contains("cash", StringComparison.Ordinal)
                || text.Contains("bank", StringComparison.Ordinal)
                || text.Contains("deposit", StringComparison.Ordinal);
        }

        return text.Contains("interest expense", StringComparison.Ordinal)
            || text.Contains("finance cost", StringComparison.Ordinal)
            || text.Contains("finance charge", StringComparison.Ordinal)
            || text.Contains("borrowing", StringComparison.Ordinal)
            || text.Contains("lease interest", StringComparison.Ordinal)
            || text.Contains("loan interest", StringComparison.Ordinal);
    }

    private static bool IsInvesting(string text)
        => text.Contains("dividend", StringComparison.Ordinal)
            || text.Contains("associate", StringComparison.Ordinal)
            || text.Contains("joint venture", StringComparison.Ordinal)
            || text.Contains("equity method", StringComparison.Ordinal)
            || text.Contains("investment", StringComparison.Ordinal)
            || text.Contains("fair value", StringComparison.Ordinal)
            || text.Contains("interest income", StringComparison.Ordinal)
            || text.Contains("finance income", StringComparison.Ordinal);

    private static int CodeNumber(string? code)
    {
        var digits = new string((code ?? string.Empty).Where(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }

    private static string Normalize(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();
}

public enum ErpIfrs18Category
{
    Operating,
    Investing,
    Financing,
    IncomeTax,
    Discontinued,
}

public enum ErpIfrs18Liquidity
{
    CurrentAsset,
    NonCurrentAsset,
    CurrentLiability,
    NonCurrentLiability,
    Equity,
}

public sealed record ErpIfrs18Row(string Kind, string Code, string Label, decimal? Accounting, decimal? Reporting);

public sealed record ErpFinancialReportSheet(string Name, IReadOnlyList<string> Headers, IReadOnlyList<ErpIfrs18Row> Rows);

public sealed record ErpFinancialReportDocument(string Title, string Intro, IReadOnlyList<ErpFinancialReportSheet> Sheets);
