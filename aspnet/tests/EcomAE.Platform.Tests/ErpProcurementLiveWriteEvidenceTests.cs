using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpProcurementLiveWriteEvidenceTests
{
    [Theory]
    [InlineData("/erp/procurement/requisitions/save")]
    [InlineData("/erp/procurement/requisitions/add-line")]
    [InlineData("/erp/procurement/requisitions/submit")]
    [InlineData("/erp/procurement/requisitions/decision")]
    public void Catalog_records_confirmed_AspNet_writer(string route)
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == route);

        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("confirm_writes=false", row.Notes, StringComparison.Ordinal);
        Assert.Contains("confirm_writes=true", row.Notes, StringComparison.Ordinal);
        Assert.Contains("ASP.NET writer", row.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Submit_dry_run_is_non_mutating_and_not_php_authoritative()
    {
        var result = new ErpProcReqSubmitDryRun().Evaluate(new ErpProcReqSubmitRequest(12));

        Assert.Equal(0, result.Writes);
        Assert.False(result.PhpAuthoritative);
        Assert.Contains("no write was performed", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Decision_confirmation_is_explicit()
    {
        var result = new ErpProcReqDecisionDryRun().Evaluate(
            new ErpProcReqDecisionRequest(12, ConfirmWrites: true));

        Assert.Equal("confirm_writes_refused", result.ValidationCode);
        Assert.Contains("confirm_writes=true", result.Detail, StringComparison.Ordinal);
    }
}
