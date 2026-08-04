using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontQuoteSubmitDryRunTests
{
    private static CpQuoteRequestsRowDigest Quote(long id, long userId, string status = "draft") =>
        new(id, userId, 0, status, 1_700_000_000, 1_700_000_000, 0, 0);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = StorefrontQuoteSubmitDryRun.EvaluateAgainstQuotes(
            5, [Quote(9, 5)], new StorefrontQuoteSubmitRequest(9, "x", true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void NonDraftIsInvalid()
    {
        var r = StorefrontQuoteSubmitDryRun.EvaluateAgainstQuotes(
            5, [Quote(9, 5, "submitted")], new StorefrontQuoteSubmitRequest(9));
        Assert.Equal("quote_not_draft", r.ValidationCode);
    }

    [Fact]
    public void OtherUsersQuoteIsInvalid()
    {
        var r = StorefrontQuoteSubmitDryRun.EvaluateAgainstQuotes(
            5, [Quote(9, 99)], new StorefrontQuoteSubmitRequest(9));
        Assert.Equal("quote_not_in_digest_window", r.ValidationCode);
    }

    [Fact]
    public void SubmitIsDryRunValidated()
    {
        var r = StorefrontQuoteSubmitDryRun.EvaluateAgainstQuotes(
            5, [Quote(9, 5)], new StorefrontQuoteSubmitRequest(9, "Please quote ASAP"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldWrite);
        Assert.Equal(0, r.Writes);
        Assert.Contains(r.SimulatedSql, s => s.Contains("NOT executed", StringComparison.Ordinal));
    }
}
