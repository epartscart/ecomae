using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacyChromeNavCatalogTests
{
    [Fact]
    public void ControlPanelNavLinksPhpModules()
    {
        Assert.Contains(LegacyChromeNavCatalog.ControlPanel, item => item.Label == "Commerce" && item.Href.StartsWith("/CP/", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.ControlPanel, item => item.Label == "ERP" && item.Href.Contains("/ERP", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Orders (OMS)" && item.Href == "/cp/orders");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Users list" && item.Href == "/cp/users-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Groups list" && item.Href == "/cp/groups-app");
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Label == "Groups PHP" && item.Href == "/CP/users/usergroups");
        Assert.All(LegacyChromeNavCatalog.ControlPanelQuickActions, item => Assert.False(string.IsNullOrWhiteSpace(item.Href)));
    }

    [Fact]
    public void ErpNavLinksPhpShellAreas()
    {
        Assert.Contains(LegacyChromeNavCatalog.Erp, item => item.Label == "Record to Report" && item.Href.Contains("area=finance", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.Erp, item => item.Label == "Order to Cash" && item.Href.Contains("area=sales", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Sales orders list" && item.Href == "/erp/sales-orders-app");
        Assert.Contains(LegacyChromeNavCatalog.ErpQuickActions, item => item.Label == "Sales orders PHP" && item.Href.Contains("tab=sales_orders", StringComparison.Ordinal));
    }

    [Fact]
    public void BosNavLinksPhpBosEntry()
    {
        Assert.Contains(LegacyChromeNavCatalog.Bos, item => item.Href.StartsWith("/BOS", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Search parts" && item.Href == "/storefront/search-app");
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Search PHP" && item.Href.Contains("part_search", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Cart" && item.Href == "/storefront/cart-app");
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Cart PHP" && item.Href.Contains("/shop/cart", StringComparison.Ordinal));
        Assert.Contains(LegacyChromeNavCatalog.Storefront, item => item.Label == "Checkout PHP" && item.Href.Contains("checkout", StringComparison.Ordinal));
    }
}
