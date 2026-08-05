using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PhpSurfaceLinkMapTests
{
    [Theory]
    [InlineData("/CP/shop/orders/orders", "/cp/orders")]
    [InlineData("/CP/shop/orders/carts", "/cp/abandoned-carts-app")]
    [InlineData("/CP/modules/modules_manager", "/cp/modules-app")]
    [InlineData("/CP/control/users", "/cp/users-app")]
    [InlineData("/CP/shop/crm/crm_main", "/cp/crm-board-app")]
    [InlineData("/CP/shop/prices", "/cp/price-lists-app")]
    [InlineData("/CP/shop/payments/payments", "/cp/payment-gateways-app")]
    [InlineData("/ERP/?epc_erp_shell=1&area=sales&tab=sales_orders", "/erp/sales-orders-app")]
    [InlineData("/ERP/?epc_erp_shell=1&area=finance", "/erp")]
    [InlineData("/ERP/?epc_erp_shell=1&area=tax&tab=einvoice", "/cp/einvoice-documents-app")]
    [InlineData("/BOS/?m=command_center", "/bos/app")]
    [InlineData("/BOS/?m=fleet_cp", "/bos/tenants-app")]
    [InlineData("/shop/part_search", "/storefront/search-app")]
    [InlineData("/shop/cart", "/storefront/cart-app")]
    [InlineData("https://epartscart.com/shop/part_search", "/storefront/search-app")]
    [InlineData("/en/shop/warehouse-search", "/storefront/search-app?mode=attr")]
    [InlineData("/shop/warehouse-search?q=oil&field=all", "/storefront/search-app?mode=attr&q=oil&field=all")]
    [InlineData("/en/katalog-laximo", "/storefront/search-app?mode=vin")]
    [InlineData("/en/vehicle-catalog", "/storefront/search-app?mode=car")]
    [InlineData("/en/users/login", "/storefront/login")]
    [InlineData("https://agriculture.ecomae.com/CP/", "https://agriculture.ecomae.com/cp")]
    [InlineData("/epc-blockchain-verify.php", "/blockchain")]
    [InlineData("/platform", "/platform")]
    [InlineData("/platform/pricing", "/platform/pricing")]
    [InlineData("/documentation", "/documentation")]
    [InlineData("/privacy", "/privacy")]
    [InlineData("/bos", "/bos")] // product Super-CP shell; knowledge article is /bos/what-is-…
    [InlineData("/", "/")] // classic-entry home URL stays / (proxied to /marketing/app)
    [InlineData("https://www.ecomae.com/", "/marketing/app")]
    [InlineData("https://www.ecomae.com/platform/pricing", "/platform/pricing")]
    [InlineData("https://www.ecomae.com/bos", "/bos/what-is-a-business-operating-system")]
    public void AspNetPrimaryHref_MapsPhpProductToAspNet(string phpHref, string expected)
    {
        Assert.Equal(expected, PhpSurfaceLinkMap.AspNetPrimaryHref(phpHref));
    }

    [Theory]
    [InlineData("/CP/shop/orders/orders", "/cp/orders")]
    [InlineData("/ERP/?epc_erp_shell=1&area=sales&tab=sales_orders", "/erp/sales-orders-app")]
    [InlineData("/shop/part_search?q=abc", "/storefront/search-app?q=abc")]
    public void TryMapIncomingPhpProductPath_DeepShells(string incoming, string expected)
    {
        Assert.True(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(incoming, out var mapped));
        Assert.Equal(expected, mapped);
    }

    [Theory]
    [InlineData("/CP")]
    [InlineData("/CP/")]
    [InlineData("/ERP/")]
    [InlineData("/BOS")]
    [InlineData("/cp/orders")]
    [InlineData("/php-reference/cp")]
    public void TryMapIncomingPhpProductPath_SkipsExactShellsAndAspNet(string incoming)
    {
        Assert.False(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(incoming, out _));
    }

    [Fact]
    public void PhpReferenceOnlyHref_StaysUnderPhpReference()
    {
        Assert.Equal("/php-reference/cp", PhpSurfaceLinkMap.PhpReferenceOnlyHref("/CP/shop/orders/orders"));
        Assert.Equal("/php-reference/erp", PhpSurfaceLinkMap.PhpReferenceOnlyHref("/ERP/?epc_erp_shell=1"));
        Assert.Equal("/php-reference/home", PhpSurfaceLinkMap.PhpReferenceOnlyHref("/shop/part_search"));
    }
}
