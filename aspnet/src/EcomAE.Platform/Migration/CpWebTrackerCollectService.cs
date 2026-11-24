using System.Collections.Concurrent;
using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Data;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Routing;
using Microsoft.Extensions.Logging;

namespace EcomAE.Platform.Migration;

public readonly record struct CpWebTrackerIngestResult(
    bool Ok,
    long SessionId,
    int Pageviews,
    int Events,
    int Status,
    string? Error);

public sealed class CpWebTrackerCollectPayload
{
    public string? SiteKey { get; set; }
    public string? Hostname { get; set; }
    public string? SessionUid { get; set; }
    public string? VisitorUid { get; set; }
    public int UserId { get; set; }
    public bool IsRegistered { get; set; }
    public string? Referrer { get; set; }
    public string? Ua { get; set; }
    public CpWebTrackerCollectUtm? Utm { get; set; }
    public int ScreenW { get; set; }
    public int ScreenH { get; set; }
    public string? Language { get; set; }
    public string? Timezone { get; set; }
    public int DurationMs { get; set; }
    public List<CpWebTrackerCollectPageview>? Pageviews { get; set; }
    public List<CpWebTrackerCollectEvent>? Events { get; set; }
}

public sealed class CpWebTrackerCollectUtm
{
    public string? Source { get; set; }
    public string? Medium { get; set; }
    public string? Campaign { get; set; }
    public string? Term { get; set; }
    public string? Content { get; set; }
}

public sealed class CpWebTrackerCollectPageview
{
    public string? Path { get; set; }
    public string? Query { get; set; }
    public string? Title { get; set; }
    public string? Referrer { get; set; }
    public long Ts { get; set; }
    public int LoadTimeMs { get; set; }
    public int TimeOnPageMs { get; set; }
    public int ScrollMaxPct { get; set; }
    public int ViewportW { get; set; }
    public int ViewportH { get; set; }
}

public sealed class CpWebTrackerCollectEvent
{
    public string? Type { get; set; }

    [JsonPropertyName("event_type")]
    public string? EventType { get; set; }

    public string? Path { get; set; }
    public long Ts { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int PageX { get; set; }
    public int PageY { get; set; }
    public string? Tag { get; set; }
    public string? Id { get; set; }

    [JsonPropertyName("class")]
    public string? Class { get; set; }

    public string? Text { get; set; }
    public string? Href { get; set; }
    public string? Name { get; set; }
    public string? Css { get; set; }
    public string? Search { get; set; }

    [JsonPropertyName("search_ctx")]
    public string? SearchCtx { get; set; }

    public JsonElement Meta { get; set; }
}

/// <summary>In-memory twin of PHP file-based 120/min/IP collect limit.</summary>
public sealed class CpWebTrackerCollectRateLimiter
{
    private readonly ConcurrentDictionary<string, (long WindowStart, int Count)> _buckets = new(StringComparer.Ordinal);

    public bool TryAcquire(string ip, int maxPerMinute = 120)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var next = _buckets.AddOrUpdate(
            string.IsNullOrEmpty(ip) ? "0" : ip,
            _ => (now, 1),
            (_, prev) => prev.WindowStart < now - 60 ? (now, 1) : (prev.WindowStart, prev.Count + 1));
        return next.Count <= maxPerMinute;
    }
}

public interface ICpWebTrackerCollectService
{
    Task<CpWebTrackerIngestResult> IngestAsync(
        string? rawJson,
        HttpRequest request,
        CancellationToken cancellationToken = default,
        string? trustedSiteKey = null,
        int trustedUserId = -1);
}

/// <summary>
/// Native twin of <c>epc-web-tracker-collect.php</c> / <c>epc_web_tracker_ingest</c>.
/// Tenant-isolation law: each beacon lands only in the owning tenant's DB — never
/// broadcast into shared stores. Creates the tracker tables when a tenant DB lacks them.
/// </summary>
public sealed class CpWebTrackerCollectService : ICpWebTrackerCollectService
{
    internal const string BeaconVersion = "20260909";

