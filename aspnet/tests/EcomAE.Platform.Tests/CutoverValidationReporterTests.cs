using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CutoverValidationReporterTests
{
    [Fact]
    public void BuildReportKeepsPhpFallbackAndManualApprovalGates()
    {
        var report = new CutoverValidationReporter().BuildReport();

        Assert.Equal("validation-plan-ready-traffic-cutover-blocked", report.Status);
        Assert.Contains(report.RollbackControls, control => control.Contains("PHP authoritative", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.ApprovalGates, gate => gate.Contains("manual release owner", StringComparison.OrdinalIgnoreCase));
    }
}
