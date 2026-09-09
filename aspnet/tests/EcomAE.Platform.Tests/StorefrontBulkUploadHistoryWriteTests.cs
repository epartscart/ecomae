using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontBulkUploadHistoryWriteTests
{
    [Fact]
    public void Routes_expose_history_update()
    {
        Assert.Equal("/storefront/bulk-upload/check", EcomAeRoutes.StorefrontBulkUploadCheck);
        Assert.Equal("/storefront/bulk-upload/history-update", EcomAeRoutes.StorefrontBulkUploadHistoryUpdate);
        Assert.Equal("/storefront/bulk-upload/history", EcomAeRoutes.StorefrontBulkUploadHistory);
    }

    [Fact]
    public void Normalize_priority_file_and_summary()
    {
        Assert.Equal("price", StorefrontBulkUploadHistoryWriteService.NormalizePriority(""));
        Assert.Equal("price", StorefrontBulkUploadHistoryWriteService.NormalizePriority("PRICE"));
        Assert.Equal("delivery", StorefrontBulkUploadHistoryWriteService.NormalizePriority("delivery"));
        Assert.Equal("pads.csv", StorefrontBulkUploadHistoryWriteService.NormalizeFileName("  pads.csv  "));
        Assert.Equal(255, StorefrontBulkUploadHistoryWriteService.NormalizeFileName(new string('a', 300)).Length);

        var summary = StorefrontBulkUploadHistoryWriteService.NormalizeSummary(
            new StorefrontBulkUploadSummary(0, 2, 1, 1, 3),
            5);
        Assert.Equal(5, summary.Uploaded);
        Assert.Equal(2, summary.Available);
        Assert.Equal(1, summary.Cross);
        Assert.Equal(3, summary.Notfound);
    }

    [Fact]
    public void TryParseSummary_reads_php_shape()
    {
        Assert.True(StorefrontBulkUploadHistoryWriteService.TryParseSummary(
            "{\"uploaded\":4,\"available\":2,\"cross\":1,\"short\":1,\"notfound\":2}",
            0,
            out var summary));
        Assert.Equal(4, summary.Uploaded);
        Assert.Equal(2, summary.Available);
        Assert.False(StorefrontBulkUploadHistoryWriteService.TryParseSummary("{", 0, out _));
    }

    [Fact]
    public void SerializeRows_keeps_php_keys()
    {
        var line = new StorefrontBulkUploadLine("BOSCH", "0986424795", 2, "45", "1", "pads");
        var row = StorefrontBulkUploadMatcher.BuildRow(line, null, null, false);
        var json = StorefrontBulkUploadHistoryWriteService.SerializeRows([row]);
        Assert.Contains("\"input\"", json, StringComparison.Ordinal);
        Assert.Contains("0986424795", json, StringComparison.Ordinal);
        Assert.Contains("\"status_label\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("password", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_check_and_update_write_live_gated()
    {
        var check = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/storefront/bulk-upload/check");
        Assert.Equal("write-live-gated", check.Status);
        Assert.Contains("epc_bulk_upload_history", check.Notes, StringComparison.Ordinal);

        var update = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/storefront/bulk-upload/history-update");
        Assert.Equal("write-live-gated", update.Status);
        Assert.Contains("history_update", update.Notes, StringComparison.Ordinal);
        Assert.Contains("quote", update.Notes, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Program_and_module_register_history_write()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IStorefrontBulkUploadHistoryWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/StorefrontModule.cs"));
        Assert.Contains("StorefrontBulkUploadHistoryUpdate", module, StringComparison.Ordinal);
        Assert.Contains("confirmWrites", module, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE IF NOT EXISTS `epc_bulk_upload_history`", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Storefront/StorefrontBulkUploadHistoryWriteService.cs"));
        Assert.Contains("INSERT INTO `epc_bulk_upload_history`", service, StringComparison.Ordinal);
        Assert.Contains("Schema ensure stays on the Classic twin", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj"))
                || File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.Platform.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repo root.");
    }
}
