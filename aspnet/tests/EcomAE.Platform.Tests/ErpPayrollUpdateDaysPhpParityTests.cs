using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_erp_payroll_update_line_days</c> twin.</summary>
public sealed class ErpPayrollUpdateDaysPhpParityTests
{
    [Fact]
    public void PayrollApp_PostsNativeUpdateDaysForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpPayrollApp.razor"));
        Assert.Contains("action=\"/erp/payroll/update-days\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"line_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"days_worked\"", text, StringComparison.Ordinal);
        Assert.Contains("Save line days", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersUpdateDaysWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPayrollUpdateDaysWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksUpdateDaysLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/payroll/update-days");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_payroll_update_line_days", row.Notes, StringComparison.Ordinal);
        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/payroll-update-days");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpPayrollUpdateDaysDryRun().Evaluate(new ErpPayrollUpdateDaysRequest(4, 15));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);

        var bad = new ErpPayrollUpdateDaysDryRun().Evaluate(new ErpPayrollUpdateDaysRequest(-1, 15));
        Assert.Equal("invalid_request", bad.ValidationCode);

        var confirm = new ErpPayrollUpdateDaysDryRun().Evaluate(new ErpPayrollUpdateDaysRequest(4, 15, true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
    }

    [Fact]
    public void Calc_MatchesPhp()
    {
        var full = ErpPayrollUpdateDaysWriteService.Calc(6000m, 0m, 30m, 30);
        Assert.Equal(6000m, full.GrossPay);
        Assert.Equal(0m, full.Deductions);
        Assert.Equal(6000m, full.NetPay);
        Assert.Equal(200m, full.DailyRate);
        var half = ErpPayrollUpdateDaysWriteService.Calc(6000m, 0m, 15m, 30);
        Assert.Equal(3000m, half.GrossPay);
        Assert.Equal(3000m, half.NetPay);
        var extra = ErpPayrollUpdateDaysWriteService.Calc(6000m, 0m, 31m, 30);
        Assert.Equal(1m, extra.ExtraDays);
        Assert.Equal(6200m, extra.NetPay);
    }

    [Fact]
    public void Module_WiresFormAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpPayrollUpdateDays", text, StringComparison.Ordinal);
        Assert.Contains("HandlePayrollUpdateDaysAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPayrollUpdateDaysWriteService.cs"));
        Assert.Contains("Days updated — net ", service, StringComparison.Ordinal);
        Assert.Contains("Cannot edit paid payroll line", service, StringComparison.Ordinal);
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
