using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpBplanPositionAddPhpParityTests
{
    [Fact]
    public void BudgetsApp_PostsNativePositionForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpBudgetsApp.razor"));
        Assert.Contains("ErpBudgetPlanPositionAdd", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"plan_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"title\"", text, StringComparison.Ordinal);
        Assert.Contains("Add forecast position", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersBplanPositionAddWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpBplanPositionAddWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksBplanPositionAddLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/budget-planning/positions/add");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_bplan_position_add", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/bplan-position-add").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpBplanPositionAddDryRun().Evaluate(new ErpBplanPositionAddRequest(PlanId: 1, Title: "Analyst"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpBplanPositionAddDryRun().Evaluate(new ErpBplanPositionAddRequest(ConfirmWrites: true, PlanId: 1)).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpBudgetPlanPositionAdd", text, StringComparison.Ordinal);
        Assert.Contains("HandleBplanPositionAddAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpBplanPositionAddWriteService.cs"));
        Assert.Contains("Forecast position added", service, StringComparison.Ordinal);
        Assert.Contains("Position title is required", service, StringComparison.Ordinal);
        Assert.Contains("Plan not found", service, StringComparison.Ordinal);
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
