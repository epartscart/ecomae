using System.Globalization;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-horn BOC audit-log page.
/// PHP identifiers kept for the inventory: <c>epc_boc_h</c>, <c>epc_boc_brand</c>,
/// <c>epc_boc_audit_recent</c>.
/// Path: <c>cp/content/control/portal/epc_boc_audit_log.php</c>.
/// GET never mints a session cookie. Leftover unique portal, leftover unique
/// kernel parent, leftover unique user helper, and Super-CP host check stay
/// injected and are not named here.
/// </summary>
public static class PhpPlanQ1Horn
{
    public const string BocAuditLogPath = "cp/content/control/portal/epc_boc_audit_log.php";
    public const string DeniedSuper =
        "<div class=\"alert alert-warning\">Audit log is available on <strong>BOC</strong> only.</div>";
    public const string DeniedDb =
        "<div class=\"alert alert-danger\">Platform database unavailable.</div>";

    public static Func<bool>? IsSuperCp { get; set; }
    public static Func<bool>? IsAdmin { get; set; }
    public static object? BackendDir { get; set; }
    public static bool HasPdo { get; set; }
    public static string AreaQuery { get; set; } = "";
    public static bool AreaQuerySet { get; set; }
    public static List<Dictionary<string, object?>> AuditRows { get; set; } = new();
    public static string LastOutput { get; private set; } = "";

    public static void Reset()
    {
        IsSuperCp = null;
        IsAdmin = null;
        BackendDir = null;
        HasPdo = false;
        AreaQuery = "";
        AreaQuerySet = false;
        AuditRows = new List<Dictionary<string, object?>>();
        LastOutput = "";
    }

    public static string EpcBocH(object? value)
        => Str(value)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal);

    public static Dictionary<string, object?> EpcBocBrand()
        => new(StringComparer.Ordinal)
        {
            ["name"] = "Business Operation System",
            ["short"] = "BOS",
            ["control"] = "Control",
            ["legacy"] = "Super CP",
            ["tagline"] = "One BOS for every tenant, ERP-only client and demo — operators control the whole fleet, each tenant sees only their own area."
        };

    public static string EpcBocSanitizeArea(object? value)
        => Regex.Replace(Str(value), "[^a-z0-9_.]", "");

    public static IReadOnlyList<Dictionary<string, object?>> EpcBocAuditRecent(int limit = 100, string area = "")
    {
        limit = Math.Max(1, Math.Min(1000, limit));
        IEnumerable<Dictionary<string, object?>> query = AuditRows;
        if (area != "")
        {
            query = query.Where(row => Str(Get(row, "area")) == area);
        }

        return query
            .OrderByDescending(row => ToInt64(Get(row, "id")))
            .Take(limit)
            .ToList();
    }

    public static string EpcBocAuditLogPage()
    {
        if (IsSuperCp?.Invoke() != true)
        {
            LastOutput = DeniedSuper;
            return LastOutput;
        }

        if (IsAdmin?.Invoke() != true)
        {
            LastOutput = "<div class=\"alert alert-warning\">Please <a href=\"/" + EpcBocH(BackendDir) + "/\">log in to BOC</a>.</div>";
            return LastOutput;
        }

        if (!HasPdo)
        {
            LastOutput = DeniedDb;
            return LastOutput;
        }

        var areaFilter = AreaQuerySet ? EpcBocSanitizeArea(AreaQuery) : "";
        var rows = EpcBocAuditRecent(500, areaFilter);
        _ = EpcBocBrand();
        LastOutput = RenderTable(areaFilter, rows);
        return LastOutput;
    }

    public static string RenderTable(string areaFilter, IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var blurb = areaFilter != ""
            ? "Filtered to area <strong>" + EpcBocH(areaFilter) + "</strong>."
            : "Showing the latest 500 entries.";
        // PHP eats the newline after ?>; leftover indent from the PHP tag stays.
        var html = "<div class=\"col-lg-12 epc-erp-shell\" id=\"epc-boc-audit-log\">\n"
            + "    <div class=\"hpanel\"><div class=\"panel-body\">\n"
            + "        <div style=\"background:linear-gradient(135deg,#3f2d5c,#5b3f86);color:#fff;border-radius:12px;padding:18px;margin-bottom:16px\">\n"
            + "            <h3 style=\"margin:0 0 6px;color:#fff\"><i class=\"fa fa-history\"></i> BOC audit log</h3>\n"
            + "            <p style=\"margin:0;opacity:.92\">Immutable record of privileged operator actions. " + blurb + "</p>\n"
            + "        </div>\n";
        if (rows.Count == 0)
        {
            html += "                <p class=\"text-muted\">No audited actions recorded yet.</p>\n"
                + "            </div></div>\n</div>\n";
        }
        else
        {
            html += "                <table class=\"table table-condensed table-striped\" style=\"font-size:12px\">\n"
                + "            <thead><tr><th>When</th><th>Operator</th><th>Area</th><th>Action</th><th>Target</th><th>IP</th><th>Detail</th></tr></thead>\n"
                + "            <tbody>\n";
            foreach (var row in rows)
            {
                var meta = FormatMeta(Get(row, "meta"));
                var actor = Str(Get(row, "actor"));
                var operatorLabel = actor != "" ? actor : ("#" + ToInt64(Get(row, "user_id")).ToString(CultureInfo.InvariantCulture));
                html += "                            <tr>\n"
                    + "                    <td class=\"text-muted\" style=\"white-space:nowrap\">" + EpcBocH(FormatTs(Get(row, "ts"))) + "</td>\n"
                    + "                    <td>" + EpcBocH(operatorLabel) + "</td>\n"
                    + "                    <td><code style=\"font-size:11px\">" + EpcBocH(Get(row, "area")) + "</code></td>\n"
                    + "                    <td>" + EpcBocH(Get(row, "action")) + "</td>\n"
                    + "                    <td class=\"text-muted\">" + EpcBocH(Get(row, "target")) + "</td>\n"
                    + "                    <td class=\"text-muted\">" + EpcBocH(Get(row, "ip")) + "</td>\n"
                    + "                    <td class=\"text-muted\" style=\"font-size:11px\">" + EpcBocH(meta) + "</td>\n"
                    + "                </tr>\n";
            }

            html += "                        </tbody>\n"
                + "        </table>\n"
                + "            </div></div>\n</div>\n";
        }

        return html;
    }

    private static string FormatTs(object? value)
    {
        var ts = ToInt64(value);
        if (ts < 0)
        {
            ts = 0;
        }

        return DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static string FormatMeta(object? value)
    {
        var raw = Str(value);
        if (raw == "" || raw == "0")
        {
            return "";
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                return raw;
            }

            var parts = new List<string>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                parts.Add(prop.Name + "=" + ScalarOrJson(prop.Value));
            }

            return string.Join(", ", parts);
        }
        catch (System.Text.Json.JsonException)
        {
            return raw;
        }
    }

    private static string ScalarOrJson(System.Text.Json.JsonElement value)
        => value.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => value.GetString() ?? "",
            System.Text.Json.JsonValueKind.Number => value.GetRawText(),
            System.Text.Json.JsonValueKind.True => "1",
            System.Text.Json.JsonValueKind.False => "",
            System.Text.Json.JsonValueKind.Null => "",
            _ => value.GetRawText()
        };

    private static object? Get(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) ? value : null;

    private static long ToInt64(object? value)
    {
        if (value is null || value is "")
        {
            return 0;
        }

        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static string Str(object? value)
        => value is null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
}
