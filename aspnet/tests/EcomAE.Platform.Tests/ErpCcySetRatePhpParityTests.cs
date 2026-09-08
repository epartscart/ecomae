using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCcySetRatePhpParityTests
{
    [Fact]
    public void MultiCurrencyApp_PostsNativeDatedRateForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpMultiCurrencyGlApp.razor"));
        Assert.Contains("/erp/currency/set-rate", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"from\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"to\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"as_of\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersCcySetRateWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpCcySetRateWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksCcySetRateLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/currency/set-rate");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_ccy_set_rate", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/ccy-set-rate").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpCcySetRateDryRun().Evaluate(new ErpCcySetRateRequest("USD", "AED", 3.67m));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.Equal("invalid_request", new ErpCcySetRateDryRun().Evaluate(new ErpCcySetRateRequest("USD", "AED", 0)).ValidationCode);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpCcySetRateDryRun().Evaluate(new ErpCcySetRateRequest("USD", "AED", 3.67m, true)).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpCcySetRate", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxCcySetRate", text, StringComparison.Ordinal);
        Assert.Contains("HandleCcySetRateAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpCcySetRateWriteService.cs"));
        Assert.Contains("Provide from, to and a positive rate", service, StringComparison.Ordinal);
        Assert.Contains("Rate saved: 1 ", service, StringComparison.Ordinal);
        Assert.Contains("ON DUPLICATE KEY UPDATE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_ccy_ensure_schema", service, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveAsOf_MatchesPhpNoonAndEmpty()
    {
        Assert.Equal(0, ErpCcySetRateWriteService.ResolveAsOfUnix("", 0));
        Assert.Equal(42, ErpCcySetRateWriteService.ResolveAsOfUnix("", 42));
        var unix = ErpCcySetRateWriteService.ResolveAsOfUnix("2026-09-08", 0);
        Assert.Equal(1788868800, unix);
        Assert.Equal("2026-09-08", ErpCcySetRateWriteService.FormatYmd(unix));
        Assert.Equal("3.67", ErpCcySetRateWriteService.FormatRate(3.67m));
        Assert.Equal("3", ErpCcySetRateWriteService.FormatRate(3m));
        Assert.Equal(
            "Rate saved: 1 USD = 3.67 AED as of 2026-09-08",
            ErpCcySetRateWriteService.SuccessMessage("USD", "AED", 3.67m, unix));
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
