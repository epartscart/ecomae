using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPurchaseVoidDryRunTests
{
    private static ErpPurchaseDigest Purchase(long id, string status = "posted") =>
        new(id, 3, "Supplier Co", 1_700_000_000, $"PI-{id}", 250m, status, 0, []);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = ErpPurchaseVoidDryRun.EvaluateAgainstPurchases(
            [Purchase(9)], new ErpPurchaseVoidRequest(9, "x", true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void VoidIsDryRunValidated()
    {
        var r = ErpPurchaseVoidDryRun.EvaluateAgainstPurchases(
            [Purchase(9)], new ErpPurchaseVoidRequest(9, "duplicate invoice"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldWrite);
        Assert.Equal(0, r.Writes);
        Assert.Contains(r.SimulatedSql, s => s.Contains("epc_erp_purchases", StringComparison.Ordinal));
        Assert.Contains(r.SimulatedSql, s => s.Contains("NOT executed", StringComparison.Ordinal));
    }

    [Fact]
    public void AlreadyVoidedIsInvalid()
    {
        var r = ErpPurchaseVoidDryRun.EvaluateAgainstPurchases(
            [Purchase(9, "voided")], new ErpPurchaseVoidRequest(9));
        Assert.Equal("purchase_already_voided", r.ValidationCode);
        Assert.False(r.WouldWrite);
    }
}
