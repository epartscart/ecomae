using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpMultiEntitySavePhpParityTests
{
    [Fact]
    public void MultiEntityApp_PostsNativePreferenceForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpMultiEntityApp.razor"));
        Assert.Contains("action=\"/erp/multi-entity/preference/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"enabled\"", text, StringComparison.Ordinal);
        Assert.Contains("Save preference", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersMultiEntitySaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpMultiEntitySaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksMultiEntitySaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/multi-entity/preference/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_multi_entity_set", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/multi-entity-save").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpMultiEntitySaveDryRun().Evaluate(new ErpMultiEntitySaveRequest(Enabled: 1));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal("confirm_writes_refused", new ErpMultiEntitySaveDryRun().Evaluate(new ErpMultiEntitySaveRequest(ConfirmWrites: true, Enabled: 1)).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpMultiEntityPreferenceSave", text, StringComparison.Ordinal);
        Assert.Contains("HandleMultiEntitySaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpMultiEntitySaveWriteService.cs"));
        Assert.Contains("Multi-entity preference saved", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
    }

    [Fact]
    public void ResidualFootnote_MarksPreferenceLive()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Migration/PhpVsAspNetRemovalMatrix.cs"));
        Assert.Contains("ajax_erp multi_entity_save", text, StringComparison.Ordinal);
        Assert.Contains("are ASP.NET-live", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ajax_erp multi_entity_save stays dry-run", text, StringComparison.Ordinal);
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
