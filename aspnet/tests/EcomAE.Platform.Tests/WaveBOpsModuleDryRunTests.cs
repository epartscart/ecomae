using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class WaveBOpsModuleDryRunTests
{
    [Fact] public void MarketingCreateValidated()
    {
        var r = new ErpMarketingCreateDryRun().Evaluate(new ErpMarketingCreateRequest("Spring promo"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.CutoverAllowed);
    }

    [Fact] public void SubscriptionSaveRequiresCodeCustomer()
    {
        var r = new ErpSubscriptionSaveDryRun().Evaluate(new ErpSubscriptionSaveRequest("", "Acme"));
        Assert.Equal("code_customer_required", r.ValidationCode);
    }

    [Fact] public void ContractSaveValidated()
    {
        var r = new ErpContractSaveDryRun().Evaluate(new ErpContractSaveRequest("CTR-1", "MSA"));
        Assert.Equal("dry-run-validated", r.Status);
    }

    [Fact] public void WmsReceiveRequiresItem()
    {
        var r = new ErpWmsReceiveDryRun().Evaluate(new ErpWmsReceiveRequest("", 1));
        Assert.Equal("item_required", r.ValidationCode);
    }

    [Fact] public void WmsLocationSaveValidated()
    {
        var r = new ErpWmsLocationSaveDryRun().Evaluate(new ErpWmsLocationSaveRequest("A-01-01"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Equal(0, r.Writes);
        Assert.False(r.PhpAuthoritative);
        var missing = new ErpWmsLocationSaveDryRun().Evaluate(new ErpWmsLocationSaveRequest(""));
        Assert.Equal("code_required", missing.ValidationCode);
        var refused = new ErpWmsLocationSaveDryRun().Evaluate(new ErpWmsLocationSaveRequest("A-01-01", ConfirmWrites: true));
        Assert.Equal("dry-run-confirm-refused", refused.Status);
    }

    [Fact] public void CollectionsCaseSaveValidated()
    {
        var r = new ErpCollectionsCaseSaveDryRun().Evaluate(new ErpCollectionsCaseSaveRequest(9));
        Assert.Equal("dry-run-validated", r.Status);
    }

    [Fact] public void ProcReqSaveValidated()
    {
        var r = new ErpProcReqSaveDryRun().Evaluate(new ErpProcReqSaveRequest("buyer@ecom.ae"));
        Assert.Equal("dry-run-validated", r.Status);
    }

    [Fact] public void FinPeriodStatusValidated()
    {
        var r = new ErpFinPeriodStatusDryRun().Evaluate(new ErpFinPeriodStatusRequest(2026, 8, "closed"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Equal(0, r.Writes);
    }
}
