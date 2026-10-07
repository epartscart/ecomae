using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// JSON/CSV envelope for <c>epc_api_v1_powerbi_respond</c>. Row values come from the caller.
/// Failure shapes match the PHP dataset catches and do not add sample rows.
/// </summary>
public static class EpcPowerBiDatasets
{
    public static readonly string[] KpiHeaders = ["site_key", "metric", "value", "period_from", "period_to", "unit"];
    public static readonly string[] OrderHeaders = ["site_key", "order_id", "order_time", "user_id", "paid", "paid_type", "status_name"];
    public static readonly string[] SalesHeaders = ["Order", "Date", "Customer", "Sale ex VAT", "Paid", "Due"];
    public static readonly string[] StockHeaders = ["SKU", "Name", "Warehouse", "Qty", "Avg cost", "Value"];
    public static readonly string[] GlHeaders = ["Code", "Account", "Type", "Debit", "Credit", "Balance"];
    public static readonly string[] MetricHeaders = ["site_key", "metric_key", "value", "previous_value", "change_pct", "period_start", "computed_at"];

    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public sealed record Dataset(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<object?>> Rows, IReadOnlyDictionary<string, object?> Meta);

    public static Dataset FromDashboard(string siteKey, IReadOnlyDictionary<string, object?> dash)
    {
        var fromUnix = Unix(dash, "date_from");
        var toUnix = Unix(dash, "date_to");
        var from = fromUnix > 0 ? DateTimeOffset.FromUnixTimeSeconds(fromUnix).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
        var to = toUnix > 0 ? DateTimeOffset.FromUnixTimeSeconds(toUnix).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
        (string Metric, object Value, string Unit)[] map =
        [
            ("order_count", OrderCount(dash), "count"),
            ("revenue_ex_vat", Money(dash, "revenue_ex_vat"), "currency"),
            ("profit_ex_vat", Money(dash, "profit_ex_vat"), "currency"),
            ("receivable_due_orders", Money(dash, "receivable_due_orders"), "currency"),
            ("customer_ledger_balance", Money(dash, "customer_ledger_balance"), "currency"),
            ("payable_balance", Money(dash, "payable_balance"), "currency"),
            ("cash_bank_total", Money(dash, "cash_bank_total"), "currency"),
            ("vat_net_payable", Money(dash, "vat_net_payable"), "currency"),
        ];
        var rows = new List<IReadOnlyList<object?>>(map.Length);
        foreach (var (metric, value, unit) in map)
        {
            rows.Add([siteKey, metric, value, from, to, unit]);
        }

        return new Dataset(KpiHeaders, rows, new Dictionary<string, object?>
        {
            ["source"] = "epc_erp_dashboard",
            ["period_from"] = from,
            ["period_to"] = to,
        });
    }

    public static Dataset OrdersUnavailable()
        => new(OrderHeaders, [], new Dictionary<string, object?> { ["error"] = "orders_unavailable" });

    public static Dataset ReportFailed(string message)
        => new(["error"], [], new Dictionary<string, object?> { ["error"] = message });

    public static Dataset MetricsFailed()
        => new(MetricHeaders, [], new Dictionary<string, object?> { ["error"] = "bi_query_failed" });

    public static Dataset OrdersReady(string siteKey, IReadOnlyList<IReadOnlyList<object?>> rows, int limit)
        => new(OrderHeaders, rows, new Dictionary<string, object?> { ["limit"] = limit, ["count"] = rows.Count });

    public static Dataset ReportReady(string type, long fromUnix, long toUnix, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows)
        => new(headers, rows, new Dictionary<string, object?>
        {
            ["type"] = type,
            ["from"] = fromUnix > 0 ? DateTimeOffset.FromUnixTimeSeconds(fromUnix).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "",
            ["to"] = toUnix > 0 ? DateTimeOffset.FromUnixTimeSeconds(toUnix).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "",
            ["count"] = rows.Count,
        });

