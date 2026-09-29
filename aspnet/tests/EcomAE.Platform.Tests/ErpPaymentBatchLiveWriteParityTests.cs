using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPaymentBatchLiveWriteParityTests
{
    [Fact]
    public void DryRun_UsesLiveWriterContractWithoutWriting()
    {
        var result = new ErpPaymentBatchSaveDryRun().Evaluate(new ErpPaymentBatchSaveRequest(
            AccountId: 12,
            BatchType: "sepa",
            LineCount: 2));

        Assert.Equal("dry-run-validated", result.Status);
        Assert.Equal(0, result.Writes);
        Assert.True(result.WritesBlocked);
        Assert.False(result.PhpAuthoritative);
        Assert.Contains(result.SimulatedSql, item => item.Contains("epc_erp_payment_batches", StringComparison.Ordinal));
        Assert.Contains("no write was performed", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_DescribesAspNetDraftCreation()
    {
        var row = SurfacePayloadContractCatalog.Functions.Single(item =>
            item.AspNetRouteOrCapability == "/erp/payment-batches-app");
        var endpoint = SurfacePayloadContractCatalog.Functions.Single(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/payment-batch-save");

        Assert.Equal("live-write-gated", row.Status);
        Assert.Contains("confirm_writes=true", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("payment_batch_save stays Classic", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", endpoint.Status);
        Assert.Contains("authenticated ASP.NET writer", endpoint.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP authoritative", endpoint.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Module_UsesConfirmedWriteService()
    {
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPaymentBatchSaveWriteService.cs"));

        Assert.Contains("IErpPaymentBatchSaveWriteService", module, StringComparison.Ordinal);
        Assert.Contains("writes.CreateAsync", module, StringComparison.Ordinal);
        Assert.Contains("epc_erp_payment_batches", service, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
            var alt = Path.GetFullPath(Path.Combine(dir.FullName, "..", "..", "..", "..", "..", relative));
            if (File.Exists(alt)) return alt;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Could not locate " + relative);
    }
}
