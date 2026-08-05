using EcomAE.Platform.Configuration;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Microsoft.Extensions.Options;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpReferenceModeReporterTests
{
    [Fact]
    public void BuildReportDeclaresAspNetPrimaryPhpReferenceWithoutFlippingCutover()
    {
        var reporter = new PhpReferenceModeReporter(
            Options.Create(new PhpReferenceOptions
            {
                Enabled = true,
                ArchitectureConfirmed = true,
                KeepPhpProjectAvailable = true,
                WwwPhpBaseUrl = "https://www.ecomae.com/",
                TenantPhpBaseUrl = "https://www.epartscart.com/",
                DedicatedCpPhpBaseUrl = "https://cp.ecomae.com/",
                AspNetPrimaryBaseUrl = "https://www.ecomae.com/"
            }),
            Options.Create(new MigrationRouteCutoverOptions
            {
                RequirePhpFallback = true,
                StorefrontAspNetEnabled = false,
                AdminAspNetEnabled = false
            }));

        var report = reporter.BuildReport();

        Assert.Equal("aspnet-primary-intent-php-reference-retained", report.Status);
        Assert.Equal("aspnet-primary-php-reference", report.Mode);
        Assert.True(report.Enabled);
        Assert.True(report.ArchitectureConfirmed);
        Assert.True(report.KeepPhpProjectAvailable);
        Assert.False(report.CutoverAllowed);
        Assert.False(report.ReadyForPhpRemoval);
        Assert.True(report.RequirePhpFallback);
        Assert.False(report.StorefrontAspNetEnabled);
        Assert.False(report.AdminAspNetEnabled);
        Assert.Equal("https://www.ecomae.com", report.WwwPhpBaseUrl);
        Assert.Contains(report.ComparePairs, p => p.Area == "marketing" && p.PhpUrl.EndsWith("/index.php", StringComparison.Ordinal));
        Assert.Contains(report.ComparePairs, p => p.Area == "cp" && p.PhpUrl.Contains("/cp/shop/orders/orders", StringComparison.Ordinal));
        Assert.Contains(report.ComparePairs, p => p.AspNetUrl.Contains("/erp/app", StringComparison.Ordinal));
        Assert.Contains(report.HardLocks, lockLine => lockLine.Contains("RELEASE_OWNER_APPROVAL.md", StringComparison.Ordinal)
            && lockLine.Contains("KeepPhpProjectAvailable", StringComparison.Ordinal));
        Assert.Contains(report.OperatorSteps, step => step.Contains("--keep-php-fallback", StringComparison.Ordinal));
        Assert.Equal("/migration/php-reference-mode", EcomAeRoutes.MigrationPhpReferenceMode);
    }

    [Fact]
    public void CutoverValidationMentionsPhpReferenceRetention()
    {
        var report = new CutoverValidationReporter().BuildReport();
        Assert.Contains(report.RollbackControls, c => c.Contains("php-reference-mode", StringComparison.OrdinalIgnoreCase)
            || c.Contains("PHP project stays available as reference", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.ApprovalGates, g => g.Contains("PHP reference host remains reachable", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.RollbackControls, c => c.Contains("RequirePhpFallback=true", StringComparison.Ordinal));
    }
}
