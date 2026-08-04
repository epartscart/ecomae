using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpMasterDataWriteDryRunTests
{
    [Fact]
    public void SupplierCreateRequiresName()
    {
        var r = new ErpSupplierCreateDryRun().Evaluate(new ErpSupplierCreateRequest("  "));
        Assert.Equal("name_required", r.ValidationCode);
    }

    [Fact]
    public void SupplierCreateValidated()
    {
        var r = new ErpSupplierCreateDryRun().Evaluate(new ErpSupplierCreateRequest("Acme Parts"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Equal(0, r.Writes);
    }

    [Fact]
    public void PurchaseCreateRequiresSupplier()
    {
        var r = new ErpPurchaseCreateDryRun().Evaluate(new ErpPurchaseCreateRequest(0, 10m));
        Assert.Equal("supplier_required", r.ValidationCode);
    }

    [Fact]
    public void PurchaseDeleteDraftOk()
    {
        var dig = new ErpPurchaseDigest(1, 2, "S", 1, "INV", 10m, "draft", 0);
        var r = ErpPurchaseDeleteDryRun.EvaluateAgainstPurchases([dig], new ErpPurchaseDeleteRequest(1));
        Assert.Equal("dry-run-validated", r.Status);
    }

    [Fact]
    public void InvoiceDeleteNonDraftRejected()
    {
        var dig = new ErpInvoiceDigest(1, "SI-1", 0, 9, "a@b.c", 1, "posted", 10m);
        var r = ErpInvoiceDeleteDryRun.EvaluateAgainstInvoices([dig], new ErpInvoiceDeleteRequest(1));
        Assert.Equal("not_draft", r.ValidationCode);
    }
}
