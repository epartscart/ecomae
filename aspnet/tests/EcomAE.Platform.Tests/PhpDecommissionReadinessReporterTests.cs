using EcomAE.Platform.Migration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpDecommissionReadinessReporterTests
{
    [Fact]
    public void BuildReportBlocksPhpRemovalAndExposesChecklist()
    {
        var report = new PhpDecommissionReadinessReporter(new RepoHostEnvironment()).BuildReport();

        Assert.Equal("blocked-not-ready-for-php-removal", report.Status);
        Assert.False(report.ReadyToRemovePhp);
        Assert.True(report.ChecklistTotalCount >= 8);
        Assert.True(report.BlockerCount >= 1);
        Assert.Contains(report.Checklist, item => item.Id == "release-owner-approval" && item.Status == "missing");
        Assert.Contains(report.Checklist, item => item.Id == "rollback-validated" && item.Status == "present");
        Assert.Contains(report.Checklist, item => item.Id == "exact-route-shadows-only" && item.Status == "present");
        Assert.Contains(report.Checklist, item => item.Id == "public-probes" && item.Status == "present");
        Assert.Contains(report.Checklist, item => item.Id == "cloudpanel-capture-script" && item.Status == "present");
        Assert.Contains(report.NextActions, action => action.Contains("run_zero_php_final_gate_checklist.sh", StringComparison.OrdinalIgnoreCase));
        Assert.True(report.ChecklistCompletePercent < 100);
        Assert.False(string.Equals(report.Checklist.First(item => item.Id == "staging-smoke-price").Status, "present", StringComparison.Ordinal));
    }

    private sealed class RepoHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "EcomAE.Platform.Tests";
        public string ContentRootPath { get; set; } = FindRepoRoot();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        private static string FindRepoRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "docs", "migration", "PHP_DECOMMISSION_READINESS.md")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            return AppContext.BaseDirectory;
        }
    }
}
