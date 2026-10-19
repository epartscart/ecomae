namespace EcomAE.Platform.Erp;

/// <summary>
/// Chart types the statements understand. A blank or shop type falls back to the
/// account code so income (4xxx) and expenses (5xxx–9xxx) are not dropped.
/// </summary>
public static class ErpAccountClass
{
    public static string Normalize(string? accountType, string? code)
    {
        var type = (accountType ?? string.Empty).Trim().ToLowerInvariant();
        var known = type switch
        {
            "income" or "sales" or "turnover" or "other income" or "revenue" => "revenue",
            "expense" or "expenses" or "cost" or "cogs" or "purchases" or "expenditure" or "overhead" => "expense",
            "asset" or "assets" or "bank" or "cash" => "asset",
            "liability" or "liabilities" => "liability",
            "equity" or "capital" => "equity",
            _ => string.Empty,
        };
        if (known.Length > 0)
        {
            return known;
        }

        foreach (var ch in code ?? string.Empty)
        {
            if (!char.IsDigit(ch))
            {
                continue;
            }

            return ch switch
            {
                '1' => "asset",
                '2' => "liability",
                '3' => "equity",
                '4' => "revenue",
                '5' or '6' or '7' or '8' or '9' => "expense",
                _ => string.Empty,
            };
        }

        return string.Empty;
    }

    /// <summary>
    /// A journal belongs to the selected company when the header company matches
    /// or the line legal entity matches. An unassigned header is not a member of
    /// every company. A line entity must not hide a journal whose header company
    /// is the one on the report.
    /// </summary>
    public static bool InCompany(long journalCompanyId, long lineLegalEntityId, int selectedCompanyId)
    {
        if (selectedCompanyId <= 0)
        {
            return true;
        }

        if (lineLegalEntityId == selectedCompanyId)
        {
            return true;
        }

        return journalCompanyId == selectedCompanyId;
    }

    /// <summary>
    /// The year-end profit-and-loss clearing entry. Depreciation and currency
    /// revaluation stay on the income statement. This entry only moves the result
    /// into retained earnings.
    /// </summary>
    public static bool IsProfitAndLossClose(string? sourceType, string? journalNo, string? reference, string? description)
    {
        if (!string.Equals(sourceType?.Trim(), "adjustment", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var blob = ((journalNo ?? string.Empty) + " " + (reference ?? string.Empty) + " " + (description ?? string.Empty)).ToLowerInvariant();
        return blob.Contains("year-end close", StringComparison.Ordinal)
            || blob.Contains("year end close", StringComparison.Ordinal)
            || blob.Contains("closing transaction", StringComparison.Ordinal)
            || blob.Contains("p&l close", StringComparison.Ordinal)
            || blob.Contains("profit and loss close", StringComparison.Ordinal)
            || blob.Contains("fy-close", StringComparison.Ordinal);
    }
}
