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
        Assert.Contains(report.RequiredSignals, signal => signal.Contains("ensure→issue", StringComparison.OrdinalIgnoreCase)
            || signal.Contains("ensure", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.RequiredSignals, signal => signal.Contains("compare_", StringComparison.Ordinal));
        Assert.Contains(report.RollbackControls, control => control.Contains("PHP authoritative", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.RollbackControls, control => control.Contains("RequirePhpFallback=true", StringComparison.Ordinal));
        Assert.Contains(report.ApprovalGates, gate => gate.Contains("RELEASE_OWNER_APPROVAL.md", StringComparison.Ordinal));
        Assert.Contains(report.ApprovalGates, gate => gate.Contains("location =", StringComparison.Ordinal));
    }
}
