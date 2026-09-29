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
        Assert.False(r.PhpAuthoritative);
    }

    [Fact]
    public void CashEntryValidated()
    {
        var r = ErpCashEntryCreateDryRun.EvaluateShape(new ErpCashEntryCreateRequest(3, 25.5m, Direction: true));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.Equal("receipt", r.EntryType);
        Assert.False(r.PhpAuthoritative);
        Assert.Contains("no write was performed", r.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void CashEntryCatalogUsesLiveWriteGate()
    {
        var row = SurfacePayloadContractCatalog.Functions.Single(item =>
            item.AspNetRouteOrCapability == "/erp/cash-entries/create");

        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("confirm_writes=true", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP authoritative", row.Notes, StringComparison.Ordinal);
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
