using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-leftover helpers. PHP identifiers kept for the inventory:
/// <c>epc_page_cache_dir</c>, <c>epc_page_cache_enabled</c>, <c>epc_page_cache_key</c>,
/// <c>epc_page_cache_try_serve</c>, <c>epc_page_cache_start_capture</c>,
/// <c>epc_page_cache_release_lock</c>, <c>epc_page_cache_flush</c>,
/// <c>epc_page_cache_purge_all</c>,
/// <c>EPC_ISOLATION_ANOMALY_VERSION</c>, <c>epc_anomaly_ensure_schema</c>,
/// <c>epc_anomaly_scan</c>, <c>epc_anomaly_check_violation_spikes</c>,
/// <c>epc_anomaly_check_multi_tenant_ip</c>, <c>epc_anomaly_check_after_hours</c>,
/// <c>epc_anomaly_check_mfa_bypass</c>, <c>epc_anomaly_record</c>,
/// <c>epc_anomaly_list</c>, <c>epc_anomaly_resolve</c>, <c>epc_anomaly_fleet_stats</c>.
/// </summary>
public static class PhpPlanQ1Left
{
    public const string PageCachePath = "content/general_pages/epc_page_cache.php";
    public const string IsolationAnomalyPath = "content/general_pages/epc_isolation_anomaly.php";
    public const string IntegrationsHubCssPath = "content/general_pages/epc_integrations_hub_css.php";
    public const string IndustrySettingsCssPath = "content/general_pages/epc_industry_settings_css.php";
    public const string MarketingBroadcastCssPath = "content/general_pages/epc_marketing_broadcast_css.php";
    public const string BrochureInventoryPath = "content/general_pages/epc_cp_brochure_inventory.php";
    public const string CapabilityGuidesPath = "content/general_pages/epc_ecomae_platform_capability_guides.php";
    public const string EpcIsolationAnomalyVersion = "1.0.0";

