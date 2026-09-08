using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpOplParamsSavePhpParityTests
{
    [Fact]
    public void OrderPlanningApp_PostsNativeParamsForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpOrderPlanningApp.razor"));
        Assert.Contains("ErpOrderPlanningParamsSave", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"item_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"warehouse_id\"", text, StringComparison.Ordinal);
        Assert.Contains("Planning parameters", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersOplParamsSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpOplParamsSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksOplParamsSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/order-planning/params/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_opl_params_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/opl-params-save").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpOplParamsSaveDryRun().Evaluate(new ErpOplParamsSaveRequest(ItemId: 1, WarehouseId: 2));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpOplParamsSaveDryRun().Evaluate(new ErpOplParamsSaveRequest(ConfirmWrites: true, ItemId: 1, WarehouseId: 2)).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpOrderPlanningParamsSave", text, StringComparison.Ordinal);
        Assert.Contains("HandleOplParamsSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpOplParamsSaveWriteService.cs"));
        Assert.Contains("Parameters saved — recalculated", service, StringComparison.Ordinal);
        Assert.Contains("Item and warehouse required", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_opl_compute(", service, StringComparison.Ordinal);
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
