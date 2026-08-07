using EcomAE.Platform.Middleware;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class AdminSurfaceAuthGateMiddlewareTests
{
    [Theory]
    [InlineData("/cp", true)]
    [InlineData("/cp/", true)]
    [InlineData("/cp/control", true)]
    [InlineData("/cp/app", true)]
    [InlineData("/cp/orders", true)]
    [InlineData("/cp/users-app", true)]
    [InlineData("/erp", true)]
    [InlineData("/erp/sales-orders-app", true)]
    [InlineData("/bos", true)]
    [InlineData("/bos/app", true)]
    [InlineData("/ip", true)]
    [InlineData("/ip/app", true)]
    [InlineData("/IP/login", false)]
    [InlineData("/CP/shop/orders/orders", true)]
    [InlineData("/cp/login", false)]
    [InlineData("/cp/login/", false)]
    [InlineData("/cp/logout", false)]
    [InlineData("/erp/login", false)]
    [InlineData("/bos/login", false)]
    [InlineData("/ip/login", false)]
    [InlineData("/auth/login/admin", false)]
    [InlineData("/auth/logout", false)]
    [InlineData("/", false)]
    [InlineData("/lifeos", false)]
    [InlineData("/lifeos/app", false)]
    [InlineData("/lifeos/clients-board", false)]
    [InlineData("/cp/lifeos-clients-app", false)]
    [InlineData("/storefront/app", false)]
    [InlineData("/php-reference/cp", false)]
    [InlineData("/migration/php-reference-mode", false)]
    [InlineData("/health", false)]
    public void RequiresAdminMatchesPhpControlWall(string path, bool required)
    {
        Assert.Equal(required, AdminSurfaceAuthGateMiddleware.RequiresAdmin(path));
    }
}