    private static readonly Regex HostSanitize = new("[^a-z0-9.\\-]", RegexOptions.CultureInvariant);
    private static readonly Regex UriSanitize = new("[^a-zA-Z0-9/\\-_.]", RegexOptions.CultureInvariant);
    private static readonly Regex LangSanitize = new("[^a-z]", RegexOptions.CultureInvariant);
    private static readonly Regex AdminPath = new("^(/(cp|bos|api|epc-api|admin))(/|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex BrochurePath = new("^/(?:(?:en|ru|ar)/)?brochure(?:-cp|/cp)?/?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public sealed class PageCacheRequest
    {
        public string Method { get; set; } = "GET";
        public string DocumentRoot { get; set; } = "";
        public string RequestUri { get; set; } = "/";
        public string HttpHost { get; set; } = "localhost";
        public Dictionary<string, string> Cookies { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Get { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Post { get; } = new(StringComparer.Ordinal);
        public bool Active { get; set; }
        public int Ttl { get; set; } = 300;
        public int? HttpCode { get; set; }
        public object? LockFp { get; set; }
        public Func<DateTimeOffset>? Clock { get; set; }

        public DateTimeOffset Now() => Clock?.Invoke() ?? DateTimeOffset.UtcNow;
    }

    public sealed class AnomalyRow
    {
        public int Id { get; set; }
        public string SiteKey { get; set; } = "";
        public string AnomalyType { get; set; } = "";
        public string Severity { get; set; } = "medium";
        public string Description { get; set; } = "";
        public string? EvidenceJson { get; set; }
        public double RiskScore { get; set; }
        public int Resolved { get; set; }
        public int? ResolvedBy { get; set; }
        public string? ResolvedAt { get; set; }
        public string? ResolutionNote { get; set; }
        public string CreatedAt { get; set; } = "";
    }

    public sealed class ViolationRow
    {
        public string SiteKey { get; set; } = "";
        public string Ip { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public sealed class MfaAuditRow
    {
        public int UserId { get; set; }
        public string Action { get; set; } = "";
        public int Success { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class AnomalyStore
    {
        public bool SchemaReady { get; set; }
        public bool ViolationsTable { get; set; } = true;
        public bool MfaTable { get; set; } = true;
        public List<AnomalyRow> Anomalies { get; } = new();
        public List<ViolationRow> Violations { get; } = new();
        public List<MfaAuditRow> MfaLog { get; } = new();
        public int NextId { get; set; } = 1;
        public Func<DateTime>? Clock { get; set; }

        public DateTime Now() => Clock?.Invoke() ?? DateTime.UtcNow;
    }

    public static string EpcPageCacheDir(PageCacheRequest req)
    {
        var dir = (req.DocumentRoot ?? "") + "/content/files/epc_page_cache";
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static bool EpcPageCacheEnabled(PageCacheRequest req)
    {
        if (!string.Equals(req.Method ?? "", "GET", StringComparison.Ordinal))
        {
            return false;
        }

        if (req.Post.Count > 0)
        {
            return false;
        }

        if (HasCookie(req, "PHPSESSID") || HasCookie(req, "admin_hash") || HasCookie(req, "epc_user_id"))
        {
            return false;
        }

        var path = ParseUrlPath(req.RequestUri);
        if (path is null)
        {
            return false;
        }

        if (AdminPath.IsMatch(path) || BrochurePath.IsMatch(path))
        {
            return false;
        }

        return !req.Get.ContainsKey("nocache") && !req.Get.ContainsKey("epc_debug");
    }

    public static string EpcPageCacheKey(PageCacheRequest req)
    {
        var host = HostSanitize.Replace(req.HttpHost ?? "localhost", "").ToLowerInvariant();
        var uri = UriSanitize.Replace(ParseUrlPath(req.RequestUri) ?? "", "_");
        var langRaw = req.Cookies.TryGetValue("lang", out var cookie) ? cookie : "en";
        var lang = LangSanitize.Replace(langRaw.ToLowerInvariant(), "");
        var digest = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(uri + "|" + lang))).ToLowerInvariant();
        return "page_" + host + "_" + digest;
    }

    public static bool EpcPageCacheTryServe(PageCacheRequest req, out string html)
    {
        html = "";
        if (!EpcPageCacheEnabled(req))
        {
            return false;
        }

        var file = EpcPageCacheDir(req) + "/" + EpcPageCacheKey(req) + ".html";
        if (!File.Exists(file))
        {
            return false;
        }

        var metaPath = file + ".meta";
        if (File.Exists(metaPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(metaPath));
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("exp", out var exp)
                    && StorefrontPhpIntCast(exp) < Unix(req.Now()))
                {
                    TryDelete(file);
                    TryDelete(metaPath);
                    return false;
                }
            }
            catch (JsonException)
            {
                // PHP json_decode failure leaves $m unset; serve continues.
            }
        }

        html = File.ReadAllText(file);
        if (Encoding.UTF8.GetByteCount(html) < 100)
        {
            html = "";
            return false;
        }

        var trimmed = html.Trim();
        if (trimmed.IndexOf("<!doctype", StringComparison.OrdinalIgnoreCase) != 0
            && html.IndexOf("<html", StringComparison.OrdinalIgnoreCase) < 0)
        {
            TryDelete(file);
            TryDelete(metaPath);
            html = "";
            return false;
        }

        if (html.IndexOf("epc-platform-status", StringComparison.OrdinalIgnoreCase) >= 0
            && html.IndexOf("Service update", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            TryDelete(file);
            TryDelete(metaPath);
            html = "";
            return false;
        }

        return true;
    }

    /// <summary>Lock/exit goldens are skipped; this only arms capture flags like PHP after the flock path.</summary>
    public static void EpcPageCacheStartCapture(PageCacheRequest req, int ttl = 300)
    {
        if (!EpcPageCacheEnabled(req))
        {
            return;
        }

        req.Ttl = ttl;
        req.Active = true;
    }

    public static void EpcPageCacheReleaseLock(PageCacheRequest req)
    {
        if (req.LockFp is not null)
        {
            req.LockFp = null;
        }
    }

    public static void EpcPageCacheFlush(PageCacheRequest req, string? html)
    {
        if (!req.Active)
        {
            EpcPageCacheReleaseLock(req);
            return;
        }

        req.Active = false;
        if (html is null || Encoding.UTF8.GetByteCount(html) < 200)
        {
            EpcPageCacheReleaseLock(req);
            return;
        }

        if (req.HttpCode is not null && req.HttpCode != 200)
        {
            EpcPageCacheReleaseLock(req);
            return;
        }

        var trimmed = html.Trim();
        var hasHtmlOpen = trimmed.IndexOf("<!doctype", StringComparison.OrdinalIgnoreCase) == 0
            || trimmed.IndexOf("<html", StringComparison.OrdinalIgnoreCase) >= 0;
        var hasHtmlClose = trimmed.IndexOf("</html>", StringComparison.OrdinalIgnoreCase) >= 0;
        if (!hasHtmlOpen || !hasHtmlClose)
        {
            EpcPageCacheReleaseLock(req);
            return;
        }

        if (html.IndexOf("Service update", StringComparison.OrdinalIgnoreCase) >= 0
            && html.IndexOf("epc-platform-status", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            EpcPageCacheReleaseLock(req);
            return;
        }

        if (html.IndexOf("Temporarily Busy", StringComparison.OrdinalIgnoreCase) >= 0
            && html.IndexOf("Retry-After", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            EpcPageCacheReleaseLock(req);
            return;
        }

        if (html.IndexOf("Loading your store", StringComparison.OrdinalIgnoreCase) >= 0
            && Encoding.UTF8.GetByteCount(html) < 2000)
        {
            EpcPageCacheReleaseLock(req);
            return;
        }

        var ttl = req.Ttl;
        var file = EpcPageCacheDir(req) + "/" + EpcPageCacheKey(req) + ".html";
        File.WriteAllText(file, html);
        var now = req.Now();
        File.WriteAllText(file + ".meta", JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["exp"] = Unix(now) + ttl,
            ["uri"] = req.RequestUri ?? "/",
            ["host"] = req.HttpHost ?? "",
            ["time"] = now.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        }));
        EpcPageCacheReleaseLock(req);
    }

    public static int EpcPageCachePurgeAll(PageCacheRequest req)
    {
        var dir = EpcPageCacheDir(req);
        if (!Directory.Exists(dir))
        {
            return 0;
        }

        var count = 0;
        foreach (var f in Directory.GetFiles(dir))
        {
            try
            {
                File.Delete(f);
                count++;
            }
            catch (IOException)
            {
            }
        }

        return count;
    }

    public static void EpcAnomalyEnsureSchema(AnomalyStore store) => store.SchemaReady = true;

    public static Dictionary<string, object?> EpcAnomalyScan(AnomalyStore store)
    {
        EpcAnomalyEnsureSchema(store);
        var results = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["scanned"] = 0,
            ["anomalies_found"] = 0,
            ["details"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        };
        var details = (Dictionary<string, object?>)results["details"]!;

        var violations = EpcAnomalyCheckViolationSpikes(store);
        details["violation_spikes"] = violations;
        results["anomalies_found"] = Convert.ToInt32(results["anomalies_found"], CultureInfo.InvariantCulture)
            + ((List<Dictionary<string, object?>>)violations["anomalies"]!).Count;

        var multi = EpcAnomalyCheckMultiTenantIp(store);
        details["multi_tenant_ip"] = multi;
        results["anomalies_found"] = Convert.ToInt32(results["anomalies_found"], CultureInfo.InvariantCulture)
            + ((List<Dictionary<string, object?>>)multi["anomalies"]!).Count;

        var after = EpcAnomalyCheckAfterHours(store);
        details["after_hours"] = after;
        results["anomalies_found"] = Convert.ToInt32(results["anomalies_found"], CultureInfo.InvariantCulture)
            + ((List<Dictionary<string, object?>>)after["anomalies"]!).Count;

        var mfa = EpcAnomalyCheckMfaBypass(store);
        details["mfa_bypass"] = mfa;
        results["anomalies_found"] = Convert.ToInt32(results["anomalies_found"], CultureInfo.InvariantCulture)
            + ((List<Dictionary<string, object?>>)mfa["anomalies"]!).Count;

        results["scanned"] = 4;
        results["scan_time"] = store.Now().ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture);
        return results;
    }

    public static Dictionary<string, object?> EpcAnomalyCheckViolationSpikes(AnomalyStore store)
    {
        var anomalies = new List<Dictionary<string, object?>>();
        if (!store.ViolationsTable)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["check"] = "violation_spikes", ["anomalies"] = anomalies };
        }

        var since = store.Now().AddDays(-7);
        var groups = store.Violations
            .Where(v => v.CreatedAt >= since)
            .GroupBy(v => (v.SiteKey, v.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))
            .Select(g => new { g.Key.SiteKey, Day = g.Key.Item2, Count = g.Count() })
            .Where(g => g.Count > 10)
            .OrderByDescending(g => g.Count)
            .ToList();
        foreach (var row in groups)
        {
            var item = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = "violation_spike",
                ["site_key"] = row.SiteKey,
                ["severity"] = row.Count > 50 ? "critical" : row.Count > 25 ? "high" : "medium",
                ["description"] = row.Count + " isolation violations on " + row.Day,
                ["risk_score"] = Math.Min(100, row.Count * 2)
            };
            anomalies.Add(item);
            EpcAnomalyRecord(store, row.SiteKey, "violation_spike", item);
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["check"] = "violation_spikes", ["anomalies"] = anomalies };
    }

    public static Dictionary<string, object?> EpcAnomalyCheckMultiTenantIp(AnomalyStore store)
    {
        var anomalies = new List<Dictionary<string, object?>>();
        if (!store.ViolationsTable)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["check"] = "multi_tenant_ip", ["anomalies"] = anomalies };
        }

