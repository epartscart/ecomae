using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Dynamics LedgerTrialBalanceListPage parameters and signed-balance math.
/// Official fields: from/to date, posting layer, opening display, closing
/// transaction types, dimension set, separate dimension columns, voucher drill.
/// </summary>
public enum ErpTrialBalanceOpeningDisplay
{
    Column,
    IncludeInPeriod,
    Exclude,
}

public enum ErpTrialBalancePostingLayer
{
    Current,
    Operations,
    Tax,
}

public enum ErpTrialBalanceDimensionSet
{
    MainAccount,
    MainAccountCompany,
}

public sealed record ErpTrialBalanceParameters(
    DateOnly FromDate,
    DateOnly ToDate,
    ErpTrialBalancePostingLayer PostingLayer,
    ErpTrialBalanceOpeningDisplay OpeningDisplay,
    bool IncludeClosingTransactions,
    bool IncludeClosingAdjustments,
    ErpTrialBalanceDimensionSet DimensionSet,
    bool SeparateDimensionColumns,
    string? AccountCode = null,
    int Limit = 500)
{
    public const string AppPath = "/erp/trial-balance-app";
    public const string SnapshotPath = "/erp/trial-balance/snapshot";
    public const string ExportPath = "/erp/trial-balance/export";
    public const string DigestPath = "/erp/trial-balance";

    public static ErpTrialBalanceParameters FromQuery(
        string? from,
        string? to,
        string? layer,
        string? opening,
        string? includeClosing,
        string? includeClosingAdj,
        string? dimensionSet,
        string? separateDims,
        string? account,
        string? limit)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = ParseDate(from) ?? new DateOnly(today.Year, 1, 1);
        var toDate = ParseDate(to) ?? today;
        if (toDate < fromDate)
        {
            (fromDate, toDate) = (toDate, fromDate);
        }

        var parsedLimit = 500;
        if (int.TryParse(limit, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0)
        {
            parsedLimit = Math.Min(n, 2000);
        }

        return new(
            fromDate,
            toDate,
            ParseLayer(layer),
            ParseOpening(opening),
            ParseFlag(includeClosing, true),
            ParseFlag(includeClosingAdj, true),
            ParseDimensionSet(dimensionSet),
            ParseFlag(separateDims, false),
            string.IsNullOrWhiteSpace(account) ? null : account.Trim(),
            parsedLimit);
    }

    public long FromUnix => new DateTimeOffset(FromDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeSeconds();

    public long ToUnix => new DateTimeOffset(ToDate.ToDateTime(new TimeOnly(23, 59, 59)), TimeSpan.Zero).ToUnixTimeSeconds();

    public string QueryString(string? accountOverride = null)
    {
        var account = accountOverride ?? AccountCode;
        var parts = new List<string>
        {
            "from=" + FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "to=" + ToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "layer=" + LayerKey(PostingLayer),
            "opening=" + OpeningKey(OpeningDisplay),
            "include_closing=" + (IncludeClosingTransactions ? "1" : "0"),
            "include_closing_adj=" + (IncludeClosingAdjustments ? "1" : "0"),
            "dimension_set=" + DimensionKey(DimensionSet),
            "separate_dims=" + (SeparateDimensionColumns ? "1" : "0"),
        };
        if (!string.IsNullOrWhiteSpace(account))
        {
            parts.Add("account=" + Uri.EscapeDataString(account));
        }

        return string.Join("&", parts);
    }

    public string AppHref(string? accountOverride = null)
        => AppPath + "?" + QueryString(accountOverride);

    public string ExportHref()
        => ExportPath + "?" + QueryString();

    public static bool IsClosingTransaction(string? sourceType, string? journalNo, string? reference, string? description)
    {
        if (!string.Equals(sourceType?.Trim(), "adjustment", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var blob = ((journalNo ?? "") + " " + (reference ?? "") + " " + (description ?? "")).ToLowerInvariant();
        return blob.Contains("year-end", StringComparison.Ordinal)
            || blob.Contains("year end", StringComparison.Ordinal)
            || blob.Contains("ye-", StringComparison.Ordinal)
            || blob.Contains("closing transaction", StringComparison.Ordinal)
            || blob.Contains("year-end close", StringComparison.Ordinal);
    }

    public static bool IsClosingAdjustment(string? sourceType, string? journalNo, string? reference, string? description)
    {
        if (!string.Equals(sourceType?.Trim(), "adjustment", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var blob = ((journalNo ?? "") + " " + (reference ?? "") + " " + (description ?? "")).ToLowerInvariant();
        return blob.Contains("closing adj", StringComparison.Ordinal)
            || blob.Contains("closing adjustment", StringComparison.Ordinal);
    }

    public static bool IsTaxLayer(string? legislationRef)
        => !string.IsNullOrWhiteSpace(legislationRef);

    public bool AcceptsJournal(string? sourceType, string? journalNo, string? reference, string? description, string? legislationRef)
    {
        if (!IncludeClosingTransactions && IsClosingTransaction(sourceType, journalNo, reference, description))
        {
            return false;
        }

        if (!IncludeClosingAdjustments && IsClosingAdjustment(sourceType, journalNo, reference, description))
        {
            return false;
        }

        return PostingLayer switch
        {
            ErpTrialBalancePostingLayer.Operations => !IsTaxLayer(legislationRef),
            ErpTrialBalancePostingLayer.Tax => IsTaxLayer(legislationRef),
            _ => true,
        };
    }

    public static decimal SignedOpening(string? normalSide, decimal openingBalance)
        => string.Equals((normalSide ?? "debit").Trim(), "credit", StringComparison.OrdinalIgnoreCase)
            ? -openingBalance
            : openingBalance;

    public static ErpTrialBalanceLine BuildLine(
        string code,
        string name,
        string accountType,
        string normalSide,
        decimal openingBalance,
        decimal openingDebits,
        decimal openingCredits,
        decimal periodDebits,
        decimal periodCredits,
        string company = "",
        ErpTrialBalanceOpeningDisplay openingDisplay = ErpTrialBalanceOpeningDisplay.Column)
    {
        var opening = openingDisplay == ErpTrialBalanceOpeningDisplay.Exclude
            ? 0m
            : SignedOpening(normalSide, openingBalance) + openingDebits - openingCredits;
        var debit = periodDebits;
        var credit = periodCredits;
        if (openingDisplay == ErpTrialBalanceOpeningDisplay.IncludeInPeriod)
        {
            if (opening >= 0)
            {
                debit += opening;
            }
            else
            {
                credit += Math.Abs(opening);
            }

            opening = 0m;
        }

        var closing = opening + debit - credit;
        return new ErpTrialBalanceLine(code, name, accountType, normalSide, company, opening, debit, credit, closing);
    }

    public static DateOnly? ParseDate(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        return DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    public static ErpTrialBalancePostingLayer ParseLayer(string? raw)
        => (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "operations" => ErpTrialBalancePostingLayer.Operations,
            "tax" => ErpTrialBalancePostingLayer.Tax,
            _ => ErpTrialBalancePostingLayer.Current,
        };

    public static ErpTrialBalanceOpeningDisplay ParseOpening(string? raw)
        => (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "include_in_period" or "period" => ErpTrialBalanceOpeningDisplay.IncludeInPeriod,
            "exclude" => ErpTrialBalanceOpeningDisplay.Exclude,
            _ => ErpTrialBalanceOpeningDisplay.Column,
        };

    public static ErpTrialBalanceDimensionSet ParseDimensionSet(string? raw)
        => (raw ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "company" or "main_account_company" => ErpTrialBalanceDimensionSet.MainAccountCompany,
            _ => ErpTrialBalanceDimensionSet.MainAccount,
        };

    public static bool ParseFlag(string? raw, bool fallback)
    {
        var text = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (text is "1" or "true" or "yes" or "on")
        {
            return true;
        }

        if (text is "0" or "false" or "no" or "off")
        {
            return false;
        }

        return fallback;
    }

    public static string LayerKey(ErpTrialBalancePostingLayer layer)
        => layer switch
        {
            ErpTrialBalancePostingLayer.Operations => "operations",
            ErpTrialBalancePostingLayer.Tax => "tax",
            _ => "current",
        };

    public static string OpeningKey(ErpTrialBalanceOpeningDisplay opening)
        => opening switch
        {
            ErpTrialBalanceOpeningDisplay.IncludeInPeriod => "include_in_period",
            ErpTrialBalanceOpeningDisplay.Exclude => "exclude",
            _ => "column",
        };

    public static string DimensionKey(ErpTrialBalanceDimensionSet set)
        => set == ErpTrialBalanceDimensionSet.MainAccountCompany ? "company" : "main_account";

    public static string LayerLabel(ErpTrialBalancePostingLayer layer)
        => layer switch
        {
            ErpTrialBalancePostingLayer.Operations => "Operations",
            ErpTrialBalancePostingLayer.Tax => "Tax",
            _ => "Current",
        };

    public static string OpeningLabel(ErpTrialBalanceOpeningDisplay opening)
        => opening switch
        {
            ErpTrialBalanceOpeningDisplay.IncludeInPeriod => "Include in period activity",
            ErpTrialBalanceOpeningDisplay.Exclude => "Do not show opening",
            _ => "Opening balance column",
        };

    public static string DimensionLabel(ErpTrialBalanceDimensionSet set)
        => set == ErpTrialBalanceDimensionSet.MainAccountCompany
            ? "Main account + company"
            : "Main account";
}

public sealed record ErpTrialBalanceLine(
    string Code,
    string Name,
    string AccountType,
    string NormalSide,
    string Company,
    decimal Opening,
    decimal Debit,
    decimal Credit,
    decimal Closing);

public sealed record ErpTrialBalanceVoucher(
    long JournalId,
    string JournalNo,
    long JournalDate,
    string SourceType,
    string Reference,
    string Description,
    string LegislationRef,
    decimal Debit,
    decimal Credit);

public sealed record ErpTrialBalanceSnapshotRow(
    long Id,
    string TrackingId,
    string FromDate,
    string ToDate,
    string PostingLayer,
    string DimensionSet,
    int RowCount,
    decimal OpeningTotal,
    decimal DebitTotal,
    decimal CreditTotal,
    decimal ClosingTotal,
    long TimeCreated);

public sealed record ErpTrialBalanceResult(
    ErpTrialBalanceParameters Parameters,
    IReadOnlyList<ErpTrialBalanceLine> Lines,
    IReadOnlyList<ErpTrialBalanceVoucher> Vouchers,
    IReadOnlyList<ErpTrialBalanceSnapshotRow> Snapshots,
    decimal OpeningTotal,
    decimal DebitTotal,
    decimal CreditTotal,
    decimal ClosingTotal,
    string Source,
    string Message);
