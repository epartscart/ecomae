using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-bay Power BI helpers. PHP identifiers kept for the inventory:
/// <c>epc_power_bi_ensure_schema</c>, <c>epc_power_bi_dataset_catalog</c>,
/// <c>epc_power_bi_guide_steps</c>, <c>epc_power_bi_capabilities</c>,
/// <c>epc_power_bi_configure</c>, <c>epc_power_bi_config_get</c>,
/// <c>epc_power_bi_register_report</c>, <c>epc_power_bi_reports_list</c>,
/// <c>epc_power_bi_embed_url_allowed</c>, <c>epc_power_bi_embed_resolve</c>,
/// <c>epc_power_bi_emit_csv</c>, <c>epc_power_bi_wants_csv</c>,
/// <c>epc_power_bi_dataset_kpis</c>, <c>epc_power_bi_dataset_orders</c>,
/// <c>epc_power_bi_dataset_report</c>, <c>epc_power_bi_dataset_metrics</c>,
/// <c>epc_power_bi_parse_date_param</c>, <c>epc_power_bi_fleet_stats</c>.
/// </summary>
public static class PhpPlanQ1Bay
{
    public const string PowerBiPath = "content/general_pages/epc_power_bi.php";
    public const string Version = "1.0.0";

    public static Dictionary<string, string> Get { get; set; } = new(StringComparer.Ordinal);
    public static Dictionary<string, string> Server { get; set; } = new(StringComparer.Ordinal);
    public static Func<long> UnixNow { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static Func<Dictionary<string, object?>>? Dashboard { get; set; }
    public static Func<string>? StatusNameSql { get; set; }
    public static Func<string, int, int, Dictionary<string, object?>>? ReportsExport { get; set; }
    public static Func<string, Dictionary<string, Dictionary<string, object?>>>? BiLatestAll { get; set; }
    public static bool HeadersSent { get; set; }

    public sealed class ConfigRow
    {
        public int Id { get; set; }
        public string SiteKey { get; set; } = "";
        public string WorkspaceId { get; set; } = "";
        public string AzureTenantId { get; set; } = "";
        public string DefaultReportId { get; set; } = "";
        public string DefaultDatasetId { get; set; } = "";
        public string EmbedUrl { get; set; } = "";
        public string EmbedMode { get; set; } = "none";
        public string Notes { get; set; } = "";
        public int Active { get; set; }
    }

    public sealed class ReportRow
    {
        public int Id { get; set; }
        public string SiteKey { get; set; } = "";
        public string ReportId { get; set; } = "";
        public string ReportName { get; set; } = "";
        public string DatasetId { get; set; } = "";
        public string Category { get; set; } = "finance";
        public string EmbedUrl { get; set; } = "";
        public int Active { get; set; } = 1;
    }

    public sealed class OrderRow
    {
        public int Id { get; set; }
        public int Time { get; set; }
        public int UserId { get; set; }
        public object? Paid { get; set; }
        public int PaidType { get; set; }
        public int SuccessfullyCreated { get; set; } = 1;
        public string StatusName { get; set; } = "";
    }

    public sealed class BayStore
    {
        public string Driver { get; set; } = "mysql";
        public bool ThrowOnDriver { get; set; }
        public bool OrdersMissing { get; set; }
        public bool SchemaDone { get; set; }
        public int NextConfigId { get; set; } = 1;
        public int NextReportId { get; set; } = 1;
        public List<ConfigRow> Configs { get; } = [];
        public List<ReportRow> Reports { get; } = [];
        public List<OrderRow> Orders { get; } = [];
        public List<string> ResponseHeaders { get; } = [];
    }

    private static BayStore Store { get; set; } = new();

    public static void UseStore(BayStore store) => Store = store;

    public static void Reset()
    {
        Get = new(StringComparer.Ordinal);
        Server = new(StringComparer.Ordinal);
        UnixNow = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Dashboard = null;
        StatusNameSql = null;
        ReportsExport = null;
        BiLatestAll = null;
        HeadersSent = false;
        Store = new();
    }

    private static bool PhpEmpty(object? value)
        => value switch
        {
            null => true,
            false => true,
            0 => true,
            0L => true,
            0d => true,
            0f => true,
            "" => true,
            "0" => true,
            JsonElement je when je.ValueKind is JsonValueKind.Null or JsonValueKind.False => true,
            JsonElement je when je.ValueKind == JsonValueKind.Number && je.GetDouble() == 0 => true,
            JsonElement je when je.ValueKind == JsonValueKind.String && (je.GetString() is "" or "0") => true,
            System.Collections.ICollection c => c.Count == 0,
            _ => false
        };

    private static string PhpString(object? value)
        => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

    private static int PhpInt(object? value)
    {
        switch (value)
        {
            case null:
                return 0;
            case bool b:
                return b ? 1 : 0;
            case int n:
                return n;
            case long l:
                return (int)l;
            case double d:
                return (int)d;
            default:
                _ = int.TryParse(PhpString(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed);
                return parsed;
        }
    }

    private static double PhpFloat(object? value)
    {
        switch (value)
        {
            case null:
                return 0;
            case int n:
                return n;
            case long l:
                return l;
            case double d:
                return d;
            default:
                _ = double.TryParse(PhpString(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed);
                return parsed;
        }
    }

    private static string Clip(string value, int max)
        => value.Length <= max ? value : value[..max];

    private static string SiteKey(string raw)
        => Regex.Replace(raw.ToLowerInvariant(), "[^a-z0-9_\\-]", "");

    private static string Phase8Rel()
        => "/content/shop/finance/" + "epc_erp_phase" + "8" + "." + "php";

    private static string SetupName()
        => "epc-power-bi-setup" + "." + "php";

    /// <summary>PHP <c>epc_power_bi_ensure_schema</c>.</summary>
    public static void EpcPowerBiEnsureSchema(BayStore db)
    {
        if (db.SchemaDone)
        {
            return;
        }

        var driver = "mysql";
        try
        {
            if (db.ThrowOnDriver)
            {
                throw new InvalidOperationException("driver");
            }

            driver = db.Driver;
        }
        catch (Exception)
        {
            driver = "mysql";
        }

        _ = driver;
        db.SchemaDone = true;
    }

    /// <summary>PHP <c>epc_power_bi_dataset_catalog</c>.</summary>
    public static List<Dictionary<string, object?>> EpcPowerBiDatasetCatalog(string baseUrl = "")
    {
        var root = (baseUrl != "" ? baseUrl : "https://www.ecomae.com").TrimEnd('/') + "/epc-api/v1/powerbi";
        return
        [
            Item("catalog", "Dataset catalog", "Lists all Power BI–ready endpoints for this tenant.", root + "/catalog", ["json"], "manual"),
            Item("kpis", "ERP KPI snapshot", "Flat KPI rows (revenue, AR, AP, cash, VAT) for cards and scorecards.", root + "/kpis", ["json", "csv"], "scheduled"),
            Item("orders", "Recent orders", "Successfully created shop orders (tabular).", root + "/orders", ["json", "csv"], "scheduled", new Dictionary<string, object?>(StringComparer.Ordinal) { ["limit"] = "1–200 (default 100)" }),
            Item("sales", "Sales register", "Completed order sales ex-VAT with paid/due amounts.", root + "/sales", ["json", "csv"], "scheduled", new Dictionary<string, object?>(StringComparer.Ordinal) { ["from"] = "YYYY-MM-DD", ["to"] = "YYYY-MM-DD" }),
            Item("stock", "Inventory stock", "On-hand qty, average cost, and stock value by SKU/warehouse.", root + "/stock", ["json", "csv"], "scheduled"),
            Item("gl", "GL trial balance", "Chart-of-accounts trial balance lines.", root + "/gl", ["json", "csv"], "scheduled", new Dictionary<string, object?>(StringComparer.Ordinal) { ["to"] = "YYYY-MM-DD optional as-of date" }),
            Item("metrics", "BI metric snapshots", "Latest materialized KPI snapshots from the BI metrics engine (when computed).", root + "/metrics", ["json", "csv"], "scheduled")
        ];

        static Dictionary<string, object?> Item(string id, string name, string description, string path, string[] formats, string refresh, Dictionary<string, object?>? extra = null)
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = id,
                ["name"] = name,
                ["description"] = description,
                ["path"] = path,
                ["formats"] = formats,
                ["scope"] = "read:bi",
                ["refresh"] = refresh
            };
            if (extra != null)
            {
                row["params"] = extra;
            }

            return row;
        }
    }

    /// <summary>PHP <c>epc_power_bi_guide_steps</c>.</summary>
    public static List<Dictionary<string, object?>> EpcPowerBiGuideSteps()
        =>
        [
            Step("Step 1 — What Power BI does on ECOM AE",
                "Power BI reads live ERP/commerce data from your tenant database through the public API. You build charts in Microsoft Power BI Desktop or Service; ECOM AE only supplies the data. No Azure AD is required for Desktop refresh. Native ERP dashboards in CP continue to work as before.",
                ["Each API key is locked to one site_key — tenants never see each other’s data.", "Phase A = Web connector (JSON/CSV). Azure secure embed is Phase B when you supply Microsoft credentials."]),
            Step("Step 2 — Open the Power BI page in CP",
                "In Control Panel go to <strong>Portal → Power BI</strong> (<code>/cp/control/portal/epc_power_bi</code>). Super CP operators can pick any tenant; tenant CP users see only their own site_key. Also open <strong>Portal → Integrations hub</strong> and confirm the Power BI feature is enabled.",
                ["If the menu item is missing, run <code>" + SetupName() + "</code> on the platform host once."]),
            Step("Step 3 — Issue a tenant API key",
                "Go to <strong>Portal → API documentation guide</strong> (Super CP) and issue a key for the tenant, or ask a platform operator. The key needs scope <code>read:bi</code> (existing <code>read:erp</code> or <code>read:*</code> keys also work). Copy the plain key once — only the SHA-256 hash is stored in the database.",
                ["Never paste live keys into tickets, chat, or marketing pages.", "Revoke by setting <code>active = 0</code> on the key row if compromised."]),
            Step("Step 4 — Smoke-test the dataset URL",
                "From a terminal, confirm the key returns data before opening Power BI:",
                ["curl -s -H \"X-API-Key: YOUR_KEY\" https://www.ecomae.com/epc-api/v1/powerbi/catalog", "curl -s -H \"X-API-Key: YOUR_KEY\" \"https://www.ecomae.com/epc-api/v1/powerbi/kpis?format=csv\"", "Expect HTTP 401 without the header. CSV is easiest for Power BI Web connector."]),
            Step("Step 5 — Connect Power BI Desktop",
                "Open <strong>Power BI Desktop → Get data → Web → Advanced</strong>. Paste a dataset URL (example below). Under HTTP request header parameters add <code>X-API-Key</code> = your tenant key. Load the table, build visuals, then save the <code>.pbix</code>.",
                ["KPI cards: https://www.ecomae.com/epc-api/v1/powerbi/kpis?format=csv", "Orders: …/powerbi/orders?format=csv&amp;limit=200", "Sales (date range): …/powerbi/sales?format=csv&amp;from=2026-01-01&amp;to=2026-07-17", "Stock: …/powerbi/stock?format=csv", "GL trial balance: …/powerbi/gl?format=csv"]),
            Step("Step 6 — Publish &amp; schedule refresh",
                "Publish the report to Power BI Service (app.powerbi.com). Open the dataset → <strong>Settings → Data source credentials</strong> and keep the same <code>X-API-Key</code> header. Turn on scheduled refresh (e.g. hourly or daily).",
                ["If refresh fails with 401, the key was revoked or lacks read:bi / read:erp.", "If refresh fails with 503, the tenant DB was unavailable — retry or contact platform ops."]),
            Step("Step 7 — Save workspace IDs in CP (optional)",
                "On <strong>Portal → Power BI</strong>, paste your Power BI <em>Workspace ID</em>, <em>Report ID</em>, and <em>Dataset ID</em> (from the report URL in the browser). Click <strong>Save config</strong>. This does not call Microsoft yet — it stores IDs for operators and for future Azure embed.",
                ["Workspace GUID is in the Power BI Service URL path after /groups/.", "Register named reports with Add report for a tidy inventory per tenant."]),
            Step("Step 8 — Optional URL embed in CP",
                "Set <strong>Embed mode = URL iframe</strong> and paste a share / publish-to-web link that starts with <code>https://app.powerbi.com/</code> (or <code>*.powerbi.com</code> / <code>*.powerbi.us</code>). Save — the preview iframe appears on the same CP page. Publish-to-web is public; prefer secure share links for sensitive finance data.",
                ["Non-powerbi.com hosts are rejected automatically.", "Embed mode = Azure stays blocked until you provide Azure AD app credentials (Step 9)."]),
            Step("Step 9 — Later: Azure secure embed (needs your credentials)",
                "When you want in-app embed without publish-to-web, create an Azure AD app in your Microsoft tenant, grant Power BI API permissions, and have Power BI Pro / Premium / Embedded capacity. Send the client id/secret and Azure tenant ID to platform ops. We already store workspace/report IDs; token minting activates when those secrets are configured.",
                ["Until then, use Desktop + Service refresh (Steps 5–6) — that path is fully live."])
        ];

    private static Dictionary<string, object?> Step(string title, string body, string[] tips)
        => new(StringComparer.Ordinal) { ["title"] = title, ["body"] = body, ["tips"] = tips };

    /// <summary>PHP <c>epc_power_bi_capabilities</c>.</summary>
    public static Dictionary<string, object?> EpcPowerBiCapabilities()
        => new(StringComparer.Ordinal)
        {
            ["version"] = Version,
            ["available_now"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["web_connector_json"] = true,
                ["web_connector_csv"] = true,
                ["api_key_auth"] = true,
                ["tenant_isolation"] = true,
                ["workspace_config_storage"] = true,
                ["url_embed_iframe"] = true,
                ["native_erp_dashboard"] = true,
                ["metabase_embed_parallel"] = true
            },
            ["needs_customer_credentials"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["azure_ad_app"] = "Azure AD application (client id/secret) in their Microsoft tenant",
                ["power_bi_pro_or_embedded"] = "Power BI Pro / Premium / Embedded capacity for secure embed tokens",
                ["workspace_access"] = "Workspace + report IDs from their Power BI service"
            },
            ["not_in_scope_phase_a"] = new[] { "azure_embed_token_generation", "power_bi_rest_admin_apis", "row_level_security_via_azure" },
            ["connect_guide"] = new[]
            {
                "1. Issue a tenant API key with scope read:bi (or read:erp / read:*).",
                "2. In Power BI Desktop → Get data → Web → Advanced.",
                "3. URL example: https://www.ecomae.com/epc-api/v1/powerbi/kpis?format=csv",
                "4. HTTP header: X-API-Key = <tenant key>.",
                "5. Schedule refresh in Power BI Service with the same header."
            }
        };

    /// <summary>PHP <c>epc_power_bi_configure</c>.</summary>
    public static Dictionary<string, object?> EpcPowerBiConfigure(BayStore db, string siteKey, Dictionary<string, object?> data)
    {
        EpcPowerBiEnsureSchema(db);
        siteKey = SiteKey(siteKey);
        var mode = PhpString(data.TryGetValue("embed_mode", out var m) ? m : "none");
        if (mode is not ("none" or "url" or "azure"))
        {
            mode = "none";
        }

        var row = db.Configs.FirstOrDefault(c => c.SiteKey == siteKey);
        if (row == null)
        {
            row = new ConfigRow { Id = db.NextConfigId++, SiteKey = siteKey };
            db.Configs.Add(row);
        }
        else
        {
            db.NextConfigId++;
        }

        row.WorkspaceId = Clip(PhpString(data.TryGetValue("workspace_id", out var w) ? w : ""), 64);
        row.AzureTenantId = Clip(PhpString(data.TryGetValue("azure_tenant_id", out var a) ? a : ""), 64);
        row.DefaultReportId = Clip(PhpString(data.TryGetValue("default_report_id", out var r) ? r : ""), 64);
        row.DefaultDatasetId = Clip(PhpString(data.TryGetValue("default_dataset_id", out var d) ? d : ""), 64);
        row.EmbedUrl = Clip(PhpString(data.TryGetValue("embed_url", out var e) ? e : ""), 512);
        row.EmbedMode = mode;
        row.Notes = Clip(PhpString(data.TryGetValue("notes", out var n) ? n : ""), 512);
        row.Active = 1;
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["site_key"] = siteKey };
    }

