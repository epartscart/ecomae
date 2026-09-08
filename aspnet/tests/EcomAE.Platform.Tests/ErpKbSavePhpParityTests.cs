using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpKbSavePhpParityTests
{
    [Fact]
    public void GuideApp_PostsNativeArticleSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpGuideApp.razor"));
        Assert.Contains("action=\"/erp/guide/articles/save\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"title\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"category\"", text, StringComparison.Ordinal);
        Assert.Contains("Publish article", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersKbSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpKbSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksKbSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/guide/articles/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_kb_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/kb-save").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpKbSaveDryRun().Evaluate(new ErpKbSaveRequest(Title: "Month-end close"));
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal("Article title required", new ErpKbSaveDryRun().Evaluate(new ErpKbSaveRequest()).Detail);
        Assert.Equal("confirm_writes_refused", new ErpKbSaveDryRun().Evaluate(new ErpKbSaveRequest(ConfirmWrites: true, Title: "Month-end close")).ValidationCode);
    }

    [Fact]
    public void Validate_AndSlug_MatchPhp()
    {
        Assert.Equal("Article title required", ErpKbSaveWriteService.Validate(""));
        Assert.Null(ErpKbSaveWriteService.Validate("Month-end close"));
        Assert.Equal("month-end-close", ErpKbSaveWriteService.SlugFromTitle("Month-end close"));
        Assert.Equal("hello-world-", ErpKbSaveWriteService.SlugFromTitle("Hello World!"));
        Assert.Equal("fulfilment-pipeline", ErpKbSaveWriteService.SlugFromTitle("Fulfilment pipeline"));
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpGuideArticlesSave", text, StringComparison.Ordinal);
        Assert.Contains("HandleKbSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpKbSaveWriteService.cs"));
        Assert.Contains("Knowledge article published", service, StringComparison.Ordinal);
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
