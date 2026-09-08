using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_ins_save</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpInsSavePhpParityTests
{
    [Fact]
    public void InsuranceApp_PostsNativePolicySaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpInsuranceComplianceApp.razor"));
        Assert.Contains("action=\"/erp/insurance/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"policy_no\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"expiry_date_str\"", text, StringComparison.Ordinal);
        Assert.Contains("Save policy", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersInsSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpInsSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpInsSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksInsSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/insurance/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_ins_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/ins-save");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void Defaults_MatchPhp()
    {
        Assert.Equal("other", ErpInsSaveWriteService.NormalizeClass(""));
        Assert.Equal("other", ErpInsSaveWriteService.NormalizeClass("bogus"));
        Assert.Equal("marine", ErpInsSaveWriteService.NormalizeClass("marine"));
        Assert.Equal("active", ErpInsSaveWriteService.NormalizeStatus(""));
        Assert.Equal("active", ErpInsSaveWriteService.NormalizeStatus("nope"));
        Assert.Equal("cancelled", ErpInsSaveWriteService.NormalizeStatus("cancelled"));
        Assert.Equal("AED", ErpInsSaveWriteService.NormalizeCurrency(""));
        Assert.Equal("USD", ErpInsSaveWriteService.NormalizeCurrency("usd"));
        Assert.Equal("90,60,30,7", ErpInsSaveWriteService.NormalizeReminderDays(""));
        Assert.Equal("60,30", ErpInsSaveWriteService.NormalizeReminderDays("30,60,30"));
        Assert.Equal(0, ErpInsSaveWriteService.ResolveDateUnix(""));
        Assert.Equal(1_788_480_000, ErpInsSaveWriteService.ResolveDateUnix("2026-09-04"));
    }

    [Fact]
    public void DryRun_RequiresPolicyExpiryAndRefusesConfirm()
    {
        var ok = new ErpInsSaveDryRun().Evaluate(new ErpInsSaveRequest(0, "MAR-001", "2026-12-31"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missingNo = new ErpInsSaveDryRun().Evaluate(new ErpInsSaveRequest());
        Assert.Equal("invalid_request", missingNo.ValidationCode);
        Assert.Equal("Policy number is required", missingNo.Detail);

        var missingExp = new ErpInsSaveDryRun().Evaluate(new ErpInsSaveRequest(0, "MAR-001"));
        Assert.Equal("Expiry date is required", missingExp.Detail);

        var confirm = new ErpInsSaveDryRun().Evaluate(new ErpInsSaveRequest(0, "MAR-001", "2026-12-31", true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpInsuranceSave", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxInsSave", text, StringComparison.Ordinal);
        Assert.Contains("IErpInsSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleInsSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpInsSaveWriteService.cs"));
        Assert.Contains("Policy saved", service, StringComparison.Ordinal);
        Assert.Contains("Insurance policy table is not provisioned", service, StringComparison.Ordinal);
        Assert.Contains("Policy number is required", service, StringComparison.Ordinal);
        Assert.Contains("Expiry date is required", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_docx", service, StringComparison.Ordinal);
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
