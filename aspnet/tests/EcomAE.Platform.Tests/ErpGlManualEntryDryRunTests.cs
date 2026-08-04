using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpGlManualEntryDryRunTests
{
    private static ErpCoaAccountDigest Coa(long id) =>
        new(id, $"1{id}", "Acct", "asset", "debit", 0, 0m, true);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = ErpGlManualEntryDryRun.EvaluateAgainstCoa(
            [Coa(1), Coa(2)],
            new ErpGlManualEntryRequest([new(1, 10, 0), new(2, 0, 10)], ConfirmWrites: true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void BalancedManualIsValidated()
    {
        var r = ErpGlManualEntryDryRun.EvaluateAgainstCoa(
            [Coa(1), Coa(2)],
            new ErpGlManualEntryRequest([new(1, 25, 0), new(2, 0, 25)], "JE-1", "test"));
        Assert.Equal("ok", r.ValidationCode);
        Assert.True(r.WouldWrite);
        Assert.Equal(0, r.Writes);
    }

    [Fact]
    public void UnbalancedIsRejected()
    {
        var r = ErpGlManualEntryDryRun.EvaluateAgainstCoa(
            [Coa(1), Coa(2)],
            new ErpGlManualEntryRequest([new(1, 25, 0), new(2, 0, 10)]));
        Assert.Equal("unbalanced", r.ValidationCode);
    }
}
