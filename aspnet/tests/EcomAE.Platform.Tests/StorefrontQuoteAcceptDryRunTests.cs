using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontQuoteAcceptDryRunTests
{
    private static CpQuoteRequestsRowDigest Quote(long id, long userId, string status = "quoted") =>
        new(id, userId, 0, status, 1_700_000_000, 1_700_000_000, 0, 0);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = StorefrontQuoteAcceptDryRun.EvaluateAgainstQuotes(
            5, [Quote(9, 5)], new StorefrontQuoteAcceptRequest(9, true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void NonQuotedIsInvalid()
    {
        var r = StorefrontQuoteAcceptDryRun.EvaluateAgainstQuotes(
            5, [Quote(9, 5, "draft")], new StorefrontQuoteAcceptRequest(9));
        Assert.Equal("quote_not_quoted", r.ValidationCode);
    }

    [Fact]
    public void OtherUsersQuoteIsInvalid()
    {
        var r = StorefrontQuoteAcceptDryRun.EvaluateAgainstQuotes(
            5, [Quote(9, 99)], new StorefrontQuoteAcceptRequest(9));
        Assert.Equal("quote_not_in_digest_window", r.ValidationCode);
    }

    [Fact]
    public void AcceptIsDryRunValidated()
    {
        var r = StorefrontQuoteAcceptDryRun.EvaluateAgainstQuotes(
            5, [Quote(9, 5)], new StorefrontQuoteAcceptRequest(9));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldWrite);
        Assert.Equal(0, r.Writes);
        Assert.Contains(r.SimulatedSql, s => s.Contains("NOT executed", StringComparison.Ordinal));
        Assert.Contains(r.SimulatedSql, s => s.Contains("shop_carts", StringComparison.Ordinal));
    }
}
