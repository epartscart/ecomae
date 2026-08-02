using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PythonSidecarCatalogReporterTests
{
    [Fact]
    public void BuildReportKeepsAspNetCoreAsPlatformAndPythonForStrongSidecars()
    {
        var report = new PythonSidecarCatalogReporter().BuildReport();

        Assert.Contains("ASP.NET Core primary platform", report.TargetArchitecture, StringComparison.Ordinal);
        Assert.Contains("PHP remains fallback only", report.PhpRetirementRule, StringComparison.Ordinal);
        Assert.Contains(report.Workloads, workload => workload.Key == "price-ingest" && workload.RuntimeOwner == "python-ai-service");
        Assert.Contains(report.Workloads, workload => workload.Key == "analytics-forecasting" && workload.PythonAdvantage.Contains("forecasting", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.IntegrationRules, rule => rule.Contains("database transactions", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.NextImplementationSlices, slice => slice.Contains("price API facade", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.NextImplementationSlices, slice => slice.Contains("ASP.NET Core worker", StringComparison.OrdinalIgnoreCase));
    }
}
