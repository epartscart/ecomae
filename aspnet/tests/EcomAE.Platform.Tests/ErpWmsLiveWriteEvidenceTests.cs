using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpWmsLiveWriteEvidenceTests
{
    [Theory]
    [InlineData("/erp/wms/receive")]
    [InlineData("/erp/wms/locations/save")]
    [InlineData("/erp/wms/waves/create")]
    [InlineData("/erp/wms/waves/release")]
    [InlineData("/erp/wms/work/complete")]
    [InlineData("/erp/wms/locations/delete")]
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
    public void Location_delete_dry_run_is_non_mutating_and_non_php_authoritative()
    {
        var result = new ErpWmsLocationDeleteDryRun().Evaluate(new ErpWmsLocationDeleteRequest(4));

        Assert.Equal(0, result.Writes);
        Assert.False(result.PhpAuthoritative);
        Assert.Contains("no write was performed", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Wave_release_confirmation_is_explicit()
    {
        var result = new ErpWmsWaveReleaseDryRun().Evaluate(new ErpWmsWaveReleaseRequest(4, true));

        Assert.Equal("confirm_writes_refused", result.ValidationCode);
        Assert.False(result.PhpAuthoritative);
        Assert.Contains("confirm_writes=true", result.Detail, StringComparison.Ordinal);
    }
}
