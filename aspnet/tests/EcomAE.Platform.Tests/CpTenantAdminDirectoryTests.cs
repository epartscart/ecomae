using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpTenantAdminDirectoryTests
{
    [Fact]
    public void Filter_SiteKeyIsolatesTenants_EmptyKeySeesAll()
    {
        var rows = SampleDirectory();

        var all = CpTenantAdminDirectoryService.Filter(rows, null, null, 20);
        Assert.Equal(3, all.Count);
        Assert.Contains(all, row => row.SiteKey == "indj");
        Assert.Contains(all, row => row.SiteKey == "asap");
        Assert.Contains(all, row => row.SiteKey == "real");

        var jewellery = CpTenantAdminDirectoryService.Filter(rows, "INDJ", null, 20);
        Assert.Single(jewellery);
        Assert.Equal("indj", jewellery[0].SiteKey);
        Assert.DoesNotContain(jewellery, row => row.SiteKey == "asap");
        Assert.DoesNotContain(jewellery, row => row.SiteKey == "real");
    }

    [Fact]
    public void Filter_QueryMatchesEmailOrTenant_DoesNotLeakOtherAdmins()
    {
        var rows = SampleDirectory();
        var ada = CpTenantAdminDirectoryService.Filter(rows, null, "ada@indj.test", 20);
        Assert.Single(ada);
        Assert.Equal("indj", ada[0].SiteKey);

        var fleet = CpTenantAdminDirectoryService.Filter(rows, null, "asap", 20);
        Assert.Single(fleet);
        Assert.Equal("asap", fleet[0].SiteKey);
        Assert.False(CpTenantAdminDirectoryService.Matches(rows[0], "real"));
        Assert.True(CpTenantAdminDirectoryService.Matches(rows[0] with { Phone = "+971501112233" }, "501112233"));
    }

    [Fact]
    public void AdministratorGroup_MatchesLiveAdminsLabel_NotDepartmentAdmin()
    {
        Assert.True(ErpUserAccess.IsAdministratorGroup("Admins"));
        Assert.True(ErpUserAccess.IsAdministratorGroup("admins"));
        Assert.True(ErpUserAccess.IsAdministratorGroup("Admin"));
        Assert.True(ErpUserAccess.IsAdministratorGroup("Administrators"));
        Assert.True(ErpUserAccess.IsAdministratorGroup("Administrator"));
        Assert.True(ErpUserAccess.IsAdministratorGroup("Администратор"));
        Assert.False(ErpUserAccess.IsAdministratorGroup("EPC_ERP_DEPT_ADMIN"));
        Assert.False(ErpUserAccess.IsAdministratorGroup("EPC_ERP_TEAM"));
        Assert.False(ErpUserAccess.IsAdministratorGroup("808"));
        Assert.False(ErpUserAccess.IsAdministratorGroup(""));
        Assert.False(ErpUserAccess.IsAdministratorGroup(null));
    }

    [Fact]
    public void LoginHow_NamesEmailPasswordAndCompanyUrl_NeverStoresSecret()
    {
        var erp = CpTenantAdminDirectoryService.HowTheyLogin("erp_only", "indj", null, "indj@ecomae.com");
        Assert.Equal("indj@ecomae.com + password at /cp/client-erp/indj/", erp);
        Assert.Equal("/cp/client-erp/indj/", CpTenantAdminDirectoryService.LoginUrl("erp_only", "indj", "ignored.example"));
        Assert.Equal("/cp/client-erp/indj/erp/users-app", CpTenantAdminDirectoryService.UsersAppHref("erp_only", "indj", null));

        var commerce = CpTenantAdminDirectoryService.HowTheyLogin("commerce", "shop", "https://WWW.shop.example/en/", "ops@shop.example");
        Assert.Contains("ops@shop.example + password at https://www.shop.example/erp/login", commerce, StringComparison.Ordinal);

        Assert.DoesNotContain("$2y$", erp, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", erp, StringComparison.OrdinalIgnoreCase);
        Assert.True(CpTenantAdminDirectoryService.AuditLooksSafe("""{"user_id":"4","unlocked":"1"}"""));
        Assert.False(CpTenantAdminDirectoryService.AuditLooksSafe("""{"password":"nope"}"""));
    }

    [Fact]
    public void SuperCpSeesAllTenants_TenantUsersAppHasNoSiteKey()
    {
        var super = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpTenantAdminsApp.razor"));
        Assert.Contains("All tenants", super, StringComparison.Ordinal);
        Assert.Contains("How they login", super, StringComparison.Ordinal);
        Assert.Contains("Login sessions", super, StringComparison.Ordinal);
        Assert.Contains("Audit log", super, StringComparison.Ordinal);
        Assert.Contains("Control users", super, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", super, StringComparison.Ordinal);
        Assert.Contains("/cp/client-erp/{key}/", super, StringComparison.Ordinal);
        Assert.Contains("UsersAppHref", super, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", super, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", super, StringComparison.Ordinal);

        var tenant = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpUsersApp.razor"));
        Assert.Contains("This company only", tenant, StringComparison.Ordinal);
        Assert.Contains("Login sessions", tenant, StringComparison.Ordinal);
        Assert.Contains("Audit log", tenant, StringComparison.Ordinal);
        Assert.DoesNotContain("site_key", tenant, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenRegistryAsync", tenant, StringComparison.Ordinal);
        Assert.DoesNotContain("All tenants", tenant, StringComparison.Ordinal);
    }

    [Fact]
    public void ServiceAndHandlersNeverSelectPassword()
    {
        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Cp/CpTenantAdminDirectoryService.cs"));
        Assert.Contains("OpenRegistryAsync", service, StringComparison.Ordinal);
        Assert.Contains("epc_erp_audit_log", service, StringComparison.Ordinal);
        Assert.Contains("HowTheyLogin", service, StringComparison.Ordinal);
        Assert.Contains("PublicTenant", service, StringComparison.Ordinal);
        Assert.DoesNotContain("u.`password`", service, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"password\"]", service, StringComparison.Ordinal);
        Assert.Contains("AuditLooksSafe", service, StringComparison.Ordinal);

        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("MapGet(EcomAeRoutes.ControlPanelTenantAdmins,", module, StringComparison.Ordinal);
        Assert.Contains("MapGet(EcomAeRoutes.ControlPanelTenantAdminsDetail,", module, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", module, StringComparison.Ordinal);
        Assert.Contains("Tenant admin directory is Super CP only", module, StringComparison.Ordinal);
    }

    [Fact]
    public void RoutesNavAndCatalogAreWired()
    {
        Assert.Equal("/cp/tenant-admins-app", EcomAeRoutes.ControlPanelTenantAdminsApp);
        Assert.Equal("/cp/tenant-admins", EcomAeRoutes.ControlPanelTenantAdmins);
        Assert.Equal("/cp/tenant-admins/detail", EcomAeRoutes.ControlPanelTenantAdminsDetail);

        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpTenantAdminDirectoryService", program, StringComparison.Ordinal);

        Assert.Contains(PhpSuperCpBocNav.Areas, a => a.Key == "tenant_admins" && a.Href == "/cp/tenant-admins-app");
        Assert.Equal("/cp/tenant-admins-app", PhpSurfaceLinkMap.AspNetPrimaryHref("/CP/control/portal/epc_tenant_admins"));
        Assert.Contains(LegacyChromeNavCatalog.ControlPanelQuickActions, item => item.Href == "/cp/tenant-admins-app");
        Assert.Contains(EcomAE.Platform.Services.EcomaeEcosystemCatalog.ClientManagement, c => c.Key == "tenant-admins" && c.Href == "/cp/tenant-admins-app");

        Assert.Equal("digest-wired-awaiting-dual-sample", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/tenant-admins-app").Status);
        Assert.Contains("Passwords never returned", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/cp/tenant-admins-app").Notes, StringComparison.Ordinal);
        Assert.Contains("This-tenant-only", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/users-app").Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void TenantUsersAppStillControlsOwnUsers()
    {
        Assert.Equal("/erp/users-app", EcomAeRoutes.ErpUsersApp);
        var page = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpUsersApp.razor"));
        Assert.Contains("action=\"/erp/users/prepare\"", page, StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/users/credentials\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", page, StringComparison.Ordinal);
        Assert.Contains("email + password", page, StringComparison.OrdinalIgnoreCase);

        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpTenantUserAdminService.cs"));
        Assert.DoesNotContain("[\"password\"]", service, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenRegistryAsync", service, StringComparison.Ordinal);
        Assert.DoesNotContain("site_key", service, StringComparison.Ordinal);
    }

    private static IReadOnlyList<CpTenantAdminUserRow> SampleDirectory() =>
    [
        Row("indj", "INDJ Jewellery", "erp_only", "Ada Admin", "ada@indj.test", 11),
        Row("asap", "ASAP Autoparts", "erp_only", "Omar Ops", "omar@asap.test", 21),
        Row("real", "REAL Estate", "erp_only", "Rita Real", "rita@real.test", 31),
    ];

    private static CpTenantAdminUserRow Row(
        string siteKey,
        string trade,
        string type,
        string name,
        string email,
        long userId)
        => new(
            siteKey,
            trade,
            type,
            CpTenantControlCenterService.TypeLabel(type),
            CpTenantAdminDirectoryService.LoginUrl(type, siteKey, null),
            CpTenantAdminDirectoryService.UsersAppHref(type, siteKey, null),
            userId,
            email,
            name,
            "Administration",
            true,
            true,
            true,
            false,
            1,
            1,
            CpTenantAdminDirectoryService.HowTheyLogin(type, siteKey, null, email),
            "signed in");

    private static string FindRepoFile(string relative)
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
