using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpOmsSetItemsStatusDryRunTests
{
    private static CpShopOrderDigest Order(long id) =>
        new(id, 1_700_000_000, 9, 1, 1, 0, 1, 1, 2, 100m);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var result = CpOmsSetItemsStatusDryRun.EvaluateAgainstOrders(
            [Order(55)],
            new CpOmsSetItemsStatusRequest(55, 3, [10, 11], ConfirmWrites: true));
        Assert.Equal("dry-run-confirm-refused", result.Status);
        Assert.Equal(0, result.Writes);
        Assert.False(result.CutoverAllowed);
    }

    [Fact]
    public void BulkStatusIsDryRunValidated()
    {
        var result = CpOmsSetItemsStatusDryRun.EvaluateAgainstOrders(
            [Order(55)],
            new CpOmsSetItemsStatusRequest(55, 3, [10, 11, 10]));
        Assert.Equal("dry-run-validated", result.Status);
        Assert.True(result.WouldWrite);
        Assert.Equal(2, result.ItemIds.Count);
        Assert.Contains("NOT executed", result.SimulatedSql, StringComparison.Ordinal);
    }
}
