using System.Text.Json;
using System.Text.Json.Serialization;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PresentationParityReporterTests
{
    [Fact]
    public void BuildReportTracksAllOperatorAndStorefrontSurfaces()
    {
        var report = new PresentationParityReporter().BuildReport();

        Assert.Equal("scaffold-not-full-php-parity", report.Status);
        Assert.Contains(report.Surfaces, surface => surface.SurfaceKey == "cp" && surface.Stylesheets.Count > 0);
        Assert.Contains(report.Surfaces, surface => surface.SurfaceKey == "erp");
        Assert.Contains(report.Surfaces, surface => surface.SurfaceKey == "bos" && surface.LegacyChromeSource.Contains("epc_bos_shell", StringComparison.Ordinal));
        Assert.Contains(report.Surfaces, surface => surface.SurfaceKey == "storefront");
        Assert.Contains(report.Guarantees, guarantee => guarantee.Contains("NOT full PHP", StringComparison.OrdinalIgnoreCase)
            || guarantee.Contains("Digests/APIs only", StringComparison.Ordinal));
        Assert.Contains(report.Guarantees, guarantee => guarantee.Contains("cloudpanel_probe_php_presentation_parity.sh", StringComparison.Ordinal));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("MODULE_FUNCTION_PARITY", StringComparison.Ordinal)
            || gap.Contains("functional test", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("PHP_VS_ASPNET_DETAILED_RECHECK", StringComparison.Ordinal));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("Pixel/DOM parity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WritePresentationParityProbeSnapshotWhenRequested()
    {
        // ECOMAE_WRITE_PRESENTATION_PARITY_PROBE=1 dotnet test --filter WritePresentationParityProbeSnapshotWhenRequested
        if (!string.Equals(Environment.GetEnvironmentVariable("ECOMAE_WRITE_PRESENTATION_PARITY_PROBE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var report = new PresentationParityReporter().BuildReport();
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        }) + "\n";

        var root = FindRepoRoot();
        var path = Path.Combine(root, "docs", "migration", "evidence", "decommission", "public-probes", "www-presentation-parity.json");
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
