using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LiveSurfaceLinkReporterTests
{
    [Fact]
    public void BuildReportCataloguesSuperCpTenantAndAspNetDiagnostics()
    {
        var report = new LiveSurfaceLinkReporter().BuildReport();

        Assert.Equal("www.ecomae.com", report.PlatformHost);
        Assert.Contains(report.Links, link => link.HostClass == "super-cp" && link.Url.Contains("/BOS/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Links, link => link.HostClass == "super-cp" && link.Url.Contains("/CP/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Links, link => link.HostClass == "super-cp" && link.Url.Contains("/ERP/", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Links, link => link.HostClass == "tenant" && link.Url.Contains("electronicae.com", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Links, link => link.HostClass == "aspnet-diagnostics" && link.StackToday == "aspnet");
        Assert.Contains(report.Links, link => link.Surface.Contains("Price lookup", StringComparison.OrdinalIgnoreCase) && link.StackToday == "aspnet");
        Assert.Contains(report.CutoverRules, rule => rule.Contains("Broad", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.NextActions, action => action.Contains("cloudpanel_capture_final_gate_artifacts.sh", StringComparison.Ordinal));
    }
}
