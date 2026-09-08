using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_bos_compliance_set_filing</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpBosComplianceFilePhpParityTests
{
    [Fact]
    public void Soc2App_PostsNativeFilingForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpSoc2ComplianceApp.razor"));
        Assert.Contains("action=\"/erp/compliance/filings/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"obligation_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"period_label\"", text, StringComparison.Ordinal);
        Assert.Contains("Save filing", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersBosComplianceFileWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpBosComplianceFileWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpBosComplianceFileWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksBosComplianceFileLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/compliance/filings/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_bos_compliance_set_filing", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/bos-compliance-file");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_RequiresObligationAndPeriodAndRefusesConfirm()
    {
        var ok = new ErpBosComplianceFileDryRun().Evaluate(new ErpBosComplianceFileRequest(4, "2026-09"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missingObl = new ErpBosComplianceFileDryRun().Evaluate(new ErpBosComplianceFileRequest());
        Assert.Equal("invalid_request", missingObl.ValidationCode);
        Assert.Equal("Select an obligation", missingObl.Detail);

        var missingPeriod = new ErpBosComplianceFileDryRun().Evaluate(new ErpBosComplianceFileRequest(4));
        Assert.Equal("Period label is required", missingPeriod.Detail);

        var confirm = new ErpBosComplianceFileDryRun().Evaluate(new ErpBosComplianceFileRequest(4, "2026-09", true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpComplianceFilingSave", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxBosComplianceFile", text, StringComparison.Ordinal);
        Assert.Contains("IErpBosComplianceFileWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleBosComplianceFileAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpBosComplianceFileWriteService.cs"));
        Assert.Contains("Filing status saved", service, StringComparison.Ordinal);
        Assert.Contains("Select an obligation", service, StringComparison.Ordinal);
        Assert.Contains("Period label is required", service, StringComparison.Ordinal);
        Assert.Contains("Compliance filing table is not provisioned", service, StringComparison.Ordinal);
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
