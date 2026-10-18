using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Year-end adjustments that must post before profit and loss is cleared.
/// Currency revaluation restates open foreign receivables at the closing rate.
/// The fraction is the fil left when that rate is rounded. Depreciation is the
/// charge for months in the year that have not already been run.
/// </summary>
public static class ErpFinancialCloseBooks
{
    public const string ReceivableAccount = "1100";
    public const string FxAccount = "6900";
    public const string FractionAccount = "6910";
    public const string DepreciationExpense = "5100";
    public const string AccumulatedDepreciation = "1550";
    public const decimal FractionLimit = 1.00m;

    public sealed record MonetaryItem(
        string Account,
        decimal BookLocal,
        decimal RevaluedLocal,
        decimal ExactLocal,
        bool DebitNormal);

    public sealed record AssetRow(
        long Id,
        decimal Cost,
        decimal Salvage,
        decimal Accumulated,
        int LifeMonths,
        string? Method);

    public sealed record AssetUpdate(long Id, decimal Accumulated, decimal BookValue, string Status);

    public sealed record MonthAmount(string Month, decimal Amount);

    public sealed record DepreciationPlan(
        decimal Total,
        IReadOnlyList<ErpFyCloseEntryLine> Lines,
        IReadOnlyList<AssetUpdate> Updates,
        IReadOnlyList<MonthAmount> Months);

    /// <summary>
    /// Move each monetary account from its booked local amount to the closing-rate amount.
    /// Amounts already posted by an earlier year-end revaluation are not posted again.
    /// A gain credits 6900. A loss debits 6900. The entry is not reversed.
    /// </summary>
    public static IReadOnlyList<ErpFyCloseEntryLine> Revaluation(
        IReadOnlyList<MonetaryItem> items,
        IReadOnlyDictionary<string, decimal>? alreadyNetDebit = null)
    {
        var desired = new Dictionary<string, (bool DebitNormal, decimal Increase)>(StringComparer.Ordinal);
        foreach (var group in Groups(items))
        {
            desired[group.Key] = (group.DebitNormal, group.Increase);
        }

        if (alreadyNetDebit is not null)
        {
            foreach (var code in alreadyNetDebit.Keys)
            {
                if (!desired.ContainsKey(code))
                {
                    desired[code] = (code != "2000", 0m);
                }
            }
        }

        var lines = new List<ErpFyCloseEntryLine>();
        foreach (var pair in desired.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var already = 0m;
            if (alreadyNetDebit is not null && alreadyNetDebit.TryGetValue(pair.Key, out var posted))
            {
                already = posted;
            }

            var desiredNetDebit = pair.Value.DebitNormal ? pair.Value.Increase : -pair.Value.Increase;
            var net = ErpFiscalYearBooks.Round(desiredNetDebit - already);
            if (net > 0m)
            {
                lines.Add(new ErpFyCloseEntryLine(pair.Key, net, 0m));
            }
            else if (net < 0m)
            {
                lines.Add(new ErpFyCloseEntryLine(pair.Key, 0m, -net));
            }
        }

        return Balance(lines, FxAccount);
    }

    /// <summary>
    /// The fil (or other minor unit) left when foreign amount times closing rate is rounded.
    /// Posts only when the new fraction is within 1.00 of the accounting currency.
    /// A larger difference is left for review and is not hidden in this account.
    /// </summary>
    public static IReadOnlyList<ErpFyCloseEntryLine> Fraction(
        IReadOnlyList<MonetaryItem> items,
        decimal alreadyIncrease = 0m)
    {
        var residue = 0m;
        foreach (var item in items)
        {
            residue += item.ExactLocal - item.RevaluedLocal;
        }

        var net = ErpFiscalYearBooks.Round(ErpFiscalYearBooks.Round(residue) - alreadyIncrease);
        if (net == 0m || Math.Abs(net) > FractionLimit)
        {
            return [];
        }

        var anchor = items.Count == 0
            ? new MonetaryItem(ReceivableAccount, 0m, 0m, 0m, true)
            : items
                .OrderByDescending(item => Math.Abs(item.RevaluedLocal))
                .ThenBy(item => item.Account, StringComparer.Ordinal)
                .First();
        var lines = new List<ErpFyCloseEntryLine>();
        AddBalance(lines, anchor.Account, anchor.DebitNormal, net);
        return Balance(lines, FractionAccount);
    }

    public static decimal SignedGain(IReadOnlyList<ErpFyCloseEntryLine> lines, string account)
        => lines.Where(line => line.Account == account).Sum(line => line.Credit - line.Debit);

