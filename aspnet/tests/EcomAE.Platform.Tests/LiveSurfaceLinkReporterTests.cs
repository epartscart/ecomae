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
        Assert.True(report.Links.Count >= 70);
        Assert.Contains(report.Links, link => link.HostClass == "super-cp" && link.Url.Contains("/BOS/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Links, link => link.HostClass == "super-cp" && link.Url.Contains("/CP/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Links, link => link.HostClass == "super-cp" && link.Url.Contains("/ERP/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Links, link => link.HostClass == "tenant" && link.Url.Contains("electronicae.com", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Links, link => link.HostClass == "aspnet-diagnostics" && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/migration/surface-field-parity");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/cp/parity");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/erp/parity");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/bos/parity");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/api/v1/catalog/models");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/api/v1/catalog/brand-parts");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/cp/groups");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/erp/gl-journals");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/bos/tenants");
        Assert.Contains(report.Links, link => link.AspNetRouteHint == "/storefront/account-summary");
        Assert.Contains(report.Links, link => link.Surface.Contains("Price lookup", StringComparison.OrdinalIgnoreCase) && link.StackToday == "aspnet");
        Assert.Contains(report.CutoverRules, rule => rule.Contains("Broad", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_ensure_epc_api_clients_table.sh", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_capture_final_gate_artifacts.sh", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("ECOMAE_CUSTOMER_COOKIE_HEADER", StringComparison.Ordinal));
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
