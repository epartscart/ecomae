using EcomAE.Workers;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PriceImportDryRunExecutorTests
{
    [Fact]
    public void ExecuteValidatesPriceImportSampleWithoutWrites()
    {
        var executor = new PriceImportDryRunExecutor();
        var job = new MigrationWorkerJob("price-import", "api/prices/upload_price.php", "EcomAE.Workers.PriceImport", "supplier-triggered and scheduled", "dry-run", "Imported row counts match PHP.");
        var request = new MigrationWorkerJobRunRequest(
            "price-import",
            DateTimeOffset.UnixEpoch,
            "migration-test",
            Parameters: new Dictionary<string, string>
            {
                ["sample_csv"] = "sku,price,currency\nA-1,10.25,AED\nB-2,0,USD"
            });

        var output = executor.Execute(job, request);

        Assert.Equal("dry-run-validated", output.Status);
        Assert.True(output.WritesBlocked);
        Assert.Equal("2", output.Metrics["rows_read"]);
        Assert.Equal("2", output.Metrics["valid_rows"]);
        Assert.Equal("0", output.Metrics["invalid_rows"]);
        Assert.Equal("AED,USD", output.Metrics["currencies"]);
        Assert.Equal("0", output.Metrics["writes"]);
    }

    [Fact]
    public void ExecuteRequiresBaselineSampleBeforeParityReview()
    {
        var executor = new PriceImportDryRunExecutor();
        var job = new MigrationWorkerJob("price-import", "api/prices/upload_price.php", "EcomAE.Workers.PriceImport", "supplier-triggered and scheduled", "dry-run", "Imported row counts match PHP.");
        var request = new MigrationWorkerJobRunRequest("price-import", DateTimeOffset.UnixEpoch, "migration-test");

        var output = executor.Execute(job, request);

        Assert.Equal("dry-run-needs-sample", output.Status);
        Assert.True(output.WritesBlocked);
        Assert.Contains("sample_csv", output.Summary, StringComparison.Ordinal);
        Assert.Equal("0", output.Metrics["writes"]);
    }
}
