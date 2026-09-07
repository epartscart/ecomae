using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards the live PHP <c>epc_coll_dunning_run</c> twin: SSR form, DI, catalog.
/// Schema ensure stays PHP.
/// </summary>
public sealed class ErpCollectionsDunningRunPhpParityTests
{
    [Fact]
    public void CollectionsDunningApp_PostsNativeDunningForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpCollectionsDunningApp.razor"));
        Assert.Contains("action=\"/erp/collections/dunning/run\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"customers\"", text, StringComparison.Ordinal);
        Assert.Contains("Run dunning", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Dunning run stays on the Classic twin", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersDunningWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpCollectionsDunningRunWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpCollectionsDunningRunWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksDunningLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/collections/dunning/run");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_coll_dunning_run", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/coll-dunning-run");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpCollDunningRunDryRun().Evaluate(new ErpCollDunningRunRequest(Customers: "502|500|0|0|0"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.False(ok.CutoverAllowed);
        Assert.True(ok.WouldWrite);

        var missing = new ErpCollDunningRunDryRun().Evaluate(new ErpCollDunningRunRequest());
        Assert.Equal("invalid_request", missing.ValidationCode);

        var confirm = new ErpCollDunningRunDryRun().Evaluate(new ErpCollDunningRunRequest(true, "502|500|0|0|0"));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Plan_MatchesPhpDunningLevels()
    {
        Assert.Equal(0, ErpCollectionsDunningRunWriteService.Level(0, 0, 0, 0).Level);
        Assert.Equal(1, ErpCollectionsDunningRunWriteService.Level(500, 0, 0, 0).Level);
        Assert.Equal(2, ErpCollectionsDunningRunWriteService.Level(0, 0, 100, 0).Level);
        var late = ErpCollectionsDunningRunWriteService.Level(0, 0, 0, 2000);
        Assert.Equal(3, late.Level);
        Assert.Equal(2000m, late.Overdue);
        Assert.Equal("Final notice — account may be referred to collections.", late.Message);

        var parsed = ErpCollectionsDunningRunWriteService.ParseCustomers("501|0|0|0|0\n502|500|0|0|0\n503|0|0|0|2000");
        var plan = ErpCollectionsDunningRunWriteService.Plan(parsed);
        Assert.Equal(2, plan.Count);
        Assert.Equal(502, plan[0].CustomerId);
        Assert.Equal(1, plan[0].Level);
        Assert.Equal(503, plan[1].CustomerId);
        Assert.Equal(3, plan[1].Level);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpCollectionsDunningRun", text, StringComparison.Ordinal);
        Assert.Contains("IErpCollectionsDunningRunWriteService", text, StringComparison.Ordinal);
        Assert.Contains("customers", text, StringComparison.Ordinal);
        Assert.Contains(
            "Dunning run #",
            File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpCollectionsDunningRunWriteService.cs")),
            StringComparison.Ordinal);
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
