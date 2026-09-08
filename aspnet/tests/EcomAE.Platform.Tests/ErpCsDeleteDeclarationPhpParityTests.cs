using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCsDeleteDeclarationPhpParityTests
{
    [Fact]
    public void CarriersApp_PostsNativeDeleteForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpCarriersApp.razor"));
        Assert.Contains("/erp/custom-shipping/delete", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("Delete declaration", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersCsDeleteWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpCsDeleteDeclarationWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksCsDeleteLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/custom-shipping/delete");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_cs_delete_declaration", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/cs-delete-declaration").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpCsDeleteDeclarationDryRun().Evaluate(new ErpCsDeleteDeclarationRequest());
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpCsDeleteDeclarationDryRun().Evaluate(new ErpCsDeleteDeclarationRequest(1, "x", true)).ValidationCode);
        Assert.Equal(
            "invalid_request",
            new ErpCsDeleteDeclarationDryRun().Evaluate(new ErpCsDeleteDeclarationRequest(-1)).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpCsDeleteDeclaration", text, StringComparison.Ordinal);
        Assert.Contains("HandleCsDeleteDeclarationAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpCsDeleteDeclarationWriteService.cs"));
        Assert.Contains("Declaration deleted", service, StringComparison.Ordinal);
        Assert.Contains("Invalid declaration id", service, StringComparison.Ordinal);
        Assert.Contains("Declaration not found", service, StringComparison.Ordinal);
        Assert.Contains("epc_custom_shipping_declaration_items", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("unlink", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_cs_ensure", service, StringComparison.Ordinal);
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
