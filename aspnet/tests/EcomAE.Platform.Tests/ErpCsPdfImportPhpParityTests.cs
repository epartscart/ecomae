using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCsPdfImportPhpParityTests
{
    private static string RepoFile(params string[] parts)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..");
        return File.ReadAllText(Path.Combine(root, Path.Combine(parts)));
    }

    [Fact]
    public void CsPdfImportService_PortAnchorPhpContracts()
    {
        var service = RepoFile("src/EcomAE.Platform/Erp/ErpCsPdfImportService.cs");
        Assert.Contains("Upload must be a PDF file", service);
        Assert.Contains("PDF exceeds 15 MB limit", service);
        Assert.Contains("Could not extract any text from the PDF.", service);
        Assert.Contains("poppler-utils", service);
        Assert.Contains("pdftotext", service);
        Assert.Contains("ExtractTextFromStreams", service);
        Assert.Contains("DecodeFlateStream", service);
        Assert.Contains("PdfUnescapeString", service);
        Assert.Contains("FindHsCodeBlock", service);
        Assert.Contains("CollectRunsBackward", service);
        Assert.Contains("DedupeRepeatedBlock", service);
        Assert.Contains("DedupeLineItems", service);
        Assert.Contains("LineItemsZoneEnd", service);
        Assert.Contains("ParseBox45Fields", service);
        Assert.Contains("epc_custom_shipping_pdfs", service);
        Assert.Contains("staging", service);
        Assert.Contains("AssertUniqueDeclarationNumberAsync", service);
        Assert.Contains("customs_emirate", service);
        Assert.Contains("DUBAI", service);
        Assert.Contains("Invalid PDF file", service);
    }

    [Fact]
    public void CsPdfImportHandler_BindsPhpContract()
    {
        var module = RepoFile("src/EcomAE.Platform/Modules/ErpModule.cs");
        Assert.Contains("ErpAjaxCsImportDeclarationPdf, HandleCsImportDeclarationPdfAsync", module);
        Assert.Contains("declaration_pdf", module);
        Assert.Contains("No PDF file uploaded", module);
        Assert.Contains("PDF parsed — review auto-filled fields", module);
        Assert.Contains("Review highlighted fields before saving.", module);
        Assert.Contains("pdf_preview_url", module);
        Assert.Contains("pdftotext_diag_url", module);
        Assert.Contains("epc_cs_apply_parsed_to_form_data", module);
        Assert.Contains("pdf_autofill_keys", module);
    }

    [Fact]
    public void Catalog_PromotesCsPdfImportToLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.Single(
            item => item.AspNetRouteOrCapability == "/erp/ajax/cs-import-declaration-pdf");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("confirm_writes=true", row.Notes);
    }
}