    public static Dataset MetricsReady(IReadOnlyList<IReadOnlyList<object?>> rows)
        => new(MetricHeaders, rows, new Dictionary<string, object?> { ["count"] = rows.Count });

    public static string DatasetJson(string siteKey, string datasetId, Dataset dataset)
    {
        var objects = new List<Dictionary<string, object?>>(dataset.Rows.Count);
        foreach (var row in dataset.Rows)
        {
            var obj = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < dataset.Headers.Count; i++)
            {
                obj[dataset.Headers[i]] = i < row.Count ? row[i] : null;
            }

            objects.Add(obj);
        }

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["ok"] = true,
            ["tenant_site_key"] = siteKey,
            ["dataset"] = datasetId,
            ["count"] = objects.Count,
            ["meta"] = dataset.Meta,
            ["columns"] = dataset.Headers,
            ["rows"] = objects,
            ["power_bi"] = new Dictionary<string, object?>
            {
                ["format_hint"] = "Add ?format=csv for Power BI Web connector CSV refresh",
                ["scope"] = "read:bi",
            },
        }, Pretty);
    }

    public static bool WantsCsv(HttpRequest request)
    {
        var format = request.Query["format"].ToString().Trim();
        if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var accept = request.Headers.Accept.ToString();
        return accept.Contains("text/csv", StringComparison.OrdinalIgnoreCase);
    }

    public static string Csv(Dataset dataset)
    {
        var sb = new StringBuilder();
        sb.Append('\uFEFF');
        sb.AppendLine(CsvLine(dataset.Headers));
        foreach (var row in dataset.Rows)
        {
            sb.AppendLine(CsvLine(row.Select(cell => cell?.ToString() ?? "")));
        }

        return sb.ToString();
    }

    public static int OrdersLimit(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 100;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? Math.Clamp(n, 1, 200)
            : 1;
    }

    /// <summary>PHP <c>epc_power_bi_parse_date_param</c>: <c>YYYY-MM-DD</c> at UTC midnight, else the fallback unix time.</summary>
    public static long ParseDate(string? raw, long fallback)
    {
        if (!string.IsNullOrWhiteSpace(raw)
            && DateTime.TryParseExact(raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            return new DateTimeOffset(d.Year, d.Month, d.Day, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        }

        return fallback;
    }

    public static long DefaultFrom(long toUnix)
        => toUnix - (90L * 24 * 60 * 60);

    public static object Money(IReadOnlyDictionary<string, object?> dash, string key)
    {
        if (!dash.TryGetValue(key, out var raw) || raw is null)
        {
            return 0;
        }

        var rounded = Math.Round(Convert.ToDecimal(raw, CultureInfo.InvariantCulture), 2, MidpointRounding.AwayFromZero);
        if (rounded == decimal.Truncate(rounded))
        {
            return decimal.ToInt64(rounded);
        }

        return (double)rounded;
    }

    public static object Money(decimal value)
    {
        var rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        if (rounded == decimal.Truncate(rounded))
        {
            return decimal.ToInt64(rounded);
        }

        return (double)rounded;
    }

    private static int OrderCount(IReadOnlyDictionary<string, object?> dash)
    {
        if (!dash.TryGetValue("order_count", out var raw) || raw is null)
        {
            return 0;
        }

        return Convert.ToInt32(raw, CultureInfo.InvariantCulture);
    }

    private static long Unix(IReadOnlyDictionary<string, object?> dash, string key)
    {
        if (!dash.TryGetValue(key, out var raw) || raw is null)
        {
            return 0;
        }

        return Convert.ToInt64(raw, CultureInfo.InvariantCulture);
    }

    private static string CsvLine(IEnumerable<string> cells)
    {
        return string.Join(",", cells.Select(cell =>
        {
            if (cell.Contains('"') || cell.Contains(',') || cell.Contains('\n') || cell.Contains('\r'))
            {
                return "\"" + cell.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
            }

            return cell;
        }));
    }
}
