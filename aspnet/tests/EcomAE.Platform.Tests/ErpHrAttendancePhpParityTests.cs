using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_hr_attendance_log</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpHrAttendancePhpParityTests
{
    [Fact]
    public void HrOverviewApp_PostsNativeAttendanceForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpHrOverviewApp.razor"));
        Assert.Contains("action=\"/erp/hr/attendance/log\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"employee_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"work_date_str\"", text, StringComparison.Ordinal);
        Assert.Contains("Record attendance", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Attendance, payroll generate, and schema ensure stay PHP", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersAttendanceWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpHrAttendanceWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpHrAttendanceWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksAttendanceLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/hr/attendance/log");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_hr_attendance_log", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/hr-attendance");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpHrAttendanceDryRun().Evaluate(new ErpHrAttendanceRequest(9, "present"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missing = new ErpHrAttendanceDryRun().Evaluate(new ErpHrAttendanceRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);
        Assert.Equal("Select an employee", missing.Detail);

        var confirm = new ErpHrAttendanceDryRun().Evaluate(new ErpHrAttendanceRequest(9, "present", true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void NormalizeWorkDate_UsesUtcMidnight()
    {
        Assert.Equal(1_788_480_000, ErpHrAttendanceWriteService.NormalizeWorkDateUnix(null, "2026-09-04", 99));
        Assert.Equal(1_788_480_000, ErpHrAttendanceWriteService.NormalizeWorkDateUnix("1788480000", null, 99));
        var today = DateTime.UtcNow.Date;
        var expectedToday = new DateTimeOffset(today.Year, today.Month, today.Day, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        Assert.Equal(expectedToday, ErpHrAttendanceWriteService.NormalizeWorkDateUnix(null, null, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpHrAttendanceLog", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxHrAttendance", text, StringComparison.Ordinal);
        Assert.Contains("IErpHrAttendanceWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleHrAttendanceAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpHrAttendanceWriteService.cs"));
        Assert.Contains("Attendance recorded", service, StringComparison.Ordinal);
        Assert.Contains("HR attendance table is not provisioned", service, StringComparison.Ordinal);
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
