using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpOmsSetItemStatusDryRunTests
{
    private static CpShopOrderDigest Order(long id) =>
        new(id, 1_700_000_000, 9, 1, 1, 0, 1, 1, 2, 100m);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var result = CpOmsSetItemStatusDryRun.EvaluateAgainstOrders(
            [Order(55)],
            new CpOmsSetItemStatusRequest(55, 100, 3, ConfirmWrites: true));

        Assert.Equal("dry-run-confirm-refused", result.Status);
        Assert.Equal(0, result.Writes);
        Assert.True(result.WritesBlocked);
        Assert.False(result.CutoverAllowed);
        Assert.False(result.WouldWrite);
    }

    [Fact]
    public void ValidOrderInWindowIsDryRunValidated()
    {
        var result = CpOmsSetItemStatusDryRun.EvaluateAgainstOrders(
            [Order(55)],
            new CpOmsSetItemStatusRequest(55, 100, 3));

        Assert.Equal("dry-run-validated", result.Status);
        Assert.Equal("ok", result.ValidationCode);
        Assert.True(result.WouldWrite);
        Assert.Equal(0, result.Writes);
        Assert.Contains(result.SimulatedSql, s => s.Contains("shop_orders_items", StringComparison.Ordinal));
        Assert.Contains(result.SimulatedSql, s => s.Contains("NOT executed", StringComparison.Ordinal));
    }

    [Fact]
    public void OrderOutsideDigestWindowIsInvalid()
    {
        var result = CpOmsSetItemStatusDryRun.EvaluateAgainstOrders(
            [Order(1)],
            new CpOmsSetItemStatusRequest(99, 100, 3));

        Assert.Equal("order_not_in_digest_window", result.ValidationCode);
        Assert.False(result.WouldWrite);
    }

    [Fact]
    public void InvalidIdsAreRejected()
    {
        var result = CpOmsSetItemStatusDryRun.EvaluateAgainstOrders(
            [Order(55)],
            new CpOmsSetItemStatusRequest(55, 0, 3));

        Assert.Equal("invalid_request", result.ValidationCode);
    }
}
