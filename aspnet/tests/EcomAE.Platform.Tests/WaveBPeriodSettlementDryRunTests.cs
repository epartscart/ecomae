using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class WaveBPeriodSettlementDryRunTests
{
    private static CpShopOrderDigest Order(long id, int successfullyCreated = 1) =>
        new(id, 1, 9, 1, 0, 0, 1, successfullyCreated, 1, 10m);

    [Fact]
    public void RefreshItemCostValidated()
    {
        var r = CpOmsRefreshItemCostDryRun.EvaluateAgainstOrders(
            [Order(5)], new CpOmsRefreshItemCostRequest(5, 9));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Equal(0, r.Writes);
    }

    [Fact]
    public void PurchaseFromOrderRequiresComplete()
    {
        var bad = ErpPurchaseFromOrderDryRun.EvaluateAgainstOrders(
            [Order(5, 0)], new ErpPurchaseFromOrderRequest(5, 2));
        Assert.Equal("order_incomplete", bad.ValidationCode);

        var ok = ErpPurchaseFromOrderDryRun.EvaluateAgainstOrders(
            [Order(5)], new ErpPurchaseFromOrderRequest(5, 2));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.False(ok.CutoverAllowed);
    }

    [Fact]
    public void CcySetRateRequiresPositive()
    {
        var r = new ErpCcySetRateDryRun().Evaluate(new ErpCcySetRateRequest("USD", "AED", 0));
        Assert.Equal("invalid_request", r.ValidationCode);
    }

    [Fact]
    public void PeriodSoftCloseValidated()
    {
        var r = new ErpPeriodSoftCloseDryRun().Evaluate(new ErpPeriodSoftCloseRequest("2026-08", "wave-b"));
        Assert.Equal("dry-run-validated", r.Status);
    }

    [Fact]
    public void PeriodLockRequiresYm()
    {
        var r = new ErpPeriodLockDryRun().Evaluate(new ErpPeriodLockRequest("2026"));
        Assert.Equal("year_month_required", r.ValidationCode);
    }

    [Fact]
    public void CustomerSettlementWriteOffDirection()
    {
        var r = new ErpCustomerSettlementDryRun().Evaluate(
            new ErpCustomerSettlementRequest(1, 10m, "credit", "write_off"));
        Assert.Equal("write_off_direction", r.ValidationCode);
    }

    [Fact]
    public void SupplierSettlementValidated()
    {
        var r = new ErpSupplierSettlementDryRun().Evaluate(new ErpSupplierSettlementRequest(3, 25m, "decrease"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Equal(0, r.Writes);
    }

    [Fact]
    public void FiscalSetLockClearValidated()
    {
        var r = new ErpFiscalSetLockDryRun().Evaluate(new ErpFiscalSetLockRequest(0, "clear"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.Clearing);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void ConfirmWritesRefused()
    {
        Assert.Equal("dry-run-confirm-refused",
            new ErpCcySetRateDryRun().Evaluate(new ErpCcySetRateRequest("USD", "AED", 3.67m, true)).Status);
        Assert.Equal("dry-run-confirm-refused",
            CpOmsRefreshItemCostDryRun.EvaluateAgainstOrders(
                [Order(1)], new CpOmsRefreshItemCostRequest(1, 1, true)).Status);
    }
}
