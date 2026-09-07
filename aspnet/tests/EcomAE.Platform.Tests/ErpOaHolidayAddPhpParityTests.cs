using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpOaHolidayAddPhpParityTests
{
    [Fact]
    public void TenantConfigApp_PostsNativeHolidayAddForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpTenantConfigApp.razor"));
        Assert.Contains("action=\"/erp/org/holidays/add\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"calendar_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"holiday_date\"", text, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"2026-12-02\"", text, StringComparison.Ordinal);
        Assert.Contains("Add holiday", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersHolidayAddWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpOaHolidayAddWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksHolidayAddLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/org/holidays/add");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_oa_holiday_add", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/oa-holiday-add").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpOaHolidayAddDryRun().Evaluate(new ErpOaHolidayAddRequest(1, "2026-12-02", "UAE National Day"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal("Holiday date must be Y-m-d", new ErpOaHolidayAddDryRun().Evaluate(new ErpOaHolidayAddRequest(1, "02/12/2026")).Detail);
        Assert.Equal("confirm_writes_refused", new ErpOaHolidayAddDryRun().Evaluate(new ErpOaHolidayAddRequest(ConfirmWrites: true, CalendarId: 1, HolidayDate: "2026-12-02")).ValidationCode);
    }

    [Fact]
    public void Validate_MatchesPhpBounds()
    {
        Assert.Equal("Holiday date must be Y-m-d", ErpOaHolidayAddWriteService.Validate(""));
        Assert.Equal("Holiday date must be Y-m-d", ErpOaHolidayAddWriteService.Validate("2026/12/02"));
        Assert.Null(ErpOaHolidayAddWriteService.Validate("2026-12-02"));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpOrgHolidaysAdd", text, StringComparison.Ordinal);
        Assert.Contains("HandleOaHolidayAddAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpOaHolidayAddWriteService.cs"));
        Assert.Contains("Holiday added", service, StringComparison.Ordinal);
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
