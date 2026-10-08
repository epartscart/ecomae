using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// The PHP CP pages that only redirect (hub folders, legacy routes) must land where their PHP target lands.
/// </summary>
public sealed class CpPhpRedirectPageMapTests
{
    [Theory]
    [InlineData("/cp/shop/customer_mgmt", "/cp/shop/customer_mgmt/customer_mgmt")]
    [InlineData("/cp/shop/document_control", "/cp/shop/document_control/document_control")]
    [InlineData("/cp/shop/pos", "/cp/shop/pos/terminal")]
    [InlineData("/cp/shop/tenant_hub", "/cp/shop/tenant_hub/tenant_hub")]
    [InlineData("/cp/users/customer_mgmt", "/cp/shop/customer_mgmt/customer_mgmt")]
    [InlineData("/cp/shop/modul-pechati-dokumentov", "/cp/shop/document_control/document_control")]
    [InlineData("/cp/shop/finance/payment_systems", "/cp/shop/payments/payments?tab=configure")]
    public void Redirect_pages_land_where_their_php_target_lands(string phpPage, string phpTarget)
    {
        Assert.True(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(phpPage, out var page));
        Assert.True(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(phpTarget, out var target));
        Assert.Equal(target, page);
    }

    [Theory]
    [InlineData("/cp/shop/crm/crm", "/cp/crm-board-app")]
    [InlineData("/cp/shop/crm/crm?tab=pipeline", "/cp/crm-board-app?tab=pipeline")]
    [InlineData("/cp/shop/crm/crm?tab=Lead-s_1", "/cp/crm-board-app?tab=eads_")]
    [InlineData("/cp/shop/crm/crm?tab=leads&tab=tickets", "/cp/crm-board-app?tab=tickets")]
    [InlineData("/cp/shop/crm/crm?from=2026-01-01&to=0", "/cp/crm-board-app?from=2026-01-01")]
    [InlineData("/cp/shop/crm/crm?tab=quotes&from=a b&to=2026-02-01", "/cp/crm-board-app?tab=quotes&from=a%20b&to=2026-02-01")]
    [InlineData("/CP/shop/crm/crm?tab=deals", "/cp/crm-board-app?tab=deals")]
    public void Legacy_crm_route_keeps_phps_tab_and_dates(string phpPage, string expected)
    {
        Assert.True(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath(phpPage, out var href));
        Assert.Equal(expected, href);
    }

    [Fact]
    public void Crm_main_still_opens_the_board()
    {
        Assert.True(PhpSurfaceLinkMap.TryMapIncomingPhpProductPath("/cp/shop/crm/crm_main", out var href));
        Assert.StartsWith("/cp/crm-board-app", href, StringComparison.Ordinal);
    }
}
