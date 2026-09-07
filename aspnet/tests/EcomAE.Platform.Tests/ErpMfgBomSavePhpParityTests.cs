using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Guards the live PHP <c>epc_mfg_bom_save</c> twin: SSR form, DI, catalog.</summary>
public sealed class ErpMfgBomSavePhpParityTests
{
    [Fact]
    public void ProductionApp_PostsNativeBomSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpProductionOverviewApp.razor"));
        Assert.Contains("action=\"/erp/manufacturing/bom/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"product_item_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"component_item_id\"", text, StringComparison.Ordinal);
        Assert.Contains("Save BOM", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersMfgBomSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpMfgBomSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpMfgBomSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksMfgBomSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/manufacturing/bom/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_mfg_bom_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/mfg-bom-save");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void ParseLines_KeepsPositiveComponentsAndSkipsEmpty()
    {
        var fromJson = ErpMfgBomSaveWriteService.ParseLines(
            null,
            """[{"componentItemId":10,"qtyPer":2.5,"scrapPercent":1.5},{"componentItemId":0,"qtyPer":9}]""",
            null);
        Assert.Single(fromJson);
        Assert.Equal(10, fromJson[0].ComponentItemId);
        Assert.Equal(2.5m, fromJson[0].QtyPer);
        Assert.Equal(1.5m, fromJson[0].ScrapPercent);

        var snake = ErpMfgBomSaveWriteService.ParseLines(
            null,
            """[{"component_item_id":11,"qty_per":3,"scrap_percent":0}]""",
            null);
        Assert.Equal(11, snake[0].ComponentItemId);
        Assert.Equal(3m, snake[0].QtyPer);

        var typed = ErpMfgBomSaveWriteService.ParseLines(
            [new ErpMfgBomLine(12, 1, 0), new ErpMfgBomLine(0, 4, 0)],
            null,
            null);
        Assert.Single(typed);
        Assert.Equal(12, typed[0].ComponentItemId);
    }

    [Fact]
    public void DryRun_RequiresProductLinesAndRefusesConfirm()
    {
        var ok = new ErpMfgBomSaveDryRun().Evaluate(new ErpMfgBomSaveRequest(0, 100, 1));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.True(ok.WouldWrite);

        var missingProduct = new ErpMfgBomSaveDryRun().Evaluate(new ErpMfgBomSaveRequest());
        Assert.Equal("invalid_request", missingProduct.ValidationCode);
        Assert.Equal("Select a finished product", missingProduct.Detail);

        var missingLines = new ErpMfgBomSaveDryRun().Evaluate(new ErpMfgBomSaveRequest(0, 100, 0));
        Assert.Equal("Add at least one component", missingLines.Detail);

        var confirm = new ErpMfgBomSaveDryRun().Evaluate(new ErpMfgBomSaveRequest(0, 100, 1, true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void FormatSavedMessage_MatchesPhp()
    {
        Assert.Equal("BOM saved (#7) with 2 component(s)", ErpMfgBomSaveWriteService.FormatSavedMessage(7, 2));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpManufacturingBomSave", text, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxMfgBomSave", text, StringComparison.Ordinal);
        Assert.Contains("IErpMfgBomSaveWriteService", text, StringComparison.Ordinal);
        Assert.Contains("HandleMfgBomSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpMfgBomSaveWriteService.cs"));
        Assert.Contains("BOM saved (#", service, StringComparison.Ordinal);
        Assert.Contains("Manufacturing BOM tables are not provisioned", service, StringComparison.Ordinal);
        Assert.Contains("Select a finished product", service, StringComparison.Ordinal);
        Assert.Contains("Add at least one component", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_inventory", service, StringComparison.Ordinal);
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