        var since = store.Now().AddHours(-24);
        var groups = store.Violations
            .Where(v => v.CreatedAt >= since && v.Ip != "")
            .GroupBy(v => v.Ip)
            .Select(g => new { Ip = g.Key, Tenants = g.Select(x => x.SiteKey).Distinct().ToList() })
            .Where(g => g.Tenants.Count > 1)
            .ToList();
        foreach (var row in groups)
        {
            anomalies.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = "multi_tenant_ip",
                ["ip"] = row.Ip,
                ["severity"] = row.Tenants.Count > 3 ? "critical" : "high",
                ["description"] = "IP " + row.Ip + " accessed " + row.Tenants.Count + " tenants: " + string.Join(",", row.Tenants),
                ["risk_score"] = Math.Min(100, row.Tenants.Count * 25)
            });
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["check"] = "multi_tenant_ip", ["anomalies"] = anomalies };
    }

    public static Dictionary<string, object?> EpcAnomalyCheckAfterHours(AnomalyStore store)
    {
        var anomalies = new List<Dictionary<string, object?>>();
        if (!store.ViolationsTable)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["check"] = "after_hours", ["anomalies"] = anomalies };
        }

        var since = store.Now().AddDays(-7);
        var groups = store.Violations
            .Where(v => v.CreatedAt >= since && (v.CreatedAt.Hour < 6 || v.CreatedAt.Hour > 22))
            .GroupBy(v => v.SiteKey)
            .Select(g => new { SiteKey = g.Key, Count = g.Count() })
            .Where(g => g.Count > 5)
            .ToList();
        foreach (var row in groups)
        {
            anomalies.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = "after_hours",
                ["site_key"] = row.SiteKey,
                ["severity"] = "medium",
                ["description"] = row.Count + " after-hours access events for " + row.SiteKey,
                ["risk_score"] = Math.Min(50, row.Count * 5)
            });
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["check"] = "after_hours", ["anomalies"] = anomalies };
    }

    public static Dictionary<string, object?> EpcAnomalyCheckMfaBypass(AnomalyStore store)
    {
        var anomalies = new List<Dictionary<string, object?>>();
        if (!store.MfaTable)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal) { ["check"] = "mfa_bypass", ["anomalies"] = anomalies };
        }

        var since = store.Now().AddHours(-24);
        var groups = store.MfaLog
            .Where(v => (v.Action is "verify_failed" or "verify") && v.Success == 0 && v.CreatedAt >= since)
            .GroupBy(v => v.UserId)
            .Select(g => new { UserId = g.Key, Failed = g.Count() })
            .Where(g => g.Failed > 5)
            .ToList();
        foreach (var row in groups)
        {
            anomalies.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["type"] = "mfa_brute_force",
                ["user_id"] = row.UserId,
                ["severity"] = row.Failed > 20 ? "critical" : "high",
                ["description"] = "User " + row.UserId + " had " + row.Failed + " failed MFA attempts in 24h",
                ["risk_score"] = Math.Min(100, row.Failed * 5)
            });
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["check"] = "mfa_bypass", ["anomalies"] = anomalies };
    }

    public static int EpcAnomalyRecord(AnomalyStore store, string siteKey, string type, Dictionary<string, object?> data)
    {
        EpcAnomalyEnsureSchema(store);
        var id = store.NextId++;
        store.Anomalies.Add(new AnomalyRow
        {
            Id = id,
            SiteKey = siteKey,
            AnomalyType = type,
            Severity = data.TryGetValue("severity", out var sev) && sev is not null ? Convert.ToString(sev, CultureInfo.InvariantCulture) ?? "medium" : "medium",
            Description = data.TryGetValue("description", out var desc) && desc is not null ? Convert.ToString(desc, CultureInfo.InvariantCulture) ?? "" : "",
            EvidenceJson = JsonSerializer.Serialize(data),
            RiskScore = data.TryGetValue("risk_score", out var risk) && risk is not null ? Convert.ToDouble(risk, CultureInfo.InvariantCulture) : 0,
            CreatedAt = store.Now().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        });
        return id;
    }

    /// <summary>PHP binds LIMIT ? which fatals on this MariaDB; the twin takes an int limit.</summary>
    public static List<AnomalyRow> EpcAnomalyList(AnomalyStore store, bool unresolvedOnly = true, string siteKey = "", int limit = 100)
    {
        EpcAnomalyEnsureSchema(store);
        IEnumerable<AnomalyRow> q = store.Anomalies;
        if (unresolvedOnly)
        {
            q = q.Where(a => a.Resolved == 0);
        }

        if (siteKey != "")
        {
            q = q.Where(a => a.SiteKey == siteKey);
        }

        return q.OrderByDescending(a => a.RiskScore).ThenByDescending(a => a.CreatedAt).Take(limit).ToList();
    }

    public static Dictionary<string, object?> EpcAnomalyResolve(AnomalyStore store, int anomalyId, int userId, string note = "")
    {
        var row = store.Anomalies.FirstOrDefault(a => a.Id == anomalyId && a.Resolved == 0);
        if (row is not null)
        {
            row.Resolved = 1;
            row.ResolvedBy = userId;
            row.ResolvedAt = store.Now().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            row.ResolutionNote = note;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal) { ["ok"] = true };
    }

    public static Dictionary<string, object?> EpcAnomalyFleetStats(AnomalyStore store)
    {
        EpcAnomalyEnsureSchema(store);
        var bySeverity = store.Anomalies
            .GroupBy(a => a.Severity)
            .Select(g => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["severity"] = g.Key,
                ["count"] = g.Count(),
                ["unresolved"] = g.Count(x => x.Resolved == 0)
            })
            .ToList();
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["unresolved_total"] = store.Anomalies.Count(a => a.Resolved == 0),
            ["by_severity"] = bySeverity
        };
    }

    public static string EpcIntegrationsHubCss() => PhpPlanQ1LeftData.IntegrationsHubCss;

    public static string EpcIndustrySettingsCss() => PhpPlanQ1LeftData.IndustrySettingsCss;

    public static string EpcMarketingBroadcastCss() => PhpPlanQ1LeftData.MarketingBroadcastCss;

    public static JsonElement EpcCpBrochureInventory()
        => JsonDocument.Parse(PhpPlanQ1LeftData.BrochureInventoryJson).RootElement.Clone();

    public static JsonElement EpcEcomaePlatformCapabilityGuides()
        => JsonDocument.Parse(PhpPlanQ1LeftData.CapabilityGuidesJson).RootElement.Clone();

    private static bool HasCookie(PageCacheRequest req, string name)
        => req.Cookies.TryGetValue(name, out var value) && !IsPhpEmpty(value);

    private static bool IsPhpEmpty(string? value)
        => string.IsNullOrEmpty(value) || value == "0";

    private static string? ParseUrlPath(string? uri)
    {
        if (uri is null)
        {
            return "/";
        }

        var cut = uri.IndexOfAny(new[] { '?', '#' });
        return cut < 0 ? uri : uri[..cut];
    }

    private static long Unix(DateTimeOffset value) => value.ToUnixTimeSeconds();

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    private static long StorefrontPhpIntCast(JsonElement el)
        => el.ValueKind switch
        {
            JsonValueKind.Number when el.TryGetInt64(out var n) => n,
            JsonValueKind.String => long.TryParse(el.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0,
            _ => 0
        };
}
