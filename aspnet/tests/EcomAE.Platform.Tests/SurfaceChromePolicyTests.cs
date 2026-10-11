using EcomAE.Platform.Auth;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Security;
using EcomAE.Platform.Services;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class SurfaceChromePolicyTests
{
    [Fact]
    public void SuperCp_Nav_StaysOnSuperCp_NoErpWindow_NoRepeats()
    {
        var nav = PhpSuperCpBocNav.Nav().ToList();
        Assert.NotEmpty(nav);
        Assert.DoesNotContain(nav, n => n.Group.Key.Equals("erp", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(nav, n => n.Group.Key.Equals("shop", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(nav, n => n.Group.Key.Equals("catalogue", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(nav, n => n.Group.Key.Equals("logistics", StringComparison.OrdinalIgnoreCase));

        var areas = nav.SelectMany(n => n.Areas).ToList();
        Assert.NotEmpty(areas);
        Assert.Contains(areas, a => a.Key == "command_center");
        Assert.Contains(areas, a => a.Key == "tenant_hub");
        Assert.DoesNotContain(areas, a => a.Group.Equals("erp", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(areas, a => SurfaceChromePolicy.IsErpDestination(a.Href));
        Assert.DoesNotContain(areas, a => SurfaceChromePolicy.IsBosDestination(a.Href));
        Assert.DoesNotContain(areas, a => SurfaceChromePolicy.IsTenantShopDesk(a.PhpPath));

        var paths = areas.Select(a => PathOnly(a.Href)).ToList();
        var repeats = paths.GroupBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        Assert.True(repeats.Count == 0, "Super CP menu repeats: " + string.Join(", ", repeats));
    }

    [Fact]
    public void TenantCp_Menu_HidesErpWindowDestinations()
    {
        var file = Directory.GetFiles(Path.Combine(FindRepoRoot(), "content/files/epc_cache"), "epc_cp_menu_rows_v1_*.json")
            .Order(StringComparer.Ordinal)
            .First();
        var rows = CpNavMenuService.ParseSnapshot(File.ReadAllText(file), Path.GetFileName(file));
        Assert.NotNull(rows);
        var groups = TopMenuLinks.ForHost(
            TopMenuLinks.DedupeCp(CpNavTree.Build(rows.Groups, rows.Items, CpNavPolicy.Tenant(), static _ => null)),
            "www.epartscart.com");
        var hrefs = groups.SelectMany(g => g.Items).Select(i => CpNavTree.AspNetHref(i.Url)).ToList();
        Assert.Contains(hrefs, h => h.Contains("/cp/orders", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(hrefs, SurfaceChromePolicy.IsErpDestination);
        Assert.DoesNotContain(groups, SurfaceChromePolicy.IsErpNavGroup);
    }

    [Fact]
    public void SuperHost_Menu_HidesTenantShopAndErp()
    {
        var file = Directory.GetFiles(Path.Combine(FindRepoRoot(), "content/files/epc_cache"), "epc_cp_menu_rows_v1_*.json")
            .Order(StringComparer.Ordinal)
            .First();
        var rows = CpNavMenuService.ParseSnapshot(File.ReadAllText(file), Path.GetFileName(file));
        Assert.NotNull(rows);
        var groups = TopMenuLinks.ForHost(
            TopMenuLinks.DedupeCp(CpNavTree.Build(rows.Groups, rows.Items, CpNavPolicy.SuperOperator(), static _ => null)),
            "www.ecomae.com");
        var hrefs = groups.SelectMany(g => g.Items).Select(i => CpNavTree.AspNetHref(i.Url)).ToList();
        Assert.DoesNotContain(hrefs, SurfaceChromePolicy.IsErpDestination);
        Assert.DoesNotContain(hrefs, SurfaceChromePolicy.IsBosDestination);
        Assert.DoesNotContain(hrefs, SurfaceChromePolicy.IsTenantShopDesk);
        Assert.DoesNotContain(groups, SurfaceChromePolicy.IsErpNavGroup);
    }

    [Fact]
    public void Brochure_DropsErpWindowOnTenantAndSuper()
    {
        var tenant = LegacyDesktopChromeCatalog.BrochureFeaturesForHost(PhpModuleCatalog.CpBrochureFeatures, "www.epartscart.com");
        var super = LegacyDesktopChromeCatalog.BrochureFeaturesForHost(PhpModuleCatalog.CpBrochureFeatures, "www.ecomae.com");
        Assert.DoesNotContain(tenant, l => SurfaceChromePolicy.IsErpDestination(l.Href));
        Assert.DoesNotContain(super, l => SurfaceChromePolicy.IsErpDestination(l.Href));
        Assert.DoesNotContain(super, l => SurfaceChromePolicy.IsTenantShopDesk(l.Href));
    }

    [Fact]
    public void TenantTopnav_DropsErpGroup()
    {
        var groups = LegacyDesktopChromeCatalog.ControlPanelTopnav(includeSuperOnly: false, industryCode: "auto_parts");
        Assert.DoesNotContain(groups, g => g.Label.Equals("ERP", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(groups.SelectMany(g => g.Links), l => SurfaceChromePolicy.IsErpDestination(l.Href));
        Assert.Contains(groups, g => g.Label.Equals("Commerce", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("/erp")]
    [InlineData("/erp/payables-app")]
    [InlineData("/CP/shop/finance/erp?epc_erp_shell=1&area=gl")]
    [InlineData("/ERP/?epc_erp_shell=1&area=ap")]
    public void ErpDestinations_AreDetected(string href)
        => Assert.True(SurfaceChromePolicy.IsErpDestination(href), href);

    [Theory]
    [InlineData("/cp/orders")]
    [InlineData("/cp/uae-tax-compliance-app")]
    [InlineData("/cp/tenants-app")]
    [InlineData("/brochure/cp")]
    public void CpDestinations_AreNotErp(string href)
        => Assert.False(SurfaceChromePolicy.IsErpDestination(href), href);

    [Fact]
    public void Switchers_HonorCapabilityAndTenantMode()
    {
        var both = new LegacySessionContext(
            LegacySessionKind.Admin, 1, "s",
            [EcomAePermissions.TenantCpAccess, EcomAePermissions.TenantErpAccess]);
        var cpOnly = new LegacySessionContext(
            LegacySessionKind.Admin, 2, "s",
            LegacyAdminPermissionSets.ForCpOnlyTenant());
        var erpOnly = new LegacySessionContext(
            LegacySessionKind.Admin, 3, "s",
            LegacyAdminPermissionSets.ForErpOnlyTenant());

        Assert.True(SurfaceChromePolicy.ShowErpSwitcher(both, TenantMode.LiveTenant));
        Assert.True(SurfaceChromePolicy.ShowCpSwitcher(both, TenantMode.LiveTenant));

        Assert.False(SurfaceChromePolicy.ShowErpSwitcher(cpOnly, TenantMode.LiveTenant));
        Assert.True(SurfaceChromePolicy.ShowCpSwitcher(cpOnly, TenantMode.LiveTenant));

        Assert.True(SurfaceChromePolicy.ShowErpSwitcher(erpOnly, TenantMode.LiveTenant));
        Assert.False(SurfaceChromePolicy.ShowCpSwitcher(erpOnly, TenantMode.LiveTenant));

        Assert.False(SurfaceChromePolicy.ShowCpSwitcher(both, TenantMode.ErpOnlyTenant));
        Assert.False(SurfaceChromePolicy.ShowErpSwitcher(both, TenantMode.ErpOnlyTenant));
        Assert.False(SurfaceChromePolicy.ShowCpSwitcher(erpOnly, TenantMode.ErpOnlyTenant));
    }

    [Fact]
    public void PermissionMint_ErpOnlyTenant_HasNoCpClaim()
    {
        var live = LegacyAdminPermissionSets.ForRequest("www.epartscart.com", TenantMode.LiveTenant);
        var erpOnly = LegacyAdminPermissionSets.ForRequest("www.taxofinca.com", TenantMode.ErpOnlyTenant);
        var super = LegacyAdminPermissionSets.ForRequest("www.ecomae.com", TenantMode.Platform);

        Assert.Contains(EcomAePermissions.TenantCpAccess, live);
        Assert.Contains(EcomAePermissions.TenantErpAccess, live);
        Assert.DoesNotContain(EcomAePermissions.TenantCpAccess, erpOnly);
        Assert.Contains(EcomAePermissions.TenantErpAccess, erpOnly);
        Assert.Contains(EcomAePermissions.SuperCpAccess, super);
        Assert.Contains(EcomAePermissions.SuperErpAccess, super);
    }

    [Fact]
    public void CpShortcuts_HideErpWhenCpOnly()
    {
        var withErp = CpShortcutCatalog.ForCpSurface(includeErp: true);
        var cpOnly = CpShortcutCatalog.ForCpSurface(includeErp: false);
        Assert.Contains(withErp, i => SurfaceChromePolicy.IsErpDestination(i.Url));
        Assert.DoesNotContain(cpOnly, i => SurfaceChromePolicy.IsErpDestination(i.Url));
        Assert.Contains(cpOnly, i => i.Key == "orders");
    }

    [Fact]
    public void TenantCpCommandCentre_StaysOnCpSurface()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Components/Pages/CpCommandCentreApp.razor"));
        Assert.Contains("ForCpSurface(includeErp: false)", src, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildErpWorkspaceHomeAsync", src, StringComparison.Ordinal);
        Assert.DoesNotContain("ForCpSurface(_showErp)", src, StringComparison.Ordinal);
    }

    [Fact]
    public void SuperInnerPages_KeepBocMegaMenu_NotEmptyLeftRail()
    {
        var chrome = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor"));
        var css = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "cp/templates/bootstrap_admin/css/epc_cp_professional.css"));
        Assert.Contains("_showSuperBocChrome", chrome, StringComparison.Ordinal);
        Assert.Contains("PhpSuperCpBocTopnav", chrome, StringComparison.Ordinal);
        Assert.Contains("margin-left:0 !important", chrome, StringComparison.Ordinal);
        Assert.Contains("flex-wrap:wrap", chrome, StringComparison.Ordinal);
        Assert.Contains(":not(:has(#epc_boc_topnav))", css, StringComparison.Ordinal);
    }

    [Fact]
    public void CpMenus_DoNotRepeatFirstItemViaHubLink()
    {
        var super = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpSuperCpCommandCentre.razor"));
        var tenant = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor"));
        Assert.DoesNotContain("epc-boc__topnav-panel-hub", super, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-cp-topnav-panel-hub", tenant, StringComparison.Ordinal);
    }

    [Fact]
    public void DeployProbes_StayOnRealHealthEndpoints()
    {
        var program = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.DoesNotContain("ProbeBypassMiddleware", program, StringComparison.Ordinal);
        Assert.Contains("app.MapHealthChecks(EcomAeRoutes.Health)", program, StringComparison.Ordinal);
        Assert.Contains("app.MapGet(EcomAeRoutes.Readiness", program, StringComparison.Ordinal);

        var isolationPaste = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "scripts/cloudpanel_deploy_cp_erp_chrome_isolation_now.sh"));
        Assert.Contains("REFUSED", isolationPaste, StringComparison.Ordinal);
        Assert.DoesNotContain("ECOMAE_EMERGENCY_PUBLISH=\"${ECOMAE_EMERGENCY_PUBLISH:-1}\"", isolationPaste, StringComparison.Ordinal);

        var gated = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "scripts/cloudpanel_deploy_main_gated.sh"));
        Assert.Contains("ECOMAE_EMERGENCY_PUBLISH=0", gated, StringComparison.Ordinal);
        Assert.Contains("wait_for_aspnet_health.sh", gated, StringComparison.Ordinal);
        Assert.Contains("\"$BRANCH\" != \"main\"", gated, StringComparison.Ordinal);
    }

    private static string PathOnly(string href)
    {
        var key = TopMenuLinks.DestinationKey(href);
        var q = key.IndexOf('?', StringComparison.Ordinal);
        return q < 0 ? key : key[..q];
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}
