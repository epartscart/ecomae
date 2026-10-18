using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Year-end arithmetic. A fiscal year is open (journals post), on hold (journals
/// are refused, the books stay unclosed), or closed (the closing entry has moved
/// profit or loss into retained earnings and the next year can open).
/// </summary>
public static class ErpFiscalYearBooks
{
    public const string RetainedAccount = ErpFyCloseWriteService.RetainedAccount;
    public const string ClearingAccount = ErpFyCloseWriteService.PlClearingAccount;

    public static readonly string[] Actions = ["hold", "open", "close"];

    public sealed record Movement(string Code, string Type, decimal Debit, decimal Credit);

    public static bool IsAction(string? action)
        => Actions.Contains(Normalize(action), StringComparer.Ordinal);

    public static string Normalize(string? action)
        => (action ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>A blank status means no fiscal year covers the date, so posting stays allowed.</summary>
    public static bool AllowsPosting(string? yearStatus, string? periodStatus)
        => !Blocks(yearStatus) && !Blocks(periodStatus);

    public static bool Blocks(string? status)
        => !string.IsNullOrWhiteSpace(status)
            && !status.Trim().Equals("open", StringComparison.OrdinalIgnoreCase);

    public static string BlockedMessage(string? yearStatus, string? periodStatus)
    {
        if (Blocks(yearStatus))
        {
            return "This fiscal year is " + yearStatus!.Trim().ToLowerInvariant()
                + ". Hold and close refuse new journals. Open the year to post.";
        }

        return "This fiscal period is " + (periodStatus ?? string.Empty).Trim().ToLowerInvariant()
            + ". Only an open period accepts journals.";
    }

    /// <summary>The next year starts the second after this year ends and keeps the same length.</summary>
    public static (long Start, long End) NextWindow(long start, long end)
    {
        var length = end >= start ? end - start : 0;
        var nextStart = end + 1;
        return (nextStart, nextStart + length);
    }

    /// <summary>FY2026 becomes FY2027. A label with no year gains the word "next".</summary>
    public static string NextLabel(string? label)
    {
        var text = (label ?? string.Empty).Trim();
        var digits = 0;
        for (var i = text.Length - 1; i >= 0 && char.IsDigit(text[i]); i--)
        {
            digits++;
        }

        if (digits == 4 && int.TryParse(text[^4..], NumberStyles.None, CultureInfo.InvariantCulture, out var year))
        {
            return Clip(text[..^4] + (year + 1).ToString(CultureInfo.InvariantCulture), 40);
        }

        return Clip(string.IsNullOrEmpty(text) ? "Next year" : text + " next", 40);
    }

    /// <summary>
    /// Zero every income and expense account for the year, then move the net
    /// through P&amp;L clearing into retained earnings. Profit credits 3200.
    /// A loss debits 3200. Clearing 3900 finishes at zero.
    /// </summary>
    public static ErpFiscalYearEntry CloseProfitAndLoss(IReadOnlyList<Movement> movements)
    {
        var lines = new List<ErpFyCloseEntryLine>();
        decimal toClearingCredit = 0m;
        decimal toClearingDebit = 0m;
        foreach (var row in movements.OrderBy(m => m.Code, StringComparer.Ordinal))
        {
            var type = (row.Type ?? string.Empty).Trim().ToLowerInvariant();
            if (type is "revenue")
            {
                var net = Round(row.Credit - row.Debit);
                if (net > 0m)
                {
                    lines.Add(new ErpFyCloseEntryLine(row.Code, net, 0m));
                    toClearingCredit += net;
                }
                else if (net < 0m)
                {
                    lines.Add(new ErpFyCloseEntryLine(row.Code, 0m, -net));
                    toClearingDebit += -net;
                }
            }
            else if (type is "expense")
            {
                var net = Round(row.Debit - row.Credit);
                if (net > 0m)
                {
                    lines.Add(new ErpFyCloseEntryLine(row.Code, 0m, net));
                    toClearingDebit += net;
                }
                else if (net < 0m)
                {
                    lines.Add(new ErpFyCloseEntryLine(row.Code, -net, 0m));
                    toClearingCredit += -net;
                }
            }
        }

        if (toClearingCredit > 0m)
        {
            lines.Add(new ErpFyCloseEntryLine(ClearingAccount, 0m, toClearingCredit));
        }

        if (toClearingDebit > 0m)
        {
            lines.Add(new ErpFyCloseEntryLine(ClearingAccount, toClearingDebit, 0m));
        }

        var profit = Round(toClearingCredit - toClearingDebit);
        if (profit > 0m)
        {
            lines.Add(new ErpFyCloseEntryLine(ClearingAccount, profit, 0m));
            lines.Add(new ErpFyCloseEntryLine(RetainedAccount, 0m, profit));
        }
        else if (profit < 0m)
        {
            lines.Add(new ErpFyCloseEntryLine(ClearingAccount, 0m, -profit));
            lines.Add(new ErpFyCloseEntryLine(RetainedAccount, -profit, 0m));
        }

        return new ErpFiscalYearEntry(profit, lines);
    }

    /// <summary>
    /// Balance-sheet accounts carry into the new year. Income and expense do not.
    /// A rounding difference is posted to retained earnings so the opening journal balances.
    /// </summary>
    public static IReadOnlyList<ErpFyCloseEntryLine> OpeningBalances(IReadOnlyList<Movement> balances)
    {
        var lines = new List<ErpFyCloseEntryLine>();
        foreach (var row in balances.OrderBy(m => m.Code, StringComparer.Ordinal))
        {
            var type = (row.Type ?? string.Empty).Trim().ToLowerInvariant();
            if (type is "asset")
            {
                var net = Round(row.Debit - row.Credit);
                if (net > 0m)
                {
                    lines.Add(new ErpFyCloseEntryLine(row.Code, net, 0m));
                }
                else if (net < 0m)
                {
                    lines.Add(new ErpFyCloseEntryLine(row.Code, 0m, -net));
                }
            }
            else if (type is "liability" or "equity")
            {
                var net = Round(row.Credit - row.Debit);
                if (net > 0m)
                {
                    lines.Add(new ErpFyCloseEntryLine(row.Code, 0m, net));
                }
                else if (net < 0m)
                {
                    lines.Add(new ErpFyCloseEntryLine(row.Code, -net, 0m));
                }
            }
        }

        var difference = Round(lines.Sum(l => l.Debit) - lines.Sum(l => l.Credit));
        if (difference > 0m)
        {
            lines.Add(new ErpFyCloseEntryLine(RetainedAccount, 0m, difference));
        }
        else if (difference < 0m)
        {
            lines.Add(new ErpFyCloseEntryLine(RetainedAccount, -difference, 0m));
        }

        return lines.Count >= 2 ? lines : [];
    }

    public static decimal Round(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];
}

public sealed record ErpFiscalYearEntry(decimal NetProfit, IReadOnlyList<ErpFyCloseEntryLine> Lines);
