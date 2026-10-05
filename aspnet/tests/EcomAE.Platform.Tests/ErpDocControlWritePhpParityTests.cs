using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Pins the PHP sync_einvoice_seller / docx_run_reminders port contract:
/// DI registration, live-gated catalog rows, verbatim field/DDL anchors.
/// </summary>
public sealed class ErpDocControlWritePhpParityTests
{
    private static string RepoFile(params string[] parts)
        => Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", Path.Combine(parts));

    [Fact]
    public void DocControlWriteService_IsRegistered()
    {
        var program = File.ReadAllText(RepoFile("src", "EcomAE.Platform", "Program.cs"));
        Assert.Contains("IErpDocControlWriteService, EcomAE.Platform.Erp.ErpDocControlWriteService", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_Rows_AreLiveGated()
    {
        var catalog = File.ReadAllText(RepoFile("src", "EcomAE.Platform", "Migration", "SurfacePayloadContractCatalog.cs"));
        foreach (var route in new[] { "/erp/ajax/sync-einvoice-seller", "/erp/ajax/docx-run-reminders" })
        {
            var i = catalog.IndexOf("\"" + route + "\"", StringComparison.Ordinal);
            Assert.True(i > 0, route + " missing from catalog");
            Assert.Contains("write-live-gated", catalog.Substring(i, 1400), StringComparison.Ordinal);
            Assert.Contains("confirm_writes=true", catalog.Substring(i, 1400), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Module_Handlers_UseTheLiveWriteService()
    {
        var module = File.ReadAllText(RepoFile("src", "EcomAE.Platform", "Modules", "ErpModule.cs"));
        Assert.Contains("EcomAeRoutes.ErpAjaxSyncEinvoiceSeller, HandleSyncEinvoiceSellerAsync", module, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.ErpAjaxDocxRunReminders, HandleDocxRunRemindersAsync", module, StringComparison.Ordinal);
        Assert.Contains("IErpDocControlWriteService", module, StringComparison.Ordinal);
    }

    [Fact]
    public void Service_PortsThePhpAnchors()
    {
        var service = File.ReadAllText(RepoFile("src", "EcomAE.Platform", "Erp", "ErpDocControlWriteService.cs"));
        // epc_dc_sync_seller_from_einvoice anchors
        Assert.Contains("epc_document_company", service, StringComparison.Ordinal);
        Assert.Contains("epc_einvoice_settings", service, StringComparison.Ordinal);
        Assert.Contains("seller_address_line1", service, StringComparison.Ordinal);
        Assert.Contains("seller_bank_account", service, StringComparison.Ordinal);
        Assert.Contains("PINT-AE", service, StringComparison.Ordinal);
        Assert.Contains("row_version", service, StringComparison.Ordinal);
        Assert.Contains("Imported seller details from E-Invoicing settings", service, StringComparison.Ordinal);
        // epc_docx_run_reminders anchors
        Assert.Contains("epc_erp_doc_expiry", service, StringComparison.Ordinal);
        Assert.Contains("epc_erp_doc_expiry_reminders", service, StringComparison.Ordinal);
        Assert.Contains("INSERT IGNORE", service, StringComparison.Ordinal);
        Assert.Contains("x_doc_threshold", service, StringComparison.Ordinal);
        Assert.Contains("[Document expiry]", service, StringComparison.Ordinal);
        Assert.Contains("Please action the renewal before the expiry date.", service, StringComparison.Ordinal);
        Assert.Contains("owner_email", service, StringComparison.Ordinal);
        // Company resolution as PHP epc_erp_active_company_id.
        Assert.Contains("ErpFinAdvancedCompany.ResolveAsync", service, StringComparison.Ordinal);
    }
}
