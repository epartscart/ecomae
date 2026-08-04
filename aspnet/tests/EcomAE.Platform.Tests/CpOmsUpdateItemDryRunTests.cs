using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpOmsUpdateItemDryRunTests
{
    private static readonly CpShopOrderDigest SampleOrder = new(
        Id: 42, TimeUnix: 1, UserId: 9, Status: 1, Paid: 0, PaidType: 0, OfficeId: 1, SuccessfullyCreated: 1, CountItems: 1, OrderSum: 12m);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = CpOmsUpdateItemDryRun.EvaluateAgainstOrders(
            [SampleOrder],
            new CpOmsUpdateItemRequest(42, 7, 12m, 2, "Bosch", "0986", ConfirmWrites: true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void ValidUpdateIsDryRunValidated()
    {
        var r = CpOmsUpdateItemDryRun.EvaluateAgainstOrders(
            [SampleOrder],
            new CpOmsUpdateItemRequest(42, 7, 12m, 2, "Bosch", "0986"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.True(r.PhpAuthoritative);
    }

    [Fact]
    public void MissingOrderIsRejected()
    {
        var r = CpOmsUpdateItemDryRun.EvaluateAgainstOrders(
            [],
            new CpOmsUpdateItemRequest(99, 7, 12m, 2, "Bosch", "0986"));
        Assert.Equal("order_not_in_digest_window", r.ValidationCode);
    }
}
