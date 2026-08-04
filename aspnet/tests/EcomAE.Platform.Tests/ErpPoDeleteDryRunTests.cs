using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPoDeleteDryRunTests
{
    private static ErpPurchaseOrderDigest Po(long id, string status = "draft") =>
        new(id, $"PO-{id}", 3, "Parts", 80m, status, 1_700_000_000);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = ErpPoDeleteDryRun.EvaluateAgainstOrders(
            [Po(9)], new ErpPoDeleteRequest(9, true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
    }

    [Fact]
    public void PostedIsInvalid()
    {
        var r = ErpPoDeleteDryRun.EvaluateAgainstOrders(
            [Po(9, "posted")], new ErpPoDeleteRequest(9));
        Assert.Equal("po_not_draft", r.ValidationCode);
    }

    [Fact]
    public void DeleteIsDryRunValidated()
    {
        var r = ErpPoDeleteDryRun.EvaluateAgainstOrders(
            [Po(9)], new ErpPoDeleteRequest(9));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldWrite);
        Assert.Equal(0, r.Writes);
        Assert.Contains(r.SimulatedSql, s => s.Contains("epc_erp_purchase_orders", StringComparison.Ordinal));
    }
}
