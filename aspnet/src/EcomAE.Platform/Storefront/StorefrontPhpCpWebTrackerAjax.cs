using System.Data.Common;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Data;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string TrackerTablesMissing = "Website tracker tables are not installed.";

    public sealed record WebTrackerGate(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error")] string Error,
        [property: JsonPropertyName("message")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Message = null);

    public sealed record WebTrackerMessage(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("message")] string Message);

    public sealed record WebTrackerCsv(string Body, string FileName);

    public static async Task<object> WebTrackerAsync(
        DbConnection connection,
        ITenantDbConnectionFactory connections,
        string? adminSession,
        string? adminUser,
        IReadOnlyDictionary<string, string> fields,
        string? requestHost,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(
            connection,
            adminSession,
            adminUser,
            new CodedJson(403, new WebTrackerGate(false, "forbidden")),
            cancellationToken).ConfigureAwait(false);
        if (denied is FlagBody flag)
        {
            return new WebTrackerMessage(false, flag.Message);
        }

        if (denied is not null)
        {
            return denied;
        }

        var isSuper = PlatformHostPolicy.IsSuperCpHost(requestHost);
        var own = CpWebTrackerDashboardBuilder.ResolveOwnSiteKey(requestHost);
        var posted = Regex.Replace(OmsField(fields, "site_key").Trim().ToLowerInvariant(), @"[^a-z0-9_\-]", string.Empty);
        if (!isSuper && posted.Length > 0 && posted != "_all" && !string.Equals(posted, own, StringComparison.Ordinal))
        {
            return new CodedJson(403, new WebTrackerGate(false, "tenant_scope"));
        }

        var filters = CpWebTrackerDashboardBuilder.NormalizeFilters(
            OmsField(fields, "site_key"),
            OmsField(fields, "from"),
            OmsField(fields, "to"),
            OmsField(fields, "device"),
            OmsField(fields, "country"),
            OmsField(fields, "ip"),
            OmsField(fields, "user_id"),
            OmsField(fields, "user_type"),
            OmsField(fields, "browser"),
            OmsField(fields, "path"),
            isSuper,
            own);
        var action = OmsField(fields, "action").Trim().ToLowerInvariant();
        if (action.Length == 0)
        {
            action = "dashboard";
        }

        if (action == "session")
        {
            _ = long.TryParse(OmsField(fields, "id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id);
            var siteKey = isSuper && filters.SiteKey is "" or "_all" ? "_all" : filters.SiteKey;
            var detail = await CpWebTrackerDashboardBuilder.BuildSessionDetailAsync(
                connections,
                id,
                siteKey,
                isSuper,
                cancellationToken).ConfigureAwait(false);
            return detail.Ok && detail.Session is not null
                ? (object)WebTrackerSession(detail)
                : TrackerFailure(detail.Source, detail.Message);
        }

        var dashboard = await CpWebTrackerDashboardBuilder.BuildDashboardAsync(connections, filters, cancellationToken).ConfigureAwait(false);
        if (!dashboard.Ok)
        {
            return TrackerFailure(dashboard.Source, dashboard.Message);
        }

        if (action is "csv" or "export_csv")
        {
            var label = dashboard.SiteKey is "_all" or "" ? "all" : dashboard.SiteKey;
            var fromDay = DateTimeOffset.FromUnixTimeSeconds(dashboard.FromUnix).UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            var toDay = DateTimeOffset.FromUnixTimeSeconds(dashboard.ToUnix).UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            return new WebTrackerCsv(
                CpWebTrackerDashboardBuilder.BuildCsv(dashboard),
                "web-tracker-" + label + "-" + fromDay + "-" + toDay + ".csv");
        }

        return WebTrackerDashboard(dashboard);
    }

    private static object TrackerFailure(string source, string message)
    {
        if (source is "database-error" or "migration" || TrackerSchemaMissing(message))
        {
            if (message.Contains("not configured", StringComparison.OrdinalIgnoreCase))
            {
                return new CodedJson(503, new WebTrackerGate(false, "db"));
            }

            var text = TrackerSchemaMissing(message) ? TrackerTablesMissing : message;
            if (text.Length == 0)
            {
                text = TrackerTablesMissing;
            }

            return new CodedJson(500, new WebTrackerGate(false, "query_failed", text));
        }

        return new WebTrackerMessage(false, message.Length == 0 ? TrackerTablesMissing : message);
    }

    private static bool TrackerSchemaMissing(string message)
        => message.Contains("No tracker database connection available", StringComparison.Ordinal)
            || message.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase)
            || message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Unknown column", StringComparison.OrdinalIgnoreCase);

    private static WebTrackerDashboardBody WebTrackerDashboard(CpWebTrackerDashboardResult result)
        => new(
            true,
            result.SiteKey,
            result.FromUnix,
            result.ToUnix,
            new WebTrackerFilters(
                result.Filters.Device,
                result.Filters.Country,
                result.Filters.Ip,
                result.Filters.UserId,
                result.Filters.UserType,
                result.Filters.Path,
                result.Filters.Browser),
            result.IsSuper,
            result.Db,
            new WebTrackerData(
                new WebTrackerSummary(
                    result.Summary.Sessions,
                    result.Summary.Visitors,
                    result.Summary.Pageviews,
                    result.Summary.Clicks,
                    result.Summary.Searches,
                    result.Summary.GuestSessions,
                    result.Summary.RegisteredSessions,
                    result.Summary.AvgDurationMs,
                    result.Summary.AvgPages,
                    result.Summary.BounceRate),
                result.Daily.Select(x => new WebTrackerDaily(x.Date, x.Sessions, x.Pageviews)).ToArray(),
                result.TopPages.Select(x => new WebTrackerPage(x.Path, x.Views, x.Sessions, x.AvgTimeMs, x.AvgScroll)).ToArray(),
                result.Geo.Select(x => new WebTrackerGeo(x.CountryCode, x.CountryName, x.City, x.Sessions)).ToArray(),
                result.Devices.Select(x => new WebTrackerDevice(x.DeviceType, x.Browser, x.Os, x.Sessions)).ToArray(),
                result.Searches.Select(x => new WebTrackerSearch(x.SearchQuery, x.SearchContext, x.Hits, x.Sessions)).ToArray(),
                result.TopClicks.Select(x => new WebTrackerClick(x.Path, x.ElementTag, x.ElementId, x.ElementText, x.ElementHref, x.Hits)).ToArray(),
                result.Referrers.Select(x => new WebTrackerReferrer(x.Host, x.UtmSource, x.UtmMedium, x.UtmCampaign, x.Sessions)).ToArray(),
                result.RecentSessions.Select(MapRecent).ToArray(),
                result.ByTenant.Select(x => new WebTrackerTenant(x.SiteKey, x.Hostname, x.Sessions, x.Pageviews, x.Visitors)).ToArray(),
                new WebTrackerFacetsBody(
                    result.Facets.Countries.Select(x => new WebTrackerCountryFacet(x.Value, x.Label, x.Sessions)).ToArray(),
                    result.Facets.Devices.Select(x => new WebTrackerNamedFacet(x.Value, x.Sessions)).ToArray(),
                    result.Facets.Browsers.Select(x => new WebTrackerBrowserFacet(x.Value, x.Sessions)).ToArray())));

    private static WebTrackerSessionBody WebTrackerSession(CpWebTrackerSessionDetailResult detail)
        => new(
            true,
            new WebTrackerDetail(
                MapRecent(detail.Session!),
                detail.Pageviews.Select(p => new WebTrackerPageview(p.Id, p.Ts, p.Path, p.Query, p.Title, p.TimeOnPageMs, p.ScrollMaxPct, p.LoadTimeMs)).ToArray(),
                detail.Events.Select(e => new WebTrackerEvent(e.Id, e.Ts, e.EventType, e.Path, e.SearchQuery, e.SearchContext, e.ElementTag, e.ElementId, e.ElementText, e.ElementHref, e.X, e.Y)).ToArray()));

    private static WebTrackerRecent MapRecent(CpWebTrackerRecentSessionRow row)
        => new(
            row.Id,
            row.SessionUid,
            row.SiteKey,
            row.Hostname,
            row.UserId,
            row.IsRegistered ? 1 : 0,
            row.FirstSeenAt,
            row.LastSeenAt,
            row.PageviewCount,
            row.EventCount,
            row.DurationMs,
            row.LandingPath,
            row.ExitPath,
            row.CountryCode,
            row.CountryName,
            row.City,
            row.Region,
            row.DeviceType,
            row.Browser,
            row.Os,
            row.Ip,
            row.ReferrerHost,
            row.UtmSource);

    public sealed record WebTrackerDashboardBody(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("site_key")] string SiteKey,
        [property: JsonPropertyName("from")] long From,
        [property: JsonPropertyName("to")] long To,
        [property: JsonPropertyName("filters")] WebTrackerFilters Filters,
        [property: JsonPropertyName("is_super")] bool IsSuper,
        [property: JsonPropertyName("db")] string Db,
        [property: JsonPropertyName("data")] WebTrackerData Data);

    public sealed record WebTrackerFilters(
        [property: JsonPropertyName("device")] string Device,
        [property: JsonPropertyName("country")] string Country,
        [property: JsonPropertyName("ip")] string Ip,
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("user_type")] string UserType,
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("browser")] string Browser);

    public sealed record WebTrackerData(
        [property: JsonPropertyName("summary")] WebTrackerSummary Summary,
        [property: JsonPropertyName("daily")] IReadOnlyList<WebTrackerDaily> Daily,
        [property: JsonPropertyName("top_pages")] IReadOnlyList<WebTrackerPage> TopPages,
        [property: JsonPropertyName("geo")] IReadOnlyList<WebTrackerGeo> Geo,
        [property: JsonPropertyName("devices")] IReadOnlyList<WebTrackerDevice> Devices,
        [property: JsonPropertyName("searches")] IReadOnlyList<WebTrackerSearch> Searches,
        [property: JsonPropertyName("top_clicks")] IReadOnlyList<WebTrackerClick> TopClicks,
        [property: JsonPropertyName("referrers")] IReadOnlyList<WebTrackerReferrer> Referrers,
        [property: JsonPropertyName("recent_sessions")] IReadOnlyList<WebTrackerRecent> RecentSessions,
        [property: JsonPropertyName("by_tenant")] IReadOnlyList<WebTrackerTenant> ByTenant,
        [property: JsonPropertyName("facets")] WebTrackerFacetsBody Facets);

    public sealed record WebTrackerSummary(
        [property: JsonPropertyName("sessions")] long Sessions,
        [property: JsonPropertyName("visitors")] long Visitors,
        [property: JsonPropertyName("pageviews")] long Pageviews,
        [property: JsonPropertyName("clicks")] long Clicks,
        [property: JsonPropertyName("searches")] long Searches,
        [property: JsonPropertyName("guest_sessions")] long GuestSessions,
        [property: JsonPropertyName("registered_sessions")] long RegisteredSessions,
        [property: JsonPropertyName("avg_duration_ms")] long AvgDurationMs,
        [property: JsonPropertyName("avg_pages")] double AvgPages,
        [property: JsonPropertyName("bounce_rate")] double BounceRate);

    public sealed record WebTrackerDaily(
        [property: JsonPropertyName("date")] string Date,
        [property: JsonPropertyName("sessions")] long Sessions,
        [property: JsonPropertyName("pageviews")] long Pageviews);

    public sealed record WebTrackerPage(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("views")] long Views,
        [property: JsonPropertyName("sessions")] long Sessions,
        [property: JsonPropertyName("avg_time_ms")] long AvgTimeMs,
        [property: JsonPropertyName("avg_scroll")] long AvgScroll);

    public sealed record WebTrackerGeo(
        [property: JsonPropertyName("country_code")] string CountryCode,
        [property: JsonPropertyName("country_name")] string CountryName,
        [property: JsonPropertyName("city")] string City,
        [property: JsonPropertyName("sessions")] long Sessions);

    public sealed record WebTrackerDevice(
        [property: JsonPropertyName("device_type")] string DeviceType,
        [property: JsonPropertyName("browser")] string Browser,
        [property: JsonPropertyName("os")] string Os,
        [property: JsonPropertyName("sessions")] long Sessions);

    public sealed record WebTrackerSearch(
        [property: JsonPropertyName("search_query")] string SearchQuery,
        [property: JsonPropertyName("search_context")] string SearchContext,
        [property: JsonPropertyName("hits")] long Hits,
        [property: JsonPropertyName("sessions")] long Sessions);

    public sealed record WebTrackerClick(
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("element_tag")] string ElementTag,
        [property: JsonPropertyName("element_id")] string ElementId,
        [property: JsonPropertyName("element_text")] string ElementText,
        [property: JsonPropertyName("element_href")] string ElementHref,
        [property: JsonPropertyName("hits")] long Hits);

    public sealed record WebTrackerReferrer(
        [property: JsonPropertyName("host")] string Host,
        [property: JsonPropertyName("utm_source")] string UtmSource,
        [property: JsonPropertyName("utm_medium")] string UtmMedium,
        [property: JsonPropertyName("utm_campaign")] string UtmCampaign,
        [property: JsonPropertyName("sessions")] long Sessions);

    public sealed record WebTrackerRecent(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("session_uid")] string SessionUid,
        [property: JsonPropertyName("site_key")] string SiteKey,
        [property: JsonPropertyName("hostname")] string Hostname,
        [property: JsonPropertyName("user_id")] long UserId,
        [property: JsonPropertyName("is_registered")] int IsRegistered,
        [property: JsonPropertyName("first_seen_at")] long FirstSeenAt,
        [property: JsonPropertyName("last_seen_at")] long LastSeenAt,
        [property: JsonPropertyName("pageview_count")] long PageviewCount,
        [property: JsonPropertyName("event_count")] long EventCount,
        [property: JsonPropertyName("duration_ms")] long DurationMs,
        [property: JsonPropertyName("landing_path")] string LandingPath,
        [property: JsonPropertyName("exit_path")] string ExitPath,
        [property: JsonPropertyName("country_code")] string CountryCode,
        [property: JsonPropertyName("country_name")] string CountryName,
        [property: JsonPropertyName("city")] string City,
        [property: JsonPropertyName("region")] string Region,
        [property: JsonPropertyName("device_type")] string DeviceType,
        [property: JsonPropertyName("browser")] string Browser,
        [property: JsonPropertyName("os")] string Os,
        [property: JsonPropertyName("ip")] string Ip,
        [property: JsonPropertyName("referrer_host")] string ReferrerHost,
        [property: JsonPropertyName("utm_source")] string UtmSource);

    public sealed record WebTrackerTenant(
        [property: JsonPropertyName("site_key")] string SiteKey,
        [property: JsonPropertyName("hostname")] string Hostname,
        [property: JsonPropertyName("sessions")] long Sessions,
        [property: JsonPropertyName("pageviews")] long Pageviews,
        [property: JsonPropertyName("visitors")] long Visitors);

    public sealed record WebTrackerFacetsBody(
        [property: JsonPropertyName("countries")] IReadOnlyList<WebTrackerCountryFacet> Countries,
        [property: JsonPropertyName("devices")] IReadOnlyList<WebTrackerNamedFacet> Devices,
        [property: JsonPropertyName("browsers")] IReadOnlyList<WebTrackerBrowserFacet> Browsers);

    public sealed record WebTrackerCountryFacet(
        [property: JsonPropertyName("country_code")] string CountryCode,
        [property: JsonPropertyName("country_name")] string CountryName,
        [property: JsonPropertyName("sessions")] long Sessions);

    public sealed record WebTrackerNamedFacet(
        [property: JsonPropertyName("device_type")] string DeviceType,
        [property: JsonPropertyName("sessions")] long Sessions);

    public sealed record WebTrackerBrowserFacet(
        [property: JsonPropertyName("browser")] string Browser,
        [property: JsonPropertyName("sessions")] long Sessions);

    public sealed record WebTrackerSessionBody(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("detail")] WebTrackerDetail Detail);

    public sealed record WebTrackerDetail(
        [property: JsonPropertyName("session")] WebTrackerRecent Session,
        [property: JsonPropertyName("pageviews")] IReadOnlyList<WebTrackerPageview> Pageviews,
        [property: JsonPropertyName("events")] IReadOnlyList<WebTrackerEvent> Events);

    public sealed record WebTrackerPageview(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("ts")] long Ts,
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("query")] string Query,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("time_on_page_ms")] long TimeOnPageMs,
        [property: JsonPropertyName("scroll_max_pct")] long ScrollMaxPct,
        [property: JsonPropertyName("load_time_ms")] long LoadTimeMs);

    public sealed record WebTrackerEvent(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("ts")] long Ts,
        [property: JsonPropertyName("event_type")] string EventType,
        [property: JsonPropertyName("path")] string Path,
        [property: JsonPropertyName("search_query")] string SearchQuery,
        [property: JsonPropertyName("search_context")] string SearchContext,
        [property: JsonPropertyName("element_tag")] string ElementTag,
        [property: JsonPropertyName("element_id")] string ElementId,
        [property: JsonPropertyName("element_text")] string ElementText,
        [property: JsonPropertyName("element_href")] string ElementHref,
        [property: JsonPropertyName("x")] int X,
        [property: JsonPropertyName("y")] int Y);
}
