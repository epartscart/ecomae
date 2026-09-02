using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class WaveBGlWorkflowDryRunTests
{
    private static ErpPurchaseDigest Purchase(long id) =>
        new(id, 1, "S", 1_700_000_000, "INV", 100m, "posted", 0, []);

    private static CpShopOrderDigest Order(long id, int ok = 1) =>
        new(id, 1, 9, 1, 0, 0, 1, ok, 1, 10m);

    [Fact]
    public void PeriodReopenValidated()
    {
        var r = new ErpPeriodReopenDryRun().Evaluate(new ErpPeriodReopenRequest("2026-08"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Equal(0, r.Writes);
    }

    [Fact]
    public void PurchaseAdjustmentValidated()
    {
        var r = ErpPurchaseAdjustmentDryRun.EvaluateAgainstPurchases(
            [Purchase(4)], new ErpPurchaseAdjustmentRequest(4, -5m, "wave-b"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void OrderSettlementRequiresComplete()
    {
        var bad = ErpOrderSettlementDryRun.EvaluateAgainstOrders(
            [Order(3, 0)], new ErpOrderSettlementRequest(3, 10m));
        Assert.Equal("order_incomplete", bad.ValidationCode);
    }

    [Fact]
    public void SyncSuppliersValidated()
    {
        var r = new ErpSyncSuppliersDryRun().Evaluate(new ErpSyncSuppliersRequest());
        Assert.Equal("dry-run-validated", r.Status);
    }

    [Fact]
    public void GlPostSalesValidated()
    {
        var r = new ErpGlPostSalesDryRun().Evaluate(new ErpGlPostSalesRequest(1, 2));
        Assert.Equal("dry-run-validated", r.Status);
    }

    [Fact]
    public void GlSyncUnpostedRefusesConfirm()
    {
        var r = new ErpGlSyncUnpostedDryRun().Evaluate(new ErpGlSyncUnpostedRequest(true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
    }

    [Fact]
    public void WorkflowStatusInvalidRejected()
    {
        var r = new ErpWorkflowStatusDryRun().Evaluate(new ErpWorkflowStatusRequest(1, "nope"));
        Assert.Equal("invalid_status", r.ValidationCode);
    }

    [Fact]
    public void WorkflowCreateValidated()
    {
        var r = new ErpWorkflowCreateDryRun().Evaluate(new ErpWorkflowCreateRequest("Pick parts", "warehouse", "high", 9));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }
}
