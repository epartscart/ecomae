using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Storefront;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-grow web tracker helpers. PHP identifiers kept for the inventory:
/// <c>epc_web_tracker_h</c>, <c>epc_web_tracker_ensure_schema</c>,
/// <c>epc_web_tracker_geo_from_request</c>, <c>epc_web_tracker_client_ip</c>,
/// <c>epc_web_tracker_lookup_ip</c>, <c>epc_web_tracker_country_name</c>,
/// <c>epc_web_tracker_parse_ua</c>, <c>epc_web_tracker_clip</c>,
/// <c>epc_web_tracker_uuid_ok</c>, <c>epc_web_tracker_ingest</c>,
/// <c>epc_web_tracker_resolve_site_key</c>, <c>epc_web_tracker_beacon_html</c>,
/// <c>epc_web_tracker_range_from_request</c>, <c>epc_web_tracker_filters_from_request</c>,
/// <c>epc_web_tracker_session_filter_sql</c>, <c>epc_web_tracker_dashboard</c>,
/// <c>epc_web_tracker_session_detail</c>, <c>epc_web_tracker_format_duration</c>,
/// <c>epc_web_tracker_csv_cell</c>, <c>epc_web_tracker_csv_line</c>,
/// <c>epc_web_tracker_export_csv</c>.
/// </summary>
public static class PhpPlanQ1Grow
{
    public const string WebTrackerPath = "content/general_pages/epc_web_tracker.php";

