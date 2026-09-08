using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpBplanAdvancePhpParityTests
{
    [Fact]
    public void BudgetsApp_PostsNativeAdvanceForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpBudgetsApp.razor"));
        Assert.Contains("/erp/budgets/advance", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"id\"", text, StringComparison.Ordinal);
        Assert.Contains("Advance plan", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersBplanAdvanceWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpBplanAdvanceWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksBplanAdvanceLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/budgets/advance");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_bplan_advance_stage", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/bplan-advance").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpBplanAdvanceDryRun().Evaluate(new ErpBplanAdvanceRequest(4));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpBplanAdvanceDryRun().Evaluate(new ErpBplanAdvanceRequest(4, true)).ValidationCode);
        Assert.Equal(
            "invalid_request",
            new ErpBplanAdvanceDryRun().Evaluate(new ErpBplanAdvanceRequest(0)).ValidationCode);
    }

    [Fact]
    public void Stages_MatchPhpOrder()
    {
        Assert.Equal(["draft", "review", "approved", "published"], ErpBplanAdvanceWriteService.Stages);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpBplanAdvance", text, StringComparison.Ordinal);
        Assert.Contains("HandleBplanAdvanceAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpBplanAdvanceWriteService.cs"));
        Assert.Contains("Budget plan published", service, StringComparison.Ordinal);
        Assert.Contains("Budget plan advanced to ", service, StringComparison.Ordinal);
        Assert.Contains("Plan not found", service, StringComparison.Ordinal);
        Assert.Contains("already at the final stage", service, StringComparison.Ordinal);
        Assert.Contains("headcount` * `annual_cost", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_bplan_ensure_schema", service, StringComparison.Ordinal);
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
