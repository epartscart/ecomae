using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpOrderLifecycleDryRunTests
{
    [Theory]
    [InlineData("/erp/ajax/po-save")]
    [InlineData("/erp/ajax/po-status")]
    [InlineData("/erp/ajax/po-receive-lines")]
    [InlineData("/erp/ajax/po-to-invoice")]
    [InlineData("/erp/ajax/so-save")]
    [InlineData("/erp/ajax/so-status")]
    [InlineData("/erp/ajax/so-to-invoice")]
    public void CatalogPromotesConfirmedOrderLifecycleWriters(string route)
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == route);

        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("confirm_writes=false", row.Notes, StringComparison.Ordinal);
        Assert.Contains("confirm_writes=true", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP authoritative", row.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void PurchaseOrderDryRunReportsAspNetOwnership()
    {
        var result = new ErpPoSaveDryRun().Evaluate(new ErpPoSaveRequest(7, "PO-7"));

        Assert.Equal(0, result.Writes);
        Assert.False(result.PhpAuthoritative);
        Assert.Contains("no write was performed", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void SalesOrderDryRunRequiresExplicitConfirmation()
    {
        var result = new ErpSoSaveDryRun().Evaluate(new ErpSoSaveRequest(9, "SO-9", true));

        Assert.Equal("confirm_writes_refused", result.ValidationCode);
        Assert.False(result.PhpAuthoritative);
        Assert.Contains("confirm_writes=true", result.Detail, StringComparison.Ordinal);
    }
}
