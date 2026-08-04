using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpOmsSetViewedDryRunTests
{
    private static CpShopOrderDigest Order(long id) =>
        new(id, 1_700_000_000, 5, 1, 0, 0, 1, 1, 2, 120m);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = CpOmsSetViewedDryRun.EvaluateAgainstOrders(
            [Order(9)], new CpOmsSetViewedRequest([9], 1, true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void InvalidFlagIsRejected()
    {
        var r = CpOmsSetViewedDryRun.EvaluateAgainstOrders(
            [Order(9)], new CpOmsSetViewedRequest([9], 2));
        Assert.Equal("invalid_viewed_flag", r.ValidationCode);
    }

    [Fact]
    public void MissingOrderIsInvalid()
    {
        var r = CpOmsSetViewedDryRun.EvaluateAgainstOrders(
            [Order(99)], new CpOmsSetViewedRequest([9], 1));
        Assert.Equal("order_not_in_digest_window", r.ValidationCode);
    }

    [Fact]
    public void SetViewedIsDryRunValidated()
    {
        var r = CpOmsSetViewedDryRun.EvaluateAgainstOrders(
            [Order(9), Order(10)], new CpOmsSetViewedRequest([9, 10], 1));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldWrite);
        Assert.Equal(0, r.Writes);
        Assert.Contains(r.SimulatedSql, s => s.Contains("viewed_flag", StringComparison.Ordinal));
    }
}
