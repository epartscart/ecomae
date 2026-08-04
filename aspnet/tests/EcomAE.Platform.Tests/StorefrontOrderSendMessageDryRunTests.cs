using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontOrderSendMessageDryRunTests
{
    private static StorefrontOrderDigest Order(long id, int status = 1, int paid = 0) =>
        new(id, 1_700_000_000, paid, 1, status);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = StorefrontOrderSendMessageDryRun.EvaluateAgainstOrders(
            5, [Order(55)], new StorefrontOrderSendMessageRequest(55, "hi", true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void EmptyTextIsInvalid()
    {
        var r = StorefrontOrderSendMessageDryRun.EvaluateAgainstOrders(
            5, [Order(55)], new StorefrontOrderSendMessageRequest(55, " "));
        Assert.Equal("message_text_required", r.ValidationCode);
    }

    [Fact]
    public void MissingOrderIsInvalid()
    {
        var r = StorefrontOrderSendMessageDryRun.EvaluateAgainstOrders(
            5, [Order(99)], new StorefrontOrderSendMessageRequest(55, "hello"));
        Assert.Equal("order_not_owned", r.ValidationCode);
    }

    [Fact]
    public void SendMessageIsDryRunValidated()
    {
        var r = StorefrontOrderSendMessageDryRun.EvaluateAgainstOrders(
            5, [Order(55)], new StorefrontOrderSendMessageRequest(55, "Where is my order?"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldWrite);
        Assert.Equal(0, r.Writes);
        Assert.Contains(r.SimulatedSql, s => s.Contains("is_customer", StringComparison.Ordinal));
    }
}
