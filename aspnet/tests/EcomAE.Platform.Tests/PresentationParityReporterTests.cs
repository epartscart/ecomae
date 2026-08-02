using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PresentationParityReporterTests
{
    [Fact]
    public void BuildReportTracksAllOperatorAndStorefrontSurfaces()
    {
        var report = new PresentationParityReporter().BuildReport();

        Assert.Equal("presentation-shell-scaffolded", report.Status);
        Assert.Contains(report.Surfaces, surface => surface.SurfaceKey == "cp" && surface.Stylesheets.Count > 0);
        Assert.Contains(report.Surfaces, surface => surface.SurfaceKey == "erp");
        Assert.Contains(report.Surfaces, surface => surface.SurfaceKey == "bos" && surface.LegacyChromeSource.Contains("epc_bos_shell", StringComparison.Ordinal));
        Assert.Contains(report.Surfaces, surface => surface.SurfaceKey == "storefront");
        Assert.Contains(report.Guarantees, guarantee => guarantee.Contains("digest JSON", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.RemainingGaps, gap => gap.Contains("Pixel/DOM parity", StringComparison.OrdinalIgnoreCase));
    }
}