    /// <summary>Charge one period at a time so declining balance compounds. Caps at salvage.</summary>
    public static DepreciationPlan Depreciate(IReadOnlyList<AssetRow> assets, IReadOnlyList<string> months)
    {
        var updates = new List<AssetUpdate>();
        var monthTotals = new decimal[months.Count];
        foreach (var asset in assets)
        {
            var accumulated = asset.Accumulated;
            var charged = 0m;
            for (var i = 0; i < months.Count; i++)
            {
                var amount = ErpFixedAssetWriteService.PeriodDepreciation(
                    asset.Cost,
                    asset.Salvage,
                    accumulated,
                    asset.LifeMonths,
                    asset.Method);
                if (amount <= 0m)
                {
                    break;
                }

                monthTotals[i] += amount;
                charged += amount;
                accumulated = ErpFixedAssetWriteService.R2(accumulated + amount);
            }

            if (charged <= 0m)
            {
                continue;
            }

            var book = Math.Max(asset.Salvage, ErpFixedAssetWriteService.R2(asset.Cost - accumulated));
            var status = book <= asset.Salvage + 0.01m ? "fully_depreciated" : "active";
            updates.Add(new AssetUpdate(asset.Id, accumulated, book, status));
        }

        var monthsOut = new List<MonthAmount>();
        decimal total = 0m;
        for (var i = 0; i < months.Count; i++)
        {
            var amount = ErpFixedAssetWriteService.R2(monthTotals[i]);
            if (amount <= 0m)
            {
                continue;
            }

            monthsOut.Add(new MonthAmount(months[i], amount));
            total += amount;
        }

        total = ErpFixedAssetWriteService.R2(total);
        IReadOnlyList<ErpFyCloseEntryLine> lines = total > 0m
            ?
            [
                new ErpFyCloseEntryLine(DepreciationExpense, total, 0m),
                new ErpFyCloseEntryLine(AccumulatedDepreciation, 0m, total),
            ]
            : [];
        return new DepreciationPlan(total, lines, updates, monthsOut);
    }

    public static IReadOnlyList<string> Months(long startUnix, long endUnix)
    {
        if (endUnix < startUnix || startUnix <= 0)
        {
            return [];
        }

        var start = DateTimeOffset.FromUnixTimeSeconds(startUnix).UtcDateTime;
        var end = DateTimeOffset.FromUnixTimeSeconds(endUnix).UtcDateTime;
        var cursor = new DateTime(start.Year, start.Month, 1);
        var last = new DateTime(end.Year, end.Month, 1);
        var months = new List<string>();
        while (cursor <= last && months.Count < 18)
        {
            months.Add(cursor.ToString("yyyy-MM", CultureInfo.InvariantCulture));
            cursor = cursor.AddMonths(1);
        }

        return months;
    }

    public static IReadOnlyList<string> UnpostedMonths(IReadOnlyList<string> months, IReadOnlyCollection<string> posted)
    {
        var done = new HashSet<string>(posted, StringComparer.Ordinal);
        return months.Where(month => !done.Contains(month)).ToList();
    }

    private sealed record Group(string Key, bool DebitNormal, decimal Increase);

    private static IEnumerable<Group> Groups(IReadOnlyList<MonetaryItem> items)
    {
        return items
            .Where(item => !string.IsNullOrWhiteSpace(item.Account))
            .GroupBy(item => item.Account.Trim(), StringComparer.Ordinal)
            .Select(group => new Group(
                group.Key,
                group.First().DebitNormal,
                group.Sum(item => ErpFiscalYearBooks.Round(item.RevaluedLocal - item.BookLocal))));
    }

    private static void AddBalance(List<ErpFyCloseEntryLine> lines, string account, bool debitNormal, decimal increase)
    {
        var amount = ErpFiscalYearBooks.Round(increase);
        if (amount == 0m)
        {
            return;
        }

        if (debitNormal)
        {
            lines.Add(amount > 0m
                ? new ErpFyCloseEntryLine(account, amount, 0m)
                : new ErpFyCloseEntryLine(account, 0m, -amount));
            return;
        }

        lines.Add(amount > 0m
            ? new ErpFyCloseEntryLine(account, 0m, amount)
            : new ErpFyCloseEntryLine(account, -amount, 0m));
    }

    private static IReadOnlyList<ErpFyCloseEntryLine> Balance(List<ErpFyCloseEntryLine> lines, string plugAccount)
    {
        var difference = ErpFiscalYearBooks.Round(lines.Sum(line => line.Debit) - lines.Sum(line => line.Credit));
        if (difference > 0m)
        {
            lines.Add(new ErpFyCloseEntryLine(plugAccount, 0m, difference));
        }
        else if (difference < 0m)
        {
            lines.Add(new ErpFyCloseEntryLine(plugAccount, -difference, 0m));
        }

        return lines.Count >= 2 && lines.Sum(line => line.Debit) == lines.Sum(line => line.Credit) ? lines : [];
    }
}
