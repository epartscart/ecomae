using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-inlet Super-CP tenant hub helpers. PHP identifiers kept for the inventory:
/// <c>epc_th_h</c>, <c>epc_th_tenant_action_urls</c>, <c>epc_th_tenant_db_connect_ok</c>,
/// <c>epc_th_require_super_cp</c>, <c>epc_th_list_tenants</c>, <c>epc_th_platform_stats</c>,
/// <c>epc_th_probe_url</c>, <c>epc_th_add_tenant</c>, <c>epc_th_update_tenant_status</c>,
/// <c>epc_th_update_tenant_blockchain_mode</c>, <c>epc_th_anchor_blockchain_pending_now</c>,
/// <c>epc_th_onboard_client</c>, <c>epc_th_launch_checklist</c>,
/// <c>epc_th_apply_industry_theme</c>.
/// Path: <c>content/shop/tenant_hub/epc_tenant_hub_helpers.php</c>.
/// GET never mints a session cookie. Leftover portal / demo / client-ERP stay injected.
/// Do not write leftover unique portal or client-ERP router basenames.
/// </summary>
public static class PhpPlanQ1Inlet
{
    public const string TenantHubHelpersPath = "content/shop/tenant_hub/epc_tenant_hub_helpers.php";

    public static Func<Dictionary<string, object?>, bool>? IsSharedErpRow { get; set; }
    public static Func<string, string>? ClientErpLoginUrl { get; set; }
    public static Func<string, string>? ClientErpShellUrl { get; set; }
    public static Func<string, string>? CommerceHost { get; set; }
    public static Func<Dictionary<string, string>>? ResolveDbCredentials { get; set; }
    public static Func<string, string, string, bool>? TryConnect { get; set; }
    public static Func<bool>? IsPlatformOperator { get; set; }
    public static Func<MySqlConnection, List<Dictionary<string, object?>>>? ListTenants { get; set; }
    public static Func<Dictionary<string, Dictionary<string, object?>>>? Industries { get; set; }
    public static Func<Dictionary<string, Dictionary<string, object?>>>? Ecosystems { get; set; }
    public static Func<Dictionary<string, string>>? TenantStatuses { get; set; }
    public static Func<string, Dictionary<string, object?>, Dictionary<string, string>>? DemoUrls { get; set; }
    public static Func<string, string>? DemoCpAutologinUrl { get; set; }
    public static Func<string, string>? DemoCpLoginUrl { get; set; }
    public static Func<string, string>? DemoErpShellUrl { get; set; }
    public static Func<string, Dictionary<string, object?>>? IntroDecode { get; set; }
    public static Func<string>? PlatformIp { get; set; }
    public static Func<string>? PlatformHost { get; set; }
    public static Func<MySqlConnection, Dictionary<string, object?>, Dictionary<string, object?>>? SaveTenant { get; set; }
    public static Func<MySqlConnection, string, Dictionary<string, object?>>? SyncPacks { get; set; }
    public static Func<string, string>? NormalizeBlockchainMode { get; set; }
    public static Action<string>? ClearBlockchainCache { get; set; }
    public static Func<Dictionary<string, string>>? BlockchainModes { get; set; }
    public static Func<int, Dictionary<string, object?>>? AnchorPending { get; set; }
    public static Func<string>? AnchorNetwork { get; set; }
    public static Func<MySqlConnection, Dictionary<string, object?>, string, Dictionary<string, object?>>? OnboardClient { get; set; }
    public static Func<MySqlConnection, string, Dictionary<string, object?>>? LaunchChecklist { get; set; }
    public static Func<MySqlConnection, string, Dictionary<string, object?>, Dictionary<string, object?>>? ApplyIndustryTheme { get; set; }
    public static Func<string, (object? Body, string Header)>? ProbeFetch { get; set; }
    public static Func<long>? Clock { get; set; }
    public static List<Dictionary<string, object?>> SavedTenants { get; } = [];
    public static List<string> ClearedModes { get; } = [];

