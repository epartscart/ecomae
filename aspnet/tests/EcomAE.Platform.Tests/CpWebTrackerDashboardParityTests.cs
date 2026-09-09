using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public class CpWebTrackerDashboardParityTests
{
    [Fact]
    public void Routes_expose_php_parity_dashboard_session_csv()
    {
        Assert.Equal("/cp/web-tracker-app", EcomAeRoutes.ControlPanelWebTrackerApp);
        Assert.Equal("/cp/web-tracker/dashboard", EcomAeRoutes.ControlPanelWebTrackerDashboard);
        Assert.Equal("/cp/web-tracker/session", EcomAeRoutes.ControlPanelWebTrackerSession);
        Assert.Equal("/cp/web-tracker/csv", EcomAeRoutes.ControlPanelWebTrackerCsv);
        Assert.Equal("/epc-web-tracker-collect.php", EcomAeRoutes.WebTrackerCollectPhp);
        Assert.Equal("/epc-web-tracker-collect", EcomAeRoutes.WebTrackerCollect);
    }

    [Theory]
    [InlineData("www.epartscart.com", "epartscart")]
    [InlineData("epartscart.com", "epartscart")]
    [InlineData("www.ecomae.com", "ecomae")]
    [InlineData("cp.ecomae.com", "ecomae")]
    public void ResolveOwnSiteKey_matches_php(string host, string expected)
        => Assert.Equal(expected, CpWebTrackerDashboardBuilder.ResolveOwnSiteKey(host));

    [Fact]
    public void NormalizeFilters_tenant_locked_to_own_site()
    {
        var f = CpWebTrackerDashboardBuilder.NormalizeFilters(
            "_all", "2026-08-01", "2026-08-10", "mobile", "AE", "86.96.",
            "5", "guest", "Chrome", "/en/parts/", isSuper: false, ownSiteKey: "epartscart");
        Assert.Equal("epartscart", f.SiteKey);
        Assert.Equal("mobile", f.Device);
        Assert.Equal("AE", f.Country);
        Assert.Equal("86.96.", f.Ip);
        Assert.Equal("5", f.UserId);
        Assert.Equal("guest", f.UserType);
        Assert.Equal("Chrome", f.Browser);
        Assert.Equal("/en/parts/", f.Path);
        Assert.False(f.IsSuper);
    }

    [Fact]
    public void NormalizeFilters_rejects_unknown_device_and_who()
    {
        var f = CpWebTrackerDashboardBuilder.NormalizeFilters(
            "epartscart", null, null, "smartwatch", "ae!", "bad ip!!",
            "x", "bots", "Chrome<script>", "/a\nb", isSuper: true, ownSiteKey: "ecomae");
        Assert.Equal("epartscart", f.SiteKey);
        Assert.Equal(string.Empty, f.Device);
        Assert.Equal("AE", f.Country);
        Assert.Equal(string.Empty, f.UserType);
        Assert.DoesNotContain('<', f.Browser);
        Assert.DoesNotContain('\n', f.Path);
    }

    [Fact]
    public void RangeUnix_covers_full_end_day()
    {
        var (from, to) = CpWebTrackerDashboardBuilder.RangeUnix("2026-08-01", "2026-08-01");
        Assert.True(to > from);
        Assert.Equal(86399, to - from);
    }

    [Fact]
    public void BuildCsv_includes_summary_and_sessions_sections()
    {
        var filters = new CpWebTrackerFilterQuery(
            "epartscart", "2026-08-01", "2026-08-10", "", "", "", "", "", "", "", false);
        var dash = new CpWebTrackerDashboardResult(
            true, "epartscart", 1, 2, false, "tracker",
            new CpWebTrackerDashSummary(2, 1, 4, 1, 1, 0, 1, 1, 1000, 2, 10),
            [new CpWebTrackerDailyRow("2026-08-01", 2, 4)],
            [], [], [], [], [], [],
            [new CpWebTrackerRecentSessionRow(
                9, "u", "epartscart", "www.epartscart.com", 0, false, 1, 2, 2, 0, 500,
                "/", "/en", "AE", "UAE", "Dubai", "", "desktop", "Chrome", "Linux", "1.2.3.4", "", "")],
            [],
            new CpWebTrackerFacets([], [], []),
            filters, ["epartscart"], "database", "");
        var csv = CpWebTrackerDashboardBuilder.BuildCsv(dash);
        Assert.Contains("Website tracker full report", csv);
        Assert.Contains("SECTION,Summary", csv);
        Assert.Contains("SECTION,Recent sessions", csv);
        Assert.Contains("epartscart", csv);
        Assert.Contains("1.2.3.4", csv);
    }

    [Fact]
    public void AspNet_assets_exist_for_platform_bridge()
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "content/general_pages/epc_web_tracker_cp.css")));
        Assert.True(File.Exists(Path.Combine(root, "content/general_pages/epc_web_tracker_aspnet.js")));
        Assert.True(File.Exists(Path.Combine(root, "content/general_pages/epc_web_tracker.js")));
        var js = File.ReadAllText(Path.Combine(root, "content/general_pages/epc_web_tracker_aspnet.js"));
        Assert.Contains("/cp/web-tracker/dashboard", js);
        Assert.Contains("wt-donut", js);
        Assert.Contains("svgLineChart", js);
        var css = File.ReadAllText(Path.Combine(root, "content/general_pages/epc_web_tracker_cp.css"));
        Assert.Contains("wt-hero", css);
        Assert.Contains("wt-funnel", css);
        var beaconJs = File.ReadAllText(Path.Combine(root, "content/general_pages/epc_web_tracker.js"));
        Assert.Contains("EPC_WEB_TRACKER", beaconJs);
        Assert.Contains("sendBeacon", beaconJs);
        Assert.Contains("Never track CP", beaconJs);
        var bridge = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs"));
        Assert.Contains("/platform-assets/epc_web_tracker.js", bridge);
        Assert.DoesNotContain("\"/php-reference/\"", bridge);
    }

    [Fact]
    public void Blazor_shell_marks_php_parity_structure()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpWebTrackerApp.razor"));
        Assert.Contains("epc-web-tracker", razor);
        Assert.Contains("wt_kpis", razor);
        Assert.Contains("wt_sessions", razor);
        Assert.Contains("wt-table", razor);
        Assert.Contains("method=\"get\"", razor);
        Assert.Contains("BuildCpWebTrackerDashboardAsync", razor);
        Assert.Contains("BuildCpWebTrackerSessionDetailAsync", razor);
        Assert.Contains("data-epc-wt-ssr", razor);
        Assert.Contains("CpWebTrackerStylesheets", razor);
        Assert.Contains("@page \"/cp/shop/statistics/web_tracker\"", razor);
        Assert.DoesNotContain("Loading traffic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_web_tracker_aspnet.js", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("Compare PHP reference", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Tracker_builder_prefers_docpart_when_richer_than_open_null()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Migration/CpWebTrackerDashboardBuilder.cs"));
        Assert.Contains("OpenAsync(\"docpart\"", src, StringComparison.Ordinal);
        Assert.Contains("CommandTimeout = 12", src, StringComparison.Ordinal);
        Assert.Contains("PickFreshestTrackerIndex", src, StringComparison.Ordinal);
        Assert.Contains("MAX(`last_seen_at`)", src, StringComparison.Ordinal);
    }

    [Fact]
    public void PickFreshestTracker_prefers_newer_last_seen_over_larger_historical_count()
    {
        var staleRegistry = new CpWebTrackerDashboardBuilder.TrackerProbe(Count: 80_000, MaxSeen: 1_755_216_000, InRange: 12_000); // ~2025-08-15
        var freshShop = new CpWebTrackerDashboardBuilder.TrackerProbe(Count: 2_400, MaxSeen: 1_757_376_000, InRange: 2_400); // ~2025-09-09
        var pick = CpWebTrackerDashboardBuilder.PickFreshestTrackerIndex([staleRegistry, freshShop]);
        Assert.Equal(1, pick);
    }

    [Fact]
    public void PickFreshestTracker_uses_in_range_then_count_when_last_seen_ties()
    {
        var a = new CpWebTrackerDashboardBuilder.TrackerProbe(10, 1_757_376_000, 2);
        var b = new CpWebTrackerDashboardBuilder.TrackerProbe(8, 1_757_376_000, 8);
        Assert.Equal(1, CpWebTrackerDashboardBuilder.PickFreshestTrackerIndex([a, b]));

        var c = new CpWebTrackerDashboardBuilder.TrackerProbe(20, 1_757_376_000, 5);
        var d = new CpWebTrackerDashboardBuilder.TrackerProbe(9, 1_757_376_000, 5);
        Assert.Equal(0, CpWebTrackerDashboardBuilder.PickFreshestTrackerIndex([c, d]));
    }

    [Fact]
    public void NormalizeUnixSeconds_divides_millisecond_timestamps()
    {
        Assert.Equal(1_757_376_000, CpWebTrackerDashboardBuilder.NormalizeUnixSeconds(1_757_376_000_000));
        Assert.Equal(1_757_376_000, CpWebTrackerDashboardBuilder.NormalizeUnixSeconds(1_757_376_000));
    }

    [Fact]
    public void Storefront_and_marketing_chrome_inject_first_party_beacon()
    {
        var root = FindRepoRoot();
        var storefront = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor"));
        var marketing = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpEcomaeMarketingChrome.razor"));
        var beacon = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpWebTrackerBeacon.razor"));
        Assert.Contains("<PhpWebTrackerBeacon", storefront);
        Assert.Contains("<PhpWebTrackerBeacon", marketing);
        Assert.Contains("window.EPC_WEB_TRACKER", beacon);
        Assert.Contains("/platform-assets/epc_web_tracker.js", beacon);
        Assert.Contains("/epc-web-tracker-collect.php", beacon + "\n" + File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Migration/CpWebTrackerCollectService.cs")));
        Assert.DoesNotContain("/php-reference/", storefront);
        Assert.DoesNotContain("/php-reference/", marketing);
        Assert.DoesNotContain("/php-reference/", beacon);
    }

    [Fact]
    public void Collect_route_is_mapped_and_not_redirected_off_kestrel()
    {
        var root = FindRepoRoot();
        var module = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs"));
        var middleware = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Middleware/PhpProductPathRedirectMiddleware.cs"));
        Assert.Contains("WebTrackerCollectPhp", module);
        Assert.Contains("HandleWebTrackerCollectAsync", module);
        Assert.Contains("AllowAnonymous", module);
        Assert.Contains("epc-web-tracker-collect.php", middleware);
        Assert.Contains("return _next(context)", middleware);
    }

    [Fact]
    public void BuildBeaconConfigJson_matches_php_shape()
    {
        var json = CpWebTrackerCollectService.BuildBeaconConfigJson("www.epartscart.com", 7);
        Assert.Contains("\"endpoint\":\"/epc-web-tracker-collect.php\"", json.Replace(" ", string.Empty));
        Assert.Contains("\"site_key\":\"epartscart\"", json.Replace(" ", string.Empty));
        Assert.Contains("\"user_id\":7", json.Replace(" ", string.Empty));
        Assert.Contains("\"is_registered\":true", json.Replace(" ", string.Empty));
        Assert.DoesNotContain("php-reference", json);
        Assert.DoesNotContain("ASP.NET", json);
    }

    [Fact]
    public void TryParsePayload_rejects_bad_ids_and_json()
    {
        Assert.False(CpWebTrackerCollectService.TryParsePayload("not-json", out _, out var badJson));
        Assert.Equal("bad_json", badJson);

        Assert.False(CpWebTrackerCollectService.TryParsePayload(
            """{"site_key":"epartscart","session_uid":"nope"}""", out _, out var badIds));
        Assert.Equal("bad_ids", badIds);

        Assert.True(CpWebTrackerCollectService.TryParsePayload(
            """{"site_key":"ePartsCart!","session_uid":"aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee","visitor_uid":"bad"}""",
            out var payload,
            out var err));
        Assert.Null(err);
        Assert.Equal("epartscart", payload!.SiteKey);
        Assert.Equal(string.Empty, payload.VisitorUid);
    }

    [Fact]
    public void ParseUa_and_clip_match_php()
    {
        var chrome = CpWebTrackerCollectService.ParseUa("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0");
        Assert.Equal("desktop", chrome.Device);
        Assert.Equal("Chrome", chrome.Browser);
        Assert.Equal("Windows", chrome.Os);

        var mobile = CpWebTrackerCollectService.ParseUa("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 Version/17.0 Mobile/15E148 Safari/604.1");
        Assert.Equal("mobile", mobile.Device);
        Assert.Equal("Safari", mobile.Browser);
        Assert.Equal("iOS", mobile.Os);

        Assert.Equal("hello world", CpWebTrackerCollectService.Clip("  hello   world  ", 50));
        Assert.Equal("abc", CpWebTrackerCollectService.Clip("abcdef", 3));
        Assert.True(CpWebTrackerCollectService.IsUuidOk("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"));
        Assert.False(CpWebTrackerCollectService.IsUuidOk("nope"));
    }

    [Fact]
    public void RateLimiter_caps_at_120_per_minute()
    {
        var limiter = new CpWebTrackerCollectRateLimiter();
        for (var i = 0; i < 120; i++)
        {
            Assert.True(limiter.TryAcquire("203.0.113.9"));
        }

        Assert.False(limiter.TryAcquire("203.0.113.9"));
        Assert.True(limiter.TryAcquire("203.0.113.10"));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.Platform.sln"))
                || File.Exists(Path.Combine(dir.FullName, "content", "general_pages", "epc_web_tracker.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root");
    }
}
