using EcomAE.Platform.Bos;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class BosPayrollEmployeeAddWriteTests
{
    [Fact]
    public void Route_exposes_employee_add()
    {
        Assert.Equal("/bos/payroll/employee-add", EcomAeRoutes.BosPayrollEmployeeAdd);
    }

    [Fact]
    public void Php_employee_data_matches_php_defaults()
    {
        Assert.Equal("acme1", BosPayrollWriteService.PhpBosSiteKey("Acme-1!"));
        Assert.Equal("", BosPayrollWriteService.PhpBosSiteKey("!!!"));
        Assert.Equal(12.5m, BosPayrollWriteService.PhpFloat("12.5x"));
        Assert.Equal(0m, BosPayrollWriteService.PhpFloat("abc"));

        var today = BosPayrollWriteService.DefaultJoinDate(new DateTime(2026, 9, 11));
        Assert.Equal("2026-09-11", today);

        var empty = BosPayrollWriteService.ParseEmployeeData(null, new DateTime(2026, 9, 11));
        Assert.Equal("", empty.EmployeeId);
        Assert.Equal("", empty.FullName);
        Assert.Equal("AED", empty.Currency);
        Assert.Equal("2026-09-11", empty.JoinDate);
        Assert.Equal(0m, empty.BasicSalary);
        Assert.Equal(empty, BosPayrollWriteService.ParseEmployeeData("{}", new DateTime(2026, 9, 11)));
        Assert.Equal(empty, BosPayrollWriteService.ParseEmployeeData("not-json", new DateTime(2026, 9, 11)));

        var parsed = BosPayrollWriteService.ParseEmployeeData(
            "{\"employee_id\":\"E1\",\"full_name\":\"Ada\",\"iban\":\"AE1\",\"basic_salary\":\"1200.5x\",\"currency\":\"USD\",\"join_date\":\"2020-01-02\"}",
            new DateTime(2026, 9, 11));
        Assert.Equal("E1", parsed.EmployeeId);
        Assert.Equal("Ada", parsed.FullName);
        Assert.Equal("AE1", parsed.Iban);
        Assert.Equal(1200.5m, parsed.BasicSalary);
        Assert.Equal("USD", parsed.Currency);
        Assert.Equal("2020-01-02", parsed.JoinDate);

        var emptyJoin = BosPayrollWriteService.ParseEmployeeData("{\"join_date\":\"\",\"currency\":\"\"}", new DateTime(2026, 9, 11));
        Assert.Equal("", emptyJoin.JoinDate);
        Assert.Equal("", emptyJoin.Currency);
    }

    [Fact]
    public async Task Missing_site_key_fails_invalid_before_db()
    {
        var written = await new BosPayrollWriteService(new UnconfiguredConnections())
            .AddEmployeeAsync("!!!", "{\"full_name\":\"Ada\"}");
        Assert.False(written.Succeeded);
        Assert.Equal("invalid", written.Code);
        Assert.Equal("Missing site_key", written.Message);
    }

    [Fact]
    public void Page_posts_native_employee_add()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/BosFleetSummaryApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/bos/payroll/employee-add\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"site_key\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"full_name\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("stay Classic", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@bind", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_employee_add_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/bos/payroll/employee-add");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("employee_add", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_payroll_employee_add", write.Notes, StringComparison.Ordinal);
        Assert.Contains("epc_payroll_employees", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("ajax_epc_bos.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Super-CP", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_employee_add_write()
    {
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/BosModule.cs"));
        Assert.Contains("BosPayrollEmployeeAdd", module, StringComparison.Ordinal);
        Assert.Contains("AddEmployeeAsync", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Bos/BosPayrollWriteService.cs"));
        Assert.Contains("epc_payroll_employee_add", service, StringComparison.Ordinal);
        Assert.Contains("epc_payroll_approve_run", service, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `epc_payroll_employees`", service, StringComparison.Ordinal);
        Assert.Contains("schema-ensure stays Classic", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
    }

    private sealed class UnconfiguredConnections : EcomAE.Platform.Erp.IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unconfigured factory must not open.");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repository root with aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj was not found.");
    }
}
