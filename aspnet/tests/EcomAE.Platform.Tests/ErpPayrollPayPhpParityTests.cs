using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_erp_payroll_pay_run</c> twin.</summary>
public sealed class ErpPayrollPayPhpParityTests
{
    [Fact]
    public void PayrollApp_PostsNativePayForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpPayrollApp.razor"));
        Assert.Contains("action=\"/erp/payroll/pay\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"run_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"cash_account_id\"", text, StringComparison.Ordinal);
        Assert.Contains("Pay salaries", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersPayWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPayrollPayWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksPayLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/payroll/pay");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_payroll_pay_run", row.Notes, StringComparison.Ordinal);
        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/payroll-pay");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpPayrollPayDryRun().Evaluate(new ErpPayrollPayRequest(2, 1));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);

        var bad = new ErpPayrollPayDryRun().Evaluate(new ErpPayrollPayRequest(-1));
        Assert.Equal("invalid_request", bad.ValidationCode);

        var confirm = new ErpPayrollPayDryRun().Evaluate(new ErpPayrollPayRequest(2, 1, true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpPayrollPay", text, StringComparison.Ordinal);
        Assert.Contains("HandlePayrollPayAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPayrollPayWriteService.cs"));
        Assert.Contains("Salaries paid — ", service, StringComparison.Ordinal);
        Assert.Contains("Already paid", service, StringComparison.Ordinal);
        Assert.Contains("Nothing to pay", service, StringComparison.Ordinal);
        Assert.Contains("No cash/bank account", service, StringComparison.Ordinal);
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
