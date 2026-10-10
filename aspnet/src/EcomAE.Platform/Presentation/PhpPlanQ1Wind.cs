using System.Globalization;
using System.Text.Json;
using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-wind price-upload diagnostics. PHP identifiers kept for the inventory:
/// <c>epc_pyprices_api_url</c>, <c>epc_price_upload_diagnostics_snapshot</c>,
/// <c>epc_price_upload_channel_definitions</c>, <c>epc_price_upload_run_health_checks</c>,
/// <c>epc_price_upload_curl_json</c>, <c>epc_price_upload_curl_raw</c>.
/// Path: <c>content/shop/docpart/epc_price_upload_diagnostics.php</c>.
/// GET never mints a session cookie. Leftover history-schema parent stays injected. HTTP stays injected.
/// </summary>
public static class PhpPlanQ1Wind
{
    public const string PriceUploadDiagnosticsPath = "content/shop/docpart/epc_price_upload_diagnostics.php";

    public static Action<MySqlConnection>? EnsureSchema { get; set; }
    public static Func<string, int, Dictionary<string, string>?, Dictionary<string, object?>>? CurlRaw { get; set; }
    public static Func<string>? Clock { get; set; }
    public static Func<string, bool>? PathIsFile { get; set; }
    public static Func<string, bool>? PathIsDir { get; set; }
    public static Func<string, bool>? PathWritable { get; set; }
    public static Func<string, bool>? EnsureDir { get; set; }
    public static string DocumentRoot { get; set; } = "";
    public static string DomainPath { get; set; } = "";

    public static void Reset()
    {
        EnsureSchema = _ => { };
        CurlRaw = (_, _, _) => new Dictionary<string, object?>(StringComparer.Ordinal) { ["http_code"] = 0, ["body"] = "" };
        Clock = () => "2026-10-10 17:00:00";
        PathIsFile = _ => false;
        PathIsDir = _ => false;
        PathWritable = _ => false;
        EnsureDir = _ => false;
        DocumentRoot = "";
        DomainPath = "";
    }

    public static string EpcPypricesApiUrl(string domainPath = "")
    {
        var domain = domainPath.Trim();
        if (domain == "")
        {
            domain = DomainPath;
        }

        if (domain == "")
        {
            return "/pyprices/pyprices-api.php";
        }

        return domain.TrimEnd('/') + "/pyprices/pyprices-api.php";
    }

