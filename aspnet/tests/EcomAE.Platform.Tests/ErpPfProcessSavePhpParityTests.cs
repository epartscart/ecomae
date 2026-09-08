using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPfProcessSavePhpParityTests
{
    [Fact]
    public void ProcessFlowApp_PostsNativeProcessSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpProcessFlowTasksApp.razor"));
        Assert.Contains("action=\"/erp/process-flow/processes/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"name\"", text, StringComparison.Ordinal);
        Assert.Contains("Save process", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersProcessSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPfProcessSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksProcessSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/process-flow/processes/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_pf_process_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/pf-process-save").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpPfProcessSaveDryRun().Evaluate(new ErpPfProcessSaveRequest(Name: "Onboard"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal("Process name is required", new ErpPfProcessSaveDryRun().Evaluate(new ErpPfProcessSaveRequest()).Detail);
        Assert.Equal("confirm_writes_refused", new ErpPfProcessSaveDryRun().Evaluate(new ErpPfProcessSaveRequest(ConfirmWrites: true, Name: "Onboard")).ValidationCode);
    }

    [Fact]
    public void Validate_AndActive_MatchPhp()
    {
        Assert.Equal("Process name is required", ErpPfProcessSaveWriteService.Validate(""));
        Assert.Null(ErpPfProcessSaveWriteService.Validate("Onboard"));
        Assert.Equal(1, ErpPfProcessSaveWriteService.ResolveActive(null));
        Assert.Equal(0, ErpPfProcessSaveWriteService.ResolveActive(0));
        Assert.Equal(1, ErpPfProcessSaveWriteService.ResolveActive(1));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpProcessFlowProcessesSave", text, StringComparison.Ordinal);
        Assert.Contains("HandlePfProcessSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPfProcessSaveWriteService.cs"));
        Assert.Contains("Process saved", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
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
