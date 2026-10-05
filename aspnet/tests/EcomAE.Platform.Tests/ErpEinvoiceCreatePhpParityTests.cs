using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>Anchors the PHP <c>einvoice_create</c> ajax twin (epc_einvoice_build_from_order + save_document).</summary>
public sealed class ErpEinvoiceCreatePhpParityTests
{
    private static string RepoFile(params string[] parts)
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", Path.Combine(parts)));

    [Fact]
    public void ServiceExposesCreateDocumentForEinvoiceCreate()
    {
        var source = RepoFile("src/EcomAE.Platform/Erp/ErpInvoiceFromOrderWriteService.cs");
        Assert.Contains("CreateDocumentAsync(long orderId", source);
        Assert.Contains("epc_einvoice_build_from_order", source);
        Assert.Contains("epc_einvoice_save_document", source);
        Assert.Contains("NextEinvoiceNumberAsync", source);
        Assert.Contains("SupplyCategoryWithFlags", source);
        Assert.Contains("margin_scheme", source);
        Assert.Contains("deemed_supply", source);
        Assert.Contains("AdvanceVatCredit", source);
    }

    [Fact]
    public void HandlerIsWiredWithVerbatimContract()
    {
        var source = RepoFile("src/EcomAE.Platform/Modules/ErpModule.cs");
        Assert.Contains("HandleEinvoiceCreateAsync", source);
        Assert.Contains("transaction_flags", source);
        Assert.Contains("flag_" + "", source);
        Assert.Contains("E-invoice generated and validated", source);
        Assert.Contains(" Advance VAT credited: ", source);
        Assert.Contains("einv_section=view&einv_doc=", source);
        Assert.Contains("CreateDocumentAsync(orderId, flags", source);
    }

    [Fact]
    public void CatalogRowIsWriteLiveGated()
    {
        var source = RepoFile("src/EcomAE.Platform/Migration/SurfacePayloadContractCatalog.cs");
        var idx = source.IndexOf("einvoice_create live", StringComparison.Ordinal);
        Assert.True(idx > 0);
        Assert.Contains("/erp/ajax/einvoice-create", source);
    }
}
