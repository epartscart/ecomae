using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_pf_case_reassign</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpPfCaseReassignPhpParityTests
{
    [Fact]
    public void ProcessFlowApp_PostsNativeReassignForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpProcessFlowTasksApp.razor"));
        Assert.Contains("action=\"/erp/process-flow/cases/reassign\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"case_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"user_id\"", text, StringComparison.Ordinal);
        Assert.Contains("Reassign case", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersPfCaseReassignWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPfCaseReassignWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpPfCaseReassignWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksPfCaseReassignLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/process-flow/cases/reassign");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_pf_case_reassign", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/pf-case-reassign");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_RequiresCaseAndUserAndRefusesConfirm()
    {
        var ok = new ErpPfCaseReassignDryRun().Evaluate(new ErpPfCaseReassignRequest(4, 9));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missing = new ErpPfCaseReassignDryRun().Evaluate(new ErpPfCaseReassignRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("Case is not open", missing.Detail);

        var noUser = new ErpPfCaseReassignDryRun().Evaluate(new ErpPfCaseReassignRequest(4));
        Assert.Equal("Select an assignee", noUser.Detail);

        var confirm = new ErpPfCaseReassignDryRun().Evaluate(new ErpPfCaseReassignRequest(4, 9, true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpProcessFlowCaseReassign", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxPfCaseReassign", text, StringComparison.Ordinal);
        Assert.Contains("IErpPfCaseReassignWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandlePfCaseReassignAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPfCaseReassignWriteService.cs"));
        Assert.Contains("Case reassigned", service, StringComparison.Ordinal);
        Assert.Contains("Case is not open", service, StringComparison.Ordinal);
        Assert.Contains("Select an assignee", service, StringComparison.Ordinal);
        Assert.Contains("Process-flow case tables are not provisioned", service, StringComparison.Ordinal);
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
