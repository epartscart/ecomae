using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpMfgrRouteSavePhpParityTests
{
    [Fact]
    public void ProductionApp_PostsNativeRouteForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpProductionOverviewApp.razor"));
        Assert.Contains("/erp/mfgr/routes/save", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"product_item_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"op_no[]\"", text, StringComparison.Ordinal);
        Assert.Contains("Save route", text, StringComparison.Ordinal);
        Assert.DoesNotContain("writes=0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersMfgrRouteSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpMfgrRouteSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksMfgrRouteSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/mfgr/routes/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_mfgr_route_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/mfgr-route-save").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpMfgrRouteSaveDryRun().Evaluate(new ErpMfgrRouteSaveRequest());
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpMfgrRouteSaveDryRun().Evaluate(new ErpMfgrRouteSaveRequest(ConfirmWrites: true)).ValidationCode);
    }

    [Fact]
    public void Ajax_SkipsBlankOpsLikePhp()
    {
        Assert.True(ErpMfgrRouteSaveWriteService.ShouldSkipOp(0, 0, 0));
        Assert.False(ErpMfgrRouteSaveWriteService.ShouldSkipOp(3, 0, 0));
        Assert.False(ErpMfgrRouteSaveWriteService.ShouldSkipOp(0, 1.5m, 0));
        Assert.False(ErpMfgrRouteSaveWriteService.ShouldSkipOp(0, 0, 12));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpMfgrRouteSave", text, StringComparison.Ordinal);
        Assert.Contains("HandleMfgrRouteSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpMfgrRouteSaveWriteService.cs"));
        Assert.Contains("Route saved", service, StringComparison.Ordinal);
        Assert.Contains("DELETE FROM `epc_mfg_route_op`", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("mfgr_mrp_run", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_mfgr_mrp_run", service, StringComparison.Ordinal);
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
