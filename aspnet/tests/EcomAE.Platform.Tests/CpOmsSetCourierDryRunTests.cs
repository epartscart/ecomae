using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpOmsSetCourierDryRunTests
{
    private static CpShopOrderDigest Order(long id, int paid = 0) =>
        new(id, 1_700_000_000, 5, 1, paid, 0, 1, 1, 2, 120m);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = CpOmsSetCourierDryRun.EvaluateAgainstOrders(
            [Order(9)], new CpOmsSetCourierRequest(9, 25m, "AE", true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void PaidOrderIsInvalid()
    {
        var r = CpOmsSetCourierDryRun.EvaluateAgainstOrders(
            [Order(9, paid: 1)], new CpOmsSetCourierRequest(9, 25m));
        Assert.Equal("order_already_paid", r.ValidationCode);
        Assert.False(r.WouldWrite);
    }

    [Fact]
    public void NegativeFeeIsInvalid()
    {
        var r = CpOmsSetCourierDryRun.EvaluateAgainstOrders(
            [Order(9)], new CpOmsSetCourierRequest(9, -1m));
        Assert.Equal("negative_courier_fee", r.ValidationCode);
    }

    [Fact]
    public void SetCourierIsDryRunValidated()
    {
        var r = CpOmsSetCourierDryRun.EvaluateAgainstOrders(
            [Order(9)], new CpOmsSetCourierRequest(9, 35.5m, "ae"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldWrite);
        Assert.Equal(0, r.Writes);
        Assert.Equal("AE", r.Country);
        Assert.Contains(r.SimulatedSql, s => s.Contains("NOT executed", StringComparison.Ordinal));
    }
}