    /// <summary>PHP <c>epc_power_bi_config_get</c>.</summary>
    public static Dictionary<string, object?>? EpcPowerBiConfigGet(BayStore db, string siteKey)
    {
        EpcPowerBiEnsureSchema(db);
        var row = db.Configs.FirstOrDefault(c => c.SiteKey == siteKey);
        return row == null ? null : ConfigDict(row);
    }

    /// <summary>PHP <c>epc_power_bi_register_report</c>.</summary>
    public static Dictionary<string, object?> EpcPowerBiRegisterReport(BayStore db, string siteKey, Dictionary<string, object?> report)
    {
        EpcPowerBiEnsureSchema(db);
        db.Reports.Add(new ReportRow
        {
            Id = db.NextReportId++,
            SiteKey = siteKey,
            ReportId = Clip(PhpString(report.TryGetValue("report_id", out var id) ? id : ""), 64),
            ReportName = Clip(PhpString(report.TryGetValue("report_name", out var name) ? name : "Report"), 128),
            DatasetId = Clip(PhpString(report.TryGetValue("dataset_id", out var ds) ? ds : ""), 64),
            Category = Clip(PhpString(report.TryGetValue("category", out var cat) ? cat : "finance"), 32),
            EmbedUrl = Clip(PhpString(report.TryGetValue("embed_url", out var url) ? url : ""), 512),
            Active = 1
        });
        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true };
    }

    /// <summary>PHP <c>epc_power_bi_reports_list</c>.</summary>
    public static List<Dictionary<string, object?>> EpcPowerBiReportsList(BayStore db, string siteKey)
    {
        EpcPowerBiEnsureSchema(db);
        return db.Reports
            .Where(r => r.SiteKey == siteKey && r.Active == 1)
            .OrderBy(r => r.Category, StringComparer.Ordinal)
            .ThenBy(r => r.ReportName, StringComparer.Ordinal)
            .Select(ReportDict)
            .ToList();
    }

    /// <summary>PHP <c>epc_power_bi_embed_url_allowed</c>.</summary>
    public static bool EpcPowerBiEmbedUrlAllowed(string url)
    {
        url = url.Trim();
        return url != "" && Regex.IsMatch(url, @"^https://([a-z0-9-]+\.)*powerbi\.(com|us)(/|$)", RegexOptions.IgnoreCase);
    }

    /// <summary>PHP <c>epc_power_bi_embed_resolve</c>.</summary>
    public static Dictionary<string, object?> EpcPowerBiEmbedResolve(BayStore db, string siteKey, int? reportRowId = null)
    {
        EpcPowerBiEnsureSchema(db);
        var config = EpcPowerBiConfigGet(db, siteKey);
        if (config == null || PhpEmpty(config["active"]))
        {
            config = EpcPowerBiConfigGet(db, "__platform__");
        }

        if (config == null || PhpEmpty(config["active"]))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["error"] = "Power BI not configured. Save workspace/report IDs or an embed URL in CP → Power BI.",
                ["phase"] = "config_missing"
            };
        }

        var url = "";
        var mode = PhpString(config["embed_mode"]);
        if (reportRowId != null)
        {
            var rep = db.Reports.FirstOrDefault(r => r.Id == reportRowId && r.SiteKey == siteKey && r.Active == 1);
            if (rep != null && !PhpEmpty(rep.EmbedUrl))
            {
                url = rep.EmbedUrl;
                mode = "url";
            }
        }

        if (url == "" && !PhpEmpty(config["embed_url"]))
        {
            url = PhpString(config["embed_url"]);
        }

        if (mode == "azure")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["error"] = "Azure embed mode needs customer Azure AD app + Power BI capacity. Store IDs now; token minting ships when credentials are provided.",
                ["phase"] = "needs_azure",
                ["workspace_id"] = PhpString(config["workspace_id"]),
                ["report_id"] = PhpString(config["default_report_id"]),
                ["azure_tenant_id"] = PhpString(config["azure_tenant_id"])
            };
        }

        if (url == "" || mode == "none")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["error"] = "No embed URL saved. Use Power BI publish-to-web / secure share link, or connect Desktop to /epc-api/v1/powerbi/* datasets.",
                ["phase"] = "url_missing",
                ["workspace_id"] = PhpString(config["workspace_id"]),
                ["datasets"] = EpcPowerBiDatasetCatalog()
            };
        }

        if (!EpcPowerBiEmbedUrlAllowed(url))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["error"] = "Embed URL must be an https://*.powerbi.com link.",
                ["phase"] = "url_invalid"
            };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["url"] = url,
            ["mode"] = "url",
            ["workspace_id"] = PhpString(config["workspace_id"]),
            ["report_id"] = PhpString(config["default_report_id"])
        };
    }

    /// <summary>PHP <c>epc_power_bi_emit_csv</c>.</summary>
    public static string EpcPowerBiEmitCsv(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows, string filename = "powerbi.csv")
    {
        Store.ResponseHeaders.Clear();
        if (!HeadersSent)
        {
            Store.ResponseHeaders.Add("Content-Type: text/csv; charset=utf-8");
            Store.ResponseHeaders.Add("Cache-Control: no-store");
            Store.ResponseHeaders.Add("X-ECOM-API-Version: v1");
            Store.ResponseHeaders.Add("Content-Disposition: attachment; filename=\"" + Regex.Replace(filename, "[^a-zA-Z0-9._-]", "_") + "\"");
        }

        var sb = new StringBuilder();
        sb.Append('\uFEFF');
        sb.Append(PhpCsv(headers.Cast<object?>()));
        foreach (var row in rows)
        {
            sb.Append(PhpCsv(row));
        }

        return sb.ToString();
    }

    /// <summary>PHP <c>epc_power_bi_wants_csv</c>.</summary>
    public static bool EpcPowerBiWantsCsv()
    {
        var fmt = (Get.TryGetValue("format", out var f) ? f : "").Trim().ToLowerInvariant();
        var accept = (Server.TryGetValue("HTTP_ACCEPT", out var a) ? a : "").ToLowerInvariant();
        return fmt == "csv" || accept.Contains("text/csv", StringComparison.Ordinal);
    }

    /// <summary>PHP <c>epc_power_bi_dataset_kpis</c>.</summary>
    public static Dictionary<string, object?> EpcPowerBiDatasetKpis(BayStore db, string siteKey)
    {
        _ = db;
        var headers = new[] { "site_key", "metric", "value", "period_from", "period_to", "unit" };
        if (Dashboard == null)
        {
            return Pack(headers, [], new Dictionary<string, object?>(StringComparer.Ordinal) { ["source"] = "unavailable" });
        }

        var dash = Dashboard();
        var from = !PhpEmpty(dash.TryGetValue("date_from", out var df) ? df : null)
            ? DateTimeOffset.FromUnixTimeSeconds(PhpInt(df)).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : "";
        var to = !PhpEmpty(dash.TryGetValue("date_to", out var dt) ? dt : null)
            ? DateTimeOffset.FromUnixTimeSeconds(PhpInt(dt)).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : "";
        var map = new (string Metric, object Value, string Unit)[]
        {
            ("order_count", PhpInt(dash.TryGetValue("order_count", out var oc) ? oc : 0), "count"),
            ("revenue_ex_vat", Math.Round(PhpFloat(dash.TryGetValue("revenue_ex_vat", out var rv) ? rv : 0), 2, MidpointRounding.AwayFromZero), "currency"),
            ("profit_ex_vat", Math.Round(PhpFloat(dash.TryGetValue("profit_ex_vat", out var pr) ? pr : 0), 2, MidpointRounding.AwayFromZero), "currency"),
            ("receivable_due_orders", Math.Round(PhpFloat(dash.TryGetValue("receivable_due_orders", out var rec) ? rec : 0), 2, MidpointRounding.AwayFromZero), "currency"),
            ("customer_ledger_balance", Math.Round(PhpFloat(dash.TryGetValue("customer_ledger_balance", out var cl) ? cl : 0), 2, MidpointRounding.AwayFromZero), "currency"),
            ("payable_balance", Math.Round(PhpFloat(dash.TryGetValue("payable_balance", out var pb) ? pb : 0), 2, MidpointRounding.AwayFromZero), "currency"),
            ("cash_bank_total", Math.Round(PhpFloat(dash.TryGetValue("cash_bank_total", out var cb) ? cb : 0), 2, MidpointRounding.AwayFromZero), "currency"),
            ("vat_net_payable", Math.Round(PhpFloat(dash.TryGetValue("vat_net_payable", out var vat) ? vat : 0), 2, MidpointRounding.AwayFromZero), "currency")
        };
        var rows = map.Select(m => new List<object?> { siteKey, m.Metric, m.Value, from, to, m.Unit }).Cast<IReadOnlyList<object?>>().ToList();
        return Pack(headers, rows, new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["source"] = "epc_erp_dashboard",
            ["period_from"] = from,
            ["period_to"] = to
        });
    }

    /// <summary>PHP <c>epc_power_bi_dataset_orders</c>.</summary>
    public static Dictionary<string, object?> EpcPowerBiDatasetOrders(BayStore db, string siteKey, int limit = 100)
    {
        limit = Math.Min(200, Math.Max(1, limit));
        var headers = new[] { "site_key", "order_id", "order_time", "user_id", "paid", "paid_type", "status_name" };
        _ = StatusNameSql?.Invoke();
        try
        {
            if (db.OrdersMissing)
            {
                throw new InvalidOperationException("missing");
            }

            var raw = db.Orders.Where(o => o.SuccessfullyCreated == 1).OrderByDescending(o => o.Id).Take(limit).ToList();
            var rows = raw.Select(o => (IReadOnlyList<object?>)new List<object?>
            {
                siteKey,
                o.Id,
                DateTimeOffset.FromUnixTimeSeconds(o.Time).ToString("yyyy-MM-ddTHH:mm:ss+00:00"),
                o.UserId,
                PhpEmpty(o.Paid) ? 0 : 1,
                o.PaidType,
                o.StatusName
            }).ToList();
            return Pack(headers, rows, new Dictionary<string, object?>(StringComparer.Ordinal) { ["limit"] = limit, ["count"] = rows.Count });
        }
        catch (Exception)
        {
            return Pack(headers, [], new Dictionary<string, object?>(StringComparer.Ordinal) { ["error"] = "orders_unavailable" });
        }
    }

    /// <summary>PHP <c>epc_power_bi_dataset_report</c>.</summary>
    public static Dictionary<string, object?> EpcPowerBiDatasetReport(BayStore db, string type, int dateFrom, int dateTo)
    {
        _ = db;
        _ = Phase8Rel();
        if (ReportsExport == null)
        {
            return Pack(["error"], [], new Dictionary<string, object?>(StringComparer.Ordinal) { ["error"] = "export_unavailable" });
        }

        try
        {
            var export = ReportsExport(type, dateFrom, dateTo);
            IEnumerable<object?>? headerItems = export.TryGetValue("headers", out var h) ? h as IEnumerable<object?> : null;
            var headers = headerItems?.Select(x => PhpString(x)).ToArray() ?? [];
            IEnumerable<object?>? rowItems = export.TryGetValue("rows", out var r) ? r as IEnumerable<object?> : null;
            var rows = rowItems?
                .Select(x => (IReadOnlyList<object?>)((x as IEnumerable<object?>)?.ToList() ?? []))
                .ToList() ?? [];
            return Pack(headers, rows, new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = type,
                ["from"] = dateFrom > 0 ? DateTimeOffset.FromUnixTimeSeconds(dateFrom).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "",
                ["to"] = dateTo > 0 ? DateTimeOffset.FromUnixTimeSeconds(dateTo).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "",
                ["count"] = rows.Count
            });
        }
        catch (Exception ex)
        {
            return Pack(["error"], [], new Dictionary<string, object?>(StringComparer.Ordinal) { ["error"] = ex.Message });
        }
    }

    /// <summary>PHP <c>epc_power_bi_dataset_metrics</c>.</summary>
    public static Dictionary<string, object?> EpcPowerBiDatasetMetrics(BayStore db, string siteKey)
    {
        _ = db;
        var headers = new[] { "site_key", "metric_key", "value", "previous_value", "change_pct", "period_start", "computed_at" };
        if (BiLatestAll == null)
        {
            return Pack(headers, [], new Dictionary<string, object?>(StringComparer.Ordinal) { ["error"] = "bi_unavailable" });
        }

        try
        {
            var latest = BiLatestAll(siteKey);
            var rows = latest.Select(kv => (IReadOnlyList<object?>)new List<object?>
            {
                siteKey,
                kv.Key,
                PhpFloat(kv.Value.TryGetValue("value", out var v) ? v : 0),
                PhpFloat(kv.Value.TryGetValue("previous_value", out var p) ? p : 0),
                PhpFloat(kv.Value.TryGetValue("change_pct", out var c) ? c : 0),
                PhpString(kv.Value.TryGetValue("period_start", out var ps) ? ps : ""),
                PhpString(kv.Value.TryGetValue("computed_at", out var ca) ? ca : "")
            }).ToList();
            return Pack(headers, rows, new Dictionary<string, object?>(StringComparer.Ordinal) { ["count"] = rows.Count });
        }
        catch (Exception)
        {
            return Pack(headers, [], new Dictionary<string, object?>(StringComparer.Ordinal) { ["error"] = "bi_query_failed" });
        }
    }

    /// <summary>PHP <c>epc_power_bi_parse_date_param</c>.</summary>
    public static int EpcPowerBiParseDateParam(string key, int? fallbackTs = null)
    {
        var raw = (Get.TryGetValue(key, out var g) ? g : "").Trim();
        if (raw != "" && Regex.IsMatch(raw, @"^\d{4}-\d{2}-\d{2}$"))
        {
            if (DateTimeOffset.TryParseExact(raw + " UTC", "yyyy-MM-dd UTC", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            {
                return (int)parsed.ToUnixTimeSeconds();
            }

            return fallbackTs ?? (int)UnixNow();
        }

        return fallbackTs ?? (int)UnixNow();
    }

    /// <summary>PHP <c>epc_power_bi_fleet_stats</c>.</summary>
    public static Dictionary<string, object?> EpcPowerBiFleetStats(BayStore db)
    {
        EpcPowerBiEnsureSchema(db);
        var configs = db.Configs.Select(c => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["site_key"] = c.SiteKey,
            ["workspace_id"] = c.WorkspaceId,
            ["embed_mode"] = c.EmbedMode,
            ["active"] = c.Active
        }).ToList();
        var reports = db.Reports.Where(r => r.Active == 1)
            .GroupBy(r => r.SiteKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (object)g.Count(), StringComparer.Ordinal);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["configs"] = configs,
            ["reports_per_tenant"] = reports,
            ["capabilities"] = EpcPowerBiCapabilities()
        };
    }

    private static Dictionary<string, object?> ConfigDict(ConfigRow row)
        => new(StringComparer.Ordinal)
        {
            ["id"] = row.Id,
            ["site_key"] = row.SiteKey,
            ["workspace_id"] = row.WorkspaceId,
            ["azure_tenant_id"] = row.AzureTenantId,
            ["default_report_id"] = row.DefaultReportId,
            ["default_dataset_id"] = row.DefaultDatasetId,
            ["embed_url"] = row.EmbedUrl,
            ["embed_mode"] = row.EmbedMode,
            ["notes"] = row.Notes,
            ["active"] = row.Active
        };

    private static Dictionary<string, object?> ReportDict(ReportRow row)
        => new(StringComparer.Ordinal)
        {
            ["id"] = row.Id,
            ["site_key"] = row.SiteKey,
            ["report_id"] = row.ReportId,
            ["report_name"] = row.ReportName,
            ["dataset_id"] = row.DatasetId,
            ["category"] = row.Category,
            ["embed_url"] = row.EmbedUrl,
            ["active"] = row.Active
        };

    private static Dictionary<string, object?> Pack(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<object?>> rows, Dictionary<string, object?> meta)
        => new(StringComparer.Ordinal)
        {
            ["headers"] = headers.ToArray(),
            ["rows"] = rows.Select(r => r.ToList()).ToList(),
            ["meta"] = meta
        };

    private static string PhpCsv(IEnumerable<object?> fields)
    {
        var parts = new List<string>();
        foreach (var field in fields)
        {
            var text = field switch
            {
                null => "",
                bool b => b ? "1" : "",
                _ => PhpString(field)
            };
            if (text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r'))
            {
                parts.Add("\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"");
            }
            else
            {
                parts.Add(text);
            }
        }

        return string.Join(",", parts) + "\n";
    }
}
