using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpCsDeclarationPhpParityTests
{
    private static string RepoFile(params string[] parts)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..");
        return File.ReadAllText(Path.Combine(root, Path.Combine(parts)));
    }

    [Fact]
    public void CsDeclarationService_PortAnchorPhpContracts()
    {
        var service = RepoFile("src", "EcomAE.Platform", "Erp", "ErpCsDeclarationWriteService.cs");
        Assert.Contains("epc_custom_shipping_declarations", service);
        Assert.Contains("epc_custom_shipping_declaration_items", service);
        Assert.Contains("uq_cs_declaration_number", service);
        Assert.Contains("Declaration already saved", service);
        Assert.Contains("already exists (record #", service);
        Assert.Contains("Add at least one declaration line item", service);
        Assert.Contains("Invalid declaration type for category", service);
        Assert.Contains("Declaration saved", service);
        Assert.Contains("epc_custom_shipping_pdfs", service);
        Assert.Contains("box_45_lines", service);
        Assert.Contains("box_54_lines", service);
        Assert.Contains("cs_view=reports&cs_report=search_results", service);
        Assert.Contains("customs_inspection_required", service);
    }

    [Fact]
    public void CsDeclarationHandlers_BindPhpFieldsAndReturnContract()
    {
        var module = RepoFile("src", "EcomAE.Platform", "Modules", "ErpModule.cs");
        Assert.Contains("HandleCsSaveDeclarationAsync", module);
        Assert.Contains("HandleCsSubmitDeclarationAsync", module);
        Assert.Contains("ErpAjaxCsSaveDeclaration, HandleCsSaveDeclarationAsync", module);
        Assert.Contains("ErpAjaxCsSubmitDeclaration, HandleCsSubmitDeclarationAsync", module);
        Assert.Contains("Declaration submitted", module);
        Assert.Contains("saved.Id", module);
        Assert.Contains("saved.Redirect", module);
        Assert.Contains("cs_category=", module);
    }

    [Fact]
    public void Catalog_PromotesCsSaveAndSubmitToLiveGated()
    {
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.Single(
            item => item.AspNetRouteOrCapability == "/erp/ajax/cs-save-declaration").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.Single(
            item => item.AspNetRouteOrCapability == "/erp/ajax/cs-submit-declaration").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.Single(
            item => item.AspNetRouteOrCapability == "/erp/ajax/cs-delete-declaration").Status);
    }
}
