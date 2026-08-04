using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCashVoucherCreateDryRunTests
{
    [Fact]
    public void CashEntryConfirmWritesRefused()
    {
        var r = ErpCashEntryCreateDryRun.EvaluateShape(
            new ErpCashEntryCreateRequest(1, 10m, ConfirmWrites: true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void CashEntryValidated()
    {
        var r = ErpCashEntryCreateDryRun.EvaluateShape(new ErpCashEntryCreateRequest(3, 25.5m, Direction: true));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.Equal("receipt", r.EntryType);
    }

    [Fact]
    public void ReceiptVoucherValidated()
    {
        var r = new ErpReceiptVoucherDryRun().Evaluate(new ErpReceiptVoucherRequest(9, 2, 100m));
        Assert.Equal("ok", r.ValidationCode);
        Assert.True(r.WouldWrite);
    }

    [Fact]
    public void PaymentVoucherMissingSupplierRejected()
    {
        var r = new ErpPaymentVoucherDryRun().Evaluate(new ErpPaymentVoucherRequest(0, 2, 50m));
        Assert.Equal("invalid_request", r.ValidationCode);
    }

    [Fact]
    public void PaymentVoucherValidated()
    {
        var r = new ErpPaymentVoucherDryRun().Evaluate(new ErpPaymentVoucherRequest(5, 2, 50m));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Contains(r.SimulatedSql, s => s.Contains("NOT executed", StringComparison.Ordinal));
    }
}
