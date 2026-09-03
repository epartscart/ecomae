using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpPhpTabRouteMapTests
{
    [Fact]
    public void MapCoversAllPhpErpTabFiles()
    {
        var root = FindRepoRoot();
        var tabDir = Path.Combine(root, "cp", "content", "shop", "finance", "erp");
        Assert.True(Directory.Exists(tabDir), "PHP ERP tab directory missing: " + tabDir);

        var tabs = Directory.GetFiles(tabDir, "erp_tabs_*.php")
            .Select(f => Path.GetFileNameWithoutExtension(f)!.Replace("erp_tabs_", "", StringComparison.Ordinal))
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();

        Assert.True(tabs.Count >= 150, "Expected ~160 PHP ERP tab files, got " + tabs.Count);

        var missing = tabs
            .Where(t => !ErpPhpTabRouteMap.TryMapTab(t, out _))
            .ToList();

        Assert.True(missing.Count == 0, "Unmapped PHP ERP tabs: " + string.Join(", ", missing));
    }

    [Theory]
    [InlineData("sales_orders", "/erp/sales-orders-app")]
    [InlineData("delivery_notes", "/erp/delivery-notes-app")]
    [InlineData("rfq", "/erp/rfq-app")]
    [InlineData("three_way_match", "/erp/three-way-match-app")]
    [InlineData("contacts", "/erp/contacts-app")]
    [InlineData("payment_batches", "/erp/payment-batches-app")]
    [InlineData("year_end", "/erp/period-close-app")]
    [InlineData("collections", "/cp/collections-dunning-app")]
    [InlineData("wms", "/cp/warehouse-wms-app")]
    [InlineData("tenant_config", "/cp/tenant-config-app")]
    [InlineData("agenda", "/erp/agenda-app")]
    [InlineData("documents", "/erp/documents-app")]
    [InlineData("expense_reports", "/erp/expense-reports-app")]
    [InlineData("vat", "/erp/vat-app")]
    [InlineData("withholding", "/erp/withholding-app")]
    [InlineData("workflow", "/erp/workflow-app")]
    [InlineData("vat_return", "/erp/vat-app?tab=vat_return")]
    [InlineData("petty_cash", "/erp/cash-accounts-app?tab=petty_cash")]
    [InlineData("cash_forecast", "/erp/cash-accounts-app?tab=cash_forecast")]
    [InlineData("bank_instruments", "/erp/bank-reconciliation-app?tab=bank_instruments")]
    [InlineData("subscriptions", "/erp/sales-orders-app?tab=subscriptions")]
    [InlineData("supplier_portal", "/erp/suppliers-app?tab=supplier_portal")]
    [InlineData("virtual_warehouse", "/erp/warehouses-app?tab=virtual_warehouse")]
    [InlineData("payables", "/erp/payables-app")]
    [InlineData("receivables", "/erp/receivables-app")]
    [InlineData("ap_setup", "/erp/suppliers-app?tab=ap_setup")]
    [InlineData("ar_setup", "/erp/contacts-app?tab=ar_setup")]
    public void KnownTabsMapToDedicatedApps(string tab, string expected)
    {
        Assert.True(ErpPhpTabRouteMap.TryMapTab(tab, out var href));
        Assert.Equal(expected, href);
    }

    [Fact]
    public void PhpSurfaceLinkMapUsesTabRouteMap()
    {
        var href = PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&area=purchasing&tab=rfq");
        Assert.Equal("/erp/rfq-app", href);

        var dn = PhpSurfaceLinkMap.AspNetPrimaryHref("/ERP/?epc_erp_shell=1&tab=delivery_notes");
        Assert.Equal("/erp/delivery-notes-app", dn);
    }

    [Theory]
    [InlineData("/ERP/?epc_erp_shell=1&area=ap&tab=payables", "/erp/payables-app")]
    [InlineData("/ERP/?epc_erp_shell=1&area=ar&tab=receivables", "/erp/receivables-app")]
    [InlineData("/ERP/?epc_erp_shell=1&area=ap", "/erp/payables-app")]
    [InlineData("/ERP/?epc_erp_shell=1&area=ar", "/erp/receivables-app")]
    [InlineData("/ERP/?epc_erp_shell=1&area=purchasing", "/erp/purchase-orders-app")]
    [InlineData("/ERP/?epc_erp_shell=1&area=sales", "/erp/sales-orders-app")]
    [InlineData("/ERP/?epc_erp_shell=1&area=credit_coll", "/cp/collections-dunning-app")]
    [InlineData("/ERP/?epc_erp_shell=1&area=banking", "/erp/cash-accounts-app")]
    [InlineData("/ERP/?epc_erp_shell=1&area=finance", "/erp/gl-journals-app")]
    public void AreaAndTabHubsMatchModuleNames(string php, string expected)
    {
        Assert.Equal(expected, PhpSurfaceLinkMap.AspNetPrimaryHref(php));
    }

    [Fact]
    public void SuperBocErpLinksMatchMenuLabels()
    {
        var ap = Assert.Single(PhpSuperCpBocNav.Areas, x => x.Key == "erp_ap");
        Assert.Equal("Accounts payable", ap.Label);
        Assert.Equal("/erp/payables-app", ap.Href);

        var ar = Assert.Single(PhpSuperCpBocNav.Areas, x => x.Key == "erp_ar");
        Assert.Equal("Accounts receivable", ar.Label);
        Assert.Equal("/erp/receivables-app", ar.Href);

        var sales = Assert.Single(PhpSuperCpBocNav.Areas, x => x.Key == "erp_sales");
        Assert.Equal("/erp/sales-orders-app", sales.Href);

        var purchasing = Assert.Single(PhpSuperCpBocNav.Areas, x => x.Key == "erp_purchasing");
        Assert.Equal("/erp/purchase-orders-app", purchasing.Href);
    }

    [Fact]
    public void ErpTopnavCategoryLabelsPreferFullNames()
    {
        var groups = LegacyDesktopChromeCatalog.ErpTopnav();
        var r2r = Assert.Single(groups, g => g.Id == "record_to_report");
        Assert.Equal("Record to Report", r2r.Label);
        Assert.Equal("R2R", r2r.ShortLabel);

        var p2p = Assert.Single(groups, g => g.Id == "procure_to_pay");
        Assert.Equal("Procure to Pay", p2p.Label);
        Assert.Equal("P2P", p2p.ShortLabel);

        // Payables / Receivables menu rows must not remap to Purchases / Invoices.
        var payables = groups.SelectMany(g => g.Links)
            .First(l => l.Href.Contains("tab=payables", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("/erp/payables-app", PhpSurfaceLinkMap.AspNetPrimaryHref(payables.Href));
        Assert.Contains("Payables", payables.Label, StringComparison.OrdinalIgnoreCase);

        var receivables = groups.SelectMany(g => g.Links)
            .First(l => l.Href.Contains("tab=receivables", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("/erp/receivables-app", PhpSurfaceLinkMap.AspNetPrimaryHref(receivables.Href));
        Assert.Contains("Receivables", receivables.Label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CatalogErpTabs_AllMapped_154Of154()
    {
        var root = FindRepoRoot();
        var catalogPath = Path.Combine(root, "aspnet/src/EcomAE.Platform/Presentation/Generated/php_module_catalog.json");
        Assert.True(File.Exists(catalogPath), catalogPath);
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(catalogPath));
        var missing = new List<string>();
        var total = 0;
        foreach (var area in doc.RootElement.GetProperty("erpAreas").EnumerateArray())
        {
            foreach (var tab in area.GetProperty("tabs").EnumerateArray())
            {
                total++;
                var id = tab.GetProperty("id").GetString() ?? "";
                if (!ErpPhpTabRouteMap.TryMapTab(id, out _))
                {
                    missing.Add(id);
                }
            }
        }

        Assert.Equal(154, total);
        Assert.True(missing.Count == 0, "Unmapped catalog ERP tabs: " + string.Join(", ", missing));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cp", "content", "shop", "finance", "erp", "ajax_erp.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        // Cloud / test host often runs from aspnet/tests/... — walk up from cwd too.
        dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "cp", "content", "shop", "finance", "erp", "ajax_erp.php")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repo root with PHP ERP tabs.");
    }
}
