using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpOaCalendarSavePhpParityTests
{
    [Fact]
    public void TenantConfigApp_PostsNativeCalendarSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpTenantConfigApp.razor"));
        Assert.Contains("action=\"/erp/org/calendars/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"code\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"working_days\"", text, StringComparison.Ordinal);
        Assert.Contains("Save calendar", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersCalendarSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpOaCalendarSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksCalendarSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/org/calendars/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_oa_calendar_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/oa-calendar-save").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpOaCalendarSaveDryRun().Evaluate(new ErpOaCalendarSaveRequest(Code: "UAE"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal("Calendar code is required", new ErpOaCalendarSaveDryRun().Evaluate(new ErpOaCalendarSaveRequest()).Detail);
        Assert.Equal("At least one working day is required", new ErpOaCalendarSaveDryRun().Evaluate(new ErpOaCalendarSaveRequest(Code: "X", WorkingDays: "")).Detail);
        Assert.Equal("confirm_writes_refused", new ErpOaCalendarSaveDryRun().Evaluate(new ErpOaCalendarSaveRequest(ConfirmWrites: true, Code: "UAE")).ValidationCode);
    }

    [Fact]
    public void Validate_MatchesPhpBounds()
    {
        Assert.Equal("Calendar code is required", ErpOaCalendarSaveWriteService.Validate("", "1,2,3,4,5"));
        Assert.Equal("At least one working day is required", ErpOaCalendarSaveWriteService.Validate("UAE", "0,8"));
        Assert.Null(ErpOaCalendarSaveWriteService.Validate("UAE", "1,2,3,4,5"));
        Assert.Null(ErpOaCalendarSaveWriteService.Validate("UAE", "6,7"));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpOrgCalendarsSave", text, StringComparison.Ordinal);
        Assert.Contains("HandleOaCalendarSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpOaCalendarSaveWriteService.cs"));
        Assert.Contains("Calendar saved", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
            var alt = Path.GetFullPath(Path.Combine(dir.FullName, "..", "..", "..", "..", "..", relative));
            if (File.Exists(alt)) return alt;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Could not locate " + relative);
    }
}
