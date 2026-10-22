using System.Text.Json;
using System.Text.Json.Serialization;
using EcomAE.Platform.Migration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ZeroPhpCompletionReporterTests
{
    [Fact]
    public void BuildReportQuantifiesRemainingWorkAndBlocksPhpRemoval()
    {
        var report = new ZeroPhpCompletionReporter(new PhpDecommissionReadinessReporter(new RepoHostEnvironment())).BuildReport();

        Assert.Equal(100, report.OverallCompletePercent);
        Assert.Equal(0, report.OverallPendingPercent);
        Assert.Equal("ready-for-php-removal", report.Status);
        Assert.True(report.CutoverAllowed);
        Assert.True(report.ReadyForPhpRemoval);
        Assert.Contains(report.Areas, area => area.Name == "Foundation, deployment, and diagnostics" && area.CompletePercent == 100);
        Assert.Contains(report.Areas, area => area.Name == "PHP runtime decommission" && area.CompletePercent == 100 && area.Status == "ready-for-php-removal");
        Assert.Contains(report.Areas, area => area.Name == "Route inventory and cutover ownership" && area.CompletePercent == 100);
        Assert.Contains(report.Areas, area => area.Name == "CP, ERP, BOS, and tenant workflow parity" && area.CompletePercent == 100);
        Assert.Contains(report.NextActions, action => action.Contains("ReadyToRemovePhp", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Areas, area => area.Name == "Storefront and public API parity" && area.CompletePercent == 100);
        Assert.Contains(report.Areas, area => area.Name == "Background jobs and scheduled work" && area.CompletePercent == 100);
        Assert.Contains(report.Areas, area => area.Name == "Data, auth, observability, and rollback evidence" && area.CompletePercent == 100);
        // Post-removal: next actions are the operator decommission confirmations only.
        Assert.Contains(report.NextActions, action => action.Contains("decommission", StringComparison.OrdinalIgnoreCase)
            || action.Contains("rollback", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(report.NextActions, action => action.Contains("issue_smoke_credentials", StringComparison.Ordinal));
        Assert.Contains(
            report.Areas.First(area => area.Name == "PHP runtime decommission").PendingWork,
            work => work.Contains("ReadyToRemovePhp", StringComparison.Ordinal)
                || work.Contains("decommission", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WriteZeroPhpCompletionProbeSnapshotWhenRequested()
    {
        // ECOMAE_WRITE_ZERO_PHP_COMPLETION_PROBE=1 dotnet test --filter WriteZeroPhpCompletionProbeSnapshotWhenRequested
        if (!string.Equals(Environment.GetEnvironmentVariable("ECOMAE_WRITE_ZERO_PHP_COMPLETION_PROBE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        var report = new ZeroPhpCompletionReporter(new PhpDecommissionReadinessReporter(new RepoHostEnvironment())).BuildReport();
        Assert.Equal(100, report.OverallCompletePercent);
        Assert.Equal("ready-for-php-removal", report.Status);

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        }) + "\n";

        var root = RepoHostEnvironment.FindRepoRoot();
        var path = Path.Combine(root, "docs", "migration", "evidence", "decommission", "public-probes", "www-zero-php-completion.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    private sealed class RepoHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "EcomAE.Platform.Tests";
        public string ContentRootPath { get; set; } = FindRepoRoot();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public static string FindRepoRoot()
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
}
