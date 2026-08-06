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
