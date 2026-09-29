using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCollectionsLiveWriteEvidenceTests
{
    [Theory]
    [InlineData("/erp/collections/cases/save")]
    [InlineData("/erp/collections/cases/promise")]
    [InlineData("/erp/collections/activity/log")]
    [InlineData("/erp/collections/hold/set")]
    [InlineData("/erp/collections/dunning/run")]
    [InlineData("/erp/collections/cases/status")]
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
    public void Case_status_dry_run_is_non_mutating_and_not_php_authoritative()
    {
        var result = new ErpCollectionsCaseStatusDryRun().Evaluate(
            new ErpCollectionsCaseStatusRequest(12, "promise"));

        Assert.Equal(0, result.Writes);
        Assert.False(result.PhpAuthoritative);
        Assert.Contains("no write was performed", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Case_save_confirmation_is_explicit()
    {
        var result = new ErpCollectionsCaseSaveDryRun().Evaluate(
            new ErpCollectionsCaseSaveRequest(12, ConfirmWrites: true));

        Assert.Equal("confirm_writes_refused", result.ValidationCode);
        Assert.Contains("confirm_writes=true", result.Detail, StringComparison.Ordinal);
    }
}
