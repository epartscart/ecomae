using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontQuoteAddItemDryRunTests
{
    private static CpQuoteRequestsRowDigest Quote(long id, long userId, string status = "draft") =>
        new(id, userId, 0, status, 1_700_000_000, 1_700_000_000, 0, 0);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = StorefrontQuoteAddItemDryRun.EvaluateAgainstQuotes(
            5, [Quote(9, 5)], new StorefrontQuoteAddItemRequest(2, "Bosch", "0986", 1, true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void NonType2IsInvalid()
    {
        var r = StorefrontQuoteAddItemDryRun.EvaluateAgainstQuotes(
            5, [], new StorefrontQuoteAddItemRequest(1, "Bosch", "0986"));
        Assert.Equal("product_type_unsupported", r.ValidationCode);
    }

    [Fact]
    public void AddToExistingDraftIsValidated()
    {
        var r = StorefrontQuoteAddItemDryRun.EvaluateAgainstQuotes(
            5, [Quote(9, 5)], new StorefrontQuoteAddItemRequest(2, "Bosch", "0986", 2));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.False(r.WouldCreateDraft);
        Assert.Equal(9, r.QuoteId);
        Assert.Equal(0, r.Writes);
    }

    [Fact]
    public void CreateDraftWhenNoneExists()
    {
        var r = StorefrontQuoteAddItemDryRun.EvaluateAgainstQuotes(
            5, [Quote(9, 5, "submitted")], new StorefrontQuoteAddItemRequest(2, "Bosch", "0986"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldCreateDraft);
        Assert.Contains(r.SimulatedSql, s => s.Contains("shop_quote_requests", StringComparison.Ordinal));
    }
}
