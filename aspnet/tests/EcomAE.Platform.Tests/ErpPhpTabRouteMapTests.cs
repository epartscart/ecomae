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
