using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_pf_set_dept_head</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpPfSetDeptHeadPhpParityTests
{
    [Fact]
    public void ProcessFlowApp_PostsNativeDeptHeadForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpProcessFlowTasksApp.razor"));
        Assert.Contains("action=\"/erp/process-flow/dept-heads/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"department_code\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"head_user_id\"", text, StringComparison.Ordinal);
        Assert.Contains("Save department head", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersPfSetDeptHeadWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPfSetDeptHeadWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpPfSetDeptHeadWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksPfSetDeptHeadLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/process-flow/dept-heads/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_pf_set_dept_head", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/pf-set-dept-head");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_RequiresDeptAndUserAndRefusesConfirm()
    {
        var ok = new ErpPfSetDeptHeadDryRun().Evaluate(new ErpPfSetDeptHeadRequest("finance", 9));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missingDept = new ErpPfSetDeptHeadDryRun().Evaluate(new ErpPfSetDeptHeadRequest());
        Assert.Equal("invalid_request", missingDept.ValidationCode);
        Assert.Equal("Department code is required", missingDept.Detail);

        var missingUser = new ErpPfSetDeptHeadDryRun().Evaluate(new ErpPfSetDeptHeadRequest("finance"));
        Assert.Equal("Select a department head", missingUser.Detail);

        var confirm = new ErpPfSetDeptHeadDryRun().Evaluate(new ErpPfSetDeptHeadRequest("finance", 9, true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpProcessFlowDeptHeadSave", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxPfSetDeptHead", text, StringComparison.Ordinal);
        Assert.Contains("IErpPfSetDeptHeadWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandlePfSetDeptHeadAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPfSetDeptHeadWriteService.cs"));
        Assert.Contains("Department head saved", service, StringComparison.Ordinal);
        Assert.Contains("Department code is required", service, StringComparison.Ordinal);
        Assert.Contains("Select a department head", service, StringComparison.Ordinal);
        Assert.Contains("Process-flow department-head table is not provisioned", service, StringComparison.Ordinal);
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
