using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpIntgEntitySavePhpParityTests
{
    [Fact]
    public void IntegrationsApp_PostsNativeEntitySaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpIntegrationsApp.razor"));
        Assert.Contains("action=\"/erp/integrations/entities/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"name\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"source_table\"", text, StringComparison.Ordinal);
        Assert.Contains("Save data entity", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersEntitySaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpIntgEntitySaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksEntitySaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/integrations/entities/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_intg_entity_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/intg-entity-save").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpIntgEntitySaveDryRun().Evaluate(new ErpIntgEntitySaveRequest(Name: "DemoEntity"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal("Entity name is required", new ErpIntgEntitySaveDryRun().Evaluate(new ErpIntgEntitySaveRequest()).Detail);
        Assert.Equal("confirm_writes_refused", new ErpIntgEntitySaveDryRun().Evaluate(new ErpIntgEntitySaveRequest(ConfirmWrites: true, Name: "DemoEntity")).ValidationCode);
    }

    [Fact]
    public void Validate_AndFields_MatchPhp()
    {
        Assert.Equal("Entity name is required", ErpIntgEntitySaveWriteService.Validate(""));
        Assert.Null(ErpIntgEntitySaveWriteService.Validate("DemoEntity"));
        Assert.Equal(["id", "name", "country"], ErpIntgEntitySaveWriteService.ParseFields("id, name, country"));
        Assert.Equal(["id", "name"], ErpIntgEntitySaveWriteService.ParseFields("""["id","name"]"""));
        Assert.Equal("""["id","name"]""", ErpIntgEntitySaveWriteService.SerializeFields("id,name"));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpIntegrationsEntitiesSave", text, StringComparison.Ordinal);
        Assert.Contains("HandleIntgEntitySaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpIntgEntitySaveWriteService.cs"));
        Assert.Contains("Data entity saved", service, StringComparison.Ordinal);
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
