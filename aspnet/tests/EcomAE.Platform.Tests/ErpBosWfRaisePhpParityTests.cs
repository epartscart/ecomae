using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_bos_wf_raise</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpBosWfRaisePhpParityTests
{
    [Fact]
    public void ApprovalsApp_PostsNativeRaiseForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpApprovalsApp.razor"));
        Assert.Contains("action=\"/erp/approvals/requests/raise\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"entity_type\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"amount\"", text, StringComparison.Ordinal);
        Assert.Contains("Raise test request", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersBosWfRaiseWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpBosWfRaiseWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpBosWfRaiseWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksBosWfRaiseLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/approvals/requests/raise");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_bos_wf_raise", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/bos-wf-raise-test");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void EncodeSteps_MatchesPhpFallback()
    {
        Assert.Contains("\"role\":\"Manager\"", ErpBosWfRaiseWriteService.EncodeSteps(null), StringComparison.Ordinal);
        Assert.Contains("\"role\":\"Finance\"", ErpBosWfRaiseWriteService.EncodeSteps("""[{"role":"Manager","label":"A"},{"role":"Finance","label":"B"}]"""), StringComparison.Ordinal);
    }

    [Fact]
    public void DryRun_ValidatesAndRefusesConfirm()
    {
        var ok = new ErpBosWfRaiseTestDryRun().Evaluate(new ErpBosWfRaiseTestRequest("purchase_order", 9, "PO-9", 12000));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var confirm = new ErpBosWfRaiseTestDryRun().Evaluate(new ErpBosWfRaiseTestRequest("purchase_order", 9, "PO-9", 12000, null, true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpApprovalsRequestRaise", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxBosWfRaiseTest", text, StringComparison.Ordinal);
        Assert.Contains("IErpBosWfRaiseWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleBosWfRaiseAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpBosWfRaiseWriteService.cs"));
        Assert.Contains("Approval request raised", service, StringComparison.Ordinal);
        Assert.Contains("No rule matched — no approval needed for this amount", service, StringComparison.Ordinal);
        Assert.Contains("Approval rule table is not provisioned", service, StringComparison.Ordinal);
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
