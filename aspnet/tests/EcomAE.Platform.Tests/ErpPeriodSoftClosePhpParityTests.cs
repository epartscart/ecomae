using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPeriodSoftClosePhpParityTests
{
    [Fact]
    public void PeriodCloseApp_PostsNativeSoftCloseForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpPeriodCloseApp.razor"));
        Assert.Contains("/erp/periods/soft-close", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"year_month\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"note\"", text, StringComparison.Ordinal);
        Assert.Contains("Soft-close month", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersPeriodSoftCloseWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPeriodSoftCloseWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksPeriodSoftCloseLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/periods/soft-close");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_period_soft_close", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpPeriodSoftCloseDryRun().Evaluate(new ErpPeriodSoftCloseRequest("2026-08", "wave-b"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpPeriodSoftCloseDryRun().Evaluate(new ErpPeriodSoftCloseRequest("2026-08", "wave-b", true)).ValidationCode);
        Assert.Equal(
            "year_month_required",
            new ErpPeriodSoftCloseDryRun().Evaluate(new ErpPeriodSoftCloseRequest("2026")).ValidationCode);
    }

    [Fact]
    public void YearMonth_MatchesPhpPreg()
    {
        Assert.True(ErpPeriodSoftCloseWriteService.IsPhpYearMonth("2026-08"));
        Assert.True(ErpPeriodSoftCloseWriteService.IsPhpYearMonth("2026-13"));
        Assert.False(ErpPeriodSoftCloseWriteService.IsPhpYearMonth(""));
        Assert.False(ErpPeriodSoftCloseWriteService.IsPhpYearMonth("2026-8"));
        Assert.False(ErpPeriodSoftCloseWriteService.IsPhpYearMonth(" 2026-08 "));
        Assert.False(ErpPeriodSoftCloseWriteService.IsPhpYearMonth("2026"));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpPeriodSoftClose", text, StringComparison.Ordinal);
        Assert.Contains("HandlePeriodSoftCloseAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPeriodSoftCloseWriteService.cs"));
        Assert.Contains("Period soft-closed: ", service, StringComparison.Ordinal);
        Assert.Contains("already locked", service, StringComparison.Ordinal);
        Assert.Contains("already in soft-close", service, StringComparison.Ordinal);
        Assert.Contains("epc_erp_period_close_log", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_period_close_ensure_schema", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_period_lock", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_period_checklist", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Matrix_MentionsSoftCloseOnPeriodCloseRow()
    {
        var row = PhpVsAspNetRemovalMatrix.Rows.First(item => item.Id == "erp-fy-reopen-period");
        Assert.Contains("soft-close", row.Notes, StringComparison.Ordinal);
        Assert.Contains("lock", row.Notes, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("aspnet", row.WritesOwner);
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