    private static readonly JsonSerializerOptions PayloadJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonSerializerOptions BeaconJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly Regex UuidRx = new(@"^[a-f0-9\-]{8,36}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex SiteKeyRx = new(@"[^a-z0-9_\-]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex EventTypeRx = new(@"[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex WsRx = new(@"\s+", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Dictionary<string, string> CountryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AE"] = "United Arab Emirates", ["SA"] = "Saudi Arabia", ["QA"] = "Qatar", ["KW"] = "Kuwait",
        ["BH"] = "Bahrain", ["OM"] = "Oman", ["IN"] = "India", ["PK"] = "Pakistan", ["US"] = "United States",
        ["GB"] = "United Kingdom", ["DE"] = "Germany", ["FR"] = "France", ["RU"] = "Russia", ["CN"] = "China",
        ["JP"] = "Japan", ["KR"] = "South Korea", ["TR"] = "Turkey", ["EG"] = "Egypt", ["ZA"] = "South Africa",
        ["AU"] = "Australia", ["CA"] = "Canada", ["SG"] = "Singapore", ["MY"] = "Malaysia", ["PH"] = "Philippines",
        ["ID"] = "Indonesia", ["TH"] = "Thailand", ["VN"] = "Vietnam", ["NG"] = "Nigeria", ["KE"] = "Kenya",
        ["UA"] = "Ukraine", ["PL"] = "Poland", ["IT"] = "Italy", ["ES"] = "Spain", ["NL"] = "Netherlands",
        ["SE"] = "Sweden", ["NO"] = "Norway", ["FI"] = "Finland", ["DK"] = "Denmark", ["CH"] = "Switzerland",
        ["BR"] = "Brazil", ["MX"] = "Mexico", ["AR"] = "Argentina", ["CL"] = "Chile", ["NZ"] = "New Zealand",
    };

    private readonly ITenantDbConnectionFactory _connections;
    private readonly ILogger<CpWebTrackerCollectService> _log;

    public CpWebTrackerCollectService(
        ITenantDbConnectionFactory connections,
        ILogger<CpWebTrackerCollectService> log)
    {
        _connections = connections;
        _log = log;
    }

    public static string BuildBeaconConfigJson(string? host, int userId, string? siteKey = null)
    {
        var hostname = NormalizeHost(host);
        var cfg = new Dictionary<string, object?>
        {
            ["endpoint"] = EcomAeRoutes.WebTrackerCollectPhp,
            // siteKey comes from the resolved tenant context on domain-mapped hosts
            // (auto247.ae → auto247); the hostname heuristic would mint "auto247_ae".
            ["site_key"] = !string.IsNullOrEmpty(siteKey)
                ? NormalizeSiteKey(siteKey)
                : CpWebTrackerDashboardBuilder.ResolveOwnSiteKey(host),
            ["hostname"] = hostname,
            ["user_id"] = userId > 0 ? userId : 0,
            ["is_registered"] = userId > 0,
            ["v"] = BeaconVersion,
        };
        return JsonSerializer.Serialize(cfg, BeaconJson);
    }

    public static bool IsUuidOk(string? uid)
        => !string.IsNullOrEmpty(uid) && UuidRx.IsMatch(uid);

    public static string Clip(string? value, int max)
    {
        var s = WsRx.Replace((value ?? string.Empty).Trim(), " ");
        return s.Length <= max ? s : s[..max];
    }

