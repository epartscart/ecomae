using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCashVoucherVoidDryRunTests
{
    private static ErpCashEntryDigest Entry(long id) =>
        new(id, 1, "Cash", "cash", 1_700_000_000, 1, 50m, "V1", "n");

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = ErpCashVoucherVoidDryRun.EvaluateAgainstEntries([Entry(9)], new ErpCashVoucherVoidRequest(9, "x", true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void VoidIsDryRunValidated()
    {
        var r = ErpCashVoucherVoidDryRun.EvaluateAgainstEntries([Entry(9)], new ErpCashVoucherVoidRequest(9, "bad cheque"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldWrite);
        Assert.Contains(r.SimulatedSql, s => s.Contains("NOT executed", StringComparison.Ordinal));
    }
}
