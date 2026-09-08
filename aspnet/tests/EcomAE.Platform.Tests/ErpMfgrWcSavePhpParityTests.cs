using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpMfgrWcSavePhpParityTests
{
    [Fact]
    public void ProductionApp_PostsNativeWorkCenterForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpProductionOverviewApp.razor"));
        Assert.Contains("/erp/mfgr/work-centers/save", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"capacity_min_per_day\"", text, StringComparison.Ordinal);
        Assert.Contains("Save work center", text, StringComparison.Ordinal);
        Assert.DoesNotContain("writes=0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersMfgrWcSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpMfgrWcSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksMfgrWcSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/mfgr/work-centers/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_mfgr_wc_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/mfgr-wc-save").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpMfgrWcSaveDryRun().Evaluate(new ErpMfgrWcSaveRequest());
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpMfgrWcSaveDryRun().Evaluate(new ErpMfgrWcSaveRequest(ConfirmWrites: true)).ValidationCode);
    }

    [Fact]
    public void Code_NormalizesLikePhp()
    {
        Assert.Equal("CUT", ErpMfgrWcSaveWriteService.NormalizeCode(" cut "));
        Assert.Equal("", ErpMfgrWcSaveWriteService.NormalizeCode(""));
        Assert.Equal("", ErpMfgrWcSaveWriteService.NormalizeCode("   "));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpMfgrWcSave", text, StringComparison.Ordinal);
        Assert.Contains("HandleMfgrWcSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpMfgrWcSaveWriteService.cs"));
        Assert.Contains("Work center saved", service, StringComparison.Ordinal);
        Assert.Contains("Work center code is required", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_mfgr_route_save", service, StringComparison.Ordinal);
        Assert.DoesNotContain("mfgr_mrp_run", service, StringComparison.Ordinal);
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
