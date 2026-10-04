using EcomAE.Platform.Middleware;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class AdminCrossSiteWriteGuardMiddlewareTests
{
    [Theory]
    [InlineData("POST", "/erp/cash-entries/receipt-voucher", "www.epartscart.com", "https://www.epartscart.com", "", "same-origin", false)]
    [InlineData("POST", "/erp/cash-entries/receipt-voucher", "www.epartscart.com", "", "https://www.epartscart.com/erp/app", "", false)]
    [InlineData("POST", "/erp/cash-entries/receipt-voucher", "www.epartscart.com", "", "", "", false)]
    [InlineData("POST", "/erp/cash-entries/receipt-voucher", "www.epartscart.com", "https://evil.example", "", "cross-site", true)]
    [InlineData("POST", "/erp/cash-entries/receipt-voucher", "www.epartscart.com", "https://evil.example", "", "", true)]
    [InlineData("POST", "/erp/cash-entries/receipt-voucher", "www.epartscart.com", "", "https://evil.example/x", "", true)]
    [InlineData("POST", "/erp/cash-entries/receipt-voucher", "www.epartscart.com", "null", "", "", false)]
    [InlineData("POST", "/erp/cash-entries/receipt-voucher", "www.epartscart.com", "not a url", "", "", true)]
    [InlineData("POST", "/erp/cash-entries/receipt-voucher", "www.epartscart.com", "", "", "cross-site", true)]
    [InlineData("POST", "/cp/orders/delete", "www.epartscart.com", "https://evil.example", "", "", true)]
    [InlineData("POST", "/bos/credit/hold", "www.ecomae.com", "https://evil.example", "", "", true)]
    [InlineData("GET", "/erp/app", "www.epartscart.com", "https://evil.example", "", "cross-site", false)]
    [InlineData("POST", "/storefront/cart/add", "www.epartscart.com", "https://evil.example", "", "cross-site", false)]
    [InlineData("POST", "/php-reference/erp/x", "www.epartscart.com", "https://evil.example", "", "cross-site", false)]
    [InlineData("POST", "/erp/login", "www.epartscart.com", "https://evil.example", "", "cross-site", true)]
    public void RejectsForeignOriginWritesOnAdminSurfaces(string method, string path, string host, string origin, string referer, string secFetchSite, bool rejected)
        => Assert.Equal(rejected, AdminCrossSiteWriteGuardMiddleware.ShouldReject(method, path, host, origin, referer, secFetchSite));
}
