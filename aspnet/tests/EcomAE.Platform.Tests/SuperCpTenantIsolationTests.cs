using EcomAE.Platform.Configuration;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Super CP ↔ tenant isolation: ecomae.com hosts the PLATFORM control plane
/// (BOS + Super CP over the platform DB); tenant hosts get their OWN CP/ERP.
/// Guards the leak where ecomae.com/cp|/erp showed epartscart tenant data.
/// </summary>
public sealed class SuperCpTenantIsolationTests
{
    private sealed class HijackingRegistry : ITenantRegistry
    {
        // Simulates the live epc_portal_tenants lookup that returns an erp_only
        // shared tenant row registered under the www hostname (PHP does this) —
        // it must NEVER bind the super host to a tenant database.
        public ValueTask<TenantRegistryRecord?> FindByHostAsync(string host, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<TenantRegistryRecord?>(new TenantRegistryRecord(
                host,
                TenantMode.ErpOnlyTenant,
                "epartscart",
                "docpart",
                StorefrontEnabled: false,
                ErpEnabled: true,
                ControlPanelEnabled: false,
                BosEnabled: false,
                DbUser: "epc_user",
                DbPassword: "secret",
                DedicatedDb: true));
    }

    private static RouteTenantResolver ResolverWith(ITenantRegistry registry) =>
        new(Options.Create(new EcomAeOptions
        {
            PlatformHost = "www.ecomae.com",
            SeedTenants =
            [
                new TenantSeedOptions { Host = "www.ecomae.com", SiteKey = "platform", DatabaseName = "ecomae", Mode = TenantMode.Platform, BosEnabled = true },
            ]
        }), registry);

    [Theory]
    [InlineData("www.ecomae.com", "/cp")]
    [InlineData("www.ecomae.com", "/erp")]
    [InlineData("ecomae.com", "/cp")]
    [InlineData("cp.ecomae.com", "/cp")]
    public async Task SuperHostsNeverBindToTenantDatabase(string host, string path)
    {
        var resolver = ResolverWith(new HijackingRegistry());
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString(host);
        context.Request.Path = path;

        var tenant = await resolver.ResolveAsync(context);

        Assert.Equal(TenantMode.Platform, tenant.Mode);
        Assert.Equal("platform", tenant.SiteKey);
        Assert.Equal("ecomae", tenant.DatabaseName);
        Assert.Null(tenant.DbUser);
        Assert.Null(tenant.DbPassword);
    }

    [Fact]
    public async Task TenantHostsKeepTheirOwnRegistryRecord()
    {
        var resolver = ResolverWith(new HijackingRegistry());
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("www.epartscart.com");
        context.Request.Path = "/cp";

        var tenant = await resolver.ResolveAsync(context);

        Assert.Equal(TenantMode.ErpOnlyTenant, tenant.Mode);
        Assert.Equal("epartscart", tenant.SiteKey);
        Assert.Equal("docpart", tenant.DatabaseName);
    }

