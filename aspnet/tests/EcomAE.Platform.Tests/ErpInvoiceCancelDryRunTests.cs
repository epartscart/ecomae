using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpInvoiceCancelDryRunTests
{
    private static ErpInvoiceDigest Invoice(long id, string status = "draft") =>
        new(id, $"INV-{id}", 0, 5, "a@b.com", 1_700_000_000, status, 500m);

    [Fact]
    public void ConfirmWritesIsRefused()
    {
        var r = ErpInvoiceCancelDryRun.EvaluateAgainstInvoices(
            [Invoice(9)], new ErpInvoiceCancelRequest(9, "x", true));
        Assert.Equal("dry-run-confirm-refused", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }

    [Fact]
    public void SubmittedIsNotCancellable()
    {
        var r = ErpInvoiceCancelDryRun.EvaluateAgainstInvoices(
            [Invoice(9, "submitted")], new ErpInvoiceCancelRequest(9));
        Assert.Equal("invoice_not_cancellable", r.ValidationCode);
        Assert.False(r.WouldWrite);
    }

    [Fact]
    public void CancelIsDryRunValidated()
    {
        var r = ErpInvoiceCancelDryRun.EvaluateAgainstInvoices(
            [Invoice(9, "validated")], new ErpInvoiceCancelRequest(9, "customer cancelled"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.True(r.WouldWrite);
        Assert.Equal(0, r.Writes);
        Assert.Contains(r.SimulatedSql, s => s.Contains("NOT executed", StringComparison.Ordinal));
    }
}
