using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCtrOcrPhpParityTests
{
    [Fact]
    public void ContractsApp_PostsNativeOcrForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpContractsApp.razor"));
        Assert.Contains("/erp/contracts/ocr", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"contract_id\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"text\"", text, StringComparison.Ordinal);
        Assert.Contains("Save OCR", text, StringComparison.Ordinal);
        Assert.DoesNotContain("writes=0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersCtrOcrWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpCtrOcrWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksCtrOcrLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/contracts/ocr");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_ctr_ocr_store", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/ctr-ocr").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting_ZeroIdAllowed()
    {
        var ok = new ErpCtrOcrDryRun().Evaluate(new ErpCtrOcrRequest());
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpCtrOcrDryRun().Evaluate(new ErpCtrOcrRequest(ConfirmWrites: true)).ValidationCode);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpCtrOcr", text, StringComparison.Ordinal);
        Assert.Contains("HandleCtrOcrAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpCtrOcrWriteService.cs"));
        Assert.Contains("OCR text saved", service, StringComparison.Ordinal);
        Assert.Contains("ocr_text", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_ctr_ensure_schema", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_ctr_sign", service, StringComparison.Ordinal);
        Assert.DoesNotContain("Tesseract", service, StringComparison.Ordinal);
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
