using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_hr_employee_save</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpHrEmpSavePhpParityTests
{
    [Fact]
    public void HrOverviewApp_PostsNativeEmployeeSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpHrOverviewApp.razor"));
        Assert.Contains("action=\"/erp/hr/employees/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"code\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"name\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"join_date_str\"", text, StringComparison.Ordinal);
        Assert.Contains("Save employee", text, StringComparison.Ordinal);
        Assert.Contains("Add an employee", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Employee save and schema ensure stay PHP", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersEmployeeSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpHrEmpSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpHrEmpSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksEmployeeSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/hr/employees/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_hr_employee_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/hr-emp-save");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpHrEmpSaveDryRun().Evaluate(new ErpHrEmpSaveRequest(Code: "E001", Name: "Ahmed"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.False(ok.CutoverAllowed);
        Assert.True(ok.WouldWrite);

        var missing = new ErpHrEmpSaveDryRun().Evaluate(new ErpHrEmpSaveRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("Code and name are required", missing.Detail);

        var confirm = new ErpHrEmpSaveDryRun().Evaluate(new ErpHrEmpSaveRequest(
            ConfirmWrites: true,
            Code: "E001",
            Name: "Ahmed"));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Validate_AndJoinDate_MatchPhp()
    {
        Assert.Equal("Code and name are required", ErpHrEmpSaveWriteService.Validate("", ""));
        Assert.Equal("Code and name are required", ErpHrEmpSaveWriteService.Validate("E001", ""));
        Assert.Null(ErpHrEmpSaveWriteService.Validate("E001", "Ahmed"));
        Assert.Equal(1_788_480_000, ErpHrEmpSaveWriteService.ResolveJoinUnix("2026-09-04", null, 99));
        Assert.Equal(1_788_480_000, ErpHrEmpSaveWriteService.ResolveJoinUnix(null, "2026-09-04", 99));
        Assert.Equal(99, ErpHrEmpSaveWriteService.ResolveJoinUnix(null, null, 99));
        Assert.Equal(0, ErpHrEmpSaveWriteService.ResolveExtraDateUnix(""));
        Assert.Equal(1_788_480_000, ErpHrEmpSaveWriteService.ResolveExtraDateUnix("2026-09-04"));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpHrEmployeesSave", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxHrEmpSave", text, StringComparison.Ordinal);
        Assert.Contains("IErpHrEmpSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleHrEmpSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpHrEmpSaveWriteService.cs"));
        Assert.Contains("Employee saved", service, StringComparison.Ordinal);
        Assert.Contains("HR employee table is not provisioned", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER TABLE", service, StringComparison.Ordinal);
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
