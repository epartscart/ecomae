using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacyChromeNavCatalogTests
{
    [Fact]
    public void ControlPanelNavIsAspNetOnly()
    {
        Assert.Contains(LegacyChromeNavCatalog.ControlPanel, item => item.Label == "Commerce" && item.Href.StartsWith("/cp", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.ControlPanel, item => item.Label == "ERP" && item.Href.Equals("/erp", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Dashboard summary KPIs" && item.Href == "/cp/dashboard-summary-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Orders (OMS)" && item.Href == "/cp/orders");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Abandoned carts" && item.Href == "/cp/abandoned-carts-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Users list" && item.Href == "/cp/users-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Modules list" && item.Href == "/cp/modules-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Tenants list" && item.Href == "/cp/tenants-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "CRM board" && item.Href == "/cp/crm-board-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Price lists" && item.Href == "/cp/price-lists-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Config items list" && item.Href == "/cp/config-items-app");
        Assert.All(LegacyChromeNavCatalog.ControlPanelQuickActions, item => Assert.False(string.IsNullOrWhiteSpace(item.Href)));
        Assert.DoesNotContain(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label.EndsWith(" PHP", StringComparison.Ordinal));
        AssertProductHrefIsAspNetOnly(LegacyChromeNavCatalog.ControlPanel);
        AssertProductHrefIsAspNetOnly(LegacyChromeNavCatalog.ControlPanelQuickActions);
    }

    [Fact]
    public void ErpNavLinksAspNetShellAreas()
    {
        Assert.Contains(LegacyChromeNavCatalog.Erp, item => item.Label == "Record to Report" && item.Href.StartsWith("/erp", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(LegacyChromeNavCatalog.Erp, item => item.Label == "Order to Cash" && item.Href.StartsWith("/erp", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Sales orders list" && item.Href == "/erp/sales-orders-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Purchase orders list" && item.Href == "/erp/purchase-orders-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Invoices list" && item.Href == "/erp/invoices-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Dashboard summary KPIs" && item.Href == "/erp/dashboard-summary-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Cash & bank list" && item.Href == "/erp/cash-accounts-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Warehouses list" && item.Href == "/erp/warehouses-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Inventory stock" && item.Href == "/erp/inventory-stock-app");
        Assert.DoesNotContain(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label.EndsWith(" PHP", StringComparison.Ordinal));
        AssertProductHrefIsAspNetOnly(LegacyChromeNavCatalog.Erp);
        AssertProductHrefIsAspNetOnly(LegacyChromeNavCatalog.ErpQuickActions);
    }

    [Fact]
    public void BosAndStorefrontNavAreAspNetOnly()
    {
        Assert.Contains(LegacyChromeNavCatalog.Bos, item => item.Href.StartsWith("/bos", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(LegacyChromeNavCatalog.Bos, item => item.Href.StartsWith("/BOS", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.BosQuickActions, item => item.Label == "Audit log list" && item.Href == "/bos/audit-log-app");
        Assert.Contains(LegacyChromeNavCatalog.BosQuickActions, item => item.Label == "Fleet tenants list" && item.Href == "/bos/tenants-app");
        Assert.Contains(LegacyChromeNavCatalog.BosQuickActions, item => item.Label == "Fleet health KPIs" && item.Href == "/bos/fleet-health-app");
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Search parts" && item.Href == "/storefront/search-app");
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Cart" && item.Href == "/storefront/cart-app");
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Checkout" && item.Href == "/storefront/checkout-app");
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "My orders" && item.Href == "/storefront/orders-app");
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Garage" && item.Href == "/storefront/garage-app");
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Profile" && item.Href == "/storefront/profile-app");
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Account summary" && item.Href == "/storefront/account-summary-app");
        Assert.DoesNotContain(LegacyChromeNavCatalog.BosQuickActions, item => item.Label.EndsWith(" PHP", StringComparison.Ordinal));
        Assert.DoesNotContain(LegacyChromeNavCatalog.Storefront, item => item.Label.EndsWith(" PHP", StringComparison.Ordinal));
        AssertProductHrefIsAspNetOnly(LegacyChromeNavCatalog.Bos);
        AssertProductHrefIsAspNetOnly(LegacyChromeNavCatalog.BosQuickActions);
        AssertProductHrefIsAspNetOnly(LegacyChromeNavCatalog.Storefront);
    }

    private static void AssertProductHrefIsAspNetOnly(IEnumerable<LegacyChromeNavCatalog.NavItem> items)
    {
        foreach (var item in items)
        {
            Assert.False(PhpSurfaceLinkMap.IsPhpProductHref(item.Href), $"Product nav must not emit PHP href: {item.Label} → {item.Href}");
            Assert.False(item.Href.StartsWith("/CP", StringComparison.Ordinal), item.Href);
            Assert.False(item.Href.StartsWith("/ERP", StringComparison.Ordinal), item.Href);
            Assert.False(item.Href.StartsWith("/BOS", StringComparison.Ordinal), item.Href);
            Assert.False(item.Href.Contains("/shop/", StringComparison.OrdinalIgnoreCase)
                && !item.Href.StartsWith("/storefront/", StringComparison.OrdinalIgnoreCase), item.Href);
            Assert.False(item.Href.EndsWith(".php", StringComparison.OrdinalIgnoreCase), item.Href);
        }
    }
}
