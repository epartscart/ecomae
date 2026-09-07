using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards the live PHP <c>epc_wms_work_complete</c> twin: SSR form, DI, catalog.
/// Schema ensure stays PHP.
/// </summary>
public sealed class ErpWmsWorkCompletePhpParityTests
{
    [Fact]
    public void WarehouseApp_PostsNativeCompleteForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpWarehouseWmsApp.razor"));
        Assert.Contains("action=\"/erp/wms/work/complete\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("Complete work", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Work complete stays on the Classic twin", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersCompleteWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpWmsWorkCompleteWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpWmsWorkCompleteWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksCompleteLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/wms/work/complete");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_wms_work_complete", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var delete = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/wms/locations/delete");
        Assert.Equal("write-live-gated", delete.Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpWmsWorkCompleteDryRun().Evaluate(new ErpWmsWorkCompleteRequest(3));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.False(ok.CutoverAllowed);
        Assert.True(ok.WouldWrite);
        Assert.Equal("ok", ok.ValidationCode);

        var missing = new ErpWmsWorkCompleteDryRun().Evaluate(new ErpWmsWorkCompleteRequest(0));
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.False(missing.WouldWrite);

        var confirm = new ErpWmsWorkCompleteDryRun().Evaluate(new ErpWmsWorkCompleteRequest(3, ConfirmWrites: true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpWmsWorkComplete", text, StringComparison.Ordinal);
        Assert.Contains("IErpWmsWorkCompleteWriteService", text, StringComparison.Ordinal);
        Assert.Contains("work_id", text, StringComparison.Ordinal);
        Assert.Contains("Work completed", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpWmsWorkCompleteWriteService.cs")), StringComparison.Ordinal);
        Assert.Contains("WMS work tables are not provisioned", File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpWmsWorkCompleteWriteService.cs")), StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var alt = Path.GetFullPath(Path.Combine(dir.FullName, "..", "..", "..", "..", "..", relative));
            if (File.Exists(alt))
            {
                return alt;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate " + relative);
    }
}
