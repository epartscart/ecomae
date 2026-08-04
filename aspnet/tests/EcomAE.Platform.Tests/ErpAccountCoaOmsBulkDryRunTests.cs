using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpAccountCoaOmsBulkDryRunTests
{
    [Fact]
    public void CashAccountCreateValidated()
    {
        var r = new ErpCashAccountCreateDryRun().Evaluate(new ErpCashAccountCreateRequest("Main Cash", "cash"));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.Equal(0, r.Writes);
    }

    [Fact]
    public void CoaCreateRequiresCode()
    {
        var r = new ErpCoaCreateDryRun().Evaluate(new ErpCoaCreateRequest("", "Expense", "expense"));
        Assert.Equal("code_required", r.ValidationCode);
    }

    [Fact]
    public void UpdateItemsValidated()
    {
        var order = new CpShopOrderDigest(1, 1, 9, 1, 0, 0, 1, 1, 1, 10m);
        var r = CpOmsUpdateItemsDryRun.EvaluateAgainstOrders(
            [order],
            new CpOmsUpdateItemsRequest(1, [new CpOmsUpdateItemsItem(7, 12m, 2)]));
        Assert.Equal("dry-run-validated", r.Status);
        Assert.False(r.CutoverAllowed);
    }
}
