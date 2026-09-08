using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_bos_retention_save</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpBosRetentionSavePhpParityTests
{
    [Fact]
    public void Soc2App_PostsNativeRetentionForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpSoc2ComplianceApp.razor"));
        Assert.Contains("action=\"/erp/compliance/retention/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"label\"", text, StringComparison.Ordinal);
        Assert.Contains("Save retention", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersBosRetentionSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpBosRetentionSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpBosRetentionSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksBosRetentionSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/compliance/retention/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_bos_retention_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/bos-compliance-save-retention");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DefaultDocType_MatchesPhpSlugShape()
    {
        Assert.Equal("vat_invoices", ErpBosRetentionSaveWriteService.DefaultDocType("VAT invoices"));
    }

    [Fact]
    public void DryRun_RequiresLabelAndRefusesConfirm()
    {
        var ok = new ErpBosComplianceSaveRetentionDryRun().Evaluate(new ErpBosComplianceSaveRetentionRequest("VAT invoices"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missing = new ErpBosComplianceSaveRetentionDryRun().Evaluate(new ErpBosComplianceSaveRetentionRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("Label required", missing.Detail);

        var confirm = new ErpBosComplianceSaveRetentionDryRun().Evaluate(new ErpBosComplianceSaveRetentionRequest("VAT invoices", null, true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpComplianceRetentionSave", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxBosComplianceSaveRetention", text, StringComparison.Ordinal);
        Assert.Contains("IErpBosRetentionSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleBosRetentionSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpBosRetentionSaveWriteService.cs"));
        Assert.Contains("Retention rule saved", service, StringComparison.Ordinal);
        Assert.Contains("Label required", service, StringComparison.Ordinal);
        Assert.Contains("Retention rule table is not provisioned", service, StringComparison.Ordinal);
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
