using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EcomAE.Platform.Data;

namespace EcomAE.Platform.Migration;

/// <summary>
/// PHP-parity website tracker dashboard (epc_web_tracker_dashboard / session_detail / export_csv).
/// Prefers platform/registry DB when it holds more sessions than the tenant DB.
/// </summary>
public static class CpWebTrackerDashboardBuilder
{
    public static CpWebTrackerFilterQuery NormalizeFilters(
        string? siteKey,
        string? fromDate,
        string? toDate,
        string? device,
        string? country,
        string? ip,
        string? userId,
        string? userType,
        string? browser,
        string? path,
        bool isSuper,
        string ownSiteKey)
    {
        var sk = Regex.Replace((siteKey ?? string.Empty).Trim().ToLowerInvariant(), @"[^a-z0-9_\-]", string.Empty);
        if (!isSuper)
        {
            sk = ownSiteKey;
        }
        else if (string.IsNullOrEmpty(sk))
        {
            sk = "_all";
        }

        var deviceNorm = Regex.Replace((device ?? string.Empty).Trim().ToLowerInvariant(), @"[^a-z0-9_\-]", string.Empty);
        if (deviceNorm is not ("desktop" or "mobile" or "tablet"))
        {
            deviceNorm = string.Empty;
        }

        var countryNorm = Regex.Replace((country ?? string.Empty).Trim().ToUpperInvariant(), @"[^A-Z0-9]", string.Empty);
        if (countryNorm.Length > 8)
        {
            countryNorm = countryNorm[..8];
        }

        var ipNorm = Regex.Replace((ip ?? string.Empty).Trim(), @"[^0-9a-fA-F:\.]", string.Empty);
        if (ipNorm.Length > 45)
        {
            ipNorm = ipNorm[..45];
        }

        var userIdNorm = Regex.Replace((userId ?? string.Empty).Trim(), @"[^0-9]", string.Empty);
        if (userIdNorm.Length > 12)
        {
            userIdNorm = userIdNorm[..12];
        }

        var who = (userType ?? string.Empty).Trim().ToLowerInvariant();
        if (who is "reg")
        {
            who = "registered";
        }

        if (who is not ("guest" or "registered"))
        {
            who = string.Empty;
        }

        var browserNorm = Regex.Replace((browser ?? string.Empty).Trim(), @"[^a-zA-Z0-9 _\-\.]", string.Empty);
        if (browserNorm.Length > 40)
        {
            browserNorm = browserNorm[..40];
        }

        var pathNorm = (path ?? string.Empty).Trim().Replace("\0", string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
        if (pathNorm.Length > 200)
        {
            pathNorm = pathNorm[..200];
        }

        var from = string.IsNullOrWhiteSpace(fromDate)
            ? DateTime.UtcNow.Date.AddDays(-7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : Regex.Replace(fromDate, @"[^0-9\-]", string.Empty);
        var to = string.IsNullOrWhiteSpace(toDate)
            ? DateTime.UtcNow.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : Regex.Replace(toDate, @"[^0-9\-]", string.Empty);

        return new CpWebTrackerFilterQuery(sk, from, to, deviceNorm, countryNorm, ipNorm, userIdNorm, who, browserNorm, pathNorm, isSuper);
    }

    public static string ResolveOwnSiteKey(string? host)
    {
        var h = (host ?? string.Empty).Trim().ToLowerInvariant();
        var colon = h.IndexOf(':');
        if (colon > 0)
        {
            h = h[..colon];
        }

        if (h is "www.ecomae.com" or "ecomae.com" or "cp.ecomae.com")
        {
            return "ecomae";
        }

        if (h.Contains("epartscart", StringComparison.Ordinal))
        {
            return "epartscart";
        }

        h = Regex.Replace(h, @"^www\.", string.Empty);
        h = Regex.Replace(h, @"[^a-z0-9]+", "_");
        h = h.Trim('_');
        return string.IsNullOrEmpty(h) ? "unknown" : h;
    }

    public static (long FromUnix, long ToUnix) RangeUnix(string fromDate, string toDate)
    {
        if (!DateTime.TryParse(fromDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var fromDt))
        {
            fromDt = DateTime.UtcNow.Date.AddDays(-7);
        }
        else
        {
            fromDt = DateTime.SpecifyKind(fromDt.Date, DateTimeKind.Utc);
        }

        if (!DateTime.TryParse(toDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var toDt))
        {
            toDt = DateTime.UtcNow.Date;
        }
        else
        {
            toDt = DateTime.SpecifyKind(toDt.Date, DateTimeKind.Utc);
        }

        var fromUnix = new DateTimeOffset(fromDt).ToUnixTimeSeconds();
        var toUnix = new DateTimeOffset(toDt.AddDays(1).AddSeconds(-1)).ToUnixTimeSeconds();
        if (fromUnix > toUnix)
        {
            (fromUnix, toUnix) = (toUnix, fromUnix);
        }

        if (toUnix - fromUnix > 366L * 86400L)
        {
            fromUnix = toUnix - 366L * 86400L;
        }

        return (fromUnix, toUnix);
    }

    public static async Task<CpWebTrackerDashboardResult> BuildDashboardAsync(
        ITenantDbConnectionFactory connections,
        CpWebTrackerFilterQuery filters,
        CancellationToken cancellationToken = default)
    {
        var emptySummary = new CpWebTrackerDashSummary(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var emptyFacets = new CpWebTrackerFacets([], [], []);
        var (fromUnix, toUnix) = RangeUnix(filters.FromDate, filters.ToDate);
        if (!connections.IsConfigured)
        {
            return new(
                false, filters.SiteKey, fromUnix, toUnix, filters.IsSuper, "none", emptySummary,
                [], [], [], [], [], [], [], [], [], emptyFacets, filters, [],
                "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await OpenTrackerConnectionAsync(connections, cancellationToken).ConfigureAwait(false);
            const string dbLabel = "tracker";

            var allSites = filters.IsSuper && (filters.SiteKey is "" or "_all");
            var siteOptions = filters.IsSuper
                ? await LoadSiteOptionsAsync(connection, cancellationToken).ConfigureAwait(false)
                : new List<string> { filters.SiteKey };

            var filterSql = BuildSessionFilterSql(filters, alias: string.Empty, out var filterParams);
            var filterSqlS = BuildSessionFilterSql(filters, alias: "s", out var filterParamsS);
            var filtersActive = filterSqlS.Length > 0;
            var siteSql = string.Empty;
            var siteParams = new List<(string Name, object Value)>();
            if (!allSites && !string.IsNullOrEmpty(filters.SiteKey) && filters.SiteKey != "_all")
            {
                siteSql = " AND `site_key` = @site ";
                siteParams.Add(("@site", filters.SiteKey));
            }

            var scopeParams = new List<(string Name, object Value)>
            {
                ("@from", fromUnix),
                ("@to", toUnix),
            };
            scopeParams.AddRange(siteParams);
            scopeParams.AddRange(filterParams);

            var summary = await ReadSummaryAsync(connection, siteSql + filterSql, scopeParams, cancellationToken).ConfigureAwait(false);
            var (clicks, searches) = await ReadClickSearchAsync(
                connection, allSites, filters.SiteKey, filterSqlS, filterParamsS, filtersActive, fromUnix, toUnix, cancellationToken).ConfigureAwait(false);
            summary = summary with { Clicks = clicks, Searches = searches, Events = Math.Max(summary.Events, clicks + searches) };

            var daily = await ReadDailyAsync(connection, siteSql + filterSql, scopeParams, cancellationToken).ConfigureAwait(false);
            var topPages = await ReadTopPagesAsync(connection, allSites, filters, filterSqlS, filterParamsS, filtersActive, fromUnix, toUnix, cancellationToken).ConfigureAwait(false);
            var geo = await ReadGeoAsync(connection, siteSql + filterSql, scopeParams, cancellationToken).ConfigureAwait(false);
            var devices = await ReadDevicesAsync(connection, siteSql + filterSql, scopeParams, cancellationToken).ConfigureAwait(false);
            var searchRows = await ReadSearchesAsync(connection, allSites, filters.SiteKey, filterSqlS, filterParamsS, filtersActive, fromUnix, toUnix, cancellationToken).ConfigureAwait(false);
            var topClicks = await ReadTopClicksAsync(connection, allSites, filters.SiteKey, filterSqlS, filterParamsS, filtersActive, fromUnix, toUnix, cancellationToken).ConfigureAwait(false);
            var referrers = await ReadReferrersAsync(connection, siteSql + filterSql, scopeParams, cancellationToken).ConfigureAwait(false);
            var recent = await ReadRecentSessionsAsync(connection, siteSql + filterSql, scopeParams, cancellationToken).ConfigureAwait(false);
            var byTenant = allSites
                ? await ReadByTenantAsync(connection, filterSql, fromUnix, toUnix, filterParams, cancellationToken).ConfigureAwait(false)
                : Array.Empty<CpWebTrackerTenantRow>();
            var facets = await ReadFacetsAsync(connection, allSites, filters.SiteKey, fromUnix, toUnix, cancellationToken).ConfigureAwait(false);

            return new(
                true, allSites ? "_all" : filters.SiteKey, fromUnix, toUnix, filters.IsSuper, dbLabel, summary,
                daily, topPages, geo, devices, searchRows, topClicks, referrers, recent, byTenant, facets, filters, siteOptions,
                "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(
                false, filters.SiteKey, fromUnix, toUnix, filters.IsSuper, "error", emptySummary,
                [], [], [], [], [], [], [], [], [], emptyFacets, filters, [],
                "database-error", ex.Message);
        }
    }

    public static async Task<CpWebTrackerSessionDetailResult> BuildSessionDetailAsync(
        ITenantDbConnectionFactory connections,
        long sessionId,
        string siteKey,
        bool isSuper,
        CancellationToken cancellationToken = default)
    {
        if (sessionId <= 0)
        {
            return new(false, null, [], [], "invalid", "Missing session id.");
        }

        if (!connections.IsConfigured)
        {
            return new(false, null, [], [], "migration", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await OpenTrackerConnectionAsync(connections, cancellationToken).ConfigureAwait(false);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT `id`, IFNULL(`session_uid`,'') AS session_uid, IFNULL(`site_key`,'') AS site_key,
                       IFNULL(`hostname`,'') AS hostname, IFNULL(`user_id`,0) AS user_id,
                       IFNULL(`is_registered`,0) AS is_registered,
                       IFNULL(`first_seen_at`,0) AS first_seen_at, IFNULL(`last_seen_at`,0) AS last_seen_at,
                       IFNULL(`pageview_count`,0) AS pageview_count, IFNULL(`event_count`,0) AS event_count,
                       IFNULL(`duration_ms`,0) AS duration_ms,
                       IFNULL(`landing_path`,'') AS landing_path, IFNULL(`exit_path`,'') AS exit_path,
                       IFNULL(`country_code`,'') AS country_code, IFNULL(`country_name`,'') AS country_name,
                       IFNULL(`city`,'') AS city, IFNULL(`region`,'') AS region,
                       IFNULL(`device_type`,'') AS device_type, IFNULL(`browser`,'') AS browser,
                       IFNULL(`os`,'') AS os, IFNULL(`ip`,'') AS ip,
                       IFNULL(`referrer_host`,'') AS referrer_host, IFNULL(`utm_source`,'') AS utm_source
                FROM `epc_web_tracker_sessions` WHERE `id` = @id LIMIT 1
                """;
            Add(cmd, "@id", sessionId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new(false, null, [], [], "database", "Session not found.");
            }

            var session = ReadRecent(reader);
            var allSites = isSuper && (siteKey is "" or "_all");
            if (!allSites && !string.IsNullOrEmpty(siteKey) && !string.Equals(session.SiteKey, siteKey, StringComparison.OrdinalIgnoreCase))
            {
                return new(false, null, [], [], "database", "Session not in scope.");
            }

            await reader.DisposeAsync().ConfigureAwait(false);

            var pageviews = new List<CpWebTrackerPageviewDetail>();
            await using (var pv = connection.CreateCommand())
            {
                pv.CommandText = """
                    SELECT `id`, IFNULL(`ts`,0) AS ts, IFNULL(`path`,'') AS path, IFNULL(`query`,'') AS query,
                           IFNULL(`title`,'') AS title, IFNULL(`time_on_page_ms`,0) AS time_on_page_ms,
                           IFNULL(`scroll_max_pct`,0) AS scroll_max_pct, IFNULL(`load_time_ms`,0) AS load_time_ms
                    FROM `epc_web_tracker_pageviews`
                    WHERE `session_id` = @id
                    ORDER BY `ts` ASC, `id` ASC
                    LIMIT 500
                    """;
                Add(pv, "@id", sessionId);
                await using var pr = await pv.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await pr.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    pageviews.Add(new(
                        Convert.ToInt64(pr["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(pr["ts"], CultureInfo.InvariantCulture),
                        Convert.ToString(pr["path"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(pr["query"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(pr["title"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt64(pr["time_on_page_ms"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(pr["scroll_max_pct"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(pr["load_time_ms"], CultureInfo.InvariantCulture)));
                }
            }

            var events = new List<CpWebTrackerEventDetail>();
            await using (var ev = connection.CreateCommand())
            {
                ev.CommandText = """
                    SELECT `id`, IFNULL(`ts`,0) AS ts, IFNULL(`event_type`,'') AS event_type, IFNULL(`path`,'') AS path,
                           IFNULL(`search_query`,'') AS search_query, IFNULL(`search_context`,'') AS search_context,
                           IFNULL(`element_tag`,'') AS element_tag, IFNULL(`element_id`,'') AS element_id,
                           IFNULL(`element_text`,'') AS element_text, IFNULL(`element_href`,'') AS element_href,
                           IFNULL(`x`,0) AS x, IFNULL(`y`,0) AS y
                    FROM `epc_web_tracker_events`
                    WHERE `session_id` = @id
                    ORDER BY `ts` ASC, `id` ASC
                    LIMIT 2000
                    """;
                Add(ev, "@id", sessionId);
                await using var er = await ev.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await er.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    events.Add(new(
                        Convert.ToInt64(er["id"], CultureInfo.InvariantCulture),
                        Convert.ToInt64(er["ts"], CultureInfo.InvariantCulture),
                        Convert.ToString(er["event_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(er["path"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(er["search_query"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(er["search_context"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(er["element_tag"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(er["element_id"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(er["element_text"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToString(er["element_href"], CultureInfo.InvariantCulture) ?? string.Empty,
                        Convert.ToInt32(er["x"], CultureInfo.InvariantCulture),
                        Convert.ToInt32(er["y"], CultureInfo.InvariantCulture)));
                }
            }

            return new(true, session, pageviews, events, "database", string.Empty);
        }
        catch (Exception ex)
        {
            return new(false, null, [], [], "database-error", ex.Message);
        }
    }

    public static string BuildCsv(CpWebTrackerDashboardResult dash)
    {
        var sb = new StringBuilder();
        sb.Append('\uFEFF');
        Line(sb, "Website tracker full report");
        Line(sb, "Site", dash.SiteKey, "From", DateTimeOffset.FromUnixTimeSeconds(dash.FromUnix).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC",
            "To", DateTimeOffset.FromUnixTimeSeconds(dash.ToUnix).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC");
        Line(sb, "Filter device", dash.Filters.Device, "Filter country", dash.Filters.Country, "Filter IP", dash.Filters.Ip,
            "Filter user_id", dash.Filters.UserId, "Filter who", dash.Filters.UserType, "Filter path", dash.Filters.Path, "Filter browser", dash.Filters.Browser);
        sb.Append("\r\n");
        var s = dash.Summary;
        Line(sb, "SECTION", "Summary");
        Line(sb, "Sessions", "Visitors", "Pageviews", "Clicks", "Searches", "Guest sessions", "Registered sessions", "Avg duration ms", "Avg pages", "Bounce %");
        Line(sb, s.Sessions, s.Visitors, s.Pageviews, s.Clicks, s.Searches, s.GuestSessions, s.RegisteredSessions, s.AvgDurationMs, s.AvgPages, s.BounceRate);
        sb.Append("\r\n");
        Line(sb, "SECTION", "Daily");
        Line(sb, "Date", "Sessions", "Pageviews");
        foreach (var d in dash.Daily)
        {
            Line(sb, d.Date, d.Sessions, d.Pageviews);
        }

        sb.Append("\r\n");
        Line(sb, "SECTION", "Top pages");
        Line(sb, "Path", "Views", "Sessions", "Avg time ms", "Avg scroll");
        foreach (var p in dash.TopPages)
        {
            Line(sb, p.Path, p.Views, p.Sessions, p.AvgTimeMs, p.AvgScroll);
        }

        sb.Append("\r\n");
        Line(sb, "SECTION", "Recent sessions");
        Line(sb, "Id", "Site", "When", "Who", "IP", "Country", "Device", "Browser", "Pages", "Events", "Duration ms", "Landing", "Exit");
        foreach (var r in dash.RecentSessions)
        {
            Line(sb, r.Id, r.SiteKey, r.LastSeenAt, r.IsRegistered ? ("user#" + r.UserId) : "guest", r.Ip, r.CountryCode,
                r.DeviceType, r.Browser, r.PageviewCount, r.EventCount, r.DurationMs, r.LandingPath, r.ExitPath);
        }

        return sb.ToString();
    }

    private static async Task<DbConnection> OpenTrackerConnectionAsync(
        ITenantDbConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        DbConnection? registry = null;
        DbConnection? tenant = null;
        try
        {
            registry = await connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // ignore — fall back to tenant
        }

        try
        {
            tenant = await connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }

        var regCount = registry is null ? -1 : await CountSessionsSafeAsync(registry, cancellationToken).ConfigureAwait(false);
        var tenCount = tenant is null ? -1 : await CountSessionsSafeAsync(tenant, cancellationToken).ConfigureAwait(false);

        if (registry is not null && regCount >= tenCount)
        {
            if (tenant is not null)
            {
                await tenant.DisposeAsync().ConfigureAwait(false);
            }

            return registry;
        }

        if (tenant is not null)
        {
            if (registry is not null)
            {
                await registry.DisposeAsync().ConfigureAwait(false);
            }

            return tenant;
        }

        if (registry is not null)
        {
            return registry;
        }

        throw new InvalidOperationException("No tracker database connection available.");
    }

    private static async Task<long> CountSessionsSafeAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM `epc_web_tracker_sessions`";
            var o = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt64(o is DBNull or null ? 0 : o, CultureInfo.InvariantCulture);
        }
        catch
        {
            return -1;
        }
    }

    private static string BuildSessionFilterSql(
        CpWebTrackerFilterQuery filters,
        string alias,
        out List<(string Name, object Value)> parameters)
    {
        var paramList = new List<(string Name, object Value)>();
        parameters = paramList;
        string Col(string name) => string.IsNullOrEmpty(alias) ? $"`{name}`" : $"{alias}.`{name}`";
        var sql = new StringBuilder();
        var i = 0;
        string AddParam(object v)
        {
            var n = $"@f{alias}_{i}";
            i++;
            paramList.Add((n, v));
            return n;
        }

        if (!string.IsNullOrEmpty(filters.Device))
        {
            sql.Append(" AND ").Append(Col("device_type")).Append(" = ").Append(AddParam(filters.Device)).Append(' ');
        }

        if (!string.IsNullOrEmpty(filters.Country))
        {
            sql.Append(" AND ").Append(Col("country_code")).Append(" = ").Append(AddParam(filters.Country)).Append(' ');
        }

        if (!string.IsNullOrEmpty(filters.Ip))
        {
            sql.Append(" AND ").Append(Col("ip")).Append(" LIKE ").Append(AddParam("%" + filters.Ip + "%")).Append(' ');
        }

        if (!string.IsNullOrEmpty(filters.UserId))
        {
            sql.Append(" AND ").Append(Col("user_id")).Append(" = ").Append(AddParam(int.Parse(filters.UserId, CultureInfo.InvariantCulture))).Append(' ');
        }

        if (filters.UserType == "guest")
        {
            sql.Append(" AND ").Append(Col("is_registered")).Append(" = 0 ");
        }
        else if (filters.UserType == "registered")
        {
            sql.Append(" AND ").Append(Col("is_registered")).Append(" = 1 ");
        }

        if (!string.IsNullOrEmpty(filters.Browser))
        {
            sql.Append(" AND ").Append(Col("browser")).Append(" LIKE ").Append(AddParam("%" + filters.Browser + "%")).Append(' ');
        }

        if (!string.IsNullOrEmpty(filters.Path))
        {
            var like = "%" + filters.Path + "%";
            var idRef = string.IsNullOrEmpty(alias) ? "`id`" : $"{alias}.`id`";
            sql.Append(" AND (").Append(Col("landing_path")).Append(" LIKE ").Append(AddParam(like))
                .Append(" OR ").Append(Col("exit_path")).Append(" LIKE ").Append(AddParam(like))
                .Append(" OR EXISTS (SELECT 1 FROM `epc_web_tracker_pageviews` _wt_pv WHERE _wt_pv.`session_id` = ")
                .Append(idRef).Append(" AND _wt_pv.`path` LIKE ").Append(AddParam(like)).Append(")) ");
        }

        return sql.ToString();
    }

    private static async Task<CpWebTrackerDashSummary> ReadSummaryAsync(
        DbConnection connection,
        string scopeSql,
        List<(string Name, object Value)> scopeParams,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT COUNT(*) AS sessions,
                COUNT(DISTINCT NULLIF(`visitor_uid`,'')) AS visitors,
                COALESCE(SUM(`pageview_count`),0) AS pageviews,
                COALESCE(SUM(`event_count`),0) AS events,
                SUM(CASE WHEN `is_registered` = 1 THEN 1 ELSE 0 END) AS registered_sessions,
                SUM(CASE WHEN `is_registered` = 0 THEN 1 ELSE 0 END) AS guest_sessions,
                COALESCE(AVG(`duration_ms`),0) AS avg_duration_ms,
                COALESCE(AVG(`pageview_count`),0) AS avg_pages,
                SUM(CASE WHEN `pageview_count` <= 1 THEN 1 ELSE 0 END) AS bounces
            FROM `epc_web_tracker_sessions`
            WHERE `last_seen_at` BETWEEN @from AND @to{scopeSql}
            """;
        Bind(cmd, scopeParams);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        var sessions = Convert.ToInt64(reader["sessions"] is DBNull ? 0 : reader["sessions"], CultureInfo.InvariantCulture);
        var bounces = Convert.ToInt64(reader["bounces"] is DBNull ? 0 : reader["bounces"], CultureInfo.InvariantCulture);
        return new(
            sessions,
            Convert.ToInt64(reader["visitors"] is DBNull ? 0 : reader["visitors"], CultureInfo.InvariantCulture),
            Convert.ToInt64(reader["pageviews"] is DBNull ? 0 : reader["pageviews"], CultureInfo.InvariantCulture),
            Convert.ToInt64(reader["events"] is DBNull ? 0 : reader["events"], CultureInfo.InvariantCulture),
            0,
            0,
            Convert.ToInt64(reader["guest_sessions"] is DBNull ? 0 : reader["guest_sessions"], CultureInfo.InvariantCulture),
            Convert.ToInt64(reader["registered_sessions"] is DBNull ? 0 : reader["registered_sessions"], CultureInfo.InvariantCulture),
            (long)Math.Round(Convert.ToDouble(reader["avg_duration_ms"] is DBNull ? 0 : reader["avg_duration_ms"], CultureInfo.InvariantCulture)),
            Math.Round(Convert.ToDouble(reader["avg_pages"] is DBNull ? 0 : reader["avg_pages"], CultureInfo.InvariantCulture), 2),
            sessions > 0 ? Math.Round(100.0 * bounces / sessions, 1) : 0);
    }

    private static async Task<(long Clicks, long Searches)> ReadClickSearchAsync(
        DbConnection connection,
        bool allSites,
        string siteKey,
        string filterSqlS,
        List<(string Name, object Value)> filterParamsS,
        bool filtersActive,
        long fromUnix,
        long toUnix,
        CancellationToken cancellationToken)
    {
        var join = string.Empty;
        var where = string.Empty;
        var p = new List<(string Name, object Value)> { ("@from", fromUnix), ("@to", toUnix) };
        if (!allSites && !string.IsNullOrEmpty(siteKey) && siteKey != "_all")
        {
            if (filtersActive)
            {
                join = " INNER JOIN `epc_web_tracker_sessions` s ON s.`id` = e.`session_id` ";
                where = " AND s.`site_key` = @site " + filterSqlS;
                p.Add(("@site", siteKey));
                p.AddRange(filterParamsS);
            }
            else
            {
                where = " AND e.`site_key` = @site ";
                p.Add(("@site", siteKey));
            }
        }
        else if (filtersActive)
        {
            join = " INNER JOIN `epc_web_tracker_sessions` s ON s.`id` = e.`session_id` ";
            where = filterSqlS;
            p.AddRange(filterParamsS);
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT
                SUM(CASE WHEN e.`event_type` = 'click' THEN 1 ELSE 0 END) AS clicks,
                SUM(CASE WHEN e.`event_type` = 'search' THEN 1 ELSE 0 END) AS searches
            FROM `epc_web_tracker_events` e{join}
            WHERE e.`ts` BETWEEN @from AND @to{where}
            """;
        Bind(cmd, p);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return (0, 0);
        }

        return (
            Convert.ToInt64(reader["clicks"] is DBNull ? 0 : reader["clicks"], CultureInfo.InvariantCulture),
            Convert.ToInt64(reader["searches"] is DBNull ? 0 : reader["searches"], CultureInfo.InvariantCulture));
    }

    private static async Task<IReadOnlyList<CpWebTrackerDailyRow>> ReadDailyAsync(
        DbConnection connection, string scopeSql, List<(string Name, object Value)> scopeParams, CancellationToken cancellationToken)
    {
        var rows = new List<CpWebTrackerDailyRow>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT FROM_UNIXTIME(`last_seen_at`, '%Y-%m-%d') AS d,
                COUNT(*) AS sessions,
                COALESCE(SUM(`pageview_count`),0) AS pageviews
            FROM `epc_web_tracker_sessions`
            WHERE `last_seen_at` BETWEEN @from AND @to{scopeSql}
            GROUP BY d ORDER BY d ASC
            """;
        Bind(cmd, scopeParams);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new(
                Convert.ToString(reader["d"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt64(reader["sessions"] is DBNull ? 0 : reader["sessions"], CultureInfo.InvariantCulture),
                Convert.ToInt64(reader["pageviews"] is DBNull ? 0 : reader["pageviews"], CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CpWebTrackerPageRow>> ReadTopPagesAsync(
        DbConnection connection,
        bool allSites,
        CpWebTrackerFilterQuery filters,
        string filterSqlS,
        List<(string Name, object Value)> filterParamsS,
        bool filtersActive,
        long fromUnix,
        long toUnix,
        CancellationToken cancellationToken)
    {
        var join = string.Empty;
        var where = string.Empty;
        var p = new List<(string Name, object Value)> { ("@from", fromUnix), ("@to", toUnix) };
        if (!allSites && !string.IsNullOrEmpty(filters.SiteKey) && filters.SiteKey != "_all")
        {
            if (filtersActive)
            {
                join = " INNER JOIN `epc_web_tracker_sessions` s ON s.`id` = p.`session_id` ";
                where = " AND s.`site_key` = @site " + filterSqlS;
                p.Add(("@site", filters.SiteKey));
                p.AddRange(filterParamsS);
            }
            else
            {
                where = " AND p.`site_key` = @site ";
                p.Add(("@site", filters.SiteKey));
            }
        }
        else if (filtersActive)
        {
            join = " INNER JOIN `epc_web_tracker_sessions` s ON s.`id` = p.`session_id` ";
            where = filterSqlS;
            p.AddRange(filterParamsS);
        }

        if (!string.IsNullOrEmpty(filters.Path))
        {
            where += " AND p.`path` LIKE @path_pv ";
            p.Add(("@path_pv", "%" + filters.Path + "%"));
        }

        var rows = new List<CpWebTrackerPageRow>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT p.`path`, COUNT(*) AS views, COUNT(DISTINCT p.`session_uid`) AS sessions,
                ROUND(AVG(p.`time_on_page_ms`)) AS avg_time_ms,
                ROUND(AVG(p.`scroll_max_pct`)) AS avg_scroll
            FROM `epc_web_tracker_pageviews` p{join}
            WHERE p.`ts` BETWEEN @from AND @to{where}
            GROUP BY p.`path` ORDER BY views DESC LIMIT 40
            """;
        Bind(cmd, p);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new(
                Convert.ToString(reader["path"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt64(reader["views"] is DBNull ? 0 : reader["views"], CultureInfo.InvariantCulture),
                Convert.ToInt64(reader["sessions"] is DBNull ? 0 : reader["sessions"], CultureInfo.InvariantCulture),
                Convert.ToInt64(reader["avg_time_ms"] is DBNull ? 0 : reader["avg_time_ms"], CultureInfo.InvariantCulture),
                Convert.ToInt64(reader["avg_scroll"] is DBNull ? 0 : reader["avg_scroll"], CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CpWebTrackerGeoRow>> ReadGeoAsync(
        DbConnection connection, string scopeSql, List<(string Name, object Value)> scopeParams, CancellationToken cancellationToken)
    {
        var rows = new List<CpWebTrackerGeoRow>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT IFNULL(`country_code`,'') AS country_code, IFNULL(`country_name`,'') AS country_name,
                   IFNULL(`city`,'') AS city, COUNT(*) AS sessions
            FROM `epc_web_tracker_sessions`
            WHERE `last_seen_at` BETWEEN @from AND @to{scopeSql}
            GROUP BY `country_code`, `country_name`, `city`
            ORDER BY sessions DESC LIMIT 40
            """;
        Bind(cmd, scopeParams);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new(
                Convert.ToString(reader["country_code"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["country_name"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["city"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt64(reader["sessions"] is DBNull ? 0 : reader["sessions"], CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CpWebTrackerDeviceRow>> ReadDevicesAsync(
        DbConnection connection, string scopeSql, List<(string Name, object Value)> scopeParams, CancellationToken cancellationToken)
    {
        var rows = new List<CpWebTrackerDeviceRow>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT IFNULL(`device_type`,'') AS device_type, IFNULL(`browser`,'') AS browser,
                   IFNULL(`os`,'') AS os, COUNT(*) AS sessions
            FROM `epc_web_tracker_sessions`
            WHERE `last_seen_at` BETWEEN @from AND @to{scopeSql}
            GROUP BY `device_type`, `browser`, `os`
            ORDER BY sessions DESC LIMIT 30
            """;
        Bind(cmd, scopeParams);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new(
                Convert.ToString(reader["device_type"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["browser"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["os"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt64(reader["sessions"] is DBNull ? 0 : reader["sessions"], CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CpWebTrackerSearchRow>> ReadSearchesAsync(
        DbConnection connection, bool allSites, string siteKey, string filterSqlS,
        List<(string Name, object Value)> filterParamsS, bool filtersActive, long fromUnix, long toUnix, CancellationToken cancellationToken)
    {
        var join = string.Empty;
        var where = string.Empty;
        var p = new List<(string Name, object Value)> { ("@from", fromUnix), ("@to", toUnix) };
        if (!allSites && !string.IsNullOrEmpty(siteKey) && siteKey != "_all")
        {
            if (filtersActive)
            {
                join = " INNER JOIN `epc_web_tracker_sessions` s ON s.`id` = e.`session_id` ";
                where = " AND s.`site_key` = @site " + filterSqlS;
                p.Add(("@site", siteKey));
                p.AddRange(filterParamsS);
            }
            else
            {
                where = " AND e.`site_key` = @site ";
                p.Add(("@site", siteKey));
            }
        }
        else if (filtersActive)
        {
            join = " INNER JOIN `epc_web_tracker_sessions` s ON s.`id` = e.`session_id` ";
            where = filterSqlS;
            p.AddRange(filterParamsS);
        }

        var rows = new List<CpWebTrackerSearchRow>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT e.`search_query`, e.`search_context`, COUNT(*) AS hits, COUNT(DISTINCT e.`session_uid`) AS sessions
            FROM `epc_web_tracker_events` e{join}
            WHERE e.`ts` BETWEEN @from AND @to AND e.`event_type` = 'search' AND e.`search_query` <> ''{where}
            GROUP BY e.`search_query`, e.`search_context`
            ORDER BY hits DESC LIMIT 50
            """;
        Bind(cmd, p);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new(
                Convert.ToString(reader["search_query"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["search_context"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt64(reader["hits"] is DBNull ? 0 : reader["hits"], CultureInfo.InvariantCulture),
                Convert.ToInt64(reader["sessions"] is DBNull ? 0 : reader["sessions"], CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CpWebTrackerClickRow>> ReadTopClicksAsync(
        DbConnection connection, bool allSites, string siteKey, string filterSqlS,
        List<(string Name, object Value)> filterParamsS, bool filtersActive, long fromUnix, long toUnix, CancellationToken cancellationToken)
    {
        var join = string.Empty;
        var where = string.Empty;
        var p = new List<(string Name, object Value)> { ("@from", fromUnix), ("@to", toUnix) };
        if (!allSites && !string.IsNullOrEmpty(siteKey) && siteKey != "_all")
        {
            if (filtersActive)
            {
                join = " INNER JOIN `epc_web_tracker_sessions` s ON s.`id` = e.`session_id` ";
                where = " AND s.`site_key` = @site " + filterSqlS;
                p.Add(("@site", siteKey));
                p.AddRange(filterParamsS);
            }
            else
            {
                where = " AND e.`site_key` = @site ";
                p.Add(("@site", siteKey));
            }
        }
        else if (filtersActive)
        {
            join = " INNER JOIN `epc_web_tracker_sessions` s ON s.`id` = e.`session_id` ";
            where = filterSqlS;
            p.AddRange(filterParamsS);
        }

        var rows = new List<CpWebTrackerClickRow>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT e.`path`, e.`element_tag`, e.`element_id`, e.`element_text`, e.`element_href`, COUNT(*) AS hits
            FROM `epc_web_tracker_events` e{join}
            WHERE e.`ts` BETWEEN @from AND @to AND e.`event_type` = 'click'{where}
            GROUP BY e.`path`, e.`element_tag`, e.`element_id`, e.`element_text`, e.`element_href`
            ORDER BY hits DESC LIMIT 50
            """;
        Bind(cmd, p);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new(
                Convert.ToString(reader["path"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["element_tag"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["element_id"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["element_text"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["element_href"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt64(reader["hits"] is DBNull ? 0 : reader["hits"], CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CpWebTrackerReferrerRow>> ReadReferrersAsync(
        DbConnection connection, string scopeSql, List<(string Name, object Value)> scopeParams, CancellationToken cancellationToken)
    {
        var rows = new List<CpWebTrackerReferrerRow>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT IF(`referrer_host`='', '(direct)', `referrer_host`) AS host,
                IFNULL(`utm_source`,'') AS utm_source, IFNULL(`utm_medium`,'') AS utm_medium,
                IFNULL(`utm_campaign`,'') AS utm_campaign, COUNT(*) AS sessions
            FROM `epc_web_tracker_sessions`
            WHERE `last_seen_at` BETWEEN @from AND @to{scopeSql}
            GROUP BY host, `utm_source`, `utm_medium`, `utm_campaign`
            ORDER BY sessions DESC LIMIT 40
            """;
        Bind(cmd, scopeParams);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new(
                Convert.ToString(reader["host"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["utm_source"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["utm_medium"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["utm_campaign"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt64(reader["sessions"] is DBNull ? 0 : reader["sessions"], CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CpWebTrackerRecentSessionRow>> ReadRecentSessionsAsync(
        DbConnection connection, string scopeSql, List<(string Name, object Value)> scopeParams, CancellationToken cancellationToken)
    {
        var rows = new List<CpWebTrackerRecentSessionRow>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT `id`, IFNULL(`session_uid`,'') AS session_uid, IFNULL(`site_key`,'') AS site_key,
                   IFNULL(`hostname`,'') AS hostname, IFNULL(`user_id`,0) AS user_id,
                   IFNULL(`is_registered`,0) AS is_registered,
                   IFNULL(`first_seen_at`,0) AS first_seen_at, IFNULL(`last_seen_at`,0) AS last_seen_at,
                   IFNULL(`pageview_count`,0) AS pageview_count, IFNULL(`event_count`,0) AS event_count,
                   IFNULL(`duration_ms`,0) AS duration_ms,
                   IFNULL(`landing_path`,'') AS landing_path, IFNULL(`exit_path`,'') AS exit_path,
                   IFNULL(`country_code`,'') AS country_code, IFNULL(`country_name`,'') AS country_name,
                   IFNULL(`city`,'') AS city, IFNULL(`region`,'') AS region,
                   IFNULL(`device_type`,'') AS device_type, IFNULL(`browser`,'') AS browser,
                   IFNULL(`os`,'') AS os, IFNULL(`ip`,'') AS ip,
                   IFNULL(`referrer_host`,'') AS referrer_host, IFNULL(`utm_source`,'') AS utm_source
            FROM `epc_web_tracker_sessions`
            WHERE `last_seen_at` BETWEEN @from AND @to{scopeSql}
            ORDER BY `last_seen_at` DESC LIMIT 100
            """;
        Bind(cmd, scopeParams);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(ReadRecent(reader));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CpWebTrackerTenantRow>> ReadByTenantAsync(
        DbConnection connection, string filterSql, long fromUnix, long toUnix,
        List<(string Name, object Value)> filterParams, CancellationToken cancellationToken)
    {
        var rows = new List<CpWebTrackerTenantRow>();
        var p = new List<(string Name, object Value)> { ("@from", fromUnix), ("@to", toUnix) };
        p.AddRange(filterParams);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT IFNULL(`site_key`,'') AS site_key, IFNULL(`hostname`,'') AS hostname, COUNT(*) AS sessions,
                COALESCE(SUM(`pageview_count`),0) AS pageviews,
                COUNT(DISTINCT NULLIF(`visitor_uid`,'')) AS visitors
            FROM `epc_web_tracker_sessions`
            WHERE `last_seen_at` BETWEEN @from AND @to{filterSql}
            GROUP BY `site_key`, `hostname`
            ORDER BY sessions DESC LIMIT 100
            """;
        Bind(cmd, p);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new(
                Convert.ToString(reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["hostname"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt64(reader["sessions"] is DBNull ? 0 : reader["sessions"], CultureInfo.InvariantCulture),
                Convert.ToInt64(reader["pageviews"] is DBNull ? 0 : reader["pageviews"], CultureInfo.InvariantCulture),
                Convert.ToInt64(reader["visitors"] is DBNull ? 0 : reader["visitors"], CultureInfo.InvariantCulture)));
        }

        return rows;
    }

    private static async Task<CpWebTrackerFacets> ReadFacetsAsync(
        DbConnection connection, bool allSites, string siteKey, long fromUnix, long toUnix, CancellationToken cancellationToken)
    {
        var facetSite = string.Empty;
        var baseParams = new List<(string Name, object Value)> { ("@from", fromUnix), ("@to", toUnix) };
        if (!allSites && !string.IsNullOrEmpty(siteKey) && siteKey != "_all")
        {
            facetSite = " AND `site_key` = @site ";
            baseParams.Add(("@site", siteKey));
        }

        var countries = new List<CpWebTrackerFacetRow>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"""
                SELECT `country_code`, MAX(`country_name`) AS country_name, COUNT(*) AS sessions
                FROM `epc_web_tracker_sessions`
                WHERE `last_seen_at` BETWEEN @from AND @to AND `country_code` <> ''{facetSite}
                GROUP BY `country_code` ORDER BY sessions DESC LIMIT 80
                """;
            Bind(cmd, baseParams);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var code = Convert.ToString(reader["country_code"], CultureInfo.InvariantCulture) ?? string.Empty;
                var name = Convert.ToString(reader["country_name"], CultureInfo.InvariantCulture) ?? code;
                countries.Add(new(code, name, Convert.ToInt64(reader["sessions"] is DBNull ? 0 : reader["sessions"], CultureInfo.InvariantCulture)));
            }
        }

        var devices = new List<CpWebTrackerFacetRow>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"""
                SELECT `device_type`, COUNT(*) AS sessions
                FROM `epc_web_tracker_sessions`
                WHERE `last_seen_at` BETWEEN @from AND @to AND `device_type` <> ''{facetSite}
                GROUP BY `device_type` ORDER BY sessions DESC LIMIT 20
                """;
            Bind(cmd, baseParams);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var v = Convert.ToString(reader["device_type"], CultureInfo.InvariantCulture) ?? string.Empty;
                devices.Add(new(v, v, Convert.ToInt64(reader["sessions"] is DBNull ? 0 : reader["sessions"], CultureInfo.InvariantCulture)));
            }
        }

        var browsers = new List<CpWebTrackerFacetRow>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"""
                SELECT `browser`, COUNT(*) AS sessions
                FROM `epc_web_tracker_sessions`
                WHERE `last_seen_at` BETWEEN @from AND @to AND `browser` <> ''{facetSite}
                GROUP BY `browser` ORDER BY sessions DESC LIMIT 30
                """;
            Bind(cmd, baseParams);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var v = Convert.ToString(reader["browser"], CultureInfo.InvariantCulture) ?? string.Empty;
                browsers.Add(new(v, v, Convert.ToInt64(reader["sessions"] is DBNull ? 0 : reader["sessions"], CultureInfo.InvariantCulture)));
            }
        }

        return new(countries, devices, browsers);
    }

    private static async Task<IReadOnlyList<string>> LoadSiteOptionsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var list = new List<string> { "_all", "ecomae", "epartscart" };
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT DISTINCT IFNULL(`site_key`,'') AS site_key
                FROM `epc_web_tracker_sessions`
                WHERE IFNULL(`site_key`,'') <> ''
                ORDER BY site_key ASC
                LIMIT 200
                """;
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var sk = Convert.ToString(reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty;
                if (!string.IsNullOrEmpty(sk) && !list.Contains(sk, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(sk);
                }
            }
        }
        catch
        {
            // optional
        }

        return list;
    }

    private static CpWebTrackerRecentSessionRow ReadRecent(DbDataReader reader)
        => new(
            Convert.ToInt64(reader["id"] is DBNull ? 0 : reader["id"], CultureInfo.InvariantCulture),
            Convert.ToString(reader["session_uid"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["hostname"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToInt64(reader["user_id"] is DBNull ? 0 : reader["user_id"], CultureInfo.InvariantCulture),
            Convert.ToInt32(reader["is_registered"] is DBNull ? 0 : reader["is_registered"], CultureInfo.InvariantCulture) == 1,
            Convert.ToInt64(reader["first_seen_at"] is DBNull ? 0 : reader["first_seen_at"], CultureInfo.InvariantCulture),
            Convert.ToInt64(reader["last_seen_at"] is DBNull ? 0 : reader["last_seen_at"], CultureInfo.InvariantCulture),
            Convert.ToInt64(reader["pageview_count"] is DBNull ? 0 : reader["pageview_count"], CultureInfo.InvariantCulture),
            Convert.ToInt64(reader["event_count"] is DBNull ? 0 : reader["event_count"], CultureInfo.InvariantCulture),
            Convert.ToInt64(reader["duration_ms"] is DBNull ? 0 : reader["duration_ms"], CultureInfo.InvariantCulture),
            Convert.ToString(reader["landing_path"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["exit_path"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["country_code"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["country_name"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["city"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["region"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["device_type"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["browser"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["os"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["ip"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["referrer_host"], CultureInfo.InvariantCulture) ?? string.Empty,
            Convert.ToString(reader["utm_source"], CultureInfo.InvariantCulture) ?? string.Empty);

    private static void Bind(DbCommand cmd, List<(string Name, object Value)> parameters)
    {
        foreach (var (name, value) in parameters)
        {
            Add(cmd, name, value);
        }
    }

    private static void Add(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static void Line(StringBuilder sb, params object[] cells)
    {
        for (var i = 0; i < cells.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            var s = Convert.ToString(cells[i], CultureInfo.InvariantCulture) ?? string.Empty;
            if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
            {
                sb.Append('"').Append(s.Replace("\"", "\"\"", StringComparison.Ordinal)).Append('"');
            }
            else
            {
                sb.Append(s);
            }
        }

        sb.Append("\r\n");
    }
}
