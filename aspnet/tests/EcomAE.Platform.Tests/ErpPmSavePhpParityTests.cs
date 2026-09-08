using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPmSavePhpParityTests
{
    [Fact]
    public void BudgetsApp_PostsNativePmSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpBudgetsApp.razor"));
        Assert.Contains("/erp/pm/save", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"pm_table\"", text, StringComparison.Ordinal);
        Assert.Contains("Save platform master", text, StringComparison.Ordinal);
        Assert.DoesNotContain("writes=0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersPmSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPmSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksPmSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/pm/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_pm_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/pm-save").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpPmSaveDryRun().Evaluate(new ErpPmSaveRequest());
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpPmSaveDryRun().Evaluate(new ErpPmSaveRequest(ConfirmWrites: true)).ValidationCode);
    }

    [Fact]
    public void Registry_MatchesPhpWhitelist()
    {
        Assert.Equal(11, ErpPmSaveWriteService.Registry.Count);
        Assert.Equal(
            new[] { "code", "name", "legal_entity_id", "parent_id", "manager", "note" },
            ErpPmSaveWriteService.Registry["epc_erp_pm_business_units"]);
        Assert.Equal(
            new[] { "dimension_id", "code", "name", "note" },
            ErpPmSaveWriteService.Registry["epc_erp_pm_dimension_values"]);
        Assert.Equal(
            new[] { "code", "name", "symbology", "pattern", "note" },
            ErpPmSaveWriteService.Registry["epc_erp_pm_barcode_formats"]);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpPmSave", text, StringComparison.Ordinal);
        Assert.Contains("HandlePmSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPmSaveWriteService.cs"));
        Assert.Contains("Saved", service, StringComparison.Ordinal);
        Assert.Contains("Unknown master table", service, StringComparison.Ordinal);
        Assert.Contains("Nothing to save", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_pm_toggle", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_pm_budget_save", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_pm_listing_save", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_pm_cheque_save", service, StringComparison.Ordinal);
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
