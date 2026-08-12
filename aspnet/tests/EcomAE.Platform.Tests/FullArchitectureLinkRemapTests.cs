using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Cross-surface PHP→ASP.NET remap locks (Super CP, tenant CP, ERP hosts, BOS, storefront).
/// </summary>
[Collection(PreferAspNetAppsCollection.Name)]
public sealed class FullArchitectureLinkRemapTests
{
    public FullArchitectureLinkRemapTests()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = false;
    }

    // --- Super / tenant CP steals ---
    [Theory]
    [InlineData("/CP/users/usergroups", "/cp/groups-app")]
    [InlineData("/CP/shop/statistics/web_tracker", "/cp/web-tracker-app")]
    [InlineData("/CP/control/shop/catalogue/stock", "/erp/inventory-stock-app")]
    [InlineData("/CP/shop/orders/oms-guide", "/cp/orders")]
    [InlineData("/CP/shop/orders/whatsapp-guide", "/cp/orders")]
    [InlineData("/CP/control/portal/epc_webhooks", "/cp/integrations-app")]
    [InlineData("/CP/control/portal/epc_rest_api_v2", "/cp/api-clients-app")]
    [InlineData("/CP/control/portal/epc_dealer_portal", "/cp/tenants-app")]
    [InlineData("/CP/control/portal/epc_industry_license_trends", "/cp/industry-packs-app")]
    [InlineData("/CP/control/portal/epc_cp_role_home", "/cp/groups-app")]
    [InlineData("/CP/control/portal/epc_tenant_data_policy", "/cp/platform-governance-app")]
    [InlineData("/CP/control/portal/epc_boc_product_brochure", "/brochure/cp")]
    [InlineData("/CP/general_pages/epc_isolation_anomaly", "/cp/isolation-audit-app")]
    [InlineData("/content/usefull/ip.php", "/cp/server-ip-app")]
    public void CpRemaps_NoContainsStealsOrBareCollapse(string php, string expected)
    {
        Assert.Equal(expected, PhpSurfaceLinkMap.AspNetPrimaryHref(php));
    }

    // --- Industry *.ecomae.com ERP deep links (must not collapse to host+/erp) ---
    [Theory]
    [InlineData("https://agriculture.ecomae.com/ERP/?epc_erp_shell=1&area=sales&tab=sales_orders", "/erp/sales-orders-app")]
    [InlineData("https://www.ecomae.com/ERP/?epc_erp_shell=1&area=tax&tab=einvoice", "/cp/einvoice-documents-app")]
    [InlineData("https://www.ecomae.com/ERP/?epc_erp_shell=1&area=gl", "/erp/gl-journals-app")]
    [InlineData("https://www.ecomae.com/ERP/?epc_erp_shell=1&area=payroll", "/cp/hr-overview-app")]
    public void EcomaeAbsoluteErp_KeepsDeepTabOrArea(string php, string expected)
    {
        Assert.Equal(expected, PhpSurfaceLinkMap.AspNetPrimaryHref(php));
    }

    // --- ERP tab / area hubs ---
    [Theory]
    [InlineData("/ERP/?epc_erp_shell=1&area=sales&tab=sales_orders", "/erp/sales-orders-app")]
    [InlineData("/ERP/?epc_erp_shell=1&area=gl", "/erp/gl-journals-app")]
    [InlineData("/ERP/?epc_erp_shell=1&area=payroll", "/cp/hr-overview-app")]
    [InlineData("/ERP/?epc_erp_shell=1&tab=agenda", "/erp/agenda-app")]
    [InlineData("/ERP/?epc_erp_shell=1&tab=contacts", "/erp/contacts-app")]
    [InlineData("/ERP/?epc_erp_shell=1&tab=documents", "/erp/documents-app")]
    [InlineData("/ERP/?epc_erp_shell=1&tab=year_end", "/erp/period-close-app")]
    [InlineData("/ERP/?epc_erp_shell=1&tab=audit", "/cp/audit-trail-app")]
    [InlineData("/ERP/?epc_erp_shell=1&tab=unknown_future_tab_xyz", "/erp/module-app?tab=unknown_future_tab_xyz")]
    public void ErpRemaps_TabsAndAreas(string php, string expected)
    {
        Assert.Equal(expected, PhpSurfaceLinkMap.AspNetPrimaryHref(php));
    }

    // --- BOS exact m= (no substring steal) ---
    [Theory]
    [InlineData("/BOS/?m=isolation_audit", "/cp/isolation-audit-app")]
    [InlineData("/BOS/?m=tenant_email", "/cp/tenant-email-app")]
    [InlineData("/BOS/?m=command_center", "/bos/app")]
    [InlineData("/BOS/?m=fleet_cp", "/bos/tenants-app")]
    [InlineData("/BOS/?m=audit_log", "/bos/audit-log-app")]
    [InlineData("/BOS/?section=tenants", "/bos/tenants-app")]
    public void BosRemaps_ExactModuleBeforeContains(string php, string expected)
    {
        Assert.Equal(expected, PhpSurfaceLinkMap.AspNetPrimaryHref(php));
    }

    // --- Storefront ---
    [Theory]
    [InlineData("/users/profile", "/en/users/profile")]
    [InlineData("/users/", "/en/shop/balans")]
    [InlineData("/garage/login", "/en/garage/login")]
    [InlineData("/en/katalog-laximo", "/en/katalog-laximo")]
    [InlineData("https://epartscart.com/users/profile", "/en/users/profile")]
    [InlineData("https://epartscart.com/garage/login", "/en/garage/login")]
    public void StorefrontRemaps_AccountGarageVin(string php, string expected)
    {
        Assert.Equal(expected, PhpSurfaceLinkMap.AspNetPrimaryHref(php));
    }

    [Fact]
    public void StorefrontRemaps_PreferAspNetApps_ProfileAndAccount()
    {
        StorefrontSurfaceLinks.PreferAspNetApps = true;
        try
        {
            Assert.Equal("/storefront/profile-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/users/profile"));
            Assert.Equal("/storefront/account-summary-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/users/"));
            Assert.Equal("/storefront/garage-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/garage/login"));
        }
        finally
        {
            StorefrontSurfaceLinks.PreferAspNetApps = false;
        }
    }

    // --- Super-only chrome filter ---
    [Theory]
    [InlineData("/cp/tax-toolkits-app")]
    [InlineData("/CP/control/portal/epc_tax_toolkit_manage")]
    [InlineData("/cp/tenants-app")]
    [InlineData("/CP/shop/tenant_hub/tenant_hub")]
    [InlineData("/bos/audit-log-app")]
    [InlineData("/CP/control/portal/epc_boc_audit_log")]
    [InlineData("/cp/free-tools-app")]
    [InlineData("/cp/failover-status-app")]
    [InlineData("/cp/portal-settings-app")]
    [InlineData("/CP/control/portal/portal")]
    [InlineData("/cp/platform-communication-app")]
    [InlineData("/cp/info-blocks-app")]
    public void SuperOnlyFilter_HidesTenantChromeLeaks(string href)
    {
        Assert.True(LegacyDesktopChromeCatalog.IsSuperOnlyCpLink(href));
    }

    [Fact]
    public void TenantEmail_IsNotSuperOnlyFleetPortal()
    {
        Assert.False(LegacyDesktopChromeCatalog.IsSuperOnlyCpLink("/cp/tenant-email-app"));
        Assert.Equal("/cp/tenant-email-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/control/portal/epc_tenant_email_settings"));
    }

    [Fact]
    public void FreeTools_MapsToCpApp()
    {
        Assert.Equal("/cp/free-tools-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/control/portal/epc_free_tools"));
        Assert.Equal("/cp/free-tools-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/control/portal/epc_free_tools_admin"));
    }
}
