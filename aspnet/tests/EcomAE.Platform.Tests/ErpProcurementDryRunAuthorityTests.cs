using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpProcurementDryRunAuthorityTests
{
    [Fact]
    public void RequisitionConversionDryRun_ReportsAspNetOwnership()
    {
        var result = new ErpProcReqConvertDryRun()
            .Evaluate(new ErpProcReqConvertRequest(7));

        Assert.Equal("dry-run-validated", result.Status);
        Assert.False(result.PhpAuthoritative);
        Assert.False(result.CutoverAllowed);
        Assert.Equal(0, result.Writes);
        Assert.True(result.WritesBlocked);
    }

    [Fact]
    public void RfqSaveDryRun_ReportsAspNetOwnershipAndRefusesConfirmation()
    {
        var dryRun = new ErpSaveRfqDryRun();
        var result = dryRun.Evaluate(new ErpSaveRfqRequest(Code: "DUMMY"));

        Assert.Equal("dry-run-validated", result.Status);
        Assert.False(result.PhpAuthoritative);
        Assert.False(result.CutoverAllowed);
        Assert.Equal(
            "confirm_writes_refused",
            dryRun.Evaluate(new ErpSaveRfqRequest(Code: "DUMMY", ConfirmWrites: true)).ValidationCode);
    }
}
