using System.Text.Json;
using System.Text.Json.Serialization;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LiveSurfaceLinkReporterTests
{
    [Fact]
    public void BuildReportCataloguesSuperCpTenantAndAspNetDiagnostics()
    {
        var report = new LiveSurfaceLinkReporter().BuildReport();

        Assert.Equal("www.ecomae.com", report.PlatformHost);
        Assert.True(report.Links.Count >= 105);
        Assert.Contains(report.Links, link => link.HostClass == "super-cp" && link.Url.Contains("/BOS/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Links, link => link.HostClass == "super-cp" && link.Url.Contains("/CP/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Links, link => link.HostClass == "super-cp" && link.Url.Contains("/ERP/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Links, link => link.HostClass == "tenant" && link.Url.Contains("electronicae.com", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Links, link => link.HostClass == "aspnet-diagnostics" && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/migration/surface-field-parity");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/cp/parity");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/erp/parity");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/bos/parity");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/auth/session/parity");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/auth/api-client/parity");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/api/v1/catalog/parity");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/migration/data-parity");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/api/v1/catalog/models");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/api/v1/catalog/brand-parts");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/api/v1/catalog/article-brands");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/api/v1/catalog/engine-search");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/cp/groups");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/cp/users");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/erp/gl-journals");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/erp/inventory-stock");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/bos/tenants");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/bos/audit-log");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/storefront/account-summary");
        Assert.Contains(report.Links, link => link.Surface.Contains("Price lookup", StringComparison.OrdinalIgnoreCase) && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/status"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/manufacturers"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/models"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/modifications"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/brands"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/suppliers"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/vin"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/engines"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/analogs"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/article-brands"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/categories"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/products"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/engine-search"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/article-links"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/article"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/articles"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/engine"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/api/v1/catalog/brand-parts"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/dashboard-summary"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/tenants"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/users"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/groups"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/cp/modules"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/erp/dashboard-summary"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint == "/bos/audit-log"
            && link.StackToday == "aspnet");
        Assert.Equal(30, report.Links.Count(link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && (link.AspNetRouteHint.StartsWith("/cp/", StringComparison.Ordinal)
                || link.AspNetRouteHint.StartsWith("/erp/", StringComparison.Ordinal)
                || link.AspNetRouteHint.StartsWith("/bos/", StringComparison.Ordinal))));
        Assert.Contains(report.CutoverRules, rule => rule.Contains("Broad", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.CutoverRules, rule => rule.Contains("tenant", StringComparison.OrdinalIgnoreCase)
            && rule.Contains("PHP", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-diagnostics"
            && link.AspNetRouteHint == "/migration/console"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/app"
            && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/erp/app"
            && link.StackToday == "aspnet");
        Assert.Equal(4, report.Links.Count(link =>
            link.HostClass == "aspnet-exact-route-shadow-live"
            && link.AspNetRouteHint.StartsWith("/storefront/", StringComparison.Ordinal)));
        Assert.Equal(8, report.Links.Count(link => link.HostClass == "aspnet-presentation-preview"));
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/orders");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/users-app");
        Assert.Contains(report.Links, link =>
            link.HostClass == "aspnet-presentation-preview"
            && link.AspNetRouteHint == "/cp/groups-app");
        Assert.Equal(4, report.Links.Count(link => link.HostClass == "aspnet-login-bridge"));
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_ensure_epc_api_clients_table.sh", StringComparison.Ordinal));

        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_capture_final_gate_artifacts.sh", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_probe_catalog_vehicle_chain.sh", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_list_warm_catalog_vehicle_ids.sh vin", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("umapi engine_search", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("umapi article_links", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("umapi article", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("action_not_allowed", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("Wired catalog exact-routes complete", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("Surface digests: 30/30", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("Storefront digests: 4/4", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_install_storefront_digest_shadows.sh", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_probe_storefront_digest_shadows.sh", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("/migration/console", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("pairsChecked=19", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("/cp/login", StringComparison.Ordinal)
            || action.Contains("/cp|/erp|/bos|/storefront/{app,login}", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_install_presentation_app_shadows.sh", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("SecretSuccession", StringComparison.Ordinal)
            || action.Contains("secret_succession", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("CHROME_PARITY_GAP_MATRIX", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("RELEASE_OWNER_APPROVAL.md", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_probe_live_tenant_php_chrome.sh", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("ECOMAE_CONFIRM_TENANT_HOST_SHADOW", StringComparison.Ordinal));
    }

    [Fact]
    public void WriteLiveSurfaceLinkProbeSnapshotWhenRequested()
    {
        // ECOMAE_WRITE_LIVE_SURFACE_LINK_PROBE=1 dotnet test --filter WriteLiveSurfaceLinkProbeSnapshotWhenRequested
        if (!string.Equals(Environment.GetEnvironmentVariable("ECOMAE_WRITE_LIVE_SURFACE_LINK_PROBE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var report = new LiveSurfaceLinkReporter().BuildReport();
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        }) + "\n";

        var root = FindRepoRoot();
        var path = Path.Combine(root, "docs", "migration", "evidence", "decommission", "public-probes", "www-live-surface-links.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "scripts", "run_zero_php_final_gate_checklist.sh")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