    public static List<Dictionary<string, object?>> EpcPriceUploadChannelDefinitions(Dictionary<string, object?> config)
    {
        var backend = Str(config, "backend_dir");
        if (backend == "")
        {
            backend = "cp";
        }

        var domain = Str(config, "domain_path");
        var techKey = Str(config, "tech_key");
        return
        [
            Chan("cp_wizard", "CP manual upload wizard (file from PC)", 1, "PHP ajax_1–ajax_6", "/" + backend + "/shop/prices/upload?price_id={id}", "/" + backend + "/shop/prices/price?price_id={id}", "cp_wizard", "CSV, TXT, ZIP/RAR/7z (Excel converted in step 3)", "Upload a small CSV via green upload button or wizard page; check Upload history."),
            Chan("pyprices_pc", "Pyprices — file from PC (manager row)", 1, "pyprices/pyprices-api.php + upload_file.php", "/" + backend + "/shop/prices", "/" + backend + "/shop/prices/price?price_id={id}", "pyprices_upload", "CSV, XLSX, archives per pyprices", "Use file input on price row → wait for task completion → refresh record count."),
            Chan("pyprices_ftp", "Pyprices — FTP", 2, "pyprices/pyprices-api.php (ftp)", "/" + backend + "/shop/prices", "/" + backend + "/shop/prices/price?price_id={id}", "pyprices_ftp", "File on FTP matching file_name_substring", "Configure FTP on price list → Manual update FTP icon → verify rows imported."),
            Chan("pyprices_email", "Pyprices — E-mail", 3, "pyprices/pyprices-api.php (email/IMAP)", "/" + backend + "/shop/prices", "/" + backend + "/shop/prices/price?price_id={id}", "pyprices_email", "Attachment in mailbox (site mail config required)", "Send test file from allowed sender → Manual update email icon."),
            Chan("pyprices_url", "Pyprices — URL / link", 4, "pyprices/pyprices-api.php (url) or wizard download", "/" + backend + "/shop/prices", "/" + backend + "/shop/prices/price?price_id={id}", "pyprices_url", "File at URL in price list `link` field", "Set link on price → Manual update URL icon or wizard with load_mode=4."),
            Chan("cron_scheduled", "Scheduled automatic update (cron)", null, "cron_crutch.php → cron_task_executor.php → pyprices", "/" + backend + "/shop/prices", "/" + backend + "/shop/prices (schedule column)", "pyprices_ftp|pyprices_email|pyprices_url", "Same as FTP/email/URL per list in schedule", "Create schedule for one list → wait for cron minute → check last_updated.", domain + backend + "/content/shop/prices_upload/for_pyprices/for_cron/cron_crutch.php?key=" + techKey),
            Chan("deploy_api", "Deploy API (epc-upload-uae-prices.php)", null, "Direct PHP import", "(external) POST multipart", "/epc-upload-uae-prices.php", "deploy_api", "CSV, TXT, XLSX", "POST with token + tech_key + price_file; verify history_id in JSON."),
            Chan("multivendor", "Multi-vendor Excel → auto warehouses (full + short name)", null, "epc_multivendor_price_ingest.php", "/" + backend + "/shop/prices/multivendor", "/epc-upload-multivendor-prices.php", "multivendor", "Excel/CSV with Vendor full name + Vendor short columns", "Upload sample CSV → one warehouse/list per vendor_short; storefront shows short_name only."),
            Chan("api_upload_price", "Legacy API (api/prices/upload_price.php)", null, "Staged file → ajax_5 import", "/api/prices/upload_price.php", "tech_key + price id + document file", "cp_wizard", "CSV per Treelax API", "POST tech_key and file; confirm ajax_5 runs for target price_id."),
            Chan("manual_edit", "Manual row edit (not file import)", null, "prices_edit/ajax_operations.php", "/" + backend + "/shop/prices/prices_edit", "", "", "Single rows in grid", "Add one row in prices edit → confirm in storefront search."),
            Chan("price_review", "Price review (adjust prices, not bulk load)", null, "price_review/ajax_price_review.php", "/" + backend + "/shop/prices/review?price_id={id}", "", "", "Updates existing DB rows", "Open review → change price → export CSV optional.")
        ];
    }

