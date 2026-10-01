using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpEinvoiceCreditNotePhpParityTests
{
    [Fact]
    public void DryRun_RequiresSourceDocumentAndNeverWrites()
    {
        var result = new ErpEinvoiceCreditNoteDryRun().Evaluate(
            new ErpEinvoiceCreditNoteRequest(42, "Sales return"));

        Assert.Equal("dry-run-validated", result.Status);
        Assert.Equal(0, result.Writes);
        Assert.True(result.WritesBlocked);
        Assert.False(result.PhpAuthoritative);
        Assert.Contains(result.SimulatedSql, sql => sql.Contains("epc_einvoice_documents", StringComparison.Ordinal));
        Assert.Contains("no write", result.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DryRun_RejectsMissingSourceDocument()
    {
        var result = new ErpEinvoiceCreditNoteDryRun().Evaluate(new ErpEinvoiceCreditNoteRequest());

        Assert.Equal("dry-run-invalid", result.Status);
        Assert.Equal("invalid_request", result.ValidationCode);
        Assert.True(result.WritesBlocked);
    }

    [Fact]
    public void ConfirmWrites_IsHandledByLiveWriter()
    {
        var result = new ErpEinvoiceCreditNoteDryRun().Evaluate(
            new ErpEinvoiceCreditNoteRequest(42, "Sales return", true));

        Assert.Equal("dry-run-confirm-refused", result.Status);
        Assert.Equal("confirm_writes_refused", result.ValidationCode);
        Assert.True(result.WritesBlocked);
        Assert.Contains("live ASP.NET credit-note writer", result.Detail, StringComparison.OrdinalIgnoreCase);
    }
}
