using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpOmsDeleteOrdersDryRunTests
{
    private static CpShopOrderDigest Order(long id, int paid = 0) =>
        new(id, 1_700_000_000, 5, 1, paid, 0, 1, 1, 2, 120m);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = CpOmsDeleteOrdersDryRun.EvaluateAgainstOrders(
            [Order(9)], new CpOmsDeleteOrdersRequest([9], true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
    }

    [Fact]
    public void PaidOrderIsInvalid()
    {
        var r = CpOmsDeleteOrdersDryRun.EvaluateAgainstOrders(
            [Order(9, paid: 1)], new CpOmsDeleteOrdersRequest([9]));
        Assert.Equal("orders_paid", r.ValidationCode);
        Assert.False(r.WouldWrite);
    }

    [Fact]
    public void DeleteIsDryRunValidated()
    {
        var r = CpOmsDeleteOrdersDryRun.EvaluateAgainstOrders(
            [Order(9), Order(10)], new CpOmsDeleteOrdersRequest([9, 10]));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldWrite);
        Assert.Equal(0, r.Writes);
        Assert.Equal(2, r.OrderIds.Count);
        Assert.Contains(r.SimulatedSql, s => s.Contains("NOT executed", StringComparison.Ordinal));
    }
}