    public static Dictionary<string, object?> EpcPriceUploadDiagnosticsSnapshot(MySqlConnection db, Dictionary<string, object?> config)
    {
        EnsureSchema?.Invoke(db);
        Dictionary<int, string> loadModes;
        try
        {
            loadModes = new Dictionary<int, string>();
            using var q = db.CreateCommand();
            q.CommandText = "SELECT `id`, `name` FROM `shop_docpart_prices_load_modes` ORDER BY `id`";
            using var r = q.ExecuteReader();
            while (r.Read())
            {
                loadModes[ToInt(r["id"])] = Convert.ToString(r["name"]) ?? "";
            }
        }
        catch
        {
            loadModes = new Dictionary<int, string> { [1] = "Manual", [2] = "FTP", [3] = "E-mail", [4] = "URL" };
        }

        var byLoadMode = new Dictionary<string, object?>(StringComparer.Ordinal);
        var priceLists = 0;
        using (var q = db.CreateCommand())
        {
            q.CommandText = "SELECT p.`id`, p.`name`, p.`load_mode`, p.`last_updated`, (SELECT COUNT(*) FROM `shop_docpart_prices_data` d WHERE d.`price_id` = p.`id`) AS `records_count` FROM `shop_docpart_prices` p ORDER BY p.`name`";
            using var r = q.ExecuteReader();
            while (r.Read())
            {
                var lm = ToInt(r["load_mode"]);
                var key = lm.ToString(CultureInfo.InvariantCulture);
                if (!byLoadMode.TryGetValue(key, out var slotObj) || slotObj is not Dictionary<string, object?> slot)
                {
                    slot = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["count"] = 0,
                        ["records"] = 0,
                        ["lists"] = new List<Dictionary<string, object?>>()
                    };
                    byLoadMode[key] = slot;
                }

                slot["count"] = ToInt(slot["count"]) + 1;
                slot["records"] = ToInt(slot["records"]) + ToInt(r["records_count"]);
                ((List<Dictionary<string, object?>>)slot["lists"]!).Add(new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["id"] = ToInt(r["id"]),
                    ["name"] = Convert.ToString(r["name"]) ?? "",
                    ["last_updated"] = Convert.ToString(r["last_updated"]) ?? "",
                    ["records_count"] = ToInt(r["records_count"])
                });
                priceLists++;
            }
        }

        var historyBySource = new Dictionary<string, object?>(StringComparer.Ordinal);
        try
        {
            using var h = db.CreateCommand();
            h.CommandText = "SELECT `upload_source`, COUNT(*) AS `cnt`, MAX(`created_at`) AS `last_at` FROM `epc_price_upload_history` GROUP BY `upload_source`";
            using var r = h.ExecuteReader();
            while (r.Read())
            {
                historyBySource[Convert.ToString(r["upload_source"]) ?? ""] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["uploads"] = ToInt(r["cnt"]),
                    ["last_at"] = Convert.ToString(r["last_at"]) ?? ""
                };
            }
        }
        catch
        {
            historyBySource = new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        var cronTasks = 0;
        var cronPriceLinks = 0;
        try
        {
            cronTasks = ScalarInt(db, "SELECT COUNT(*) FROM `shop_docpart_pyprices_crontab`");
            cronPriceLinks = ScalarInt(db, "SELECT COUNT(*) FROM `shop_docpart_pyprices_crontab_prices`");
        }
        catch
        {
            cronTasks = -1;
        }

        var pendingTasks = 0;
        try
        {
            pendingTasks = ScalarInt(db, "SELECT COUNT(*) FROM `shop_docpart_pyprices_tasks` WHERE `status` IS NULL OR `status` = '' OR `status` NOT IN ('done','completed','error','failed')");
        }
        catch
        {
            pendingTasks = -1;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["generated_at"] = Clock != null ? Clock() : "2026-10-10 17:00:00",
            ["load_modes"] = loadModes.ToDictionary(kv => kv.Key.ToString(CultureInfo.InvariantCulture), kv => (object?)kv.Value, StringComparer.Ordinal),
            ["by_load_mode"] = byLoadMode,
            ["price_lists_total"] = priceLists,
            ["history_by_source"] = historyBySource,
            ["cron_tasks"] = cronTasks,
            ["cron_price_links"] = cronPriceLinks,
            ["pyprices_pending_tasks"] = pendingTasks,
            ["channels"] = EpcPriceUploadChannelDefinitions(config)
        };
    }

    public static Dictionary<string, object?> EpcPriceUploadRunHealthChecks(Dictionary<string, object?> config)
    {
        var domain = Str(config, "domain_path").TrimEnd('/');
        var backend = Str(config, "backend_dir");
        if (backend == "")
        {
            backend = "cp";
        }

        var docRoot = DocumentRoot;
        var checks = new Dictionary<string, object?>(StringComparer.Ordinal);
        var pyUrl = EpcPypricesApiUrl(domain);
        var post = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["key"] = Str(config, "tech_key"),
            ["just_test_db"] = "yes"
        };
        var pyDb = EpcPriceUploadCurlJson(pyUrl, post, 10);
        var statusOk = pyDb.TryGetValue("status", out var st) && !PhpEmpty(st);
        checks["pyprices_api_reachable"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = pyDb.ContainsKey("status") && pyDb.ContainsKey("list_to_handle"),
            ["detail"] = statusOk ? "API responded via " + pyUrl : "Response: " + JsonSerializer.Serialize(pyDb)[..Math.Min(200, JsonSerializer.Serialize(pyDb).Length)]
        };
        checks["pyprices_db"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = statusOk,
            ["detail"] = statusOk ? "DB connection from pyprices OK" : (pyDb.TryGetValue("message", out var msg) ? Convert.ToString(msg) ?? "" : JsonSerializer.Serialize(pyDb))
        };

        var cronUrl = domain + "/" + backend + "/content/shop/prices_upload/for_pyprices/for_cron/cron_crutch.php?key=" + Uri.EscapeDataString(Str(config, "tech_key"));
        var cronHit = EpcPriceUploadCurlRaw(cronUrl, 12);
        var code = ToInt(cronHit["http_code"]);
        var body = Convert.ToString(cronHit["body"]) ?? "";
        checks["cron_crutch"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = code >= 200 && code < 500,
            ["detail"] = "HTTP " + code + (body.Length > 0 ? " — " + TrimSub(body, 120) : "")
        };

        var tmpRel = Str(config, "tmp_dir_prices_upload");
        if (tmpRel == "")
        {
            tmpRel = "/tmp/prices_upload_files";
        }

        var tmpPath = docRoot + "/" + backend + tmpRel;
        var tmpWritable = (PathIsDir != null && PathIsDir(tmpPath) && PathWritable != null && PathWritable(tmpPath));
        if (!tmpWritable && (PathIsDir == null || !PathIsDir(tmpPath)))
        {
            tmpWritable = EnsureDir != null && EnsureDir(tmpPath) && PathWritable != null && PathWritable(tmpPath);
        }

        checks["tmp_upload_dir"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = tmpWritable, ["detail"] = tmpPath };
        var histRoot = docRoot + "/content/files/price_upload_history";
        checks["history_archive_dir"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = PathIsDir != null && PathIsDir(histRoot) && PathWritable != null && PathWritable(histRoot),
            ["detail"] = histRoot
        };
        checks["deploy_upload_endpoint"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = PathIsFile != null && PathIsFile(docRoot + "/epc-upload-uae-prices.php"),
            ["detail"] = "/epc-upload-uae-prices.php"
        };

        var wizardSteps = new[] { "ajax_1_prepare_tmp_dir.php", "ajax_5_import_csv_to_db.php", "ajax_6_complete_session.php" };
        var missing = new List<string>();
        foreach (var f in wizardSteps)
        {
            var p = docRoot + "/" + backend + "/content/shop/prices_upload/" + f;
            if (PathIsFile == null || !PathIsFile(p))
            {
                missing.Add(f);
            }
        }

        checks["cp_wizard_scripts"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = missing.Count == 0,
            ["detail"] = missing.Count == 0 ? "Steps 1, 5, 6 present" : "Missing: " + string.Join(", ", missing)
        };

        var allOk = checks.Values.Cast<Dictionary<string, object?>>().All(c => !PhpEmpty(c["ok"]));
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["all_ok"] = allOk,
            ["checks"] = checks,
            ["pyprices_url"] = pyUrl,
            ["cron_wget_example"] = "wget -O /dev/null -q '" + cronUrl + "'"
        };
    }

    public static Dictionary<string, object?> EpcPriceUploadCurlJson(string url, Dictionary<string, string>? postFields = null, int timeout = 15)
    {
        var raw = EpcPriceUploadCurlRaw(url, timeout, postFields);
        var body = (Convert.ToString(raw["body"]) ?? "").Trim();
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                return doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => (object?)JsonVal(p.Value), StringComparer.Ordinal);
            }
        }
        catch
        {
            // fall through
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["_raw"] = body.Length > 500 ? body[..500] : body,
            ["_http"] = raw["http_code"]
        };
    }

    public static Dictionary<string, object?> EpcPriceUploadCurlRaw(string url, int timeout = 15, Dictionary<string, string>? postFields = null)
        => CurlRaw != null
            ? CurlRaw(url, timeout, postFields)
            : new Dictionary<string, object?>(StringComparer.Ordinal) { ["http_code"] = 0, ["body"] = "curl not available" };

    private static Dictionary<string, object?> Chan(
        string id, string title, int? loadMode, string engine, string cpUrl, string configUrl,
        string uploadSource, string formats, string test, string? cronWget = null)
    {
        var row = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["id"] = id,
            ["title"] = title,
            ["load_mode"] = loadMode,
            ["engine"] = engine,
            ["cp_url"] = cpUrl,
            ["config_url"] = configUrl,
            ["upload_source"] = uploadSource,
            ["formats"] = formats,
            ["test"] = test
        };
        if (cronWget != null)
        {
            row["cron_wget"] = cronWget;
        }

        return row;
    }

    private static int ScalarInt(MySqlConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        var v = cmd.ExecuteScalar();
        return ToInt(v);
    }

    private static string Str(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var v) ? Convert.ToString(v) ?? "" : "";

    private static int ToInt(object? value)
        => int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static bool PhpEmpty(object? value)
        => value is null or false or "" or 0 or 0L or 0d
            || (value is string s && (s == "" || s == "0"));

    private static string TrimSub(string value, int len)
    {
        value = value.Trim();
        return value.Length <= len ? value : value[..len];
    }

    private static object? JsonVal(JsonElement el)
        => el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var n) ? n : el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => el.GetRawText()
        };
}
