using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_pf_case_start</c> / <c>epc_pf_case_act</c> twins.</summary>
public sealed class ErpPfCaseStartActPhpParityTests
{
    [Fact]
    public void ProcessFlowApp_PostsNativeStartAndActForms()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpProcessFlowTasksApp.razor"));
        Assert.Contains("action=\"/erp/process-flow/cases/start\"", text, StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/process-flow/cases/act\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"process_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"title\"", text, StringComparison.Ordinal);
        Assert.Contains("Start case", text, StringComparison.Ordinal);
        Assert.Contains("name=\"decision\"", text, StringComparison.Ordinal);
        Assert.Contains("Act on case", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Start, act, reassign, and seed stay on the classic twin", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersPfCaseStartAndActWriteServices()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPfCaseStartWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpPfCaseStartWriteService", text, StringComparison.Ordinal);
        Assert.Contains("IErpPfCaseActWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpPfCaseActWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksPfCaseStartAndActLiveGated()
    {
        var start = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/process-flow/cases/start");
        Assert.Equal("write-live-gated", start.Status);
        Assert.Contains("epc_pf_case_start", start.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", start.Notes, StringComparison.Ordinal);

        var act = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/process-flow/cases/act");
        Assert.Equal("write-live-gated", act.Status);
        Assert.Contains("epc_pf_case_act", act.Notes, StringComparison.Ordinal);

        var ajaxStart = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/pf-case-start");
        Assert.Equal("write-live-gated", ajaxStart.Status);

        var ajaxAct = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/pf-case-act");
        Assert.Equal("write-live-gated", ajaxAct.Status);
    }

    [Fact]
    public void DryRun_StartRequiresTitleAndProcessAndRefusesConfirm()
    {
        var ok = new ErpPfCaseStartDryRun().Evaluate(new ErpPfCaseStartRequest(4, "Launch case"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missingTitle = new ErpPfCaseStartDryRun().Evaluate(new ErpPfCaseStartRequest(4));
        Assert.Equal("Case title is required", missingTitle.Detail);

        var missingProcess = new ErpPfCaseStartDryRun().Evaluate(new ErpPfCaseStartRequest(0, "Launch case"));
        Assert.Equal("This process has no steps defined yet", missingProcess.Detail);

        var confirm = new ErpPfCaseStartDryRun().Evaluate(new ErpPfCaseStartRequest(4, "Launch case", true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void DryRun_ActRequiresCaseAndRefusesConfirm()
    {
        var ok = new ErpPfCaseActDryRun().Evaluate(new ErpPfCaseActRequest(4, "approve"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);

        var missing = new ErpPfCaseActDryRun().Evaluate(new ErpPfCaseActRequest());
        Assert.Equal("Case not found", missing.Detail);

        var confirm = new ErpPfCaseActDryRun().Evaluate(new ErpPfCaseActRequest(4, "reject", true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpProcessFlowCaseStart", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpProcessFlowCaseAct", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxPfCaseStart", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxPfCaseAct", text, StringComparison.Ordinal);
        Assert.Contains("IErpPfCaseStartWriteService", text, StringComparison.Ordinal);
        Assert.Contains("IErpPfCaseActWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandlePfCaseStartAsync", text, StringComparison.Ordinal);
        Assert.Contains("HandlePfCaseActAsync", text, StringComparison.Ordinal);

        var start = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPfCaseStartWriteService.cs"));
        Assert.Contains("Case started — routed to first assignee", start, StringComparison.Ordinal);
        Assert.Contains("Case title is required", start, StringComparison.Ordinal);
        Assert.Contains("This process has no steps defined yet", start, StringComparison.Ordinal);
        Assert.Contains("Process-flow case tables are not provisioned", start, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", start, StringComparison.Ordinal);

        var act = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPfCaseActWriteService.cs"));
        Assert.Contains("Case not found", act, StringComparison.Ordinal);
        Assert.Contains("This case is already ", act, StringComparison.Ordinal);
        Assert.Contains("No active step on this case", act, StringComparison.Ordinal);
        Assert.Contains("Case rejected at step ", act, StringComparison.Ordinal);
        Assert.Contains("Final step approved — case complete", act, StringComparison.Ordinal);
        Assert.Contains("Approved — routed to ", act, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", act, StringComparison.Ordinal);
    }

    [Fact]
    public void Matrix_MarksStartAndActAspNetLive()
    {
        var row = PhpVsAspNetRemovalMatrix.Rows.First(item => item.Id == "erp-pf-case-cancel");
        Assert.Equal("aspnet", row.WritesOwner);
        Assert.Contains("epc_pf_case_start", row.PhpSource, StringComparison.Ordinal);
        Assert.Contains("epc_pf_case_act", row.PhpSource, StringComparison.Ordinal);
        Assert.Contains("case start INSERT", row.Note, StringComparison.Ordinal);
        Assert.DoesNotContain("Start, act, seed, and schema ensure stay PHP", row.Note, StringComparison.Ordinal);
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
