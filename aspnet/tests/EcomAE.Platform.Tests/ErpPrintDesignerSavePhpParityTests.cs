using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPrintDesignerSavePhpParityTests
{
    [Fact]
    public void PrintDesignerApp_PostsNativeSaveForm()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpPrintDesignerApp.razor"));
        Assert.Contains("/erp/print-designer/save", text, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", text, StringComparison.Ordinal);
        Assert.Contains("name=\"doc_type\"", text, StringComparison.Ordinal);
        Assert.Contains("New print template", text, StringComparison.Ordinal);
        Assert.Contains("full HTML/CSS bodies", text, StringComparison.Ordinal);
        Assert.Contains("{{company_name}}", text, StringComparison.Ordinal);
        Assert.Contains("{{printed_date}}", text, StringComparison.Ordinal);
        Assert.Contains("srcdoc=\"@PreviewDocument\"", text, StringComparison.Ordinal);
        Assert.Contains("sandbox", text, StringComparison.Ordinal);
        Assert.DoesNotContain("280-character HTML/CSS excerpts", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Excerpt only", text, StringComparison.Ordinal);
        Assert.DoesNotContain("writes=0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Program_RegistersPrintDesignerSaveWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpPrintDesignerSaveWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksPrintDesignerSaveLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/print-designer/save");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_print_template_save", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("PHP remains authoritative", row.Notes, StringComparison.Ordinal);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/print-designer-save").Status);
    }

    [Fact]
    public void DryRun_ValidatesWithoutWriting()
    {
        var ok = new ErpPrintDesignerSaveDryRun().Evaluate(new ErpPrintDesignerSaveRequest());
        Assert.Equal("dry-run-validated", ok.Status);
        Assert.Equal(0, ok.Writes);
        Assert.False(ok.PhpAuthoritative);
        Assert.Equal(
            "confirm_writes_refused",
            new ErpPrintDesignerSaveDryRun().Evaluate(new ErpPrintDesignerSaveRequest(ConfirmWrites: true)).ValidationCode);
    }

    [Fact]
    public void FieldWhitelist_AndPhpEmptyDefaultMatchAjax()
    {
        Assert.Equal(26, ErpPrintDesignerSaveWriteService.AllowedFields.Length);
        Assert.Contains("doc_type", ErpPrintDesignerSaveWriteService.AllowedFields);
        Assert.Contains("custom_css", ErpPrintDesignerSaveWriteService.AllowedFields);
        Assert.DoesNotContain("is_default", ErpPrintDesignerSaveWriteService.AllowedFields);
        Assert.False(ErpPrintDesignerSaveWriteService.IsPhpNonEmpty(null));
        Assert.False(ErpPrintDesignerSaveWriteService.IsPhpNonEmpty(""));
        Assert.False(ErpPrintDesignerSaveWriteService.IsPhpNonEmpty("0"));
        Assert.True(ErpPrintDesignerSaveWriteService.IsPhpNonEmpty("1"));
        Assert.True(ErpPrintDesignerSaveWriteService.IsPhpNonEmpty("on"));
    }

    [Fact]
    public void DetailProjection_PreservesFullBodiesAndExposesAllAllowlistedFields()
    {
        var sql = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Migration/LegacySurfaceDashboardSql.cs"));
        var designerSql = sql[sql.IndexOf("FROM `epc_erp_print_templates`", StringComparison.Ordinal)..];
        Assert.Contains("IFNULL(`header_html`,'') AS header_html_excerpt", designerSql, StringComparison.Ordinal);
        Assert.Contains("IFNULL(`footer_html`,'') AS footer_html_excerpt", designerSql, StringComparison.Ordinal);
        Assert.Contains("IFNULL(`custom_css`,'') AS custom_css_excerpt", designerSql, StringComparison.Ordinal);
        Assert.DoesNotContain("LEFT(IFNULL(`header_html`", designerSql, StringComparison.Ordinal);
        Assert.DoesNotContain("LEFT(IFNULL(`custom_css`", designerSql, StringComparison.Ordinal);

        var catalog = SurfacePayloadContractCatalog.All.First(item =>
            item.AspNetRoute == "/erp/print-templates");
        Assert.Contains("Full HTML/CSS bodies plus layout metadata", catalog.FunctionsCovered, StringComparer.Ordinal);
        Assert.Contains("rendering/version history remain PHP-authoritative", catalog.FunctionsCovered, StringComparer.Ordinal);
    }

    [Fact]
    public void Module_WiresFormAliasesAndLiveComplete()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpPrintDesignerSave", text, StringComparison.Ordinal);
        Assert.Contains("HandlePrintDesignerSaveAsync", text, StringComparison.Ordinal);
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpPrintDesignerSaveWriteService.cs"));
        Assert.Contains("Template saved", service, StringComparison.Ordinal);
        Assert.Contains("Save failed", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", service, StringComparison.Ordinal);
        Assert.DoesNotContain("epc_erp_print_render(", service, StringComparison.Ordinal);
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
