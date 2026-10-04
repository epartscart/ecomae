using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Cp.PriceImport;

/// <summary>One skipped / error row as PHP <c>epc_price_history_issue_detail</c> / <c>_issue_module</c> records it.</summary>
public sealed record PriceImportIssue(
    long LineNo,
    string IssueType,
    string ReasonCode,
    string Details,
    IReadOnlyList<string>? Cells = null,
    IReadOnlyDictionary<string, string>? Parsed = null);

/// <summary>
/// PHP <c>epc_price_import_helpers.php</c>: reason labels, source-column labels (file header + mapped role) and the
/// <c>{history_id}_issues.csv</c> layout (<c>line_no, issue_type, reason_code, why_skipped_or_error, error_details,
/// Col…, parsed_…</c>) that the CP history panel downloads.
/// </summary>
public static class PriceImportIssues
{
    private static readonly string[] Lead = ["line_no", "issue_type", "reason_code", "why_skipped_or_error", "error_details"];
    private static readonly string[] ParsedKeys = ["manufacturer", "article", "article_show", "name", "exist", "price"];
    private static readonly Regex ColNumber = new(@"Col(\d+)", RegexOptions.CultureInvariant);

    public static string ReasonLabel(string reasonCode) => reasonCode switch
    {
        "empty_row" => "Skipped: the row is empty",
        "empty_article" => "Skipped: article/number column is empty after normalization (spaces and special characters removed)",
        "invalid_price" => "Skipped: price column is zero or not a valid number",
        "import_error" => "Error: import failed",
        "validation" => "Error: file validation failed",
        "error" => "Error: processing error",
        "general" => "Error: general import error",
        "info" => "Notice",
        _ => "Issue: " + reasonCode,
    };

    /// <summary>PHP <c>epc_price_build_source_column_labels</c>.</summary>
    public static IReadOnlyList<string> ColumnLabels(IReadOnlyList<string>? headerRow, PriceListColumnMap columns, int minColumns = 0)
    {
        var roles = columns.ByRole;
        var maxCol = Math.Max(roles.Values.DefaultIfEmpty(0).Max(), minColumns);
        if (headerRow is not null && headerRow.Count > maxCol)
        {
            maxCol = headerRow.Count;
        }

        var roleByIndex = new Dictionary<int, string>();
        foreach (var (role, col) in roles)
        {
            if (col > 0)
            {
                roleByIndex[col - 1] = role;
            }
        }

        var labels = new List<string>(maxCol);
        for (var i = 0; i < maxCol; i++)
        {
            var header = headerRow is not null && i < headerRow.Count ? headerRow[i].Trim() : string.Empty;
            roleByIndex.TryGetValue(i, out var role);
            var n = (i + 1).ToString(CultureInfo.InvariantCulture);
            labels.Add(header.Length > 0
                ? "Col" + n + ": " + header + (role is null ? string.Empty : " [" + role + "]")
                : role is not null ? "Col" + n + " [" + role + "]" : "Col" + n);
        }

        return labels;
    }

    public static Dictionary<string, string> ToFields(PriceImportIssue issue, IReadOnlyList<string> columnLabels)
    {
        var why = ReasonLabel(issue.ReasonCode) + (issue.Details.Length > 0 ? " — " + issue.Details : string.Empty);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["line_no"] = issue.LineNo.ToString(CultureInfo.InvariantCulture),
            ["issue_type"] = issue.IssueType,
            ["reason_code"] = issue.ReasonCode,
            ["why_skipped_or_error"] = why,
            ["error_details"] = issue.Details,
        };

        if (issue.Cells is not null)
        {
            for (var i = 0; i < columnLabels.Count; i++)
            {
                fields[columnLabels[i]] = i < issue.Cells.Count ? issue.Cells[i].Trim() : string.Empty;
            }

            for (var i = columnLabels.Count; i < issue.Cells.Count; i++)
            {
                fields["Col" + (i + 1).ToString(CultureInfo.InvariantCulture)] = issue.Cells[i].Trim();
            }
        }

        if (issue.Parsed is not null)
        {
            foreach (var key in ParsedKeys)
            {
                if (issue.Parsed.TryGetValue(key, out var value))
                {
                    fields["parsed_" + key] = value;
                }
            }
        }

        return fields;
    }

    /// <summary>PHP <c>epc_price_history_issues_csv_header_row</c> + <c>_issues_to_csv_rows</c>.</summary>
    public static List<string[]> ToCsvRows(IReadOnlyList<Dictionary<string, string>> issues)
    {
        var sourceColumns = new SortedSet<string>(Comparer<string>.Create(CompareColumns));
        var parsedPresent = new List<string>();
        foreach (var issue in issues)
        {
            foreach (var key in issue.Keys)
            {
                if (Lead.Contains(key))
                {
                    continue;
                }

                if (key.StartsWith("parsed_", StringComparison.Ordinal))
                {
                    continue;
                }

                sourceColumns.Add(key);
            }
        }

        foreach (var key in ParsedKeys)
        {
            var name = "parsed_" + key;
            if (issues.Any(issue => issue.ContainsKey(name)))
            {
                parsedPresent.Add(name);
            }
        }

        var header = Lead.Concat(sourceColumns).Concat(parsedPresent).ToArray();
        var rows = new List<string[]>(issues.Count + 1) { header };
        foreach (var issue in issues)
        {
            rows.Add(header.Select(col => issue.TryGetValue(col, out var v) ? v : string.Empty).ToArray());
        }

        return rows;
    }

    /// <summary>PHP <c>fputcsv</c> (comma, <c>"</c> enclosure) for one row.</summary>
    public static string CsvLine(IEnumerable<string> values)
    {
        var builder = new StringBuilder();
        var first = true;
        foreach (var value in values)
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            var v = value ?? string.Empty;
            if (v.IndexOfAny([',', '"', '\n', '\r', '\t', ' ', '\\']) >= 0)
            {
                builder.Append('"').Append(v.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
            }
            else
            {
                builder.Append(v);
            }
        }

        return builder.Append('\n').ToString();
    }

    private static int CompareColumns(string a, string b)
    {
        var ma = ColNumber.Match(a);
        var mb = ColNumber.Match(b);
        var na = ma.Success ? int.Parse(ma.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        var nb = mb.Success ? int.Parse(mb.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        return na != nb ? na.CompareTo(nb) : string.CompareOrdinal(a, b);
    }
}
