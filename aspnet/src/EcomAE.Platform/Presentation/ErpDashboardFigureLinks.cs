using System.Globalization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Points each company-home figure at the page that holds it.
/// A real-estate company with a year book and an empty shop ledger
/// shows that book's income, expenses and profit instead of zeros.
/// </summary>
public static class ErpDashboardFigureLinks
{
    public static bool UseYearBook(bool realEstate, ErpLandlordMeasure book, ErpWorkspacePeriodKpis current)
        => realEstate
           && book.Year > 0
           && (book.YearIncome != 0 || book.YearExpense != 0)
           && current.RevenueExVat == 0
           && current.ProfitExVat == 0
           && current.GlNetProfit == 0;

    public static ErpWorkspacePeriodKpis ApplyYearBook(ErpWorkspacePeriodKpis current, ErpLandlordMeasure book)
        => current with
        {
            RevenueExVat = book.YearIncome,
            PurchaseExVat = book.YearExpense,
            ProfitExVat = book.YearReturn,
            SalesInclVat = book.YearIncome,
            GlNetProfit = book.YearReturn,
        };

    public static string DisplayLabel(string label, int year)
    {
        if (year <= 0 || string.IsNullOrWhiteSpace(label))
        {
            return label ?? "";
        }

        return label.Trim() switch
        {
            "Sales (ex VAT)" or "Sales incl. VAT" or "Revenue (period)" => year + " income",
            "Purchases (ex VAT)" or "Expenses (purchases)" => year + " expenses",
            "Gross profit" or "Gross profit (ex VAT)" or "Margin (ex VAT)" or "GL net profit" => year + " profit & loss",
            "Gross profit %" or "Gross margin %" => year + " return %",
            _ => label,
        };
    }

    public static string Href(string labelOrKey, int year)
    {
        var token = (labelOrKey ?? "").Trim().ToLowerInvariant();
        if (year > 0)
        {
            var book = "/erp/landlord-app?year=" + year.ToString(CultureInfo.InvariantCulture);
            if (IsProfit(token))
            {
                return book + "#profit-and-loss";
            }

            if (IsExpense(token))
            {
                return book + "#expense-lines";
            }

            if (IsIncome(token))
            {
                return book + "#income-lines";
            }
        }

        if (IsProfit(token))
        {
            return "/erp/report-center-app?tab=pl";
        }

        if (IsExpense(token))
        {
            return "/erp/purchases-app";
        }

        if (token.Contains("cash", StringComparison.Ordinal) || token.Contains("bank", StringComparison.Ordinal))
        {
            return "/erp/cash-accounts-app";
        }

        if (token.Contains("receiv", StringComparison.Ordinal) || token.Contains("due", StringComparison.Ordinal) || token is "ar" or "dso")
        {
            return "/erp/aging-app?tab=ar";
        }

        if (token.Contains("payab", StringComparison.Ordinal) || token is "ap" or "dpo")
        {
            return "/erp/payables-app";
        }

        if (token.Contains("vat", StringComparison.Ordinal))
        {
            return "/erp/vat-app?tab=vat_return";
        }

        if (token.Contains("invent", StringComparison.Ordinal) || token.Contains("stock", StringComparison.Ordinal) || token is "inv_turnover")
        {
            return "/erp/inventory-stock-app";
        }

        if (token.Contains("order", StringComparison.Ordinal))
        {
            return "/erp/sales-orders-app";
        }

        if (IsIncome(token))
        {
            return "/erp/sales-orders-app";
        }

        return "/erp/report-center-app";
    }

    public static string ProfitPath(int year)
        => year > 0
            ? "/erp/landlord-app?year=" + year.ToString(CultureInfo.InvariantCulture) + "#profit-and-loss"
            : "/erp/report-center-app?tab=pl";

    private static bool IsProfit(string token)
        => token.Contains("profit", StringComparison.Ordinal)
           || token.Contains("margin", StringComparison.Ordinal)
           || token.Contains("return", StringComparison.Ordinal)
           || token is "pl" or "gross_margin";

    private static bool IsExpense(string token)
        => token.Contains("expense", StringComparison.Ordinal)
           || token.Contains("purchase", StringComparison.Ordinal);

    private static bool IsIncome(string token)
        => token.Contains("income", StringComparison.Ordinal)
           || token.Contains("sales", StringComparison.Ordinal)
           || token.Contains("revenue", StringComparison.Ordinal)
           || token is "revenue";
}
