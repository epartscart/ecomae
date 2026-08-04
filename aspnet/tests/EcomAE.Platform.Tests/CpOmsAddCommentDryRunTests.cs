using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpOmsAddCommentDryRunTests
{
    private static CpShopOrderDigest Order(long id, int status = 1, int paid = 0) =>
        new(id, 1_700_000_000, 5, status, paid, 0, 0, 0, 0, 0m);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = CpOmsAddCommentDryRun.EvaluateAgainstOrders(
            [Order(55)],
            new CpOmsAddCommentRequest(55, "note", true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void EmptyTextIsInvalid()
    {
        var r = CpOmsAddCommentDryRun.EvaluateAgainstOrders(
            [Order(55)],
            new CpOmsAddCommentRequest(55, "  "));
        Assert.Equal("comment_text_required", r.ValidationCode);
    }

    [Fact]
    public void MissingOrderIsInvalid()
    {
        var r = CpOmsAddCommentDryRun.EvaluateAgainstOrders(
            [Order(99)],
            new CpOmsAddCommentRequest(55, "hello"));
        Assert.Equal("order_not_in_digest_window", r.ValidationCode);
    }

    [Fact]
    public void AddCommentIsDryRunValidated()
    {
        var r = CpOmsAddCommentDryRun.EvaluateAgainstOrders(
            [Order(55)],
            new CpOmsAddCommentRequest(55, "Called customer"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldWrite);
        Assert.Equal(0, r.Writes);
        Assert.Contains(r.SimulatedSql, s => s.Contains("NOT executed", StringComparison.Ordinal));
    }
}
