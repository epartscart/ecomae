using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_bos_wf_decide</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpBosWfDecidePhpParityTests
{
    [Fact]
    public void ApprovalsApp_PostsNativeDecideForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpApprovalsApp.razor"));
        Assert.Contains("action=\"/erp/approvals/requests/decide\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"request_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"decision\"", text, StringComparison.Ordinal);
        Assert.Contains("Decide request", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersBosWfDecideWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpBosWfDecideWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpBosWfDecideWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksBosWfDecideLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/approvals/requests/decide");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_bos_wf_decide", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/bos-wf-decide");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DecodeStepCount_MatchesPhpFallback()
    {
        Assert.Equal(1, ErpBosWfDecideWriteService.DecodeStepCount(null));
        Assert.Equal(1, ErpBosWfDecideWriteService.DecodeStepCount("[]"));
        Assert.Equal(2, ErpBosWfDecideWriteService.DecodeStepCount("""[{"role":"Manager","label":"A"},{"role":"Finance","label":"B"}]"""));
    }

    [Fact]
    public void DryRun_RequiresRequestAndRefusesConfirm()
    {
        var ok = new ErpBosWfDecideDryRun().Evaluate(new ErpBosWfDecideRequest(4, "approve"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missing = new ErpBosWfDecideDryRun().Evaluate(new ErpBosWfDecideRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("Request not pending", missing.Detail);

        var confirm = new ErpBosWfDecideDryRun().Evaluate(new ErpBosWfDecideRequest(4, "approve", null, true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpApprovalsRequestDecide", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxBosWfDecide", text, StringComparison.Ordinal);
        Assert.Contains("IErpBosWfDecideWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleBosWfDecideAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpBosWfDecideWriteService.cs"));
        Assert.Contains("Approved (final)", service, StringComparison.Ordinal);
        Assert.Contains("Approved — advanced to next step", service, StringComparison.Ordinal);
        Assert.Contains("Rejected", service, StringComparison.Ordinal);
        Assert.Contains("Request not pending", service, StringComparison.Ordinal);
        Assert.Contains("Approval request table is not provisioned", service, StringComparison.Ordinal);
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
