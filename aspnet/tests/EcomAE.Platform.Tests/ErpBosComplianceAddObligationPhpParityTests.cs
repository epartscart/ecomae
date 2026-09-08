using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_bos_compliance_add_obligation</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpBosComplianceAddObligationPhpParityTests
{
    [Fact]
    public void Soc2App_PostsNativeAddObligationForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpSoc2ComplianceApp.razor"));
        Assert.Contains("action=\"/erp/compliance/obligations/add\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"title\"", text, StringComparison.Ordinal);
        Assert.Contains("Add obligation", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersBosComplianceAddObligationWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpBosComplianceAddObligationWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpBosComplianceAddObligationWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksBosComplianceAddObligationLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/compliance/obligations/add");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_bos_compliance_add_obligation", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/bos-compliance-add-obligation");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DefaultCode_MatchesPhpSlugShape()
    {
        var code = ErpBosComplianceAddObligationWriteService.DefaultCode("VAT return", 1777901234);
        Assert.Equal("obl_vat_return_1234", code);
    }

    [Fact]
    public void DryRun_RequiresTitleAndRefusesConfirm()
    {
        var ok = new ErpBosComplianceAddObligationDryRun().Evaluate(new ErpBosComplianceAddObligationRequest("VAT return"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missing = new ErpBosComplianceAddObligationDryRun().Evaluate(new ErpBosComplianceAddObligationRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("Title required", missing.Detail);

        var confirm = new ErpBosComplianceAddObligationDryRun().Evaluate(new ErpBosComplianceAddObligationRequest("VAT return", null, true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpComplianceObligationAdd", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxBosComplianceAddObligation", text, StringComparison.Ordinal);
        Assert.Contains("IErpBosComplianceAddObligationWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleBosComplianceAddObligationAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpBosComplianceAddObligationWriteService.cs"));
        Assert.Contains("Obligation saved", service, StringComparison.Ordinal);
        Assert.Contains("Title required", service, StringComparison.Ordinal);
        Assert.Contains("Compliance obligation table is not provisioned", service, StringComparison.Ordinal);
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
