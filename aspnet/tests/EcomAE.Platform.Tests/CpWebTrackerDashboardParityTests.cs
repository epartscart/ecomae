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
        var js = File.ReadAllText(Path.Combine(root, "content/general_pages/epc_web_tracker_aspnet.js"));
        Assert.Contains("/cp/web-tracker/dashboard", js);
        Assert.Contains("wt-donut", js);
        Assert.Contains("svgLineChart", js);
        var css = File.ReadAllText(Path.Combine(root, "content/general_pages/epc_web_tracker_cp.css"));
        Assert.Contains("wt-hero", css);
        Assert.Contains("wt-funnel", css);
    }

    [Fact]
    public void Blazor_shell_marks_php_parity_structure()
    {
        var root = FindRepoRoot();
        var razor = File.ReadAllText(Path.Combine(root, "aspnet/src/EcomAE.Platform/Components/Pages/CpWebTrackerApp.razor"));
        Assert.Contains("epc-web-tracker", razor);
        Assert.Contains("wt_kpis", razor);
        Assert.Contains("wt_sessions", razor);
        Assert.Contains("wt_mix", razor);
        Assert.Contains("epc_web_tracker_aspnet.js", razor);
        Assert.Contains("CpWebTrackerStylesheets", razor);
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
