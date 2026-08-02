using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontParityReporterTests
{
    [Fact]
    public void BuildReportNamesCheckoutAndSeoGaps()
    {
        var report = new StorefrontParityReporter().BuildReport();

        Assert.Equal("Storefront / customer commerce", report.Surface);
        Assert.Equal("presentation-shell-scaffolded-awaiting-staging", report.Status);
        Assert.Contains(report.VerifiedCapabilities, capability => capability.Contains("Customer session gate", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.VerifiedCapabilities, capability => capability.Contains("checkout", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.VerifiedCapabilities, capability => capability.Contains("ECOMAE_CUSTOMER_COOKIE_HEADER", StringComparison.Ordinal));
        Assert.Contains(report.VerifiedCapabilities, capability => capability.Contains("not required for ReadyToRemovePhp", StringComparison.Ordinal));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("SEO metadata", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("StorefrontAspNetEnabled=false", StringComparison.Ordinal));
    }
}
