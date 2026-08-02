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
        Assert.Contains(report.Areas, area => area.Name == "Foundation, deployment, and diagnostics" && area.CompletePercent == 100);
        Assert.Contains(report.Areas, area => area.Name == "PHP runtime decommission" && area.CompletePercent == 0 && area.Status == "blocked");
        Assert.Contains(report.Areas, area => area.Name == "Route inventory and cutover ownership" && area.CompletePercent == 100);
        Assert.Contains(report.Areas, area => area.Name == "CP, ERP, BOS, and tenant workflow parity" && area.CompletePercent == 100);
        Assert.Contains(report.NextActions, action => action.Contains("remaining 5%", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Areas, area => area.Name == "Storefront and public API parity" && area.CompletePercent == 100);
        Assert.Contains(report.Areas, area => area.Name == "Background jobs and scheduled work" && area.CompletePercent == 100);
        Assert.Contains(report.Areas, area => area.Name == "Data, auth, observability, and rollback evidence" && area.CompletePercent == 100);
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_bootstrap_from_github.sh", StringComparison.Ordinal)
            || action.Contains("cloudpanel_find_and_redeploy.sh", StringComparison.Ordinal));
        Assert.Contains(report.NextActions, action => action.Contains("ENTERPRISE_BOS_ARCHITECTURE_COMPLIANCE.md", StringComparison.Ordinal));
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
