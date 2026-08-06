using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpPhpParityTests
{
    [Theory]
    [InlineData("/CP/shop/catalogue/products", "/cp/product-catalogue-app")]
    [InlineData("/CP/shop/catalogue/products/catalogue", "/cp/product-catalogue-app")]
    [InlineData("/CP/shop/customer_mgmt/customer_mgmt", "/cp/users-app")]
    [InlineData("/CP/shop/crm/crm", "/cp/crm-board-app")]
    [InlineData("/CP/shop/parts_agent_chats", "/cp/parts-agent-chats-app")]
    [InlineData("/CP/control/portal/epc_web_tracker", "/cp/web-tracker-app")]
    [InlineData("/CP/shop/quote_requests", "/cp/quote-requests-app")]
    [InlineData("/CP/shop/pos/terminal", "/cp/pos-overview-app")]
    [InlineData("/CP/control/portal/epc_super_cp_fleet_dashboard", "/bos/fleet-health-app")]
    [InlineData("/CP/control/portal/epc_super_erp_fleet_dashboard", "/erp/fleet-app")]
    [InlineData("/CP/control/portal/epc_industry_packs", "/cp/industry-packs-app")]
    [InlineData("/CP/shop/tenant_hub/tenant_hub", "/cp/tenants-app")]
    [InlineData("/CP/control/portal/epc_tenant_control_center", "/cp/tenants-app")]
    [InlineData("/CP/shop/finance/erp/uae-tax-compliance?epc_erp_shell=1", "/cp/uae-tax-compliance-app")]
    [InlineData("/CP/control/portal/epc_erp_only_onboard_guide", "/cp/ops-guides-app")]
    [InlineData("/CP/control", "/cp/control")]
    [InlineData("/CP/control/portal/epc_tenant_config", "/cp/tenant-config-app")]
    [InlineData("/CP/control/portal/epc_design_tokens", "/cp/design-tokens-app")]
    [InlineData("/CP/control/portal/epc_config_sandbox", "/cp/config-sandbox-app")]
    [InlineData("/CP/control/portal/epc_mfa_management", "/cp/auth-mfa-app")]
    [InlineData("/CP/control/portal/epc_db_migrations", "/cp/data-migrations-app")]
    [InlineData("/CP/control/portal/epc_marketplace", "/cp/marketplace-apps-app")]
    [InlineData("/CP/control/portal/epc_ai_copilot", "/cp/ai-service-app")]
    [InlineData("/CP/control/portal/epc_boc_warehouse_control", "/cp/warehouse-wms-app")]
    [InlineData("/CP/control/portal/epc_boc_command_center", "/cp/control")]
    [InlineData("/CP/shop/procurement/procurement", "/cp/purchase-requests-app")]
    [InlineData("/CP/shop/logistics/stock", "/erp/inventory-stock-app")]
    [InlineData("/CP/shop/price-management", "/cp/price-lists-app")]
    [InlineData("/CP/shop/statistics/statistics", "/cp/statistics-app")]
    [InlineData("/CP/shop/statistics", "/cp/statistics-app")]
    [InlineData("/CP/shop/accessories", "/cp/accessories-app")]
    [InlineData("/CP/shop/manufacturers_synonyms", "/cp/synonyms-app")]
    [InlineData("/CP/shop/marketing/seo", "/cp/seo-app")]
    [InlineData("/CP/control/portal/epc_social_media_hub", "/cp/social-hub-app")]
    [InlineData("/CP/control/portal/epc_tenant_features", "/cp/tenant-features-app")]
    [InlineData("/CP/control/portal/epc_super_cp_customer_board", "/cp/customer-board-app")]
    [InlineData("/CP/shop/finance/epc_fulfillment_queue", "/cp/fulfillment-queue-app")]
    [InlineData("/CP/control/portal/epc_sso_saml", "/cp/sso-saml-app")]
    [InlineData("/CP/control/portal/epc_event_bus", "/cp/event-bus-app")]
    public void MapCpPhpPath_MapsPhpModulesToApps(string phpHref, string expected)
    {
        Assert.Equal(expected, PhpSurfaceLinkMap.MapCpPhpPath(phpHref));
        Assert.Equal(expected, PhpSurfaceLinkMap.AspNetPrimaryHref(phpHref));
    }

    [Fact]
    public void AspNetPrimaryHref_EcomaeAbsoluteCpDeepLinkKeepsApp()
    {
        var href = PhpSurfaceLinkMap.AspNetPrimaryHref(
            "https://www.ecomae.com/CP/shop/catalogue/products");
        Assert.Equal("/cp/product-catalogue-app", href);
        Assert.NotEqual("https://www.ecomae.com/cp", href);
    }

    [Fact]
    public void AspNetPrimaryHref_EcomaeBareCpStillHostShell()
    {
        Assert.Equal(
            "https://agriculture.ecomae.com/cp",
            PhpSurfaceLinkMap.AspNetPrimaryHref("https://agriculture.ecomae.com/CP/"));
    }

    [Theory]
    [InlineData("www.ecomae.com", true)]
    [InlineData("ecomae.com", true)]
    [InlineData("cp.ecomae.com", true)]
    [InlineData("epartscart.com", false)]
    [InlineData("www.epartscart.com", false)]
    public void SuperCpHostGate_MatchesPlatformHostPolicy(string host, bool allowed)
    {
        Assert.Equal(allowed, PlatformHostPolicy.IsSuperCpHost(host));
        Assert.Equal(allowed, PlatformHostPolicy.AllowSuperOnlyApp(host));
        Assert.Equal(allowed, SuperCpHostGate.IsAllowed(host));
    }

    [Fact]
    public void CpTenantsApp_HasIsSuperCpHostGate()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpTenantsApp.razor"));
        Assert.Contains("SuperCpHostGate", text, StringComparison.Ordinal);
        Assert.Contains("IsSuperCpHost", File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Services/PlatformHostPolicy.cs")), StringComparison.Ordinal);
        Assert.Contains("_allowed", text, StringComparison.Ordinal);
        Assert.Contains("Not found", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CpTenantFeaturesApp.razor")]
    [InlineData("CpCustomerBoardApp.razor")]
    [InlineData("CpSsoSamlApp.razor")]
    [InlineData("CpEventBusApp.razor")]
    public void NextWaveSuperOnlyApps_HaveSuperCpHostGate(string file)
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/" + file));
        Assert.Contains("SuperCpHostGate", text, StringComparison.Ordinal);
        Assert.Contains("_allowed", text, StringComparison.Ordinal);
        Assert.Contains("Not found", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/cp/tenant-features-app")]
    [InlineData("/cp/customer-board-app")]
    [InlineData("/cp/sso-saml-app")]
    [InlineData("/cp/event-bus-app")]
    public void NextWaveSuperOnlyApps_AreSuperOnlyChromeLinks(string href)
    {
        Assert.True(LegacyDesktopChromeCatalog.IsSuperOnlyCpLink(href));
    }


    [Fact]
    public void CpCommandCentre_HasControlPageDirectiveWithoutTrailingSlashDuplicate()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpCommandCentreApp.razor"));
        Assert.Contains("@page \"/cp/control\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@page \"/cp/control/\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TenantTopnavFilter_HidesBosAndSuperFleetOnNonSuper()
    {
        var tenantGroups = LegacyDesktopChromeCatalog.ControlPanelTopnav(includeSuperOnly: false);
        Assert.DoesNotContain(tenantGroups, g => g.Label.Equals("Platform", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(tenantGroups, g => g.Label.Equals("Operator", StringComparison.OrdinalIgnoreCase));

        var allHrefs = tenantGroups.SelectMany(g => g.Links).Select(l => l.Href).ToList();
        Assert.DoesNotContain(allHrefs, h => h.StartsWith("/bos", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(allHrefs, h => h.Contains("epc_super_cp_", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(allHrefs, h => h.Contains("fleet-health", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(allHrefs, h => h.Contains("epc_tenant_features", StringComparison.OrdinalIgnoreCase));

        Assert.True(LegacyDesktopChromeCatalog.IsSuperOnlyCpLink("/bos/fleet-health-app"));
        Assert.True(LegacyDesktopChromeCatalog.IsSuperOnlyCpLink("/CP/control/portal/epc_super_cp_fleet_dashboard"));
        Assert.False(LegacyDesktopChromeCatalog.IsSuperOnlyCpLink("/cp/orders"));
    }

    private static string Find(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