    [Fact]
    public void CpHomeBranchesSuperVsTenant()
    {
        // Mirrors PHP cp/content/control/control.php: super hosts render
        // epc_super_cp_dashboard.php (BOC console), not the tenant dashboard.
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/CpCommandCentreApp.razor"));
        Assert.Contains("PlatformHostPolicy.IsSuperCpHost", text, StringComparison.Ordinal);
        Assert.Contains("<PhpSuperCpCommandCentre", text, StringComparison.Ordinal);
        // Tenant branding must be host-aware — never hardcoded eParts Cart heading.
        Assert.DoesNotContain("<h2 class=\"cp-dash-title\">eParts Cart</h2>", text, StringComparison.Ordinal);
        Assert.Contains("ErpHostContext.Resolve", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SuperCpCommandCentreIsFleetScoped()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpSuperCpCommandCentre.razor"));
        Assert.Contains("Platform Super CP", text, StringComparison.Ordinal);
        Assert.Contains("BuildBosFleetHealthAsync", text, StringComparison.Ordinal);
        Assert.Contains("epc-boc__hero", text, StringComparison.Ordinal);
        // Full BOC console chrome (top mega-menu + fleet + platform banner).
        Assert.Contains("epc-boc__topnav", text, StringComparison.Ordinal);
        Assert.Contains("epc-boc__platform-banner", text, StringComparison.Ordinal);
        Assert.Contains("epc-boc__fleet-grid", text, StringComparison.Ordinal);
        Assert.Contains("PhpSuperCpBocNav.Nav()", text, StringComparison.Ordinal);
        // Fleet console must not query tenant shop digests.
        Assert.DoesNotContain("ListCpOrdersAsync", text, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildCpProductCatalogueDigestAsync", text, StringComparison.Ordinal);
        Assert.DoesNotContain("eParts Cart", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BocNavMirrorsPhpAreaRegistry()
    {
        // PHP epc_boc_groups(): 15 domains; epc_boc_areas(): ~110 areas.
        Assert.Equal(15, EcomAE.Platform.Presentation.PhpSuperCpBocNav.Groups.Count);
        Assert.True(EcomAE.Platform.Presentation.PhpSuperCpBocNav.Areas.Count >= 100,
            $"expected ≥100 BOC areas, found {EcomAE.Platform.Presentation.PhpSuperCpBocNav.Areas.Count}");

        foreach (var key in new[] { "command_center", "tenant_hub", "platform_health", "auto_price", "cp_orders", "erp_gl", "integrations", "operator_guide" })
        {
            Assert.Contains(EcomAE.Platform.Presentation.PhpSuperCpBocNav.Areas, a => a.Key == key);
        }

        foreach (var area in EcomAE.Platform.Presentation.PhpSuperCpBocNav.Areas)
        {
            Assert.False(string.IsNullOrWhiteSpace(area.Href), $"area {area.Key} produced an empty href");
            // The link map must land on an ASP.NET surface — never raw uppercase PHP shells.
            Assert.False(area.Href.StartsWith("/CP/", StringComparison.Ordinal),
                $"area {area.Key} href {area.Href} left the raw PHP shell path");
        }

        // Every group renders (no empty groups in the mega menu).
        var nav = EcomAE.Platform.Presentation.PhpSuperCpBocNav.Nav().ToList();
        Assert.Equal(15, nav.Count);
    }

    [Fact]
    public void ErpHomeBranchesSuperVsTenant()
    {
        // Mirrors PHP: ecomae.com /erp is the SUPER ERP (fleet command center,
        // epc_super_erp_fleet_dashboard.php), never one tenant's books.
        var app = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Pages/ErpBosDashboardApp.razor"));
        Assert.Contains("PlatformHostPolicy.IsSuperCpHost", app, StringComparison.Ordinal);
        Assert.Contains("<PhpSuperErpFleetDashboard", app, StringComparison.Ordinal);

        var fleet = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpSuperErpFleetDashboard.razor"));
        Assert.Contains("Super ERP — Fleet Command Center", fleet, StringComparison.Ordinal);
        Assert.Contains("sep-hero", fleet, StringComparison.Ordinal);
        Assert.Contains("sep-erp-grid", fleet, StringComparison.Ordinal);
        Assert.Contains("data-sep-tab", fleet, StringComparison.Ordinal);
        // All five live tenant instances (same as the PHP fleet dashboard).
        foreach (var tenant in new[] { "eParts Cart", "Electronicae", "Style N Look", "The Jewellery Trend", "Taxofinca" })
        {
            Assert.Contains(tenant, fleet, StringComparison.Ordinal);
        }

        // The 22 ERP feature modules registry.
        foreach (var module in new[] { "SLA Agreements", "Gold Rate API", "AML Compliance", "RFID System", "Landed Cost V2" })
        {
            Assert.Contains(module, fleet, StringComparison.Ordinal);
        }

        // Fleet console never queries a single tenant's ERP digests.
        Assert.DoesNotContain("BuildErpAsync", fleet, StringComparison.Ordinal);
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
