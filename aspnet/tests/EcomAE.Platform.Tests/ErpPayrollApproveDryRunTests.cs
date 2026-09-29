using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPayrollApproveDryRunTests
{
    [Fact]
    public void DryRunUsesLiveAspNetWriterEvidence()
    {
        var result = new ErpPayrollApproveDryRun().Evaluate(new ErpPayrollApproveRequest(12, "RUN-12"));

        Assert.Equal("dry-run-validated", result.Status);
        Assert.Equal(0, result.Writes);
        Assert.False(result.PhpAuthoritative);
        Assert.Contains("no write was performed", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfirmedWriteUsesExplicitGate()
    {
        var result = new ErpPayrollApproveDryRun().Evaluate(new ErpPayrollApproveRequest(12, "RUN-12", true));

        Assert.Equal("confirm_writes_refused", result.ValidationCode);
        Assert.Contains("confirm_writes=true", result.Detail, StringComparison.Ordinal);
        Assert.False(result.PhpAuthoritative);
    }
}