    private static readonly Regex UuidOk = new(@"^[a-f0-9\-]{8,36}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex CountryHeader = new(@"^[A-Za-z]{2}$", RegexOptions.CultureInvariant);
    private static readonly Regex Ws = new(@"\s+", RegexOptions.CultureInvariant);
    private static readonly Regex SiteKeyKeep = new(@"[^a-z0-9_\-]", RegexOptions.CultureInvariant);
    private static readonly Regex HostPort = new(@":\d+$", RegexOptions.CultureInvariant);
    private static readonly Regex WwwDot = new(@"^www\.", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex NonAlnum = new(@"[^a-z0-9]+", RegexOptions.CultureInvariant);
    private static readonly Regex DeviceKeep = new(@"[^a-z0-9_\-]", RegexOptions.CultureInvariant);
    private static readonly Regex CountryKeep = new(@"[^A-Z0-9]", RegexOptions.CultureInvariant);
    private static readonly Regex IpKeep = new(@"[^0-9a-fA-F:\.]", RegexOptions.CultureInvariant);
    private static readonly Regex Digits = new(@"[^0-9]", RegexOptions.CultureInvariant);
    private static readonly Regex BrowserKeep = new(@"[^a-zA-Z0-9 _\-\.]", RegexOptions.CultureInvariant);
    private static readonly Regex EventTypeKeep = new(@"[^a-z0-9_]", RegexOptions.CultureInvariant);

    private static readonly Dictionary<string, string> Countries = new(StringComparer.Ordinal)
    {
        ["AE"] = "United Arab Emirates", ["SA"] = "Saudi Arabia", ["QA"] = "Qatar", ["KW"] = "Kuwait",
        ["BH"] = "Bahrain", ["OM"] = "Oman", ["IN"] = "India", ["PK"] = "Pakistan", ["US"] = "United States",
        ["GB"] = "United Kingdom", ["DE"] = "Germany", ["FR"] = "France", ["RU"] = "Russia", ["CN"] = "China",
        ["JP"] = "Japan", ["KR"] = "South Korea", ["TR"] = "Turkey", ["EG"] = "Egypt", ["ZA"] = "South Africa",
        ["AU"] = "Australia", ["CA"] = "Canada", ["SG"] = "Singapore", ["MY"] = "Malaysia", ["PH"] = "Philippines",
        ["ID"] = "Indonesia", ["TH"] = "Thailand", ["VN"] = "Vietnam", ["NG"] = "Nigeria", ["KE"] = "Kenya",
        ["UA"] = "Ukraine", ["PL"] = "Poland", ["IT"] = "Italy", ["ES"] = "Spain", ["NL"] = "Netherlands",
        ["SE"] = "Sweden", ["NO"] = "Norway", ["FI"] = "Finland", ["DK"] = "Denmark", ["CH"] = "Switzerland",
        ["BR"] = "Brazil", ["MX"] = "Mexico", ["AR"] = "Argentina", ["CL"] = "Chile", ["NZ"] = "New Zealand"
    };

    private static readonly Dictionary<string, Dictionary<string, object?>> GeoMem = new(StringComparer.Ordinal);

    public static Dictionary<string, string> Server { get; set; } = new(StringComparer.Ordinal);
    public static Dictionary<string, string> Get { get; set; } = new(StringComparer.Ordinal);
    public static Dictionary<string, string> Post { get; set; } = new(StringComparer.Ordinal);
    public static Func<long> Clock { get; set; } = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static Func<string, Dictionary<string, object?>?> LookupIpHttp { get; set; } = _ => null;
    public static Func<string>? PortalSiteKey { get; set; }
    public static Func<int>? CurrentUserId { get; set; }

    public static void Reset()
    {
        Server = new(StringComparer.Ordinal);
        Get = new(StringComparer.Ordinal);
        Post = new(StringComparer.Ordinal);
        Clock = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        LookupIpHttp = _ => null;
        PortalSiteKey = null;
        CurrentUserId = null;
        GeoMem.Clear();
    }

    public sealed class GrowStore
    {
        public List<SessionRow> Sessions { get; } = [];
        public List<PageviewRow> Pageviews { get; } = [];
        public List<EventRow> Events { get; } = [];
        public int NextSessionId { get; set; } = 1;
        public int NextPageviewId { get; set; } = 1;
        public int NextEventId { get; set; } = 1;
        public bool SchemaReady { get; set; }
    }

    public sealed class SessionRow
    {
        public int Id { get; set; }
        public string SessionUid { get; set; } = "";
        public string VisitorUid { get; set; } = "";
        public string SiteKey { get; set; } = "";
        public string Hostname { get; set; } = "";
        public int UserId { get; set; }
        public int IsRegistered { get; set; }
        public long FirstSeenAt { get; set; }
        public long LastSeenAt { get; set; }
        public int PageviewCount { get; set; }
        public int EventCount { get; set; }
        public int DurationMs { get; set; }
        public string LandingPath { get; set; } = "";
        public string LandingTitle { get; set; } = "";
        public string ExitPath { get; set; } = "";
        public string Referrer { get; set; } = "";
        public string ReferrerHost { get; set; } = "";
        public string UtmSource { get; set; } = "";
        public string UtmMedium { get; set; } = "";
        public string UtmCampaign { get; set; } = "";
        public string UtmTerm { get; set; } = "";
        public string UtmContent { get; set; } = "";
        public string Ip { get; set; } = "";
        public string CountryCode { get; set; } = "";
        public string CountryName { get; set; } = "";
        public string Region { get; set; } = "";
        public string City { get; set; } = "";
        public string Ua { get; set; } = "";
        public string DeviceType { get; set; } = "";
        public string Browser { get; set; } = "";
        public string Os { get; set; } = "";
        public int ScreenW { get; set; }
        public int ScreenH { get; set; }
        public string Language { get; set; } = "";
        public string Timezone { get; set; } = "";
    }

    public sealed class PageviewRow
    {
        public int Id { get; set; }
        public int SessionId { get; set; }
        public string SessionUid { get; set; } = "";
        public string SiteKey { get; set; } = "";
        public int UserId { get; set; }
        public long Ts { get; set; }
        public string Path { get; set; } = "";
        public string QueryString { get; set; } = "";
        public string Title { get; set; } = "";
        public string Referrer { get; set; } = "";
        public int LoadTimeMs { get; set; }
        public int TimeOnPageMs { get; set; }
        public int ScrollMaxPct { get; set; }
        public int ViewportW { get; set; }
        public int ViewportH { get; set; }
    }

    public sealed class EventRow
    {
        public int Id { get; set; }
        public int SessionId { get; set; }
        public int PageviewId { get; set; }
        public string SessionUid { get; set; } = "";
        public string SiteKey { get; set; } = "";
        public int UserId { get; set; }
        public long Ts { get; set; }
        public string EventType { get; set; } = "";
        public string Path { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public int PageX { get; set; }
        public int PageY { get; set; }
        public string ElementTag { get; set; } = "";
        public string ElementId { get; set; } = "";
        public string ElementClass { get; set; } = "";
        public string ElementText { get; set; } = "";
        public string ElementHref { get; set; } = "";
        public string ElementName { get; set; } = "";
        public string CssPath { get; set; } = "";
        public string SearchQuery { get; set; } = "";
        public string SearchContext { get; set; } = "";
        public string? MetaJson { get; set; }
    }

    public static string EpcWebTrackerH(object? value)
        => PhpHtmlEntities.Encode(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");

    public static void EpcWebTrackerEnsureSchema(GrowStore db) => db.SchemaReady = true;

    public static Dictionary<string, object?> EpcWebTrackerGeoFromRequest()
    {
        var code = "";
        var name = "";
        var region = "";
        var city = "";
        foreach (var h in new[] { "HTTP_CF_IPCOUNTRY", "HTTP_X_COUNTRY_CODE", "HTTP_X_APPENGINE_COUNTRY" })
        {
            if (Server.TryGetValue(h, out var raw) && !Empty(raw) && CountryHeader.IsMatch(raw))
            {
                code = raw.ToUpperInvariant();
                break;
            }
        }

        if (code is "XX" or "T1")
        {
            code = "";
        }

        var ip = EpcWebTrackerClientIp();
        if (code == "" && ip != "")
        {
            var looked = EpcWebTrackerLookupIp(ip);
            code = Str(looked["code"]);
            name = Str(looked["name"]);
            region = Str(looked["region"]);
            city = Str(looked["city"]);
        }
        else if (code != "" && name == "")
        {
            name = EpcWebTrackerCountryName(code);
        }

        return Geo(code, name, region, city);
    }

    public static string EpcWebTrackerClientIp()
    {
        var candidates = new List<string>();
        foreach (var h in new[] { "HTTP_CF_CONNECTING_IP", "HTTP_X_REAL_IP", "HTTP_X_FORWARDED_FOR", "REMOTE_ADDR" })
        {
            if (!Server.TryGetValue(h, out var raw) || Empty(raw))
            {
                continue;
            }

            if (h == "HTTP_X_FORWARDED_FOR")
            {
                raw = raw.Split(',')[0].Trim();
            }

            candidates.Add(raw);
        }

        foreach (var ip in candidates)
        {
            if (IPAddress.TryParse(ip, out _))
            {
                return ip;
            }
        }

        return "";
    }

    public static Dictionary<string, object?> EpcWebTrackerLookupIp(string ip)
    {
        var empty = Geo("", "", "", "");
        ip = ip.Trim();
        if (ip == "")
        {
            return empty;
        }

        if (GeoMem.TryGetValue(ip, out var cachedMem))
        {
            return cachedMem;
        }

        if (!IPAddress.TryParse(ip, out var parsed) || IsPrivateOrReserved(parsed))
        {
            return GeoMem[ip] = empty;
        }

        var data = LookupIpHttp(ip);
        var code = "";
        var name = "";
        var region = "";
        var city = "";
        if (data != null && Empty(data.TryGetValue("error", out var err) ? err : null) && !Empty(Field(data, "country_code")))
        {
            code = Str(Field(data, "country_code")).ToUpperInvariant();
            if (code.Length > 8)
            {
                code = code[..8];
            }

            name = Str(Field(data, "country_name")).Trim();
            region = Str(Field(data, "region")).Trim();
            city = Str(Field(data, "city")).Trim();
        }

        if (code != "" && name == "")
        {
            name = EpcWebTrackerCountryName(code);
        }

        return GeoMem[ip] = Geo(code, name, region, city);
    }

    public static string EpcWebTrackerCountryName(string code)
    {
        code = code.ToUpperInvariant();
        return Countries.TryGetValue(code, out var name) ? name : code;
    }

    public static Dictionary<string, object?> EpcWebTrackerParseUa(string ua)
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

        return new(StringComparer.Ordinal) { ["device_type"] = device, ["browser"] = browser, ["os"] = os };
    }

    public static string EpcWebTrackerClip(string s, int max)
    {
        s = Ws.Replace(s, " ").Trim();
        return s.Length <= max ? s : s[..max];
    }

    public static bool EpcWebTrackerUuidOk(string uid) => UuidOk.IsMatch(uid);

    public static Dictionary<string, object?> EpcWebTrackerIngest(GrowStore db, Dictionary<string, object?> payload)
    {
        EpcWebTrackerEnsureSchema(db);
        var siteKey = SiteKeyKeep.Replace(Str(Field(payload, "site_key")).ToLowerInvariant(), "");
        var sessionUid = Str(Field(payload, "session_uid"));
        var visitorUid = Str(Field(payload, "visitor_uid"));
        if (siteKey == "" || !EpcWebTrackerUuidOk(sessionUid))
        {
            return new(StringComparer.Ordinal) { ["ok"] = false, ["session_id"] = 0, ["pageviews"] = 0, ["events"] = 0, ["error"] = "bad_ids" };
        }

        if (visitorUid != "" && !EpcWebTrackerUuidOk(visitorUid))
        {
            visitorUid = "";
        }

        var hostname = EpcWebTrackerClip(Str(Field(payload, "hostname"), Svr("HTTP_HOST")), 255);
        var userId = Math.Max(0, IntVal(Field(payload, "user_id")));
        var isReg = !Empty(Field(payload, "is_registered")) || userId > 0 ? 1 : 0;
        var now = Clock();
        var ip = EpcWebTrackerClientIp();
        var geo = EpcWebTrackerGeoFromRequest();
        var ua = EpcWebTrackerClip(Svr("HTTP_USER_AGENT", Str(Field(payload, "ua"))), 512);
        var parsedUa = EpcWebTrackerParseUa(ua);
        var screenW = Clamp(IntVal(Field(payload, "screen_w")), 0, 65535);
        var screenH = Clamp(IntVal(Field(payload, "screen_h")), 0, 65535);
        var language = EpcWebTrackerClip(Str(Field(payload, "language")), 32);
        var timezone = EpcWebTrackerClip(Str(Field(payload, "timezone")), 64);
        var utm = Field(payload, "utm") as Dictionary<string, object?> ?? [];
        var utmSource = EpcWebTrackerClip(Str(Field(utm, "source")), 128);
        var utmMedium = EpcWebTrackerClip(Str(Field(utm, "medium")), 128);
        var utmCampaign = EpcWebTrackerClip(Str(Field(utm, "campaign")), 128);
        var utmTerm = EpcWebTrackerClip(Str(Field(utm, "term")), 128);
        var utmContent = EpcWebTrackerClip(Str(Field(utm, "content")), 128);
        var referrer = EpcWebTrackerClip(Str(Field(payload, "referrer")), 1024);
        var referrerHost = "";
        if (referrer != "" && Uri.TryCreate(referrer, UriKind.Absolute, out var uri))
        {
            referrerHost = EpcWebTrackerClip(uri.Host, 255);
        }

        var pageviews = AsDictList(Field(payload, "pageviews"));
        var events = AsDictList(Field(payload, "events"));
        if (pageviews.Count > 20)
        {
            pageviews = pageviews.Take(20).ToList();
        }

        if (events.Count > 80)
        {
            events = events.Take(80).ToList();
        }

        var landingPath = "";
        var landingTitle = "";
        var exitPath = "";
        if (pageviews.Count > 0)
        {
            landingPath = EpcWebTrackerClip(Str(Field(pageviews[0], "path")), 512);
            landingTitle = EpcWebTrackerClip(Str(Field(pageviews[0], "title")), 255);
            exitPath = EpcWebTrackerClip(Str(Field(pageviews[^1], "path")), 512);
        }

        var durationMs = Math.Max(0, IntVal(Field(payload, "duration_ms")));
        if (durationMs > 86400000)
        {
            durationMs = 86400000;
        }

        var row = db.Sessions.FirstOrDefault(s => s.SiteKey == siteKey && s.SessionUid == sessionUid);
        int sessionId;
        if (row != null)
        {
            sessionId = row.Id;
            if (visitorUid != "") row.VisitorUid = visitorUid;
            if (hostname != "") row.Hostname = hostname;
            if (userId > 0) row.UserId = userId;
            if (userId > 0) row.IsRegistered = 1;
            row.LastSeenAt = now;
            row.PageviewCount += pageviews.Count;
            row.EventCount += events.Count;
            row.DurationMs = Math.Max(row.DurationMs, durationMs);
            if (exitPath != "") row.ExitPath = exitPath;
            if (row.LandingPath == "" && landingPath != "") row.LandingPath = landingPath;
            if (row.LandingTitle == "" && landingTitle != "") row.LandingTitle = landingTitle;
            if (row.Referrer == "" && referrer != "") row.Referrer = referrer;
            if (row.ReferrerHost == "" && referrerHost != "") row.ReferrerHost = referrerHost;
            if (row.UtmSource == "" && utmSource != "") row.UtmSource = utmSource;
            if (row.UtmMedium == "" && utmMedium != "") row.UtmMedium = utmMedium;
            if (row.UtmCampaign == "" && utmCampaign != "") row.UtmCampaign = utmCampaign;
            if (row.UtmTerm == "" && utmTerm != "") row.UtmTerm = utmTerm;
            if (row.UtmContent == "" && utmContent != "") row.UtmContent = utmContent;
            if (ip != "") row.Ip = ip;
            if (row.CountryCode == "" && Str(geo["code"]) != "") row.CountryCode = Str(geo["code"]);
            if (row.CountryName == "" && Str(geo["name"]) != "") row.CountryName = Str(geo["name"]);
            if (row.Region == "" && Str(geo["region"]) != "") row.Region = Str(geo["region"]);
            if (row.City == "" && Str(geo["city"]) != "") row.City = Str(geo["city"]);
            if (row.Ua == "" && ua != "") row.Ua = ua;
            if (row.DeviceType == "" && Str(parsedUa["device_type"]) != "") row.DeviceType = Str(parsedUa["device_type"]);
            if (row.Browser == "" && Str(parsedUa["browser"]) != "") row.Browser = Str(parsedUa["browser"]);
            if (row.Os == "" && Str(parsedUa["os"]) != "") row.Os = Str(parsedUa["os"]);
            if (row.ScreenW == 0 && screenW > 0) row.ScreenW = screenW;
            if (row.ScreenH == 0 && screenH > 0) row.ScreenH = screenH;
            if (row.Language == "" && language != "") row.Language = language;
            if (row.Timezone == "" && timezone != "") row.Timezone = timezone;
        }
        else
        {
            sessionId = db.NextSessionId++;
            db.Sessions.Add(new SessionRow
            {
                Id = sessionId,
                SessionUid = sessionUid,
                VisitorUid = visitorUid,
                SiteKey = siteKey,
                Hostname = hostname,
                UserId = userId,
                IsRegistered = isReg,
                FirstSeenAt = now,
                LastSeenAt = now,
                PageviewCount = pageviews.Count,
                EventCount = events.Count,
                DurationMs = durationMs,
                LandingPath = landingPath,
                LandingTitle = landingTitle,
                ExitPath = exitPath,
                Referrer = referrer,
                ReferrerHost = referrerHost,
                UtmSource = utmSource,
                UtmMedium = utmMedium,
                UtmCampaign = utmCampaign,
                UtmTerm = utmTerm,
                UtmContent = utmContent,
                Ip = ip,
                CountryCode = Str(geo["code"]),
                CountryName = Str(geo["name"]),
                Region = Str(geo["region"]),
                City = Str(geo["city"]),
                Ua = ua,
                DeviceType = Str(parsedUa["device_type"]),
                Browser = Str(parsedUa["browser"]),
                Os = Str(parsedUa["os"]),
                ScreenW = screenW,
                ScreenH = screenH,
                Language = language,
                Timezone = timezone
            });
        }

        var pvCount = 0;
        var lastPvId = 0;
        foreach (var pv in pageviews)
        {
            var path = EpcWebTrackerClip(Str(Field(pv, "path")), 512);
            if (path == "")
            {
                continue;
            }

            var ts = (long)IntVal(Field(pv, "ts"), (int)now);
            if (ts < 1000000000 || ts > now + 3600)
            {
                ts = now;
            }

            var id = db.NextPageviewId++;
            db.Pageviews.Add(new PageviewRow
            {
                Id = id,
                SessionId = sessionId,
                SessionUid = sessionUid,
                SiteKey = siteKey,
                UserId = userId,
                Ts = ts,
                Path = path,
                QueryString = EpcWebTrackerClip(Str(Field(pv, "query")), 1024),
                Title = EpcWebTrackerClip(Str(Field(pv, "title")), 255),
                Referrer = EpcWebTrackerClip(Str(Field(pv, "referrer")), 1024),
                LoadTimeMs = Clamp(IntVal(Field(pv, "load_time_ms")), 0, 600000),
                TimeOnPageMs = Clamp(IntVal(Field(pv, "time_on_page_ms")), 0, 86400000),
                ScrollMaxPct = Clamp(IntVal(Field(pv, "scroll_max_pct")), 0, 100),
                ViewportW = Clamp(IntVal(Field(pv, "viewport_w")), 0, 65535),
                ViewportH = Clamp(IntVal(Field(pv, "viewport_h")), 0, 65535)
            });
            lastPvId = id;
            pvCount++;
        }

        var evCount = 0;
        foreach (var ev in events)
        {
            var type = EventTypeKeep.Replace(Str(Field(ev, "type"), Str(Field(ev, "event_type"))).ToLowerInvariant(), "");
            if (type == "" || type.Length > 32)
            {
                continue;
            }

            var ts = (long)IntVal(Field(ev, "ts"), (int)now);
            if (ts < 1000000000 || ts > now + 3600)
            {
                ts = now;
            }

            string? meta = null;
            if (ev.TryGetValue("meta", out var metaVal) && metaVal != null)
            {
                meta = metaVal as string ?? JsonSerializer.Serialize(metaVal);
                if (meta.Length > 4000)
                {
                    meta = meta[..4000];
                }
            }

            db.Events.Add(new EventRow
            {
                Id = db.NextEventId++,
                SessionId = sessionId,
                PageviewId = lastPvId,
                SessionUid = sessionUid,
                SiteKey = siteKey,
                UserId = userId,
                Ts = ts,
                EventType = type,
                Path = EpcWebTrackerClip(Str(Field(ev, "path")), 512),
                X = IntVal(Field(ev, "x")),
                Y = IntVal(Field(ev, "y")),
                PageX = IntVal(Field(ev, "page_x")),
                PageY = IntVal(Field(ev, "page_y")),
                ElementTag = EpcWebTrackerClip(Str(Field(ev, "tag")), 32),
                ElementId = EpcWebTrackerClip(Str(Field(ev, "id")), 128),
                ElementClass = EpcWebTrackerClip(Str(Field(ev, "class")), 255),
                ElementText = EpcWebTrackerClip(Str(Field(ev, "text")), 255),
                ElementHref = EpcWebTrackerClip(Str(Field(ev, "href")), 1024),
                ElementName = EpcWebTrackerClip(Str(Field(ev, "name")), 128),
                CssPath = EpcWebTrackerClip(Str(Field(ev, "css")), 512),
                SearchQuery = EpcWebTrackerClip(Str(Field(ev, "search")), 512),
                SearchContext = EpcWebTrackerClip(Str(Field(ev, "search_ctx")), 64),
                MetaJson = meta
            });
            evCount++;
        }

        return new(StringComparer.Ordinal) { ["ok"] = true, ["session_id"] = sessionId, ["pageviews"] = pvCount, ["events"] = evCount };
    }

    public static string EpcWebTrackerResolveSiteKey()
    {
        if (PortalSiteKey != null)
        {
            var key = SiteKeyKeep.Replace(PortalSiteKey().ToLowerInvariant(), "");
            if (key != "")
            {
                return key;
            }
        }

        var host = Svr("HTTP_HOST").ToLowerInvariant();
        host = HostPort.Replace(host, "");
        if (host is "www.ecomae.com" or "ecomae.com" or "cp.ecomae.com")
        {
            return "ecomae";
        }

        if (host.Contains("epartscart", StringComparison.Ordinal))
        {
            return "epartscart";
        }

        var cleaned = WwwDot.Replace(host, "");
        cleaned = NonAlnum.Replace(cleaned, "_").Trim('_');
        return cleaned != "" ? cleaned : "unknown";
    }

    public static string EpcWebTrackerBeaconHtml()
    {
        var userId = CurrentUserId?.Invoke() ?? 0;
        var siteKey = EpcWebTrackerResolveSiteKey();
        var host = Svr("HTTP_HOST").ToLowerInvariant();
        var json = "{\"endpoint\":\"/epc-web-tracker-collect.php\",\"site_key\":" + JsonSerializer.Serialize(siteKey)
            + ",\"hostname\":" + JsonSerializer.Serialize(host) + ",\"user_id\":" + userId
            + ",\"is_registered\":" + (userId > 0 ? "true" : "false") + ",\"v\":\"20260718\"}";
        return "\n<script>window.EPC_WEB_TRACKER=" + json + ";</script>"
            + "\n<script src=\"/content/general_pages/epc_web_tracker.js?v=20260718\" defer></script>\n";
    }

    public static Dictionary<string, object?> EpcWebTrackerRangeFromRequest()
    {
        var to = Clock();
        var from = to - 7 * 86400;
        if (!Empty(G("from")))
        {
            var t = DateTimeOffset.TryParse(G("from") + " 00:00:00", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt)
                ? dt.ToUnixTimeSeconds() : 0;
            if (t != 0)
            {
                from = t;
            }
        }

        if (!Empty(G("to")))
        {
            var t = DateTimeOffset.TryParse(G("to") + " 23:59:59", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt)
                ? dt.ToUnixTimeSeconds() : 0;
            if (t != 0)
            {
                to = t;
            }
        }

        if (from > to)
        {
            (from, to) = (to, from);
        }

        if (to - from > 366L * 86400)
        {
            from = to - 366L * 86400;
        }

        return new(StringComparer.Ordinal) { ["from"] = from, ["to"] = to };
    }

    public static Dictionary<string, object?> EpcWebTrackerFiltersFromRequest()
    {
        var src = new Dictionary<string, string>(Get, StringComparer.Ordinal);
        foreach (var kv in Post)
        {
            src[kv.Key] = kv.Value;
        }

        var device = DeviceKeep.Replace(Pick(src, "device", "device_type").Trim().ToLowerInvariant(), "");
        if (device is not ("desktop" or "mobile" or "tablet"))
        {
            device = "";
        }

        var country = CountryKeep.Replace(Pick(src, "country", "country_code").Trim().ToUpperInvariant(), "");
        if (country.Length > 8)
        {
            country = country[..8];
        }

        var ip = IpKeep.Replace(Pick(src, "ip").Trim(), "");
        if (ip.Length > 45)
        {
            ip = ip[..45];
        }

        var userId = Digits.Replace(Pick(src, "user_id", "user").Trim(), "");
        if (userId.Length > 12)
        {
            userId = userId[..12];
        }

        var userType = Pick(src, "user_type", "who").Trim().ToLowerInvariant();
        if (userType is not ("guest" or "registered" or "reg"))
        {
            userType = "";
        }

        if (userType == "reg")
        {
            userType = "registered";
        }

        var path = Pick(src, "path", "page").Trim().Replace("\0", "", StringComparison.Ordinal).Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);
        if (path.Length > 200)
        {
            path = path[..200];
        }

        var browser = BrowserKeep.Replace(Pick(src, "browser").Trim(), "");
        if (browser.Length > 40)
        {
            browser = browser[..40];
        }

        return new(StringComparer.Ordinal)
        {
            ["device"] = device,
            ["country"] = country,
            ["ip"] = ip,
            ["user_id"] = userId,
            ["user_type"] = userType,
            ["path"] = path,
            ["browser"] = browser
        };
    }

    public static Dictionary<string, object?> EpcWebTrackerSessionFilterSql(Dictionary<string, object?> filters, string alias = "")
    {
        string Col(string name) => alias != "" ? alias + ".`" + name + "`" : "`" + name + "`";
        var sql = "";
        var parameters = new List<object?>();
        var device = Str(Field(filters, "device"));
        if (device != "")
        {
            sql += " AND " + Col("device_type") + " = ? ";
            parameters.Add(device);
        }

        var country = Str(Field(filters, "country"));
        if (country != "")
        {
            sql += " AND " + Col("country_code") + " = ? ";
            parameters.Add(country);
        }

        var ip = Str(Field(filters, "ip"));
        if (ip != "")
        {
            sql += " AND " + Col("ip") + " LIKE ? ";
            parameters.Add("%" + ip + "%");
        }

        var userId = Str(Field(filters, "user_id"));
        if (userId != "")
        {
            sql += " AND " + Col("user_id") + " = ? ";
            parameters.Add(int.Parse(userId, CultureInfo.InvariantCulture));
        }

        var userType = Str(Field(filters, "user_type"));
        if (userType == "guest")
        {
            sql += " AND " + Col("is_registered") + " = 0 ";
        }
        else if (userType == "registered")
        {
            sql += " AND " + Col("is_registered") + " = 1 ";
        }

        var browser = Str(Field(filters, "browser"));
        if (browser != "")
        {
            sql += " AND " + Col("browser") + " LIKE ? ";
            parameters.Add("%" + browser + "%");
        }

        var path = Str(Field(filters, "path"));
        if (path != "")
        {
            var like = "%" + path + "%";
            var idRef = alias != "" ? alias + ".`id`" : "`id`";
            sql += " AND (" + Col("landing_path") + " LIKE ? OR " + Col("exit_path") + " LIKE ?"
                + " OR EXISTS (SELECT 1 FROM `epc_web_tracker_pageviews` _wt_pv"
                + " WHERE _wt_pv.`session_id` = " + idRef + " AND _wt_pv.`path` LIKE ?)) ";
            parameters.Add(like);
            parameters.Add(like);
            parameters.Add(like);
        }

        return new(StringComparer.Ordinal) { ["sql"] = sql, ["params"] = parameters, ["active"] = sql != "" };
    }

    public static Dictionary<string, object?> EpcWebTrackerDashboard(GrowStore db, string siteKey, long from, long to, bool allSites = false, Dictionary<string, object?>? filters = null)
    {
        EpcWebTrackerEnsureSchema(db);
        filters ??= new(StringComparer.Ordinal);
        var scoped = db.Sessions.Where(s => s.LastSeenAt >= from && s.LastSeenAt <= to && SiteOk(s, siteKey, allSites) && SessionMatches(s, db, filters)).ToList();
        var sessions = scoped.Count;
        var visitors = scoped.Select(s => s.VisitorUid).Where(v => v != "").Distinct(StringComparer.Ordinal).Count();
        var pageviews = scoped.Sum(s => s.PageviewCount);
        var events = scoped.Sum(s => s.EventCount);
        var registered = scoped.Count(s => s.IsRegistered == 1);
        var guest = scoped.Count(s => s.IsRegistered == 0);
        var avgDur = sessions > 0 ? scoped.Average(s => (double)s.DurationMs) : 0;
        var avgPages = sessions > 0 ? scoped.Average(s => (double)s.PageviewCount) : 0;
        var bounces = scoped.Count(s => s.PageviewCount <= 1);
        var evRows = db.Events.Where(e => e.Ts >= from && e.Ts <= to && EventInScope(e, db, siteKey, allSites, filters)).ToList();
        var pvRows = db.Pageviews.Where(p => p.Ts >= from && p.Ts <= to && PageInScope(p, db, siteKey, allSites, filters)).ToList();
        var pathFilter = Str(Field(filters, "path"));
        if (pathFilter != "")
        {
            pvRows = pvRows.Where(p => p.Path.Contains(pathFilter, StringComparison.Ordinal)).ToList();
        }

        var summary = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["sessions"] = sessions,
            ["visitors"] = visitors,
            ["pageviews"] = pageviews,
            ["events"] = events,
            ["clicks"] = evRows.Count(e => e.EventType == "click"),
            ["searches"] = evRows.Count(e => e.EventType == "search"),
            ["registered_sessions"] = registered,
            ["guest_sessions"] = guest,
            ["avg_duration_ms"] = (int)Math.Round(avgDur, MidpointRounding.AwayFromZero),
            ["avg_pages"] = Math.Round(avgPages, 2, MidpointRounding.AwayFromZero),
            ["bounce_rate"] = sessions > 0 ? Math.Round(100.0 * bounces / sessions, 1, MidpointRounding.AwayFromZero) : 0
        };

        var topPages = pvRows.GroupBy(p => p.Path, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .Take(40)
            .Select(g => new Dictionary<string, object?>(StringComparer.Ordinal) { ["path"] = g.Key })
            .ToList();
        var searches = evRows.Where(e => e.EventType == "search" && e.SearchQuery != "")
            .GroupBy(e => e.SearchQuery, StringComparer.Ordinal)
            .Select(g => new Dictionary<string, object?>(StringComparer.Ordinal) { ["search_query"] = g.Key })
            .ToList();
        var recent = scoped.OrderByDescending(s => s.LastSeenAt).Take(100).ToList();
        var facetDevices = db.Sessions.Where(s => s.LastSeenAt >= from && s.LastSeenAt <= to && SiteOk(s, siteKey, allSites) && s.DeviceType != "")
            .GroupBy(s => s.DeviceType, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .Take(20)
            .Select(g => new Dictionary<string, object?>(StringComparer.Ordinal) { ["device_type"] = g.Key, ["sessions"] = g.Count() })
            .ToList();

        return new(StringComparer.Ordinal)
        {
            ["summary"] = summary,
            ["top_pages"] = topPages,
            ["searches"] = searches,
            ["recent_sessions"] = recent,
            ["facets"] = new Dictionary<string, object?>(StringComparer.Ordinal) { ["devices"] = facetDevices }
        };
    }

    public static Dictionary<string, object?> EpcWebTrackerSessionDetail(GrowStore db, int sessionId, string allowedSiteKey = "", bool allSites = false)
    {
        EpcWebTrackerEnsureSchema(db);
        var session = db.Sessions.FirstOrDefault(s => s.Id == sessionId);
        if (session == null || (!allSites && allowedSiteKey != "" && session.SiteKey != allowedSiteKey))
        {
            return new(StringComparer.Ordinal) { ["session"] = null, ["pageviews"] = new List<PageviewRow>(), ["events"] = new List<EventRow>() };
        }

        return new(StringComparer.Ordinal)
        {
            ["session"] = session,
            ["pageviews"] = db.Pageviews.Where(p => p.SessionId == sessionId).OrderBy(p => p.Ts).ThenBy(p => p.Id).Take(500).ToList(),
            ["events"] = db.Events.Where(e => e.SessionId == sessionId).OrderBy(e => e.Ts).ThenBy(e => e.Id).Take(2000).ToList()
        };
    }

    public static string EpcWebTrackerFormatDuration(int ms)
    {
        if (ms < 1000)
        {
            return ms + " ms";
        }

        var s = (int)Math.Round(ms / 1000.0, MidpointRounding.AwayFromZero);
        if (s < 60)
        {
            return s + "s";
        }

        var m = s / 60;
        var rs = s % 60;
        if (m < 60)
        {
            return m + "m " + rs + "s";
        }

        var h = m / 60;
        var rm = m % 60;
        return h + "h " + rm + "m";
    }

    public static string EpcWebTrackerCsvCell(object? value)
    {
        var s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        s = s.Replace("\r\n", " ", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        if (s.Contains('"', StringComparison.Ordinal) || s.Contains(',', StringComparison.Ordinal) || s.Contains(';', StringComparison.Ordinal))
        {
            return "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return s;
    }

    public static string EpcWebTrackerCsvLine(IEnumerable<object?> row)
        => string.Join(",", row.Select(EpcWebTrackerCsvCell)) + "\r\n";

    public static string EpcWebTrackerExportCsv(GrowStore db, string siteKey, long from, long to, bool allSites = false, Dictionary<string, object?>? filters = null)
    {
        var data = EpcWebTrackerDashboard(db, siteKey, from, to, allSites, filters);
        var csv = "\uFEFF" + EpcWebTrackerCsvLine(["Website tracker full report"]);
        var searches = (List<Dictionary<string, object?>>)data["searches"]!;
        foreach (var s in searches)
        {
            csv += EpcWebTrackerCsvLine([s["search_query"]]);
        }

        return csv;
    }

    private static bool SessionMatches(SessionRow s, GrowStore db, Dictionary<string, object?> filters)
    {
        var device = Str(Field(filters, "device"));
        if (device != "" && s.DeviceType != device)
        {
            return false;
        }

        var country = Str(Field(filters, "country"));
        if (country != "" && s.CountryCode != country)
        {
            return false;
        }

        var ip = Str(Field(filters, "ip"));
        if (ip != "" && !s.Ip.Contains(ip, StringComparison.Ordinal))
        {
            return false;
        }

        var userId = Str(Field(filters, "user_id"));
        if (userId != "" && s.UserId != int.Parse(userId, CultureInfo.InvariantCulture))
        {
            return false;
        }

        var userType = Str(Field(filters, "user_type"));
        if (userType == "guest" && s.IsRegistered != 0)
        {
            return false;
        }

        if (userType == "registered" && s.IsRegistered != 1)
        {
            return false;
        }

        var browser = Str(Field(filters, "browser"));
        if (browser != "" && !s.Browser.Contains(browser, StringComparison.Ordinal))
        {
            return false;
        }

        var path = Str(Field(filters, "path"));
        if (path != "" && !s.LandingPath.Contains(path, StringComparison.Ordinal) && !s.ExitPath.Contains(path, StringComparison.Ordinal)
            && !db.Pageviews.Any(p => p.SessionId == s.Id && p.Path.Contains(path, StringComparison.Ordinal)))
        {
            return false;
        }

        return true;
    }

    private static bool SiteOk(SessionRow s, string siteKey, bool allSites)
        => allSites || siteKey == "" || siteKey == "_all" || s.SiteKey == siteKey;

    private static bool EventInScope(EventRow e, GrowStore db, string siteKey, bool allSites, Dictionary<string, object?> filters)
    {
        var session = db.Sessions.FirstOrDefault(s => s.Id == e.SessionId);
        return session != null && SiteOk(session, siteKey, allSites) && SessionMatches(session, db, filters);
    }

    private static bool PageInScope(PageviewRow p, GrowStore db, string siteKey, bool allSites, Dictionary<string, object?> filters)
    {
        var session = db.Sessions.FirstOrDefault(s => s.Id == p.SessionId);
        return session != null && SiteOk(session, siteKey, allSites) && SessionMatches(session, db, filters);
    }

    private static Dictionary<string, object?> Geo(string code, string name, string region, string city)
        => new(StringComparer.Ordinal) { ["code"] = code, ["name"] = name, ["region"] = region, ["city"] = city };

    private static bool IsPrivateOrReserved(IPAddress ip)
        => IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)
            || ip.ToString().StartsWith("10.", StringComparison.Ordinal)
            || ip.ToString().StartsWith("192.168.", StringComparison.Ordinal)
            || ip.ToString().StartsWith("127.", StringComparison.Ordinal);

    private static object? Field(Dictionary<string, object?> map, string key)
        => map.TryGetValue(key, out var value) ? value : null;

    private static string Str(object? value, string fallback = "")
        => value switch
        {
            null => fallback,
            string s => s,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback
        };

    private static int IntVal(object? value, int fallback = 0)
        => value switch
        {
            int i => i,
            long l => (int)l,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
            _ => fallback
        };

    private static bool Empty(object? value)
        => value is null or false or 0 or 0L || value is string s && (s == "" || s == "0");

    private static string Svr(string key, string fallback = "")
        => Server.TryGetValue(key, out var v) ? v : fallback;

    private static string G(string key)
        => Get.TryGetValue(key, out var v) ? v : "";

    private static string Pick(Dictionary<string, string> src, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (src.TryGetValue(key, out var v))
            {
                return v;
            }
        }

        return "";
    }

    private static int Clamp(int value, int min, int max)
        => Math.Min(max, Math.Max(min, value));

    private static List<Dictionary<string, object?>> AsDictList(object? value)
        => value switch
        {
            List<Dictionary<string, object?>> list => list,
            IEnumerable<object?> e => e.OfType<Dictionary<string, object?>>().ToList(),
            _ => []
        };
}
