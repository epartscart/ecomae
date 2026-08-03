using System.Text.Json;
using System.Text.Json.Serialization;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class SurfaceParityReporterTests
{
    [Fact]
    public void BuildReportTracksEveryProductionSurfaceBeforeFiftyPercentGate()
    {
        var report = new SurfaceParityReporter().BuildReport();

        Assert.Equal("parity-not-yet-reached", report.Status);
        Assert.Contains(report.Items, item => item.Surface == "Login" && item.Status == "login-bridge-hybrid");
        Assert.Contains(report.Items, item => item.Surface == "Super CP" && item.Status == "hybrid-chrome-nav-login-bridge");
        Assert.Contains(report.Items, item => item.Surface == "Super CP" && item.AspNetRoute.Contains("/cp/app", StringComparison.Ordinal));
        Assert.Contains(report.Items, item => item.Surface == "Platform ERP" && item.RequiredEvidence.Contains("PHP ERP", StringComparison.Ordinal));
        Assert.Contains(report.Items, item => item.Surface == "Super BOS" && item.LegacyRoute == "ecomae.com/BOS");
        Assert.Contains(report.Items, item => item.Surface == "Tenant ERP" && item.RequiredEvidence.Contains("ERP-only tenant", StringComparison.Ordinal));
        Assert.Contains(report.Items, item => item.Surface == "Platform ERP" && item.Status == "hybrid-chrome-nav-login-bridge");
        Assert.Contains(report.Items, item => item.Surface == "Public API" && item.Status == "catalog-cache-routes-wired-awaiting-staging");
        Assert.Contains(report.Items, item => item.Surface == "Workers" && item.Status == "dry-run-validator-layer-complete");
        Assert.Contains(report.RequiredBeforeFiftyPercent, gate => gate.Contains("ensure_epc_api_clients_table.sh", StringComparison.Ordinal));
        Assert.Contains(report.RequiredBeforeFiftyPercent, gate => gate.Contains("run_surface_parity_harness.sh", StringComparison.Ordinal));
        Assert.Contains(report.RequiredBeforeFiftyPercent, gate => gate.Contains("surface-field-parity", StringComparison.Ordinal));
    }

    [Fact]
    public void WriteSurfaceParityProbeSnapshotWhenRequested()
    {
        // ECOMAE_WRITE_SURFACE_PARITY_PROBE=1 dotnet test --filter WriteSurfaceParityProbeSnapshotWhenRequested
        if (!string.Equals(Environment.GetEnvironmentVariable("ECOMAE_WRITE_SURFACE_PARITY_PROBE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var report = new SurfaceParityReporter().BuildReport();
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        }) + "\n";

        var root = FindRepoRoot();
        var path = Path.Combine(root, "docs", "migration", "evidence", "decommission", "public-probes", "www-surface-parity.json");
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
