using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_erp_payroll_generate_run</c> twin.</summary>
public sealed class ErpPayrollGeneratePhpParityTests
{
    [Fact]
    public void PayrollApp_PostsNativeGenerateForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpPayrollApp.razor"));
        Assert.Contains("action=\"/erp/payroll/generate\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("Generate payroll", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersGenerateWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPayrollGenerateWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksGenerateLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/payroll/generate");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_payroll_generate_run", row.Notes, StringComparison.Ordinal);
        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/payroll-generate");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpPayrollGenerateDryRun().Evaluate(new ErpPayrollGenerateRequest("2026-09"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);

        var bad = new ErpPayrollGenerateDryRun().Evaluate(new ErpPayrollGenerateRequest("Sept"));
        Assert.Equal("invalid_request", bad.ValidationCode);

        var confirm = new ErpPayrollGenerateDryRun().Evaluate(new ErpPayrollGenerateRequest("2026-09", true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
    }

    [Fact]
    public void CalcAndPeriod_MatchPhp()
    {
        Assert.Equal("2026-09", ErpPayrollGenerateWriteService.NormalizePeriodLabel("2026-09"));
        Assert.Null(ErpPayrollGenerateWriteService.NormalizePeriodLabel("2026-9"));
        var bounds = ErpPayrollGenerateWriteService.PeriodBoundsUnix("2026-09");
        Assert.Equal(1_788_220_800, bounds.Start);
        Assert.Equal(1_790_812_799, bounds.End);
        var calc = ErpPayrollGenerateWriteService.Calc(6000m, 0m, 30m, 30);
        Assert.Equal(6000m, calc.GrossPay);
        Assert.Equal(0m, calc.Deductions);
        Assert.Equal(6000m, calc.NetPay);
        Assert.Equal(200m, calc.DailyRate);
        var half = ErpPayrollGenerateWriteService.Calc(6000m, 0m, 15m, 30);
        Assert.Equal(3000m, half.GrossPay);
    }

    [Fact]
    public void Module_WiresFormAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpPayrollGenerate", text, StringComparison.Ordinal);
        Assert.Contains("HandlePayrollGenerateAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPayrollGenerateWriteService.cs"));
        Assert.Contains("Payroll generated for ", service, StringComparison.Ordinal);
        Assert.Contains("Payroll table is not provisioned", service, StringComparison.Ordinal);
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
