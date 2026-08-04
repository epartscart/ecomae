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

        Assert.Equal(95, report.OverallCompletePercent);
        Assert.Equal(5, report.OverallPendingPercent);
        Assert.Equal("not-ready-for-php-removal", report.Status);
        Assert.False(report.CutoverAllowed);
        Assert.False(report.ReadyForPhpRemoval);
        Assert.Contains(report.Areas, area => area.Name == "Foundation, deployment, and diagnostics" && area.CompletePercent == 100);
        Assert.Contains(report.Areas, area => area.Name == "PHP runtime decommission" && area.CompletePercent == 0 && area.Status == "blocked");
        Assert.Contains(report.Areas, area => area.Name == "Route inventory and cutover ownership" && area.CompletePercent == 100);
        Assert.Contains(report.Areas, area => area.Name == "CP, ERP, BOS, and tenant workflow parity" && area.CompletePercent == 100);
        Assert.Contains(report.NextActions, action => action.Contains("remaining 5%", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Areas, area => area.Name == "Storefront and public API parity" && area.CompletePercent == 100);
        Assert.Contains(report.Areas, area => area.Name == "Background jobs and scheduled work" && area.CompletePercent == 100);
        Assert.Contains(report.Areas, area => area.Name == "Data, auth, observability, and rollback evidence" && area.CompletePercent == 100);
        // Smoke attached (#612): next actions focus on redeploy + human approval, not re-issuing keys.
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_redeploy_final_gate_branch.sh", StringComparison.Ordinal)
            || action.Contains("origin/main", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("RELEASE_OWNER_APPROVAL", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.NextActions, action => action.Contains("ECOMAE_CUSTOMER_COOKIE_HEADER", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("ENTERPRISE_BOS_ARCHITECTURE_COMPLIANCE.md", StringComparison.Ordinal));
        Assert.DoesNotContain(report.NextActions, action => action.Contains("issue_smoke_credentials", StringComparison.Ordinal));
        Assert.Contains(
            report.Areas.First(area => area.Name == "PHP runtime decommission").PendingWork,
            work => work.Contains("RELEASE_OWNER_APPROVAL.md", StringComparison.Ordinal));
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
        Assert.Equal(95, report.OverallCompletePercent);
        Assert.Equal("not-ready-for-php-removal", report.Status);

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
