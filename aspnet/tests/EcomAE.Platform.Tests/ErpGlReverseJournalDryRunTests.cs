using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpGlReverseJournalDryRunTests
{
    private static ErpGlJournalDigest Journal(long id) =>
        new(id, $"JV-{id}", 1_700_000_000, "manual", 0, "posted", 100m);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = ErpGlReverseJournalDryRun.EvaluateAgainstJournals(
            [Journal(9)], new ErpGlReverseJournalRequest(9, "x", true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void ReverseIsDryRunValidated()
    {
        var r = ErpGlReverseJournalDryRun.EvaluateAgainstJournals(
            [Journal(9)], new ErpGlReverseJournalRequest(9, "void posting"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldWrite);
        Assert.Equal(0, r.Writes);
        Assert.Equal("JV-9", r.JournalNo);
        Assert.Contains(r.SimulatedSql, s => s.Contains("NOT executed", StringComparison.Ordinal));
        Assert.Contains(r.SimulatedSql, s => s.Contains("REV of JV-9", StringComparison.Ordinal));
    }

    [Fact]
    public void JournalMissingFromDigestIsInvalid()
    {
        var r = ErpGlReverseJournalDryRun.EvaluateAgainstJournals(
            [Journal(1)], new ErpGlReverseJournalRequest(99));
        Assert.Equal("journal_not_in_digest_window", r.ValidationCode);
        Assert.False(r.WouldWrite);
    }
}
