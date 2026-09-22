using EcomAE.Platform.Cp;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// cp/content/shop/procurement (procurement_main.php + ajax_procurement.php + procurement_guide.php)
/// twin: the desk page and its JSON dispatcher must keep the PHP action / DOM / tab contract.
/// </summary>
public sealed class CpProcurementDeskPhpParityTests
{
    [Fact]
    public void Dispatcher_CoversEveryPhpAjaxAction_AndKeepsCpAdminGate()
    {
        var php = File.ReadAllText(FindRepoFile("cp/content/shop/procurement/ajax_procurement.php"));
        var ajax = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Cp/CpProcurementAjax.cs"));
        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        var start = module.IndexOf("MapPost(EcomAeRoutes.CpProcurementAjax", StringComparison.Ordinal);
        Assert.True(start > 0);
        var endpoint = module[start..];

        Assert.Equal(9, CpProcurementAjax.Actions.Count);
        foreach (var action in CpProcurementAjax.Actions)
        {
            Assert.Contains("case '" + action + "':", php, StringComparison.Ordinal);
            Assert.Contains("case \"" + action + "\":", ajax, StringComparison.Ordinal);
        }

        Assert.Equal("/cp/procurement/ajax", EcomAeRoutes.CpProcurementAjax);
        Assert.Contains("!session.Capabilities.Contains(\"cp\")", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("Capabilities.Contains(\"erp\")", endpoint[..endpoint.IndexOf("DisableAntiforgery", StringComparison.Ordinal)], StringComparison.Ordinal);
        Assert.Contains("message = \"Access denied\"", endpoint, StringComparison.Ordinal);
        Assert.Contains("status = false, message", ajax, StringComparison.Ordinal);
        Assert.Contains("\"Unknown action\"", ajax, StringComparison.Ordinal);
    }

    [Fact]
    public void Tabs_MatchPhpPage()
    {
        var php = File.ReadAllText(FindRepoFile("cp/content/shop/procurement/procurement_main.php"));
        Assert.Equal(8, CpProcurementDeskService.Tabs.Count);
        foreach (var (key, label) in CpProcurementDeskService.Tabs)
        {
            Assert.Contains("'" + key + "' => '" + label + "'", php, StringComparison.Ordinal);
        }

        Assert.Equal("dashboard", CpProcurementDeskService.Tabs[0].Key);
        Assert.Equal("guide", CpProcurementDeskService.Tabs[^1].Key);
    }

    [Fact]
    public void DeskPage_KeepsPhpDomIdsClassesFormsAndGuide()
    {
        var razor = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpProcurementApp.razor"));
        var php = File.ReadAllText(FindRepoFile("cp/content/shop/procurement/procurement_main.php"));
        var guide = File.ReadAllText(FindRepoFile("cp/content/shop/procurement/procurement_guide.php"));

        Assert.Contains("@page \"/cp/procurement-app\"", razor, StringComparison.Ordinal);
        foreach (var id in new[]
                 {
                     "epc_proc_msg", "epc_proc_sync_wh", "epc_proc_recon", "epc_proc_form_supplier", "epc_proc_form_update_sup",
                     "epc_proc_form_purchase", "epc_proc_form_purchase_order", "epc_proc_form_pay", "epc_proc_form_advance",
                 })
        {
            Assert.Contains("id=\"" + id + "\"", php, StringComparison.Ordinal);
            Assert.Contains("id=\"" + id + "\"", razor, StringComparison.Ordinal);
        }

        foreach (var cls in new[]
                 {
                     "epc-proc-hero", "epc-proc-kpi", "epc-proc-nav", "epc-proc-msg", "epc-proc-note",
                     "epc-proc-name", "epc-proc-code", "epc-proc-sub", "epc-proc-recon", "epc-erp-shell",
                 })
        {
            Assert.Contains(cls, php, StringComparison.Ordinal);
            Assert.Contains(cls, razor, StringComparison.Ordinal);
        }

        Assert.Contains("linear-gradient(135deg, #0f172a 0%, #1e4d3a 100%)", razor, StringComparison.Ordinal);
        Assert.Contains("Supplier procurement — not warehouse stock", razor, StringComparison.Ordinal);
        Assert.Contains("epc_erp_ui_css.php", razor, StringComparison.Ordinal);
        foreach (var kpi in new[] { "Active suppliers", "With TRN", "Purchase bills", "Payable balance", "Advances paid", "Warehouses" })
        {
            Assert.Contains("<div class=\"lbl\">" + kpi + "</div>", razor, StringComparison.Ordinal);
        }

        foreach (var name in new[]
                 {
                     "vendor_code", "trn", "country_code", "vat_registered", "legal_reg_no", "legal_reg_type", "authority_name",
                     "address_line1", "city", "emirate", "contact_email", "contact_phone", "payment_terms", "storage_id", "notes",
                     "invoice_number", "amount_ex_vat", "order_id", "account_id", "amount", "reference",
                 })
        {
            Assert.Contains("name=\"" + name + "\"", razor, StringComparison.Ordinal);
        }

        foreach (var (id, action) in new[]
                 {
                     ("epc_proc_form_supplier", "create_supplier"), ("epc_proc_form_update_sup", "update_supplier"),
                     ("epc_proc_form_purchase", "create_purchase"), ("epc_proc_form_purchase_order", "purchase_from_order"),
                     ("epc_proc_form_pay", "supplier_payment"), ("epc_proc_form_advance", "record_advance"),
                 })
        {
            Assert.Contains("bindForm('" + id + "', '" + action + "')", razor, StringComparison.Ordinal);
        }

        Assert.Contains("postAction('sync_suppliers')", razor, StringComparison.Ordinal);
        Assert.Contains("End-to-end procurement flow", guide, StringComparison.Ordinal);
        Assert.Contains("End-to-end procurement flow", razor, StringComparison.Ordinal);
        Assert.Contains("Warehouse vs supplier", razor, StringComparison.Ordinal);
        Assert.Contains("UAE e-invoicing (purchase side)", razor, StringComparison.Ordinal);
        Assert.Contains("Live snapshot", razor, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpParityModuleBody", razor, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteService_DelegatesToLiveErpServices_AndUsesParameterizedSql()
    {
        var src = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Cp/CpProcurementWriteService.cs"));
        Assert.Contains("IErpPurchaseInvoiceWriteService", src, StringComparison.Ordinal);
        Assert.Contains("IErpCashWriteService", src, StringComparison.Ordinal);
        Assert.Contains("IErpInventoryMovementWriteService", src, StringComparison.Ordinal);
        Assert.Contains("IErpTaxAmountCalculator", src, StringComparison.Ordinal);
        Assert.Contains("epc_procurement_advances", src, StringComparison.Ordinal);
        Assert.Contains("epc_erp_purchase_inv_lines", src, StringComparison.Ordinal);
        Assert.Contains("inv_receipt_posted", src, StringComparison.Ordinal);
        Assert.DoesNotContain("DryRun", src, StringComparison.Ordinal);
        Assert.DoesNotContain("$\"INSERT", src, StringComparison.Ordinal);
        Assert.DoesNotContain("$\"UPDATE", src, StringComparison.Ordinal);
        Assert.Equal("AE", CpProcurementWriteService.NormalizeCountry("uae"));
        Assert.Equal("AE", CpProcurementWriteService.NormalizeCountry(""));
        Assert.Equal("IN", CpProcurementWriteService.NormalizeCountry("in"));
    }

    [Theory]
    [InlineData("/CP/shop/procurement/procurement?tab=suppliers", "/cp/procurement-app")]
    [InlineData("/CP/shop/procurement", "/cp/procurement-app")]
    public void RouteMaps_PointProcurementAtTheTwin_NotRequisitions(string phpHref, string expected)
    {
        Assert.Equal(expected, PhpSurfaceLinkMap.MapCpPhpPath(phpHref));
        Assert.True(CpShopModuleRouteMap.TryMap("procurement", out var href));
        Assert.Equal("/cp/procurement-app", href);
        Assert.Contains(EcomAeRoutes.ControlPanelProcurementApp, LegacyChromeNavCatalog.ControlPanel.Select(x => x.Href));
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
