using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpDocumentControlCompanySaveWriteTests
{
    [Fact]
    public void Route_exposes_document_control_write()
    {
        Assert.Equal("/cp/document-control/write", EcomAeRoutes.CpDocumentControlWrite);
    }

    [Fact]
    public void Field_allowlist_matches_php()
    {
        Assert.Contains("legal_name", CpDocumentControlWriteService.FieldMax.Keys);
        Assert.Contains("bank_iban", CpDocumentControlWriteService.FieldMax.Keys);
        Assert.Contains("legal_footer", CpDocumentControlWriteService.FieldMax.Keys);
        Assert.DoesNotContain("row_version", CpDocumentControlWriteService.FieldMax.Keys);
        Assert.Equal("eParts", CpDocumentControlWriteService.Clip("  eParts  ", 255));
        Assert.Equal(32, CpDocumentControlWriteService.Clip(new string('t', 40), 32).Length);
        Assert.Equal("invoice", CpDocumentControlWriteService.NormalizeTemplateCode(" invoice "));
        Assert.Contains("title", CpDocumentControlWriteService.TemplateFieldMax.Keys);
        Assert.DoesNotContain("code", CpDocumentControlWriteService.TemplateFieldMax.Keys);
    }

    [Fact]
    public void Page_posts_native_save_company()
    {
        var razor = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Components/Pages/CpDocumentControlApp.razor"));
        Assert.Contains("method=\"post\"", razor, StringComparison.Ordinal);
        Assert.Contains("action=\"/cp/document-control/write\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"true\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_company\"", razor, StringComparison.Ordinal);
        Assert.Contains("value=\"save_template\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"legal_name\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"code\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"title\"", razor, StringComparison.Ordinal);
        Assert.Contains("name=\"trn\"", razor, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", razor, StringComparison.Ordinal);
        Assert.Contains("Classic twin", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onsubmit:preventDefault", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"file\"", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_marks_save_company_live_gated()
    {
        var write = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/document-control/write");
        Assert.Equal("write-live-gated", write.Status);
        Assert.Contains("ajax_document_control.php", write.Notes, StringComparison.Ordinal);
        Assert.Contains("Classic", write.Notes, StringComparison.Ordinal);
        Assert.Contains("save_company", write.Notes, StringComparison.Ordinal);
        Assert.Contains("save_template", write.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_and_module_save_company()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpDocumentControlWriteService", program, StringComparison.Ordinal);
        var module = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("ICpDocumentControlWriteService", module, StringComparison.Ordinal);
        Assert.Contains("cutoverAllowed = false", module, StringComparison.Ordinal);
        var service = File.ReadAllText(Path.Combine(FindRepoRoot(), "aspnet/src/EcomAE.Platform/Cp/CpDocumentControlWriteService.cs"));
        Assert.Contains("epc_dc_save_company", service, StringComparison.Ordinal);
        Assert.Contains("epc_dc_save_template", service, StringComparison.Ordinal);
        Assert.Contains("does not invent a send", service, StringComparison.Ordinal);
        Assert.Contains("UPDATE `epc_document_company`", service, StringComparison.Ordinal);
        Assert.Contains("UPDATE `epc_document_templates`", service, StringComparison.Ordinal);
        Assert.Contains("save_template", module, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("SmtpClient", service, StringComparison.Ordinal);
        Assert.DoesNotContain("cutoverAllowed = true", service, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repository root with aspnet/src/EcomAE.Platform/EcomAE.Platform.csproj was not found.");
    }
}
