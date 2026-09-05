using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

[Collection(PreferAspNetAppsCollection.Name)]
public sealed class PhpSurfaceLinkMapTests
{
    public PhpSurfaceLinkMapTests()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = false;
    }

    [Theory]
    [InlineData("/CP/shop/orders/orders", "/cp/orders")]
    [InlineData("/CP/shop/orders/orders?order_id=42", "/cp/orders?order_id=42")]
    [InlineData("/CP/shop/orders/order?order_id=7", "/cp/orders?order_id=7")]
    [InlineData("/CP/shop/orders/carts", "/cp/abandoned-carts-app")]
    [InlineData("/CP/modules/modules_manager", "/cp/modules-app")]
    [InlineData("/CP/control/users", "/cp/users-app")]
    [InlineData("/CP/control", "/cp/control")]
    [InlineData("/cp/control", "/cp/control")]
    [InlineData("/CP/shop/crm/crm_main", "/cp/crm-board-app")]
    [InlineData("/CP/shop/prices", "/cp/prices-upload-app")]
    [InlineData("/CP/shop/prices/price?price_id=4", "/cp/shop/prices/price?price_id=4")]
    [InlineData("/CP/shop/payments/payments", "/cp/payment-gateways-app")]
    [InlineData("/ERP/?epc_erp_shell=1&area=sales&tab=sales_orders", "/erp/sales-orders-app")]
    [InlineData("/ERP/?epc_erp_shell=1&area=finance", "/erp/gl-journals-app")]
    [InlineData("/ERP/?epc_erp_shell=1&area=tax&tab=einvoice", "/cp/einvoice-documents-app")]
    [InlineData("/BOS/?m=command_center", "/bos/app")]
    [InlineData("/BOS/?m=fleet_cp", "/bos/tenants-app")]
    [InlineData("/shop/part_search", "/en/shop/part_search")]
    [InlineData("/shop/cart", "/en/shop/cart")]
    [InlineData("https://epartscart.com/shop/part_search", "/en/shop/part_search")]
    [InlineData("/en/shop/warehouse-search", "/en/shop/warehouse-search")]
    [InlineData("/shop/warehouse-search?q=oil&field=all", "/en/shop/warehouse-search?q=oil&field=all")]
    [InlineData("/en/katalog-laximo", "/en/katalog-laximo")]
    [InlineData("/en/vehicle-catalog", "/en/vehicle-catalog")]
    [InlineData("/en/users/login", "/en/users/login")]
    [InlineData("/product-family", "/en/product-family")]
    [InlineData("/umapi_catalog", "/en/umapi_catalog")]
    [InlineData("https://agriculture.ecomae.com/CP/", "https://agriculture.ecomae.com/cp")]
    [InlineData("/epc-blockchain-verify.php", "/blockchain/verify")]
    [InlineData("/epc-blockchain-verify.php?proof=prf_x", "/blockchain/verify?proof=prf_x")]
    [InlineData("/platform", "/platform")]
    [InlineData("/platform/pricing", "/platform/pricing")]
    [InlineData("/documentation", "/documentation")]
    [InlineData("/privacy", "/privacy")]
    [InlineData("/bos", "/bos")] // product Super-CP shell; knowledge article is /bos/what-is-…
    [InlineData("/", "/")] // classic-entry home URL stays / (proxied to /marketing/app)
    [InlineData("https://www.ecomae.com/", "/marketing/app")]
    [InlineData("https://www.ecomae.com/platform/pricing", "/platform/pricing")]
    [InlineData("https://www.ecomae.com/bos", "/bos")] // product Super BOS shell (not marketing article)
    [InlineData("https://www.ecomae.com/BOS/", "/bos")]
    [InlineData("https://www.ecomae.com/bos/tenants-app", "/bos/tenants-app")]
    [InlineData("https://www.ecomae.com/bos/what-is-a-business-operating-system", "/bos/what-is-a-business-operating-system")]
    [InlineData("/CP/shop/tenant_hub/tenant_hub", "/cp/tenants-app")]
    [InlineData("/CP/control/portal/epc_platform_health_checkup", "/cp/failover-status-app")]
    [InlineData("/CP/shop/finance/epc_fulfillment_queue", "/cp/fulfillment-queue-app")]
    [InlineData("/CP/shop/finance/epc_fulfillment_queue?fulfillment_id=12", "/cp/fulfillment-queue-app?fulfillment_id=12")]
    [InlineData("/CP/shop/finance/epc_credit_limit", "/cp/credit-limits-app")]
    [InlineData("/CP/shop/finance/epc_order_erp_pipeline", "/erp/order-pipeline-app")]
    [InlineData("/CP/shop/finance/epc_inventory_forecast", "/erp/inventory-forecast-app")]
    [InlineData("/CP/shop/finance/epc_multi_entity", "/erp/multi-entity-app")]
    [InlineData("/CP/shop/finance/epc_multi_currency_gl", "/erp/multi-currency-gl-app")]
    public void AspNetPrimaryHref_MapsPhpProductToAspNet(string phpHref, string expected)
    {
        Assert.Equal(expected, PhpSurfaceLinkMap.AspNetPrimaryHref(phpHref));
    }

    [Theory]
    [InlineData("/CP/shop/orders/orders", "/cp/orders")]
    [InlineData("/CP/shop/orders/order?order_id=9", "/cp/orders?order_id=9")]
    [InlineData("/ERP/?epc_erp_shell=1&area=sales&tab=sales_orders", "/erp/sales-orders-app")]
    [InlineData("/shop/part_search?q=abc", "/en/shop/part_search?q=abc")]
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
        Assert.Equal("/php-reference/cp", PhpSurfaceLinkMap.PhpReferenceOnlyHref("/CP/"));
        Assert.Equal("/php-reference/cp", PhpSurfaceLinkMap.PhpReferenceOnlyHref("/cp"));
        Assert.Equal("/php-reference/CP/shop/orders/orders", PhpSurfaceLinkMap.PhpReferenceOnlyHref("/CP/shop/orders/orders"));
        Assert.Equal("/php-reference/erp", PhpSurfaceLinkMap.PhpReferenceOnlyHref("/erp"));
        Assert.Equal("/php-reference/ERP/?epc_erp_shell=1", PhpSurfaceLinkMap.PhpReferenceOnlyHref("/ERP/?epc_erp_shell=1"));
        Assert.Equal(
            "/php-reference/ERP/?epc_erp_shell=1&area=finance&tab=ledger",
            PhpSurfaceLinkMap.PhpReferenceOnlyHref("/ERP/?epc_erp_shell=1&area=finance&tab=ledger"));
        Assert.Equal("/php-reference/home", PhpSurfaceLinkMap.PhpReferenceOnlyHref("/shop/part_search"));
        Assert.Equal("/php-reference/en/shop/pay", PhpSurfaceLinkMap.PhpReferenceOnlyHref("/en/shop/pay"));
        Assert.Equal("/php-reference/en/katalog-laximo", PhpSurfaceLinkMap.PhpReferenceOnlyHref("/en/katalog-laximo"));
        Assert.Equal("/php-reference/en/katalog-laximo?identString=ABC", PhpSurfaceLinkMap.PhpReferenceOnlyHref("/katalog-laximo?identString=ABC"));
        Assert.Equal("/php-reference/cp", PhpSurfaceLinkMap.PhpReferenceOnlyHref("/php-reference/cp"));
    }
}
