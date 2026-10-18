using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Turns each legal entity's transaction currency into the head office reporting currency.
/// Profit and loss uses the average rate. The balance sheet uses the closing rate.
/// A missing rate leaves the consolidation unconverted.
/// </summary>
public static class ErpHeadOfficeMoney
{
    public sealed record Slice(string Code, string Name, string Type, string Currency, decimal Amount);

    public sealed record Rate(string From, string To, decimal Value);

    public sealed record Line(string Code, string Name, string Type, decimal Amount);

    public sealed record Result(bool Applied, string Currency, IReadOnlyList<Line> Lines, decimal Translation, string Note);

    public static Result Consolidate(
        IReadOnlyList<Slice> slices,
        string? reportingCurrency,
        IReadOnlyList<Rate> rates,
        bool profitAndLoss)
    {
        ArgumentNullException.ThrowIfNull(slices);
        ArgumentNullException.ThrowIfNull(rates);
        var reporting = Code(reportingCurrency);
        if (reporting.Length != 3)
        {
            return new Result(false, "", [], 0m, "Head office reporting currency is missing.");
        }

        var active = new List<Slice>();
        foreach (var slice in slices)
        {
            if (slice is null || Math.Abs(slice.Amount) < 0.005m || string.IsNullOrWhiteSpace(slice.Code))
            {
                continue;
            }

            active.Add(slice);
        }

        if (active.Count == 0)
        {
            return new Result(false, reporting, [], 0m, "");
        }

        var missing = new List<string>();
        var used = new List<string>();
        var lines = new Dictionary<string, (string Name, string Type, decimal Amount)>(StringComparer.OrdinalIgnoreCase);
        decimal translation = 0m;
        foreach (var slice in active)
        {
            var from = Code(slice.Currency);
            if (from.Length != 3)
            {
                from = reporting;
            }

            var closing = Quote(from, reporting, rates, average: false);
            var selected = profitAndLoss || IsProfit(slice.Type)
                ? Quote(from, reporting, rates, average: true)
                : closing;
            if (selected is null)
            {
                missing.Add(from);
                continue;
            }

            var amount = Round2(slice.Amount * selected.Value);
            if (!profitAndLoss && IsProfit(slice.Type) && closing is not null)
            {
                translation = Round2(translation + Round2(slice.Amount * closing.Value) - amount);
            }

            Remember(used, from, reporting, selected.Value);
            if (lines.TryGetValue(slice.Code, out var existing))
            {
                lines[slice.Code] = (existing.Name.Length > 0 ? existing.Name : slice.Name, existing.Type, Round2(existing.Amount + amount));
            }
            else
            {
                lines[slice.Code] = (slice.Name, slice.Type, amount);
            }
        }

        if (missing.Count > 0)
        {
            var names = string.Join(", ", missing.Distinct(StringComparer.Ordinal));
            return new Result(false, reporting, [], 0m, "No exchange rate from " + names + " to " + reporting + ". Amounts stay in the transaction currency.");
        }

        if (!profitAndLoss && Math.Abs(translation) >= 0.005m)
        {
            lines["CTA"] = ("Currency translation to " + reporting, "equity", translation);
        }

        var note = "Consolidated accounts are in the head office reporting currency " + reporting
            + ". Each legal entity keeps the transaction currency of its country. "
            + string.Join(". ", used) + ".";
        var built = lines
            .Select(pair => new Line(pair.Key, pair.Value.Name, pair.Value.Type, pair.Value.Amount))
            .ToList();
        return new Result(true, reporting, built, translation, note);
    }

    public static string Code(string? value)
    {
        var code = (value ?? string.Empty).Trim().ToUpperInvariant();
        return code.Length >= 3 ? code[..3] : "";
    }

    private static bool IsProfit(string? type)
        => (type ?? string.Empty).Trim().ToLowerInvariant() is "revenue" or "income" or "expense";

    private static void Remember(List<string> used, string from, string reporting, decimal rate)
    {
        var text = from == reporting
            ? from + " stays " + reporting
            : from + " to " + reporting + " at " + rate.ToString("0.########", CultureInfo.InvariantCulture);
        if (!used.Contains(text, StringComparer.Ordinal))
        {
            used.Add(text);
        }
    }

    private static decimal? Quote(string from, string reporting, IReadOnlyList<Rate> rates, bool average)
    {
        if (from == reporting)
        {
            return 1m;
        }

        var direct = new List<decimal>();
        foreach (var rate in rates)
        {
            if (rate.Value <= 0m)
            {
                continue;
            }

            var source = Code(rate.From);
            var target = Code(rate.To);
            if (source == from && target == reporting)
            {
                direct.Add(rate.Value);
            }
            else if (source == reporting && target == from)
            {
                direct.Add(1m / rate.Value);
            }
        }

        if (direct.Count == 0)
        {
            return null;
        }

        if (!average)
        {
            return direct[^1];
        }

        return direct.Sum() / direct.Count;
    }

    private static decimal Round2(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
