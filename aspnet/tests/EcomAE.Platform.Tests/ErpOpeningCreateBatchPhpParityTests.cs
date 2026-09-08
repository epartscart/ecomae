using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpOpeningCreateBatchPhpParityTests
{
    [Fact]
    public void OpeningApp_PostsNativeCreateBatchForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpOpeningApp.razor"));
        Assert.Contains("/erp/opening/create-batch", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"module\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"as_of_date\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"reference\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersOpeningCreateBatchWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpOpeningCreateBatchWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksOpeningCreateBatchLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/opening/create-batch");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_opening_create_batch", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/opening-create-batch").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpOpeningCreateBatchDryRun().Evaluate(new ErpOpeningCreateBatchRequest());
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpOpeningCreateBatchDryRun().Evaluate(new ErpOpeningCreateBatchRequest(ConfirmWrites: true)).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpOpeningCreateBatch", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxOpeningCreateBatch", text, StringComparison.Ordinal);
        Assert.Contains("HandleOpeningCreateBatchAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpOpeningCreateBatchWriteService.cs"));
        Assert.Contains("Opening batch created (draft)", service, StringComparison.Ordinal);
        Assert.Contains("Opening batches table is not provisioned", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_opening_ensure_schema", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Normalize_MatchesPhpModuleAndDateDefaults()
    {
        Assert.Equal("combined", ErpOpeningCreateBatchWriteService.NormalizeModule(""));
        Assert.Equal("combined", ErpOpeningCreateBatchWriteService.NormalizeModule("other"));
        Assert.Equal("coa", ErpOpeningCreateBatchWriteService.NormalizeModule("coa"));
        Assert.Equal("cash_bank", ErpOpeningCreateBatchWriteService.NormalizeModule("cash_bank"));
        Assert.Equal("2026-09-08", ErpOpeningCreateBatchWriteService.NormalizeAsOfDate("2026-09-08"));
        Assert.Equal(ErpOpeningCreateBatchWriteService.TodayYmd(), ErpOpeningCreateBatchWriteService.NormalizeAsOfDate(""));
        Assert.Equal(ErpOpeningCreateBatchWriteService.TodayYmd(), ErpOpeningCreateBatchWriteService.NormalizeAsOfDate("0"));
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
