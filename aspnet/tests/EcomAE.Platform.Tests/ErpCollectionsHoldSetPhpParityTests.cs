using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards the live PHP <c>epc_coll_hold_set</c> twin: SSR form, DI, catalog.
/// Schema ensure stays PHP.
/// </summary>
public sealed class ErpCollectionsHoldSetPhpParityTests
{
    [Fact]
    public void CollectionsDunningApp_PostsNativeHoldForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpCollectionsDunningApp.razor"));
        Assert.Contains("action=\"/erp/collections/hold/set\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"customer_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"place\"", text, StringComparison.Ordinal);
        Assert.Contains("Set credit hold", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Dunning run and credit hold stay", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersHoldWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpCollectionsHoldSetWriteService", text, StringComparison.Ordinal);
        Assert.Contains("ErpCollectionsHoldSetWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksHoldLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/collections/hold/set");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_coll_hold_set", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);

        var ajax = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/coll-hold-set");
        Assert.Equal("write-live-gated", ajax.Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpCollHoldSetDryRun().Evaluate(new ErpCollHoldSetRequest(9));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.True(ok.WritesBlocked);
        Assert.False(ok.PhpAuthoritative);
        Assert.False(ok.CutoverAllowed);
        Assert.True(ok.WouldWrite);

        var missing = new ErpCollHoldSetDryRun().Evaluate(new ErpCollHoldSetRequest(0));
        Assert.Equal("invalid_request", missing.ValidationCode);

        var confirm = new ErpCollHoldSetDryRun().Evaluate(new ErpCollHoldSetRequest(9, ConfirmWrites: true));
        Assert.Equal("confirm_writes_refused", confirm.ValidationCode);
        Assert.Equal(0, confirm.Writes);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpCollectionsHoldSet", text, StringComparison.Ordinal);
        Assert.Contains("IErpCollectionsHoldSetWriteService", text, StringComparison.Ordinal);
        Assert.Contains("customer_id", text, StringComparison.Ordinal);
        Assert.Contains(
            "Credit hold updated",
            File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpCollectionsHoldSetWriteService.cs")),
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
