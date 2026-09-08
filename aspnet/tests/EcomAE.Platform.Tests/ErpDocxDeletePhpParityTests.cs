using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpDocxDeletePhpParityTests
{
    [Fact]
    public void DocExpiryApp_PostsNativeDeleteForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpDocExpiryApp.razor"));
        Assert.Contains("/erp/doc-expiry/delete", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"id\"", text, StringComparison.Ordinal);
        Assert.Contains("Remove document", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersDocxDeleteWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpDocxDeleteWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksDocxDeleteLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/doc-expiry/delete");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_docx_delete", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/docx-delete").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpDocxDeleteDryRun().Evaluate(new ErpDocxDeleteRequest(Id: 12));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpDocxDeleteDryRun().Evaluate(new ErpDocxDeleteRequest(ConfirmWrites: true, Id: 12)).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpDocExpiryDelete", text, StringComparison.Ordinal);
        Assert.Contains("HandleDocxDeleteAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpDocxDeleteWriteService.cs"));
        Assert.Contains("Document removed from register", service, StringComparison.Ordinal);
        Assert.Contains("epc_erp_doc_expiry_reminders", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_docx_run_reminders(", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_docx_save(", service, StringComparison.Ordinal);
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
