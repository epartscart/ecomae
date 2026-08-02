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
        Assert.Contains(report.Checklist, item => item.Id == "parity-samples-attached" && item.Status == "present");
        Assert.Contains(report.NextActions, action => action.Contains("run_zero_php_final_gate_checklist.sh", StringComparison.OrdinalIgnoreCase));
        Assert.True(report.ChecklistCompletePercent < 100);
        Assert.False(string.Equals(report.Checklist.First(item => item.Id == "staging-smoke-price").Status, "present", StringComparison.Ordinal));
        Assert.Contains(report.Blockers, blocker => blocker.Contains("Authenticated CloudPanel smoke keys", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildReportBecomesReadyOnlyWhenAllValidatedEvidenceExists()
    {
        var repoRoot = RepoHostEnvironment.FindRepoRoot();
        var releaseRoot = Path.Combine(Path.GetTempPath(), "ecomae-ready-release-" + Guid.NewGuid().ToString("N"));
        try
        {
            var evidence = Path.Combine(releaseRoot, "docs", "migration", "evidence", "decommission");
            Directory.CreateDirectory(Path.Combine(evidence, "public-probes"));
            Directory.CreateDirectory(Path.Combine(evidence, "staging-smoke"));
            Directory.CreateDirectory(Path.Combine(evidence, "parity-samples"));
            Directory.CreateDirectory(Path.Combine(releaseRoot, "deploy", "aspnet"));
            Directory.CreateDirectory(Path.Combine(releaseRoot, "scripts"));

            File.WriteAllText(Path.Combine(evidence, "public-probes", "www-zero-php-completion.json"), "{}");
            File.WriteAllText(Path.Combine(evidence, "public-probes", "www-php-decommission-readiness.json"), "{}");
            File.WriteAllText(Path.Combine(evidence, "staging-smoke", "price-lookup-aspnet.json"), """{"brand":"TOYOTA","offers":[]}""");
            File.WriteAllText(Path.Combine(evidence, "staging-smoke", "catalog-status-aspnet.json"), """{"status":"ok"}""");
            File.WriteAllText(Path.Combine(evidence, "staging-smoke", "surface-digests-aspnet.json"), """{"ok":true,"authenticatedDigest200Count":1,"routes":[{"route":"/cp/dashboard-summary","status":200}]}""");
            File.WriteAllText(Path.Combine(evidence, "parity-samples", "sample.json"), """{"route":"/api/v1/price/lookup"}""");
            File.WriteAllText(Path.Combine(evidence, "RELEASE_OWNER_APPROVAL.md"), "APPROVED_TO_REMOVE_PHP_FALLBACK\n");
            File.Copy(Path.Combine(repoRoot, "deploy", "aspnet", "nginx-price-lookup-shadow-example.conf"), Path.Combine(releaseRoot, "deploy", "aspnet", "nginx-price-lookup-shadow-example.conf"), overwrite: true);
            File.Copy(Path.Combine(repoRoot, "deploy", "aspnet", "nginx-api-shadow-example.conf"), Path.Combine(releaseRoot, "deploy", "aspnet", "nginx-api-shadow-example.conf"), overwrite: true);
            File.Copy(Path.Combine(repoRoot, "deploy", "aspnet", "nginx-surface-digests-shadow-example.conf"), Path.Combine(releaseRoot, "deploy", "aspnet", "nginx-surface-digests-shadow-example.conf"), overwrite: true);
            File.Copy(Path.Combine(repoRoot, "deploy", "aspnet", "nginx-storefront-digests-shadow-example.conf"), Path.Combine(releaseRoot, "deploy", "aspnet", "nginx-storefront-digests-shadow-example.conf"), overwrite: true);
            File.Copy(Path.Combine(repoRoot, "scripts", "cloudpanel_capture_final_gate_artifacts.sh"), Path.Combine(releaseRoot, "scripts", "cloudpanel_capture_final_gate_artifacts.sh"), overwrite: true);
            File.Copy(Path.Combine(repoRoot, "scripts", "rollback_aspnet_foundation.sh"), Path.Combine(releaseRoot, "scripts", "rollback_aspnet_foundation.sh"), overwrite: true);
            File.Copy(Path.Combine(repoRoot, "scripts", "run_zero_php_final_gate_checklist.sh"), Path.Combine(releaseRoot, "scripts", "run_zero_php_final_gate_checklist.sh"), overwrite: true);

            var report = new PhpDecommissionReadinessReporter(new RepoHostEnvironment { ContentRootPath = releaseRoot }).BuildReport();
            Assert.True(report.ReadyToRemovePhp);
            Assert.Equal("ready-for-php-removal", report.Status);
            Assert.Equal(100, report.ChecklistCompletePercent);

            var completion = new ZeroPhpCompletionReporter(new PhpDecommissionReadinessReporter(new RepoHostEnvironment { ContentRootPath = releaseRoot })).BuildReport();
            Assert.Equal(100, completion.OverallCompletePercent);
            Assert.Equal(0, completion.OverallPendingPercent);
            Assert.Equal("ready-for-php-removal", completion.Status);
        }
        finally
        {
            if (Directory.Exists(releaseRoot))
            {
                Directory.Delete(releaseRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void BuildReportReadsEvidenceFromPackedReleaseContentRoot()
    {
        var repoRoot = RepoHostEnvironment.FindRepoRoot();
        var releaseRoot = Path.Combine(Path.GetTempPath(), "ecomae-packed-release-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(releaseRoot, "docs", "migration", "evidence"));
            Directory.CreateDirectory(Path.Combine(releaseRoot, "deploy", "aspnet"));
            Directory.CreateDirectory(Path.Combine(releaseRoot, "scripts"));
            CopyDirectory(Path.Combine(repoRoot, "docs", "migration", "evidence", "decommission"), Path.Combine(releaseRoot, "docs", "migration", "evidence", "decommission"));
            File.Copy(Path.Combine(repoRoot, "docs", "migration", "PHP_DECOMMISSION_READINESS.md"), Path.Combine(releaseRoot, "docs", "migration", "PHP_DECOMMISSION_READINESS.md"), overwrite: true);
            File.Copy(Path.Combine(repoRoot, "deploy", "aspnet", "nginx-price-lookup-shadow-example.conf"), Path.Combine(releaseRoot, "deploy", "aspnet", "nginx-price-lookup-shadow-example.conf"), overwrite: true);
            File.Copy(Path.Combine(repoRoot, "deploy", "aspnet", "nginx-api-shadow-example.conf"), Path.Combine(releaseRoot, "deploy", "aspnet", "nginx-api-shadow-example.conf"), overwrite: true);
            File.Copy(Path.Combine(repoRoot, "deploy", "aspnet", "nginx-surface-digests-shadow-example.conf"), Path.Combine(releaseRoot, "deploy", "aspnet", "nginx-surface-digests-shadow-example.conf"), overwrite: true);
            File.Copy(Path.Combine(repoRoot, "deploy", "aspnet", "nginx-storefront-digests-shadow-example.conf"), Path.Combine(releaseRoot, "deploy", "aspnet", "nginx-storefront-digests-shadow-example.conf"), overwrite: true);
            File.Copy(Path.Combine(repoRoot, "scripts", "cloudpanel_capture_final_gate_artifacts.sh"), Path.Combine(releaseRoot, "scripts", "cloudpanel_capture_final_gate_artifacts.sh"), overwrite: true);
            File.Copy(Path.Combine(repoRoot, "scripts", "rollback_aspnet_foundation.sh"), Path.Combine(releaseRoot, "scripts", "rollback_aspnet_foundation.sh"), overwrite: true);

            var report = new PhpDecommissionReadinessReporter(new RepoHostEnvironment { ContentRootPath = releaseRoot }).BuildReport();

            Assert.False(report.ReadyToRemovePhp);
            Assert.Contains(report.Checklist, item => item.Id == "public-probes" && item.Status == "present");
            Assert.Contains(report.Checklist, item => item.Id == "exact-route-shadows-only" && item.Status == "present");
            Assert.Contains(report.Checklist, item => item.Id == "cloudpanel-capture-script" && item.Status == "present");
            Assert.Contains(report.Checklist, item => item.Id == "rollback-validated" && item.Status == "present");
            Assert.True(report.ChecklistCompleteCount >= 4);
        }
        finally
        {
            if (Directory.Exists(releaseRoot))
            {
                Directory.Delete(releaseRoot, recursive: true);
            }
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private sealed class RepoHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "EcomAE.Platform.Tests";
        public string ContentRootPath { get; set; } = FindRepoRoot();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

        public static string FindRepoRoot()
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
