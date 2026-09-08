using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_bos_wf_save_rule</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpBosWfSaveRulePhpParityTests
{
    [Fact]
    public void ApprovalsApp_PostsNativeSaveRuleForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpApprovalsApp.razor"));
        Assert.Contains("action=\"/erp/approvals/rules/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"name\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"entity_type\"", text, StringComparison.Ordinal);
        Assert.Contains("Save rule", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersBosWfSaveRuleWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpBosWfSaveRuleWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpBosWfSaveRuleWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksBosWfSaveRuleLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/approvals/rules/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_bos_wf_save_rule", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/bos-wf-save-rule");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void NormalizeSteps_DefaultsManagerWhenEmpty()
    {
        var steps = ErpBosWfSaveRuleWriteService.NormalizeSteps([]);
        Assert.Single(steps);
        Assert.Equal("Manager", steps[0].Role);
        Assert.Equal("Approval", steps[0].Label);
    }

    [Fact]
    public void DryRun_RequiresNameAndEntityAndRefusesConfirm()
    {
        var ok = new ErpBosWfSaveRuleDryRun().Evaluate(new ErpBosWfSaveRuleRequest(0, "High-value PO", "purchase_order"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missing = new ErpBosWfSaveRuleDryRun().Evaluate(new ErpBosWfSaveRuleRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("Name and document type required", missing.Detail);

        var confirm = new ErpBosWfSaveRuleDryRun().Evaluate(new ErpBosWfSaveRuleRequest(0, "High-value PO", "purchase_order", true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpApprovalsRuleSave", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxBosWfSaveRule", text, StringComparison.Ordinal);
        Assert.Contains("IErpBosWfSaveRuleWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleBosWfSaveRuleAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpBosWfSaveRuleWriteService.cs"));
        Assert.Contains("Approval rule saved", service, StringComparison.Ordinal);
        Assert.Contains("Name and document type required", service, StringComparison.Ordinal);
        Assert.Contains("Approval rule table is not provisioned", service, StringComparison.Ordinal);
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
