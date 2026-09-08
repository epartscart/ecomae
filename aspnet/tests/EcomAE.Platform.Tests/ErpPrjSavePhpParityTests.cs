using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_prj_save</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpPrjSavePhpParityTests
{
    [Fact]
    public void ProjectsOverviewApp_PostsNativeSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpProjectsOverviewApp.razor"));
        Assert.Contains("action=\"/erp/projects/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"code\"", text, StringComparison.Ordinal);
        Assert.Contains("Save project", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersPrjSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPrjSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpPrjSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksPrjSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/projects/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_prj_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/prj-save");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_RequiresCodeAndRefusesConfirm()
    {
        var ok = new ErpPrjSaveDryRun().Evaluate(new ErpPrjSaveRequest(0, "PRJ-001"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missing = new ErpPrjSaveDryRun().Evaluate(new ErpPrjSaveRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("Project code is required", missing.Detail);

        var confirm = new ErpPrjSaveDryRun().Evaluate(new ErpPrjSaveRequest(0, "PRJ-001", true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpProjectsSave", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxPrjSave", text, StringComparison.Ordinal);
        Assert.Contains("IErpPrjSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandlePrjSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPrjSaveWriteService.cs"));
        Assert.Contains("Project saved", service, StringComparison.Ordinal);
        Assert.Contains("Project table is not provisioned", service, StringComparison.Ordinal);
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
