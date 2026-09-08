using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_hr_payroll_run</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpHrPayrollRunPhpParityTests
{
    [Fact]
    public void HrOverviewApp_PostsNativePayrollGenerateForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpHrOverviewApp.razor"));
        Assert.Contains("action=\"/erp/hr/payroll/generate\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"period\"", text, StringComparison.Ordinal);
        Assert.Contains("Generate HR payroll", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Payroll generate stays on the Classic twin", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersPayrollRunWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpHrPayrollRunWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpHrPayrollRunWriteService", text, StringComparison.Ordinal);
        Assert.Contains("IErpHrPayrollRunDryRun", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksPayrollRunLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/hr/payroll/generate");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_hr_payroll_run", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/hr-payroll-generate");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpHrPayrollRunDryRun().Evaluate(new ErpHrPayrollRunRequest("2026-01"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);
        Assert.Equal("2026-01", ok.Period);

        var missing = new ErpHrPayrollRunDryRun().Evaluate(new ErpHrPayrollRunRequest("2026-13"));
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("Invalid period (use YYYY-MM)", missing.Detail);

        var confirm = new ErpHrPayrollRunDryRun().Evaluate(new ErpHrPayrollRunRequest("2026-01", true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Defaults_MatchPhpComputeAndMessage()
    {
        Assert.Equal("2026-01", ErpHrPayrollRunWriteService.NormalizePeriod("2026-01"));
        Assert.Null(ErpHrPayrollRunWriteService.NormalizePeriod("2026-13"));
        Assert.Equal(
            DateTime.UtcNow.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
            ErpHrPayrollRunWriteService.NormalizePeriod(""));
        Assert.Equal(
            "HR payroll generated for 2026-01 — 2 employees, net 12,500.00 AED",
            ErpHrPayrollRunWriteService.FormatGeneratedMessage("2026-01", 2, 12500m));

        var slip = ErpHrPayrollRunWriteService.ComputePayslip(
            8000,
            0,
            [new ErpHrPayrollDeduction("Loan", 500)]);
        Assert.Equal(8000m, slip.Gross);
        Assert.Equal(500m, slip.Deductions);
        Assert.Equal(7500m, slip.Net);
        Assert.Equal("Loan", Assert.Single(slip.DeductionDetail).Label);

        var mapped = ErpHrPayrollRunWriteService.ParseDeductions(
            """{"1":[{"label":"Loan","amount":500}]}""",
            0,
            null,
            0);
        Assert.Equal(500m, Assert.Single(mapped[1]).Amount);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpHrPayrollGenerate", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxHrPayrollGenerate", text, StringComparison.Ordinal);
        Assert.Contains("IErpHrPayrollRunWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleHrPayrollRunAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpHrPayrollRunWriteService.cs"));
        Assert.Contains("HR payroll generated for", service, StringComparison.Ordinal);
        Assert.Contains("HR payroll table is not provisioned", service, StringComparison.Ordinal);
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
