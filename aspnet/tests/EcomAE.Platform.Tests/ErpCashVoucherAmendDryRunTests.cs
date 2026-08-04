using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCashVoucherAmendDryRunTests
{
    private static ErpCashEntryDigest Entry(long id, string reference = "REF", string note = "n") =>
        new(id, 1, "Cash", "cash", 1_700_000_000, 1, 50m, reference, note);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var result = ErpCashVoucherAmendDryRun.EvaluateAgainstEntries(
            [Entry(9)],
            new ErpCashVoucherAmendRequest(9, "NEW", "note", ConfirmWrites: true));
        Assert.Equal("dry-run-confirm-refused", result.Status);
        Assert.Equal(0, result.Writes);
        Assert.False(result.CutoverAllowed);
    }

    [Fact]
    public void AmendIsDryRunValidated()
    {
        var result = ErpCashVoucherAmendDryRun.EvaluateAgainstEntries(
            [Entry(9, "OLD", "a")],
            new ErpCashVoucherAmendRequest(9, "NEW", "b"));
        Assert.Equal("dry-run-validated", result.Status);
        Assert.Equal("ok", result.ValidationCode);
        Assert.True(result.WouldWrite);
        Assert.Contains("NOT executed", result.SimulatedSql, StringComparison.Ordinal);
    }

    [Fact]
    public void NoChangeDoesNotClaimWrite()
    {
        var result = ErpCashVoucherAmendDryRun.EvaluateAgainstEntries(
            [Entry(9, "SAME", "n")],
            new ErpCashVoucherAmendRequest(9, "SAME", "n"));
        Assert.Equal("no_change", result.ValidationCode);
        Assert.False(result.WouldWrite);
    }
}
