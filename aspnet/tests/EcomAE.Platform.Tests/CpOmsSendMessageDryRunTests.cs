using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpOmsSendMessageDryRunTests
{
    private static CpShopOrderDigest Order(long id) =>
        new(id, 1_700_000_000, 5, 1, 0, 0, 1, 1, 2, 120m);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = CpOmsSendMessageDryRun.EvaluateAgainstOrders(
            [Order(9)], new CpOmsSendMessageRequest(9, "hello", ConfirmWrites: true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
        Assert.False(r.WouldWrite);
    }

    [Fact]
    public void EmptyTextIsInvalid()
    {
        var r = CpOmsSendMessageDryRun.EvaluateAgainstOrders(
            [Order(9)], new CpOmsSendMessageRequest(9, "  "));
        Assert.Equal("dry-run-invalid", r.Status);
        Assert.Equal("message_text_required", r.ValidationCode);
        Assert.Equal(0, r.Writes);
    }

    [Fact]
    public void SendMessageIsDryRunValidated()
    {
        var r = CpOmsSendMessageDryRun.EvaluateAgainstOrders(
            [Order(9)], new CpOmsSendMessageRequest(9, "Parts ETA tomorrow", 42));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldWrite);
        Assert.Equal(0, r.Writes);
        Assert.True(r.WritesBlocked);
        Assert.False(r.CutoverAllowed);
        Assert.Equal(42, r.ItemId);
        Assert.Contains(r.SimulatedSql, s => s.Contains("shop_orders_messages", StringComparison.Ordinal));
        Assert.Contains(r.SimulatedSql, s => s.Contains("NOT executed", StringComparison.Ordinal));
    }

    [Fact]
    public void OrderMissingFromDigestIsInvalid()
    {
        var r = CpOmsSendMessageDryRun.EvaluateAgainstOrders(
            [Order(1)], new CpOmsSendMessageRequest(99, "x"));
        Assert.Equal("order_not_in_digest_window", r.ValidationCode);
        Assert.False(r.WouldWrite);
    }
}
