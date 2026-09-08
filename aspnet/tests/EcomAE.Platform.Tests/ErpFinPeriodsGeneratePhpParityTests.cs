using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpFinPeriodsGeneratePhpParityTests
{
    [Fact]
    public void FinAdvancedApp_PostsNativeGenerateForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpFinAdvancedApp.razor"));
        Assert.Contains("/erp/fin/periods/generate", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"fy\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"start_month\"", text, StringComparison.Ordinal);
        Assert.Contains("Generate periods", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersFinPeriodsGenerateWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpFinPeriodsGenerateWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksFinPeriodsGenerateLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/fin/periods/generate");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_fin_periods_generate", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/fin-periods-generate").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpFinPeriodsGenerateDryRun().Evaluate(new ErpFinPeriodsGenerateRequest());
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpFinPeriodsGenerateDryRun().Evaluate(new ErpFinPeriodsGenerateRequest(true)).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpFinPeriodsGenerate", text, StringComparison.Ordinal);
        Assert.Contains("HandleFinPeriodsGenerateAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpFinPeriodsGenerateWriteService.cs"));
        Assert.Contains("Generated ", service, StringComparison.Ordinal);
        Assert.Contains("ON DUPLICATE KEY UPDATE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_fin_adv_ensure_schema", service, StringComparison.Ordinal);
    }

    [Fact]
    public void PeriodDates_MatchPhpUtcMktime()
    {
        Assert.Equal(2000, ErpFinPeriodsGenerateWriteService.PhpMktimeYear(0));
        Assert.Equal(2069, ErpFinPeriodsGenerateWriteService.PhpMktimeYear(69));
        Assert.Equal(1970, ErpFinPeriodsGenerateWriteService.PhpMktimeYear(70));
        Assert.Equal(2000, ErpFinPeriodsGenerateWriteService.PhpMktimeYear(100));
        Assert.Equal(2026, ErpFinPeriodsGenerateWriteService.PhpMktimeYear(2026));

        var jan = ErpFinPeriodsGenerateWriteService.PeriodDates(2026, 1);
        Assert.Equal(12, jan.Count);
        Assert.Equal(1, jan[0].PeriodNo);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), jan[0].StartUnix);
        Assert.Equal(new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.Zero).ToUnixTimeSeconds(), jan[0].EndUnix);
        Assert.Equal(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), jan[1].StartUnix);
        Assert.Equal(new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero).ToUnixTimeSeconds(), jan[11].EndUnix);

        var apr = ErpFinPeriodsGenerateWriteService.PeriodDates(2026, 4);
        Assert.Equal(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), apr[0].StartUnix);
        Assert.Equal(new DateTimeOffset(2027, 3, 31, 23, 59, 59, TimeSpan.Zero).ToUnixTimeSeconds(), apr[11].EndUnix);

        var clamped = ErpFinPeriodsGenerateWriteService.PeriodDates(2026, 0);
        Assert.Equal(jan[0].StartUnix, clamped[0].StartUnix);

        var y0 = ErpFinPeriodsGenerateWriteService.PeriodDates(0, 1);
        Assert.Equal(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds(), y0[0].StartUnix);
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
