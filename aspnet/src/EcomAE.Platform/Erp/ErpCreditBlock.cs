using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Credit-management decision for an invoice. A permanent limit and terms apply
/// unless a temporary limit is inside its dates, in which case the temporary
/// limit and its terms replace them. Every enabled rule that fails is reported.
/// The invoice is blocked when any rule fails.
/// </summary>
public static class ErpCreditBlock
{
    public const string BlockedPrefix = "Invoice blocked.";

    public static readonly string[] Tabs = ["cc_blocking_rules", "cc_credit_limit_adj", "cc_customers_hold", "cc_open_holds"];

    public static bool IsTab(string? tab)
        => Tabs.Contains((tab ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase);

    public sealed record Position(
        bool OnHold,
        bool MandatoryLimit,
        decimal PermanentLimit,
        int PermanentTermsDays,
        long ReviewUntilUnix,
        decimal TemporaryLimit,
        int TemporaryTermsDays,
        long TemporaryFromUnix,
        long TemporaryUntilUnix,
        decimal OpenExposure,
        decimal OverdueAmount,
        int OldestOverdueDays,
        decimal InvoiceAmount,
        int InvoiceTermsDays);

    public sealed record Rules(
        bool AccountStatus,
        bool CreditLimit,
        decimal LimitPercent,
        decimal Tolerance,
        bool LimitExpired,
        bool DaysOverdue,
        int DaysThreshold,
        int GraceDays,
        bool OverdueAmount,
        decimal OverdueThreshold,
        bool TermsOfPayment)
    {
        public static Rules Defaults { get; } = new(true, true, 100m, 0m, true, true, 1, 0, true, 0.01m, true);
    }

    public sealed record Decision(
        bool Allowed,
        bool TemporaryActive,
        decimal EffectiveLimit,
        int EffectiveTermsDays,
        decimal Exposure,
        IReadOnlyList<string> Reasons)
    {
        public string Message => Allowed
            ? "Invoice allowed. Exposure " + Money(Exposure) + " of limit " + Money(EffectiveLimit) + "."
            : BlockedPrefix + " " + string.Join(" ", Reasons);
    }

    public static Decision Evaluate(Position position, Rules rules, long asOfUnix)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(rules);
        var temporary = TemporaryActive(position, asOfUnix);
        var limit = temporary ? position.TemporaryLimit : position.PermanentLimit;
        var terms = temporary && position.TemporaryTermsDays > 0
            ? position.TemporaryTermsDays
            : position.PermanentTermsDays;
        var exposure = Round(position.OpenExposure + position.InvoiceAmount);
        var reasons = new List<string>();

        if (rules.AccountStatus && position.OnHold)
        {
            reasons.Add("The customer account is on hold.");
        }

        if (rules.CreditLimit)
        {
            var headroom = Round((limit * (rules.LimitPercent / 100m)) + rules.Tolerance);
            if (limit <= 0m && position.MandatoryLimit && exposure > 0m)
            {
                reasons.Add("A credit limit is mandatory and none is set.");
            }
            else if (limit > 0m && exposure > headroom)
            {
                reasons.Add("Exposure " + Money(exposure) + " exceeds the "
                    + (temporary ? "temporary" : "permanent")
                    + " credit limit " + Money(headroom) + ".");
            }
        }

        if (rules.LimitExpired)
        {
            var expiry = temporary ? position.TemporaryUntilUnix : position.ReviewUntilUnix;
            if (expiry > 0 && asOfUnix > expiry)
            {
                reasons.Add("The " + (temporary ? "temporary" : "permanent") + " credit limit expired on " + Day(expiry) + ".");
            }
        }

        var overdueDays = position.OldestOverdueDays - Math.Max(0, rules.GraceDays);
        if (rules.DaysOverdue && overdueDays >= Math.Max(0, rules.DaysThreshold) && position.OldestOverdueDays > 0)
        {
            reasons.Add("An invoice is " + position.OldestOverdueDays.ToString(CultureInfo.InvariantCulture)
                + " days overdue. The rule blocks at "
                + Math.Max(0, rules.DaysThreshold).ToString(CultureInfo.InvariantCulture) + " days.");
        }

        if (rules.OverdueAmount && position.OverdueAmount > rules.OverdueThreshold)
        {
            reasons.Add("Overdue amount " + Money(position.OverdueAmount)
                + " exceeds " + Money(rules.OverdueThreshold) + ".");
        }

        if (rules.TermsOfPayment && terms > 0 && position.InvoiceTermsDays > terms)
        {
            reasons.Add("Invoice terms of " + position.InvoiceTermsDays.ToString(CultureInfo.InvariantCulture)
                + " days are longer than the allowed " + terms.ToString(CultureInfo.InvariantCulture) + " days.");
        }

        return new Decision(reasons.Count == 0, temporary, limit, terms, exposure, reasons);
    }

    public static bool TemporaryActive(Position position, long asOfUnix)
        => position.TemporaryLimit > 0m
            && (position.TemporaryFromUnix <= 0 || asOfUnix >= position.TemporaryFromUnix)
            && (position.TemporaryUntilUnix <= 0 || asOfUnix <= position.TemporaryUntilUnix);

    public static int OverdueDays(long dueUnix, long asOfUnix, int termsDays)
    {
        var due = dueUnix > 0 ? dueUnix : 0;
        if (due <= 0)
        {
            return 0;
        }

        var late = asOfUnix - due;
        if (late <= 0)
        {
            return 0;
        }

        return (int)(late / 86400L);
    }

    public static int TermsDaysFromText(string? text, int fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        var digits = new string(text.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var days) && days > 0
            ? days
            : fallback;
    }

    public static int TermsDaysBetween(long issueUnix, long dueUnix, int fallback)
    {
        if (issueUnix <= 0 || dueUnix <= issueUnix)
        {
            return fallback;
        }

        var days = (int)((dueUnix - issueUnix) / 86400L);
        return days > 0 ? days : fallback;
    }

    public static long StartOfUtcDay(string? date)
        => ParseDay(date, end: false);

    public static long EndOfUtcDay(string? date)
        => ParseDay(date, end: true);

    public static string Day(long unix)
        => unix <= 0
            ? string.Empty
            : DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static decimal Round(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static string Money(decimal value)
        => value.ToString("#,##0.00", CultureInfo.InvariantCulture);

    private static long ParseDay(string? date, bool end)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            return 0;
        }

        if (!DateTime.TryParseExact(date.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return 0;
        }

        var start = new DateTimeOffset(parsed.Date, TimeSpan.Zero).ToUnixTimeSeconds();
        return end ? start + 86399 : start;
    }
}
