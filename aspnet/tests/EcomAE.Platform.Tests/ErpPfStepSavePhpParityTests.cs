using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPfStepSavePhpParityTests
{
    [Fact]
    public void ProcessFlowApp_PostsNativeStepSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpProcessFlowTasksApp.razor"));
        Assert.Contains("action=\"/erp/process-flow/steps/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"process_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"assign_type\"", text, StringComparison.Ordinal);
        Assert.Contains("Add step", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersStepSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPfStepSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksStepSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/process-flow/steps/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_pf_step_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/pf-step-save").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpPfStepSaveDryRun().Evaluate(new ErpPfStepSaveRequest(ProcessId: 1, Name: "Review"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal("Process is required", new ErpPfStepSaveDryRun().Evaluate(new ErpPfStepSaveRequest(Name: "Review")).Detail);
        Assert.Equal("Step name is required", new ErpPfStepSaveDryRun().Evaluate(new ErpPfStepSaveRequest(ProcessId: 1)).Detail);
        Assert.Equal("confirm_writes_refused", new ErpPfStepSaveDryRun().Evaluate(new ErpPfStepSaveRequest(ConfirmWrites: true, ProcessId: 1, Name: "Review")).ValidationCode);
    }

    [Fact]
    public void Validate_AndAssignTypes_MatchPhp()
    {
        Assert.Equal("Process is required", ErpPfStepSaveWriteService.Validate(0, "Review"));
        Assert.Equal("Step name is required", ErpPfStepSaveWriteService.Validate(1, ""));
        Assert.Null(ErpPfStepSaveWriteService.Validate(1, "Review"));
        Assert.Contains("dept_head", ErpPfStepSaveWriteService.AssignTypes);
        Assert.Contains("initiator", ErpPfStepSaveWriteService.AssignTypes);
        Assert.DoesNotContain("manager", ErpPfStepSaveWriteService.AssignTypes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpProcessFlowStepsSave", text, StringComparison.Ordinal);
        Assert.Contains("HandlePfStepSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPfStepSaveWriteService.cs"));
        Assert.Contains("Step added", service, StringComparison.Ordinal);
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
