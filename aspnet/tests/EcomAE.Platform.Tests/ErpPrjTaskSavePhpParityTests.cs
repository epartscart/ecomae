using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_prj_task_save</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpPrjTaskSavePhpParityTests
{
    [Fact]
    public void ProjectsOverviewApp_PostsNativeTaskSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpProjectsOverviewApp.razor"));
        Assert.Contains("action=\"/erp/projects/tasks/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"project_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"planned_hours\"", text, StringComparison.Ordinal);
        Assert.Contains("Save task", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersPrjTaskSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPrjTaskSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpPrjTaskSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksPrjTaskSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/projects/tasks/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_prj_task_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/prj-task-save");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_RequiresProjectAndRefusesConfirm()
    {
        var ok = new ErpPrjTaskSaveDryRun().Evaluate(new ErpPrjTaskSaveRequest(0, 4, "Design"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missing = new ErpPrjTaskSaveDryRun().Evaluate(new ErpPrjTaskSaveRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("Select a project", missing.Detail);

        var confirm = new ErpPrjTaskSaveDryRun().Evaluate(new ErpPrjTaskSaveRequest(0, 4, "Design", true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpProjectsTasksSave", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxPrjTaskSave", text, StringComparison.Ordinal);
        Assert.Contains("IErpPrjTaskSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandlePrjTaskSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPrjTaskSaveWriteService.cs"));
        Assert.Contains("Task saved", service, StringComparison.Ordinal);
        Assert.Contains("Project task table is not provisioned", service, StringComparison.Ordinal);
        Assert.Contains("Select a project", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
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