    public static void Reset()
    {
        IsSharedErpRow = null;
        ClientErpLoginUrl = null;
        ClientErpShellUrl = null;
        CommerceHost = null;
        ResolveDbCredentials = null;
        TryConnect = null;
        IsPlatformOperator = null;
        ListTenants = null;
        Industries = null;
        Ecosystems = null;
        TenantStatuses = null;
        DemoUrls = null;
        DemoCpAutologinUrl = null;
        DemoCpLoginUrl = null;
        DemoErpShellUrl = null;
        IntroDecode = null;
        PlatformIp = null;
        PlatformHost = null;
        SaveTenant = null;
        SyncPacks = null;
        NormalizeBlockchainMode = null;
        ClearBlockchainCache = null;
        BlockchainModes = null;
        AnchorPending = null;
        AnchorNetwork = null;
        OnboardClient = null;
        LaunchChecklist = null;
        ApplyIndustryTheme = null;
        ProbeFetch = null;
        Clock = null;
        SavedTenants.Clear();
        ClearedModes.Clear();
    }

    public static string EpcThH(object? value)
        => WebUtility.HtmlEncode(Str(value)).Replace("&#39;", "&#039;", StringComparison.Ordinal).Replace("'", "&#039;", StringComparison.Ordinal);

    public static Dictionary<string, object?> EpcThTenantActionUrls(Dictionary<string, object?> row)
    {
        var key = SiteKey(Str(row.GetValueOrDefault("site_key")));
        var host = Str(row.GetValueOrDefault("hostname")).Trim();
        var industry = Str(row.GetValueOrDefault("industry_code"));
        var erpStandalone = industry is "erp_standalone" or "erp_only";
        if (SharedErp(row) && key != "")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["storefront"] = "",
                ["cp"] = "",
                ["erp"] = "https://www.ecomae.com" + ShellUrl(key),
                ["erp_login"] = "https://www.ecomae.com" + LoginUrl(key)
            };
        }

        var commerceHost = CommerceHost?.Invoke(host) ?? DefaultCommerceHost(host);
        if (commerceHost == "")
        {
            commerceHost = host;
        }

        if (erpStandalone)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["storefront"] = "",
                ["cp"] = "https://" + commerceHost + "/cp/",
                ["erp"] = "https://" + commerceHost + "/cp/shop/finance/erp?epc_erp_shell=1"
            };
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["storefront"] = "https://" + commerceHost + "/en/",
            ["cp"] = "https://" + commerceHost + "/cp/",
            ["erp"] = "https://" + commerceHost + "/cp/shop/finance/erp?epc_erp_shell=1"
        };
    }

    public static bool EpcThTenantDbConnectOk(Dictionary<string, object?> row)
    {
        var db = Str(row.GetValueOrDefault("db_name")).Trim();
        var user = Str(row.GetValueOrDefault("db_user")).Trim();
        if (user == "")
        {
            user = db;
        }

        var pass = Str(row.GetValueOrDefault("db_password"));
        if (db == "docpart" && pass == "")
        {
            var creds = ResolveDbCredentials?.Invoke();
            if (creds is not null && creds.TryGetValue("password", out var pw) && !PhpEmpty(pw))
            {
                user = creds.TryGetValue("user", out var u) ? Str(u, "docpart") : "docpart";
                pass = Str(pw);
            }
        }

        if (db == "" || user == "" || pass == "")
        {
            return false;
        }

        if (TryConnect is not null)
        {
            return TryConnect(db, user, pass);
        }

        try
        {
            using var conn = new MySqlConnection($"Server=127.0.0.1;Port=3306;Database={db};User ID={user};Password={pass};Connection Timeout=3;");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.ExecuteScalar();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void EpcThRequireSuperCp()
    {
        if (IsPlatformOperator?.Invoke() != true)
        {
            throw new Exception("Super CP is only available on www.ecomae.com/cp");
        }
    }

    public static List<Dictionary<string, object?>> EpcThListTenants(MySqlConnection db)
    {
        var rows = ListTenants?.Invoke(db) ?? [];
        var industries = Industries?.Invoke() ?? DefaultIndustries();
        var ecosystems = Ecosystems?.Invoke() ?? DefaultEcosystems();
        var statuses = TenantStatuses?.Invoke() ?? DefaultStatuses();
        var outRows = new List<Dictionary<string, object?>>();
        foreach (var src in rows)
        {
            var r = new Dictionary<string, object?>(src, StringComparer.Ordinal);
            var code = Str(r.GetValueOrDefault("industry_code"));
            r["industry_name"] = industries.TryGetValue(code, out var ind) ? Str(ind.GetValueOrDefault("name"), code) : code;
            var ecoCode = ind is not null ? Str(ind.GetValueOrDefault("ecosystem")) : "";
            r["ecosystem_code"] = ecoCode;
            r["ecosystem_name"] = ecoCode != "" && ecosystems.TryGetValue(ecoCode, out var eco) ? Str(eco.GetValueOrDefault("name")) : "";
            var st = Str(r.GetValueOrDefault("status"));
            r["status_label"] = statuses.TryGetValue(st, out var label) ? label : st;
            if (!PhpEmpty(r.GetValueOrDefault("is_demo")))
            {
                var demoKey = SiteKey(Str(r.GetValueOrDefault("site_key")));
                var demoUrls = DemoUrls?.Invoke(demoKey, r) ?? new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["storefront"] = "https://demo.ecomae.com/" + demoKey + "/",
                    ["cp"] = "https://demo.ecomae.com/" + demoKey + "/cp/"
                };
                var cpAuto = DemoCpAutologinUrl?.Invoke(demoKey) ?? ("https://www.ecomae.com/demo-cp/" + demoKey);
                r["storefront_url"] = demoUrls.GetValueOrDefault("storefront") ?? "";
                r["cp_url"] = cpAuto != "" ? cpAuto : (demoUrls.GetValueOrDefault("cp") ?? ("https://www.ecomae.com" + (DemoCpLoginUrl?.Invoke(demoKey) ?? ("/demo/" + demoKey + "/cp"))));
                r["erp_url"] = "https://www.ecomae.com" + (DemoErpShellUrl?.Invoke(demoKey) ?? ("/demo/" + demoKey + "/erp"));
                r["is_demo_tenant"] = true;
            }
            else
            {
                var urls = EpcThTenantActionUrls(r);
                r["storefront_url"] = Str(urls.GetValueOrDefault("storefront"));
                r["cp_url"] = Str(urls.GetValueOrDefault("cp"));
                r["erp_url"] = Str(urls.GetValueOrDefault("erp"));
            }

            r["intro"] = (IntroDecode ?? DefaultIntroDecode)(Str(r.GetValueOrDefault("intro_json")));
            var intro = (Dictionary<string, object?>)r["intro"]!;
            r["intro_done"] = !PhpEmpty(intro.GetValueOrDefault("submitted_at"));
            r["db_connect_ok"] = EpcThTenantDbConnectOk(r);
            outRows.Add(r);
        }

        return outRows;
    }

    public static Dictionary<string, object?> EpcThPlatformStats(MySqlConnection db)
    {
        var tenants = EpcThListTenants(db);
        var live = 0;
        var pending = 0;
        foreach (var t in tenants)
        {
            if (Str(t.GetValueOrDefault("status")) == "live")
            {
                live++;
            }

            if (Str(t.GetValueOrDefault("status")) == "dns_pending")
            {
                pending++;
            }
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["tenants_total"] = tenants.Count,
            ["tenants_live"] = live,
            ["tenants_dns_pending"] = pending,
            ["platform_ip"] = PlatformIp?.Invoke() ?? "203.0.113.10",
            ["platform_host"] = PlatformHost?.Invoke() ?? "www.ecomae.com"
        };
    }

    public static Dictionary<string, object?> EpcThProbeUrl(string url)
    {
        var start = DateTime.UtcNow;
        object? bodyObj = false;
        var header = "";
        if (ProbeFetch is not null)
        {
            (bodyObj, header) = ProbeFetch(url);
        }

        var ms = (int)Math.Round((DateTime.UtcNow - start).TotalMilliseconds);
        var code = 0;
        var m = Regex.Match(header, @"\s(\d{3})\s");
        if (m.Success)
        {
            code = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        var bodyOk = bodyObj is not false and not null;
        var body = bodyOk ? Str(bodyObj) : "";
        var snippet = bodyOk ? PhpSubstr(StripTags(body), 0, 120) : "";
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = bodyOk && code is >= 200 and < 400,
            ["http_code"] = code,
            ["ms"] = ms,
            ["snippet"] = snippet
        };
    }

    public static Dictionary<string, object?> EpcThAddTenant(MySqlConnection db, Dictionary<string, object?> data)
    {
        SavedTenants.Add(data);
        return SaveTenant?.Invoke(db, data)
            ?? new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = true,
                ["site_key"] = Str(data.GetValueOrDefault("site_key"), "saved")
            };
    }

    public static Dictionary<string, object?> EpcThUpdateTenantStatus(MySqlConnection db, string siteKey, string status)
    {
        var statuses = TenantStatuses?.Invoke() ?? DefaultStatuses();
        if (!statuses.ContainsKey(status))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "Invalid status" };
        }

        var key = SiteKey(siteKey);
        int changed;
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "UPDATE `epc_portal_tenants` SET `status` = @s, `updated_at` = @t WHERE `site_key` = @k";
            cmd.Parameters.AddWithValue("@s", status);
            cmd.Parameters.AddWithValue("@t", Clock?.Invoke() ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("@k", key);
            changed = cmd.ExecuteNonQuery();
        }

        if (changed == 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "Tenant not found" };
        }

        var sync = new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["message"] = "skipped" };
        if (status == "live")
        {
            sync = SyncPacks?.Invoke(db, key) ?? sync;
        }

        var msg = "Status updated to " + status;
        if (status == "live" && !PhpEmpty(sync.GetValueOrDefault("ok")) && Str(sync.GetValueOrDefault("message")) != "skipped")
        {
            msg += " — " + Str(sync.GetValueOrDefault("message"));
        }
        else if (status == "live" && PhpEmpty(sync.GetValueOrDefault("ok")))
        {
            msg += " — pack sync: " + Str(sync.GetValueOrDefault("message"), "failed");
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["message"] = msg,
            ["client_sync"] = sync
        };
    }

    public static Dictionary<string, object?> EpcThUpdateTenantBlockchainMode(MySqlConnection db, string siteKey, string mode)
    {
        mode = (NormalizeBlockchainMode ?? PhpPlanQ1Boom.EpcBcBosNormalizeMode)(mode);
        var key = SiteKey(siteKey);
        if (key == "")
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "Invalid tenant" };
        }

        int changed;
        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText = "UPDATE `epc_portal_tenants` SET `blockchain_mode` = @m, `updated_at` = @t WHERE `site_key` = @k";
            cmd.Parameters.AddWithValue("@m", mode);
            cmd.Parameters.AddWithValue("@t", Clock?.Invoke() ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            cmd.Parameters.AddWithValue("@k", key);
            changed = cmd.ExecuteNonQuery();
        }

        if (changed == 0)
        {
            using var chk = db.CreateCommand();
            chk.CommandText = "SELECT `blockchain_mode` FROM `epc_portal_tenants` WHERE `site_key` = @k LIMIT 1";
            chk.Parameters.AddWithValue("@k", key);
            var found = chk.ExecuteScalar();
            if (found is null or DBNull)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = false, ["message"] = "Tenant not found" };
            }
        }

        if (ClearBlockchainCache is not null)
        {
            ClearBlockchainCache(key);
        }
        else
        {
            PhpPlanQ1Boom.EpcBcBosClearTenantModeCache(key);
        }

        ClearedModes.Add(key);
        var labels = BlockchainModes?.Invoke() ?? PhpPlanQ1Boom.EpcBcBosModes();
        var label = labels.GetValueOrDefault(mode, mode);
        var note = mode == "network" ? " (roadmap — currently records and anchors like Anchor)" : "";
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["message"] = "Blockchain mode for " + key + " set to " + label + note,
            ["mode"] = mode
        };
    }

    public static Dictionary<string, object?> EpcThAnchorBlockchainPendingNow(int limit = 100)
    {
        var batch = AnchorPending?.Invoke(limit) ?? PhpPlanQ1Boom.EpcBcBosAnchorPendingBatch(limit);
        if (PhpEmpty(batch.GetValueOrDefault("ok")))
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ok"] = false,
                ["message"] = "Anchor failed: " + Str(batch.GetValueOrDefault("error"), "unknown")
            };
        }

        var count = ToInt(batch.GetValueOrDefault("proof_count"));
        if (count == 0)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true, ["message"] = "No pending proofs to anchor." };
        }

        var net = Str(batch.GetValueOrDefault("anchor_network"), AnchorNetwork?.Invoke() ?? PhpPlanQ1Boom.EpcBcBosAnchorNetwork());
        var root = Str(batch.GetValueOrDefault("merkle_root"));
        var msg = "Anchored " + count.ToString(CultureInfo.InvariantCulture) + " proof(s) on " + net;
        if (root != "")
        {
            msg += " · root " + PhpSubstr(root, 0, 12) + "…";
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["ok"] = true,
            ["message"] = msg,
            ["result"] = batch
        };
    }

    public static Dictionary<string, object?> EpcThOnboardClient(MySqlConnection db, Dictionary<string, object?> post, string submittedBy = "")
        => OnboardClient?.Invoke(db, post, submittedBy)
            ?? PhpPlanQ1Kelp.EpcPortalOnboardClient(db, post, submittedBy);

    public static Dictionary<string, object?> EpcThLaunchChecklist(MySqlConnection db, string siteKey)
        => LaunchChecklist?.Invoke(db, siteKey)
            ?? PhpPlanQ1Kelp.EpcPortalTenantLaunchChecklist(db, siteKey);

    public static Dictionary<string, object?> EpcThApplyIndustryTheme(MySqlConnection db, string siteKey, Dictionary<string, object?>? opts = null)
        => ApplyIndustryTheme?.Invoke(db, siteKey, opts ?? new Dictionary<string, object?>(StringComparer.Ordinal))
            ?? PhpPlanQ1Kelp.EpcPortalApplyIndustryThemeToTenant(db, siteKey, opts);

    private static bool SharedErp(Dictionary<string, object?> row)
    {
        if (IsSharedErpRow is not null)
        {
            return IsSharedErpRow(row);
        }

        return !PhpEmpty(row.GetValueOrDefault("erp_only_shared")) || Str(row.GetValueOrDefault("hosted_on")) == "platform";
    }

    private static string LoginUrl(string key)
        => ClientErpLoginUrl?.Invoke(key) ?? ("/cp/client-erp/" + key + "/");

    private static string ShellUrl(string key)
        => ClientErpShellUrl?.Invoke(key) ?? (LoginUrl(key) + "shop/finance/erp?epc_erp_shell=1");

    private static string DefaultCommerceHost(string hostname)
    {
        var host = hostname.Trim().ToLowerInvariant();
        if (host == "")
        {
            return "";
        }

        host = Regex.Replace(host, @"^https?://", "");
        host = Regex.Replace(host, "/.*$", "");
        host = Regex.Replace(host, @"^www\.", "");
        if (host == "" || !host.Contains('.'))
        {
            return "";
        }

        return "www." + host;
    }

    private static Dictionary<string, object?> DefaultIntroDecode(string json)
    {
        if (IntroDecode is not null)
        {
            return IntroDecode(json);
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                return new Dictionary<string, object?>(StringComparer.Ordinal);
            }

            var map = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                map[p.Name] = p.Value.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.Number => p.Value.TryGetInt64(out var n) ? n : p.Value.GetDouble(),
                    System.Text.Json.JsonValueKind.String => p.Value.GetString(),
                    System.Text.Json.JsonValueKind.True => true,
                    System.Text.Json.JsonValueKind.False => false,
                    _ => p.Value.GetRawText()
                };
            }

            return map;
        }
        catch
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }
    }

    private static Dictionary<string, Dictionary<string, object?>> DefaultIndustries()
        => new(StringComparer.Ordinal)
        {
            ["auto_parts"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Auto Parts", ["ecosystem"] = "commerce" },
            ["erp_only"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "ERP only", ["ecosystem"] = "erp" },
            ["erp_standalone"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "ERP standalone", ["ecosystem"] = "erp" }
        };

    private static Dictionary<string, Dictionary<string, object?>> DefaultEcosystems()
        => new(StringComparer.Ordinal)
        {
            ["commerce"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "Commerce" },
            ["erp"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["name"] = "ERP" }
        };

    private static Dictionary<string, string> DefaultStatuses()
        => new(StringComparer.Ordinal)
        {
            ["live"] = "Live",
            ["dns_pending"] = "DNS pending",
            ["suspended"] = "Suspended"
        };

    private static string SiteKey(string value)
        => Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9_]", "");

    private static string StripTags(string html)
        => Regex.Replace(html, "<[^>]*>", "");

    private static string PhpSubstr(string value, int start, int length)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (start >= bytes.Length)
        {
            return "";
        }

        var take = Math.Min(length, bytes.Length - start);
        return Encoding.UTF8.GetString(bytes, start, take);
    }

    private static string Str(object? value, string fallback = "")
        => value is null ? fallback : Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;

    private static int ToInt(object? value)
        => value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);

    private static bool PhpEmpty(object? value)
        => value is null or false or 0 or 0L or 0d || value is string s && (s == "" || s == "0");
}
