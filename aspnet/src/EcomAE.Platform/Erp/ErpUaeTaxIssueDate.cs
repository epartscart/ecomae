using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// FTA legislation <c>issue_date</c> is a display string (e.g. <c>01 Jan 2018</c>), not a unix timestamp.
/// Newest calendar date first — PHP twin <c>epc_uae_tax_legislation_merge_catalog</c> uses <c>strtotime</c>.
/// </summary>
public static class ErpUaeTaxIssueDate
{
    public const string NewestFirstSql =
        "COALESCE("
        + "STR_TO_DATE(`issue_date`, '%b %d, %Y'),"
        + "STR_TO_DATE(`issue_date`, '%b %e, %Y'),"
        + "STR_TO_DATE(`issue_date`, '%d %b %Y'),"
        + "STR_TO_DATE(`issue_date`, '%d %M %Y'),"
        + "STR_TO_DATE(`issue_date`, '%Y-%m-%d'),"
        + "STR_TO_DATE(`issue_date`, '%d/%m/%Y'),"
        + "STR_TO_DATE(`issue_date`, '%d-%m-%Y'),"
        + "STR_TO_DATE(`issue_date`, '%d %b %y')"
        + ") DESC, `id` DESC";

    private static readonly string[] Formats =
    [
        "MMM d, yyyy",
        "MMM dd, yyyy",
        "MMMM d, yyyy",
        "MMMM dd, yyyy",
        "d MMM yyyy",
        "dd MMM yyyy",
        "d MMMM yyyy",
        "dd MMMM yyyy",
        "yyyy-MM-dd",
        "d/M/yyyy",
        "dd/MM/yyyy",
        "d-M-yyyy",
        "dd-MM-yyyy",
        "d MMM yy",
        "dd MMM yy",
    ];

    public static DateTime SortKey(string? raw)
        => TryParse(raw, out var value) ? value : DateTime.MinValue;

    public static bool TryParse(string? raw, out DateTime value)
    {
        value = default;
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return false;
        }

        if (DateTime.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
        {
            return true;
        }

        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out value);
    }

    public static IReadOnlyList<T> NewestFirst<T>(
        IEnumerable<T> items,
        Func<T, string?> issueDate,
        Func<T, long>? id = null)
    {
        return items
            .OrderByDescending(item => SortKey(issueDate(item)))
            .ThenByDescending(item => id?.Invoke(item) ?? 0L)
            .ToList();
    }
}