    public static string NormalizeSiteKey(string? siteKey)
        => SiteKeyRx.Replace((siteKey ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    public static (string Device, string Browser, string Os) ParseUa(string ua)
    {
        var device = "desktop";
        var browser = "Other";
        var os = "Other";
        var uaL = ua.ToLowerInvariant();
        if (uaL.Contains("tablet", StringComparison.Ordinal) || uaL.Contains("ipad", StringComparison.Ordinal))
        {
            device = "tablet";
        }
        else if (uaL.Contains("mobi", StringComparison.Ordinal) || uaL.Contains("android", StringComparison.Ordinal) || uaL.Contains("iphone", StringComparison.Ordinal))
        {
            device = "mobile";
        }

        if (uaL.Contains("edg/", StringComparison.Ordinal))
        {
            browser = "Edge";
        }
        else if (uaL.Contains("chrome", StringComparison.Ordinal) && !uaL.Contains("chromium", StringComparison.Ordinal))
        {
            browser = "Chrome";
        }
        else if (uaL.Contains("safari", StringComparison.Ordinal) && !uaL.Contains("chrome", StringComparison.Ordinal))
        {
            browser = "Safari";
        }
        else if (uaL.Contains("firefox", StringComparison.Ordinal))
        {
            browser = "Firefox";
        }
        else if (uaL.Contains("msie", StringComparison.Ordinal) || uaL.Contains("trident", StringComparison.Ordinal))
        {
            browser = "IE";
        }

        if (uaL.Contains("windows", StringComparison.Ordinal))
        {
            os = "Windows";
        }
        else if (uaL.Contains("mac os", StringComparison.Ordinal) || uaL.Contains("macintosh", StringComparison.Ordinal))
        {
            os = "macOS";
        }
        else if (uaL.Contains("android", StringComparison.Ordinal))
        {
            os = "Android";
        }
        else if (uaL.Contains("iphone", StringComparison.Ordinal) || uaL.Contains("ipad", StringComparison.Ordinal))
        {
            os = "iOS";
        }
        else if (uaL.Contains("linux", StringComparison.Ordinal))
        {
            os = "Linux";
        }

        return (device, browser, os);
    }

    public static string ClientIp(HttpRequest request)
    {
        foreach (var raw in new[]
                 {
                     request.Headers["CF-Connecting-IP"].ToString(),
                     request.Headers["X-Real-IP"].ToString(),
                     request.Headers["X-Forwarded-For"].ToString(),
                     request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                 })
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var candidate = raw.Contains(',', StringComparison.Ordinal) ? raw.Split(',')[0].Trim() : raw.Trim();
            if (IPAddress.TryParse(candidate, out _))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    public static (string Code, string Name, string Region, string City) GeoFromRequest(HttpRequest request)
    {
        var code = string.Empty;
        foreach (var header in new[] { "CF-IPCountry", "X-Country-Code", "X-AppEngine-Country" })
        {
            var raw = request.Headers[header].ToString().Trim();
            if (raw.Length == 2 && raw.All(char.IsLetter))
            {
                code = raw.ToUpperInvariant();
                break;
            }
        }

        if (code is "XX" or "T1")
        {
            code = string.Empty;
        }

        var name = code.Length == 0
            ? string.Empty
            : CountryNames.TryGetValue(code, out var mapped) ? mapped : code;
        return (code, name, string.Empty, string.Empty);
    }

    public static bool TryParsePayload(string? raw, out CpWebTrackerCollectPayload? payload, out string? error, string? trustedSiteKey = null, int trustedUserId = -1)
    {
        payload = null;
        error = "bad_json";
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        try
        {
            payload = JsonSerializer.Deserialize<CpWebTrackerCollectPayload>(raw, PayloadJson);
        }
        catch (JsonException)
        {
            return false;
        }

        if (payload is null)
        {
            return false;
        }

        var siteKey = NormalizeSiteKey(payload.SiteKey);
        if (siteKey.Length == 0 || !IsUuidOk(payload.SessionUid))
        {
            error = "bad_ids";
            return false;
        }

        payload.SiteKey = siteKey;
        // Server-trusted attribution wins over visitor-supplied values: site and shopper
        // ids are resolved from the request host and the authenticated session, so a
        // forged beacon cannot file activity under another storefront or user.
        if (!string.IsNullOrEmpty(trustedSiteKey))
        {
            payload.SiteKey = trustedSiteKey;
        }
        if (trustedUserId >= 0)
        {
            payload.UserId = trustedUserId;
        }
        if (!IsUuidOk(payload.VisitorUid))
        {
            payload.VisitorUid = string.Empty;
        }

        error = null;
        return true;
    }

    public async Task<CpWebTrackerIngestResult> IngestAsync(
        string? rawJson,
        HttpRequest request,
        CancellationToken cancellationToken = default,
        string? trustedSiteKey = null,
        int trustedUserId = -1)
    {
        if (!TryParsePayload(rawJson, out var payload, out var error, trustedSiteKey, trustedUserId) || payload is null)
        {
            return new(false, 0, 0, 0, 400, error ?? "bad_json");
        }

        if (!_connections.IsConfigured)
        {
            return new(false, 0, 0, 0, 503, "db");
        }

        // Tenant-isolation law: visitor sessions are tenant transactional data — each
        // beacon lands ONLY in that site's own tenant DB, never broadcast to shared
        // stores. Resolve site_key -> tenant record from the portal registry.
        DbConnection conn;
        try
        {
            conn = await OpenSiteDbAsync(payload.SiteKey ?? string.Empty, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Web tracker ingest could not resolve site db for {SiteKey}", payload.SiteKey);
            return new(false, 0, 0, 0, 503, "db");
        }

        await using (conn)
        {
            try
            {
                if (!await SessionsTableExistsAsync(conn, cancellationToken).ConfigureAwait(false))
                {
                    await EnsureTrackerTablesAsync(conn, cancellationToken).ConfigureAwait(false);
                }
                return await WriteBatchAsync(conn, payload, request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Web tracker ingest failed on {Database}", conn.Database);
                return new(false, 0, 0, 0, 503, "ingest_failed");
            }
        }
    }

    /// <summary>Opens the owning tenant DB for a site_key; unknown/platform keys land in the registry DB.</summary>
    private async Task<DbConnection> OpenSiteDbAsync(string siteKey, CancellationToken ct)
    {
        var key = NormalizeSiteKey(siteKey);
        var variants = new List<string> { key };
        // Legacy beacons used host-derived keys like stylenlook_ecomae_com.
        if (key.EndsWith("_ecomae_com", StringComparison.Ordinal))
        {
            variants.Add(key[..^"_ecomae_com".Length]);
        }

        string? dbName = null, dbUser = null, dbPass = null;
        try
        {
            await using var reg = await _connections.OpenRegistryAsync(ct).ConfigureAwait(false);
            var marks = string.Join(",", variants.Select(_ => "?"));
            await using var cmd = reg.CreateCommand();
            cmd.CommandText = ErpDb.Positional(
                "SELECT `db_name`,`db_user`,`db_password` FROM `epc_portal_tenants`"
                + " WHERE `site_key` IN (" + marks + ") AND `is_active` = 1 LIMIT 1");
            ErpDb.AddParameters(cmd, variants.Cast<object>().ToArray());
            await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (await r.ReadAsync(ct).ConfigureAwait(false))
            {
                dbName = r.IsDBNull(0) ? null : r.GetString(0);
                dbUser = r.IsDBNull(1) ? null : r.GetString(1);
                dbPass = r.IsDBNull(2) ? null : r.GetString(2);
            }
        }
        catch (DbException ex)
        {
            _log.LogWarning(ex, "Tenant registry lookup failed for site {SiteKey}", key);
        }

        if (string.IsNullOrWhiteSpace(dbName))
        {
            // Platform / unresolved hosts keep writing to the registry DB (its own tenant space).
            return await _connections.OpenRegistryAsync(ct).ConfigureAwait(false);
        }
        return !string.IsNullOrWhiteSpace(dbUser) && !string.IsNullOrWhiteSpace(dbPass)
            ? await _connections.OpenAsync(dbName, dbUser, dbPass, ct).ConfigureAwait(false)
            : await _connections.OpenAsync(dbName, ct).ConfigureAwait(false);
    }

    /// <summary>Creates the three tracker tables in a tenant DB that never received them (production DDL).</summary>
    private static async Task EnsureTrackerTablesAsync(DbConnection conn, CancellationToken ct)
    {
        const string sessions =
            "CREATE TABLE IF NOT EXISTS `epc_web_tracker_sessions` ("
            + "`id` bigint unsigned NOT NULL AUTO_INCREMENT, `session_uid` char(36) NOT NULL,"
            + "`visitor_uid` char(36) NOT NULL DEFAULT '', `site_key` varchar(64) NOT NULL,"
            + "`hostname` varchar(255) NOT NULL DEFAULT '', `user_id` int unsigned NOT NULL DEFAULT '0',"
            + "`is_registered` tinyint(1) NOT NULL DEFAULT '0', `first_seen_at` int unsigned NOT NULL DEFAULT '0',"
            + "`last_seen_at` int unsigned NOT NULL DEFAULT '0', `pageview_count` int unsigned NOT NULL DEFAULT '0',"
            + "`event_count` int unsigned NOT NULL DEFAULT '0', `duration_ms` int unsigned NOT NULL DEFAULT '0',"
            + "`landing_path` varchar(512) NOT NULL DEFAULT '', `landing_title` varchar(255) NOT NULL DEFAULT '',"
            + "`exit_path` varchar(512) NOT NULL DEFAULT '', `referrer` varchar(1024) NOT NULL DEFAULT '',"
            + "`referrer_host` varchar(255) NOT NULL DEFAULT '', `utm_source` varchar(128) NOT NULL DEFAULT '',"
            + "`utm_medium` varchar(128) NOT NULL DEFAULT '', `utm_campaign` varchar(128) NOT NULL DEFAULT '',"
            + "`utm_term` varchar(128) NOT NULL DEFAULT '', `utm_content` varchar(128) NOT NULL DEFAULT '',"
            + "`ip` varchar(45) NOT NULL DEFAULT '', `country_code` varchar(8) NOT NULL DEFAULT '',"
            + "`country_name` varchar(64) NOT NULL DEFAULT '', `region` varchar(128) NOT NULL DEFAULT '',"
            + "`city` varchar(128) NOT NULL DEFAULT '', `ua` varchar(512) NOT NULL DEFAULT '',"
            + "`device_type` varchar(16) NOT NULL DEFAULT '', `browser` varchar(64) NOT NULL DEFAULT '',"
            + "`os` varchar(64) NOT NULL DEFAULT '', `screen_w` smallint unsigned NOT NULL DEFAULT '0',"
            + "`screen_h` smallint unsigned NOT NULL DEFAULT '0', `language` varchar(32) NOT NULL DEFAULT '',"
            + "`timezone` varchar(64) NOT NULL DEFAULT '',"
            + "PRIMARY KEY (`id`), UNIQUE KEY `uq_site_session` (`site_key`,`session_uid`),"
            + "KEY `idx_site_last` (`site_key`,`last_seen_at`), KEY `idx_site_first` (`site_key`,`first_seen_at`),"
            + "KEY `idx_country` (`site_key`,`country_code`), KEY `idx_user` (`site_key`,`user_id`),"
            + "KEY `idx_visitor` (`site_key`,`visitor_uid`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        const string pageviews =
            "CREATE TABLE IF NOT EXISTS `epc_web_tracker_pageviews` ("
            + "`id` bigint unsigned NOT NULL AUTO_INCREMENT, `session_id` bigint unsigned NOT NULL DEFAULT '0',"
            + "`session_uid` char(36) NOT NULL, `site_key` varchar(64) NOT NULL,"
            + "`user_id` int unsigned NOT NULL DEFAULT '0', `ts` int unsigned NOT NULL DEFAULT '0',"
            + "`path` varchar(512) NOT NULL DEFAULT '', `query_string` varchar(1024) NOT NULL DEFAULT '',"
            + "`title` varchar(255) NOT NULL DEFAULT '', `referrer` varchar(1024) NOT NULL DEFAULT '',"
            + "`load_time_ms` int unsigned NOT NULL DEFAULT '0', `time_on_page_ms` int unsigned NOT NULL DEFAULT '0',"
            + "`scroll_max_pct` tinyint unsigned NOT NULL DEFAULT '0', `viewport_w` smallint unsigned NOT NULL DEFAULT '0',"
            + "`viewport_h` smallint unsigned NOT NULL DEFAULT '0',"
            + "PRIMARY KEY (`id`), KEY `idx_site_ts` (`site_key`,`ts`), KEY `idx_session` (`session_id`),"
            + "KEY `idx_path` (`site_key`,`path`(191))) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        const string events =
            "CREATE TABLE IF NOT EXISTS `epc_web_tracker_events` ("
            + "`id` bigint unsigned NOT NULL AUTO_INCREMENT, `session_id` bigint unsigned NOT NULL DEFAULT '0',"
            + "`pageview_id` bigint unsigned NOT NULL DEFAULT '0', `session_uid` char(36) NOT NULL,"
            + "`site_key` varchar(64) NOT NULL, `user_id` int unsigned NOT NULL DEFAULT '0',"
            + "`ts` int unsigned NOT NULL DEFAULT '0', `event_type` varchar(32) NOT NULL DEFAULT '',"
            + "`path` varchar(512) NOT NULL DEFAULT '', `x` int NOT NULL DEFAULT '0', `y` int NOT NULL DEFAULT '0',"
            + "`page_x` int NOT NULL DEFAULT '0', `page_y` int NOT NULL DEFAULT '0',"
            + "`element_tag` varchar(32) NOT NULL DEFAULT '', `element_id` varchar(128) NOT NULL DEFAULT '',"
            + "`element_class` varchar(255) NOT NULL DEFAULT '', `element_text` varchar(255) NOT NULL DEFAULT '',"
            + "`element_href` varchar(1024) NOT NULL DEFAULT '', `element_name` varchar(128) NOT NULL DEFAULT '',"
            + "`css_path` varchar(512) NOT NULL DEFAULT '', `search_query` varchar(512) NOT NULL DEFAULT '',"
            + "`search_context` varchar(64) NOT NULL DEFAULT '', `meta_json` text,"
            + "PRIMARY KEY (`id`), KEY `idx_site_ts` (`site_key`,`ts`), KEY `idx_session` (`session_id`),"
            + "KEY `idx_type` (`site_key`,`event_type`,`ts`), KEY `idx_search` (`site_key`,`search_query`(191))"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        await ErpDb.ExecuteAsync(conn, null, sessions, ct).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(conn, null, pageviews, ct).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(conn, null, events, ct).ConfigureAwait(false);
    }

    private static async Task TryOpen(
        List<DbConnection> opened,
        Func<CancellationToken, Task<DbConnection>> open,
        CancellationToken cancellationToken)
    {
        try
        {
            opened.Add(await open(cancellationToken).ConfigureAwait(false));
        }
        catch
        {
            // other candidates may still work
        }
    }

    private static async Task<bool> SessionsTableExistsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await ErpDb.ScalarAsync(connection, null, "SELECT 1 FROM `epc_web_tracker_sessions` LIMIT 1", cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (DbException)
        {
            return false;
        }
    }

    private static async Task<CpWebTrackerIngestResult> WriteBatchAsync(
        DbConnection connection,
        CpWebTrackerCollectPayload payload,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var siteKey = payload.SiteKey ?? string.Empty;
        var sessionUid = payload.SessionUid ?? string.Empty;
        var visitorUid = payload.VisitorUid ?? string.Empty;
        var hostname = Clip(string.IsNullOrWhiteSpace(payload.Hostname) ? request.Host.Value : payload.Hostname, 255);
        var userId = Math.Max(0, payload.UserId);
        var isReg = payload.IsRegistered || userId > 0 ? 1 : 0;
        var ip = Clip(ClientIp(request), 45);
        var geo = GeoFromRequest(request);
        if (geo.Code.Length == 0)
        {
            // Country storefront visitors (ae./qa./sa.…) get their country from the host when geo lookup is empty.
            var cSub = Storefront.StorefrontCountryDefaults.SubdomainOf(hostname);
            if (cSub is not null)
            {
                var ctry = Storefront.StorefrontCountryDefaults.Default(cSub);
                geo = (ctry.CountryCode, ctry.CountryName, geo.Region, geo.City);
            }
        }
        var ua = Clip(string.IsNullOrWhiteSpace(request.Headers.UserAgent) ? payload.Ua : request.Headers.UserAgent.ToString(), 512);
        var parsedUa = ParseUa(ua);
        var screenW = Clamp(payload.ScreenW, 0, 65535);
        var screenH = Clamp(payload.ScreenH, 0, 65535);
        var language = Clip(payload.Language, 32);
        var timezone = Clip(payload.Timezone, 64);
        var utm = payload.Utm;
        var utmSource = Clip(utm?.Source, 128);
        var utmMedium = Clip(utm?.Medium, 128);
        var utmCampaign = Clip(utm?.Campaign, 128);
        var utmTerm = Clip(utm?.Term, 128);
        var utmContent = Clip(utm?.Content, 128);
        var referrer = Clip(payload.Referrer, 1024);
        var referrerHost = string.Empty;
        if (referrer.Length > 0 && Uri.TryCreate(referrer, UriKind.Absolute, out var refUri))
        {
            referrerHost = Clip(refUri.Host, 255);
        }

        var pageviews = payload.Pageviews ?? [];
        var events = payload.Events ?? [];
        if (pageviews.Count > 20)
        {
            pageviews = pageviews.GetRange(0, 20);
        }

        if (events.Count > 80)
        {
            events = events.GetRange(0, 80);
        }

        var landingPath = string.Empty;
        var landingTitle = string.Empty;
        var exitPath = string.Empty;
        if (pageviews.Count > 0)
        {
            landingPath = Clip(pageviews[0].Path, 512);
            landingTitle = Clip(pageviews[0].Title, 255);
            exitPath = Clip(pageviews[^1].Path, 512);
        }

        var durationMs = Clamp(payload.DurationMs, 0, 86_400_000);

        var sessionId = await ErpDb.LongAsync(
            connection,
            null,
            "SELECT `id` FROM `epc_web_tracker_sessions` WHERE `site_key` = @p0 AND `session_uid` = @p1 LIMIT 1",
            cancellationToken,
            siteKey,
            sessionUid).ConfigureAwait(false);

        if (sessionId > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_web_tracker_sessions` SET
                        `visitor_uid` = IF(? <> '', ?, `visitor_uid`),
                        `hostname` = IF(? <> '', ?, `hostname`),
                        `user_id` = IF(? > 0, ?, `user_id`),
                        `is_registered` = IF(? > 0, 1, `is_registered`),
                        `last_seen_at` = ?,
                        `pageview_count` = `pageview_count` + ?,
                        `event_count` = `event_count` + ?,
                        `duration_ms` = GREATEST(`duration_ms`, ?),
                        `exit_path` = IF(? <> '', ?, `exit_path`),
                        `landing_path` = IF(`landing_path` = '' AND ? <> '', ?, `landing_path`),
                        `landing_title` = IF(`landing_title` = '' AND ? <> '', ?, `landing_title`),
                        `referrer` = IF(`referrer` = '' AND ? <> '', ?, `referrer`),
                        `referrer_host` = IF(`referrer_host` = '' AND ? <> '', ?, `referrer_host`),
                        `utm_source` = IF(`utm_source` = '' AND ? <> '', ?, `utm_source`),
                        `utm_medium` = IF(`utm_medium` = '' AND ? <> '', ?, `utm_medium`),
                        `utm_campaign` = IF(`utm_campaign` = '' AND ? <> '', ?, `utm_campaign`),
                        `utm_term` = IF(`utm_term` = '' AND ? <> '', ?, `utm_term`),
                        `utm_content` = IF(`utm_content` = '' AND ? <> '', ?, `utm_content`),
                        `ip` = IF(? <> '', ?, `ip`),
                        `country_code` = IF(`country_code` = '' AND ? <> '', ?, `country_code`),
                        `country_name` = IF(`country_name` = '' AND ? <> '', ?, `country_name`),
                        `region` = IF(`region` = '' AND ? <> '', ?, `region`),
                        `city` = IF(`city` = '' AND ? <> '', ?, `city`),
                        `ua` = IF(`ua` = '' AND ? <> '', ?, `ua`),
                        `device_type` = IF(`device_type` = '' AND ? <> '', ?, `device_type`),
                        `browser` = IF(`browser` = '' AND ? <> '', ?, `browser`),
                        `os` = IF(`os` = '' AND ? <> '', ?, `os`),
                        `screen_w` = IF(`screen_w` = 0 AND ? > 0, ?, `screen_w`),
                        `screen_h` = IF(`screen_h` = 0 AND ? > 0, ?, `screen_h`),
                        `language` = IF(`language` = '' AND ? <> '', ?, `language`),
                        `timezone` = IF(`timezone` = '' AND ? <> '', ?, `timezone`)
                    WHERE `id` = ?
                    """),
                cancellationToken,
                visitorUid, visitorUid,
                hostname, hostname,
                userId, userId,
                userId,
                now,
                pageviews.Count,
                events.Count,
                durationMs,
                exitPath, exitPath,
                landingPath, landingPath,
                landingTitle, landingTitle,
                referrer, referrer,
                referrerHost, referrerHost,
                utmSource, utmSource,
                utmMedium, utmMedium,
                utmCampaign, utmCampaign,
                utmTerm, utmTerm,
                utmContent, utmContent,
                ip, ip,
                geo.Code, geo.Code,
                geo.Name, geo.Name,
                geo.Region, geo.Region,
                geo.City, geo.City,
                ua, ua,
                parsedUa.Device, parsedUa.Device,
                parsedUa.Browser, parsedUa.Browser,
                parsedUa.Os, parsedUa.Os,
                screenW, screenW,
                screenH, screenH,
                language, language,
                timezone, timezone,
                sessionId).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_web_tracker_sessions` (
                        `session_uid`,`visitor_uid`,`site_key`,`hostname`,`user_id`,`is_registered`,
                        `first_seen_at`,`last_seen_at`,`pageview_count`,`event_count`,`duration_ms`,
                        `landing_path`,`landing_title`,`exit_path`,`referrer`,`referrer_host`,
                        `utm_source`,`utm_medium`,`utm_campaign`,`utm_term`,`utm_content`,
                        `ip`,`country_code`,`country_name`,`region`,`city`,
                        `ua`,`device_type`,`browser`,`os`,`screen_w`,`screen_h`,`language`,`timezone`
                    ) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                    """),
                cancellationToken,
                sessionUid, visitorUid, siteKey, hostname, userId, isReg,
                now, now, pageviews.Count, events.Count, durationMs,
                landingPath, landingTitle, exitPath, referrer, referrerHost,
                utmSource, utmMedium, utmCampaign, utmTerm, utmContent,
                ip, geo.Code, geo.Name, geo.Region, geo.City,
                ua, parsedUa.Device, parsedUa.Browser, parsedUa.Os,
                screenW, screenH, language, timezone).ConfigureAwait(false);
            sessionId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        }

        var pvCount = 0;
        var lastPvId = 0L;
        foreach (var pv in pageviews)
        {
            var path = Clip(pv.Path, 512);
            if (path.Length == 0)
            {
                continue;
            }

            var ts = NormalizeTs(pv.Ts, now);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_web_tracker_pageviews` (
                        `session_id`,`session_uid`,`site_key`,`user_id`,`ts`,`path`,`query_string`,`title`,`referrer`,
                        `load_time_ms`,`time_on_page_ms`,`scroll_max_pct`,`viewport_w`,`viewport_h`
                    ) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                    """),
                cancellationToken,
                sessionId, sessionUid, siteKey, userId, ts,
                path,
                Clip(pv.Query, 1024),
                Clip(pv.Title, 255),
                Clip(pv.Referrer, 1024),
                Clamp(pv.LoadTimeMs, 0, 600_000),
                Clamp(pv.TimeOnPageMs, 0, 86_400_000),
                Clamp(pv.ScrollMaxPct, 0, 100),
                Clamp(pv.ViewportW, 0, 65535),
                Clamp(pv.ViewportH, 0, 65535)).ConfigureAwait(false);
            lastPvId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            pvCount++;
        }

        var evCount = 0;
        foreach (var ev in events)
        {
            var type = EventTypeRx.Replace((ev.Type ?? ev.EventType ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);
            if (type.Length == 0 || type.Length > 32)
            {
                continue;
            }

            var ts = NormalizeTs(ev.Ts, now);
            string? meta = null;
            if (ev.Meta.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                meta = ev.Meta.GetRawText();
            }
            else if (ev.Meta.ValueKind == JsonValueKind.String)
            {
                meta = ev.Meta.GetString();
            }

            if (meta is { Length: > 4000 })
            {
                meta = meta[..4000];
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_web_tracker_events` (
                        `session_id`,`pageview_id`,`session_uid`,`site_key`,`user_id`,`ts`,`event_type`,`path`,
                        `x`,`y`,`page_x`,`page_y`,`element_tag`,`element_id`,`element_class`,`element_text`,
                        `element_href`,`element_name`,`css_path`,`search_query`,`search_context`,`meta_json`
                    ) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                    """),
                cancellationToken,
                sessionId,
                lastPvId,
                sessionUid, siteKey, userId, ts, type,
                Clip(ev.Path, 512),
                ev.X, ev.Y, ev.PageX, ev.PageY,
                Clip(ev.Tag, 32),
                Clip(ev.Id, 128),
                Clip(ev.Class, 255),
                Clip(ev.Text, 255),
                Clip(ev.Href, 1024),
                Clip(ev.Name, 128),
                Clip(ev.Css, 512),
                Clip(ev.Search, 512),
                Clip(ev.SearchCtx, 64),
                meta).ConfigureAwait(false);
            evCount++;
        }

        return new(true, sessionId, pvCount, evCount, 200, null);
    }

    private static long NormalizeTs(long ts, long now)
        => ts < 1_000_000_000L || ts > now + 3600L ? now : ts;

    private static int Clamp(int value, int min, int max)
        => value < min ? min : value > max ? max : value;

    private static string NormalizeHost(string? host)
    {
        var h = (host ?? string.Empty).Trim().ToLowerInvariant();
        var colon = h.IndexOf(':');
        return colon > 0 ? h[..colon] : h;
    }
}
