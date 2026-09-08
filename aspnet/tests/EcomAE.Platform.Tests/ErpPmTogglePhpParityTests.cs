using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPmTogglePhpParityTests
{
    [Fact]
    public void BudgetsApp_PostsNativeToggleForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpBudgetsApp.razor"));
        Assert.Contains("/erp/pm/toggle", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"pm_table\"", text, StringComparison.Ordinal);
        Assert.Contains("Toggle platform master", text, StringComparison.Ordinal);
        Assert.DoesNotContain("writes=0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersPmToggleWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPmToggleWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksPmToggleLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/pm/toggle");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_pm_toggle", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/pm-toggle").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpPmToggleDryRun().Evaluate(new ErpPmToggleRequest(1));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpPmToggleDryRun().Evaluate(new ErpPmToggleRequest(1, ConfirmWrites: true)).ValidationCode);
    }

    [Fact]
    public void Registry_AndActiveMatchPhp()
    {
        Assert.Equal(11, ErpPmToggleWriteService.Registry.Count);
        Assert.Contains("epc_erp_pm_business_units", ErpPmToggleWriteService.Registry.Keys);
        Assert.False(ErpPmToggleWriteService.IsPhpActive(null));
        Assert.False(ErpPmToggleWriteService.IsPhpActive("0"));
        Assert.True(ErpPmToggleWriteService.IsPhpActive("1"));
        Assert.True(ErpPmToggleWriteService.IsPhpActive("2"));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpPmToggle", text, StringComparison.Ordinal);
        Assert.Contains("HandlePmToggleAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPmToggleWriteService.cs"));
        Assert.Contains("Updated", service, StringComparison.Ordinal);
        Assert.Contains("Unknown master table", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_pm_save", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_pm_budget_save", service, StringComparison.Ordinal);
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
