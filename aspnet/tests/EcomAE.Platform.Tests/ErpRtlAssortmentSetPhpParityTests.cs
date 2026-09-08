using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpRtlAssortmentSetPhpParityTests
{
    [Fact]
    public void JewelleryRetailApp_PostsNativeAssortmentForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpJewelleryRetailApp.razor"));
        Assert.Contains("action=\"/erp/retail/assortments/set\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"channel_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"item_id\"", text, StringComparison.Ordinal);
        Assert.Contains("Set assortment", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersRtlAssortmentSetWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpRtlAssortmentSetWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksRtlAssortmentSetLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/retail/assortments/set");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_rtl_assortment_set", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/rtl-assortment-set").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpRtlAssortmentSetDryRun().Evaluate(new ErpRtlAssortmentSetRequest(ChannelId: 1, ItemId: 9));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpRtlAssortmentSetDryRun().Evaluate(new ErpRtlAssortmentSetRequest(ConfirmWrites: true, ChannelId: 1)).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpRetailAssortmentsSet", text, StringComparison.Ordinal);
        Assert.Contains("HandleRtlAssortmentSetAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpRtlAssortmentSetWriteService.cs"));
        Assert.Contains("Assortment updated", service, StringComparison.Ordinal);
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
