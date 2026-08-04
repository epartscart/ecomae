using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpOmsPayRefundDryRunTests
{
    private static readonly CpShopOrderDigest SampleOrder = new(
        Id: 42, TimeUnix: 1, UserId: 9, Status: 1, Paid: 1, PaidType: 1, OfficeId: 1, SuccessfullyCreated: 1, CountItems: 1, OrderSum: 50m);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = CpOmsPayRefundDryRun.EvaluateAgainstOrders(
            [SampleOrder],
            new CpOmsPayRefundRequest(42, true, ConfirmWrites: true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
    }

    [Fact]
    public void ValidRefundIsDryRunValidated()
    {
        var r = CpOmsPayRefundDryRun.EvaluateAgainstOrders(
            [SampleOrder],
            new CpOmsPayRefundRequest(42, true, PaidSum: 50m));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldWrite);
        Assert.False(r.CutoverAllowed);
    }
}
