using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCostmItemSetPhpParityTests
{
    [Fact]
    public void CostModelsApp_PostsNativeItemForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpCostModelsApp.razor"));
        Assert.Contains("action=\"/erp/cost-models/items/set\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"item_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"model\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"std_cost\"", text, StringComparison.Ordinal);
        Assert.Contains("Assign costing model", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersCostmItemSetWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpCostmItemSetWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksCostmItemSetLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/cost-models/items/set");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_costm_item_set", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/costm-item-set").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpCostmItemSetDryRun().Evaluate(new ErpCostmItemSetRequest(ItemId: 9001, Model: "fifo"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal("moving_avg", new ErpCostmItemSetDryRun().Evaluate(new ErpCostmItemSetRequest(ItemId: 1)).Model);
        Assert.Equal("Invalid costing model", new ErpCostmItemSetDryRun().Evaluate(new ErpCostmItemSetRequest(Model: "bogus")).Detail);
        Assert.Equal("Invalid costing model", new ErpCostmItemSetDryRun().Evaluate(new ErpCostmItemSetRequest(Model: "")).Detail);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpCostmItemSetDryRun().Evaluate(new ErpCostmItemSetRequest(ConfirmWrites: true, Model: "fifo")).ValidationCode);
    }

    [Fact]
    public void Validate_MatchesPhp()
    {
        Assert.Null(ErpCostmItemSetWriteService.Validate("standard"));
        Assert.Null(ErpCostmItemSetWriteService.Validate("fifo"));
        Assert.Null(ErpCostmItemSetWriteService.Validate("lifo"));
        Assert.Null(ErpCostmItemSetWriteService.Validate("moving_avg"));
        Assert.Equal("Invalid costing model", ErpCostmItemSetWriteService.Validate("bogus"));
        Assert.Equal("Invalid costing model", ErpCostmItemSetWriteService.Validate("FIFO"));
        Assert.Equal("Invalid costing model", ErpCostmItemSetWriteService.Validate(""));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpCostModelsItemsSet", text, StringComparison.Ordinal);
        Assert.Contains("HandleCostmItemSetAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpCostmItemSetWriteService.cs"));
        Assert.Contains("Costing model saved", service, StringComparison.Ordinal);
        Assert.Contains("ON DUPLICATE KEY UPDATE", service, StringComparison.Ordinal);
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
