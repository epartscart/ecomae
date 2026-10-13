using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Routing;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpTenantUserAdminTests
{
    [Fact]
    public void ValidatePrepare_RequiresEmailNameDepartmentAndPassword()
    {
        Assert.Equal("invalid", ErpTenantUserAdminService.ValidatePrepare(new ErpTenantUserPrepareRequest(
            Email: null, DisplayName: "Ada", JobTitle: "Sales", DepartmentCode: "sales", Password: "secret12"))!.Code);
        Assert.Equal("invalid", ErpTenantUserAdminService.ValidatePrepare(new ErpTenantUserPrepareRequest(
            Email: "not-an-email", DisplayName: "Ada", JobTitle: "Sales", DepartmentCode: "sales", Password: "secret12"))!.Code);
        Assert.Equal("invalid", ErpTenantUserAdminService.ValidatePrepare(new ErpTenantUserPrepareRequest(
            Email: "ada@tenant.test", DisplayName: "  ", JobTitle: "Sales", DepartmentCode: "sales", Password: "secret12"))!.Code);
        Assert.Equal("invalid", ErpTenantUserAdminService.ValidatePrepare(new ErpTenantUserPrepareRequest(
            Email: "ada@tenant.test", DisplayName: "Ada", JobTitle: "Sales", DepartmentCode: "nope", Password: "secret12"))!.Code);
        Assert.Equal("invalid", ErpTenantUserAdminService.ValidatePrepare(new ErpTenantUserPrepareRequest(
            Email: "ada@tenant.test", DisplayName: "Ada", JobTitle: "Sales", DepartmentCode: "sales", Password: "short"))!.Code);
        Assert.Null(ErpTenantUserAdminService.ValidatePrepare(new ErpTenantUserPrepareRequest(
            Email: "ada@tenant.test", DisplayName: "Ada Sales", JobTitle: "Executive", DepartmentCode: "sales", Password: "secret12")));
    }

    [Fact]
    public void ValidateCredentials_RefusesSelfLockAndShortPassword()
    {
        Assert.Equal("invalid", ErpTenantUserAdminService.ValidateCredentials(
            new ErpTenantUserCredentialsRequest(0, "lock"))!.Code);
        Assert.Equal("invalid", ErpTenantUserAdminService.ValidateCredentials(
            new ErpTenantUserCredentialsRequest(4, "explode"))!.Code);
        Assert.Equal("self", ErpTenantUserAdminService.ValidateCredentials(
            new ErpTenantUserCredentialsRequest(4, "lock", ActorUserId: 4))!.Code);
        Assert.Equal("invalid", ErpTenantUserAdminService.ValidateCredentials(
            new ErpTenantUserCredentialsRequest(4, "password", "short"))!.Code);
        Assert.Null(ErpTenantUserAdminService.ValidateCredentials(
            new ErpTenantUserCredentialsRequest(4, "unlock", ActorUserId: 9)));
        Assert.Null(ErpTenantUserAdminService.ValidateCredentials(
            new ErpTenantUserCredentialsRequest(4, "password", "secret12", ActorUserId: 4)));
    }

    [Fact]
    public void ValidateDepartment_RequiresKnownCatalogCode()
    {
        Assert.Equal("invalid", ErpTenantUserAdminService.ValidateDepartment(
            new ErpTenantUserDepartmentRequest(0, "sales"))!.Code);
        Assert.Equal("invalid", ErpTenantUserAdminService.ValidateDepartment(
            new ErpTenantUserDepartmentRequest(4, "warehouse-x"))!.Code);
        Assert.Null(ErpTenantUserAdminService.ValidateDepartment(
            new ErpTenantUserDepartmentRequest(4, "finance")));
        Assert.Equal("logistics", ErpTenantUserAdminService.NormalizeDepartment("Logistics"));
        Assert.Equal("EPC_ERP_DEPT_SALES", ErpTenantUserAdminService.DepartmentGroupValue("sales"));
        Assert.True(ErpTenantUserAdminService.IsErpDepartmentGroup("EPC_ERP_DEPT_HR"));
        Assert.False(ErpTenantUserAdminService.IsErpDepartmentGroup("EPC_ERP_TEAM"));
    }

    [Fact]
    public void Filter_MatchesEmailDepartmentAndId()
    {
        var rows = new[]
        {
            new ErpTenantUserRow(11, "ada@tenant.test", "", "Ada Sales", "Executive", "sales", "Sales", true, true, false, 1, 1, 1),
            new ErpTenantUserRow(12, "fin@tenant.test", "", "Omar Finance", "Accountant", "finance", "Finance", false, true, false, 0, 1, 1),
            new ErpTenantUserRow(13, "admin@tenant.test", "", "Admin", "IT", "it", "Information Technology", true, true, true, 2, 1, 1),
        };

        var sales = ErpTenantUserAdminService.Filter(rows, null, "sales", 10);
        Assert.Single(sales);
        Assert.Equal(11, sales[0].UserId);

        var ada = ErpTenantUserAdminService.Filter(rows, "ada", null, 10);
        Assert.Single(ada);
        Assert.True(ErpTenantUserAdminService.Matches(rows[1], "12"));
        Assert.False(ErpTenantUserAdminService.Matches(rows[0], "finance"));
    }

    [Fact]
    public void LooksLikeEmail_RejectsLocalHostsAndSpaces()
    {
        Assert.True(ErpTenantUserAdminService.LooksLikeEmail("ada@tenant.test"));
        Assert.False(ErpTenantUserAdminService.LooksLikeEmail("ada@localhost"));
        Assert.False(ErpTenantUserAdminService.LooksLikeEmail("ada tenant@test.com"));
        Assert.False(ErpTenantUserAdminService.LooksLikeEmail(""));
    }

    [Fact]
    public void RoutesAndHandlersAreWired()
    {
        Assert.Equal("/erp/users-app", EcomAeRoutes.ErpUsersApp);
        Assert.Equal("/erp/users/prepare", EcomAeRoutes.ErpUsersPrepare);
        Assert.Equal("/erp/users/credentials", EcomAeRoutes.ErpUsersCredentials);
        Assert.Equal("/erp/users/department", EcomAeRoutes.ErpUsersDepartment);
        Assert.Equal("/erp/users/sessions", EcomAeRoutes.ErpUsersSessions);
        Assert.Equal("/erp/users/audit", EcomAeRoutes.ErpUsersAudit);

        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("MapPost(EcomAeRoutes.ErpUsersPrepare, HandleErpUsersPrepareAsync)", module, StringComparison.Ordinal);
        Assert.Contains("MapPost(EcomAeRoutes.ErpUsersCredentials, HandleErpUsersCredentialsAsync)", module, StringComparison.Ordinal);
        Assert.Contains("MapPost(EcomAeRoutes.ErpUsersDepartment, HandleErpUsersDepartmentAsync)", module, StringComparison.Ordinal);
        Assert.Contains("MapGet(EcomAeRoutes.ErpUsersSessions, HandleErpUsersSessionsAsync)", module, StringComparison.Ordinal);
        Assert.Contains("MapGet(EcomAeRoutes.ErpUsersAudit, HandleErpUsersAuditAsync)", module, StringComparison.Ordinal);
        Assert.Contains("IErpTenantUserAdminService", module, StringComparison.Ordinal);
        Assert.Contains("confirm_required", module, StringComparison.Ordinal);

        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpTenantUserAdminService", program, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkspacePostsNativeFormsAndNeverStoresPasswordInAudit()
    {
        var page = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/ErpUsersApp.razor"));
        Assert.Contains("action=\"/erp/users/prepare\"", page, StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/users/credentials\"", page, StringComparison.Ordinal);
        Assert.Contains("action=\"/erp/users/department\"", page, StringComparison.Ordinal);
        Assert.Contains("name=\"confirmWrites\"", page, StringComparison.Ordinal);
        Assert.Contains("Prepare a department user", page, StringComparison.Ordinal);
        Assert.Contains("Login sessions", page, StringComparison.Ordinal);
        Assert.Contains("Audit log", page, StringComparison.Ordinal);
        Assert.Contains("grant_administrator", page, StringComparison.Ordinal);
        Assert.DoesNotContain("ASP.NET", page, StringComparison.Ordinal);
        Assert.DoesNotContain("/php-reference/", page, StringComparison.Ordinal);

        var service = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpTenantUserAdminService.cs"));
        Assert.Contains("user_prepare", service, StringComparison.Ordinal);
        Assert.Contains("user_password", service, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"password\"]", service, StringComparison.Ordinal);
        Assert.Contains("TeamGroupValue", service, StringComparison.Ordinal);
        Assert.Contains("EPC_ERP_DEPT_", service, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchAndChromeSurfaceTheWorkspace()
    {
        var catalog = ErpGlobalSearchCatalog.Build(jewellery: false, fitOut: false, realEstate: false);
        Assert.Contains(catalog, e => e.Href == "/erp/users-app");
        Assert.Contains(ErpGlobalSearchCatalog.Filter(catalog, "audit"), e => e.Href == "/erp/users-app");
        Assert.Contains(ErpGlobalSearchCatalog.Filter(catalog, "department"), e => e.Href == "/erp/users-app");

        Assert.Equal("/erp/users-app", ErpCapabilityCatalog.Groups.First(g => g.Key == "administration").Href);
        Assert.Contains(PhpModuleCatalog.AspNetErpTabs, t => t.Id == "setup/users");
        Assert.Equal("/erp/users-app", ErpPhpTabRouteMap.All["users"]);

        var chrome = File.ReadAllText(FindRepoFile(
            "aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor"));
        Assert.Contains("<a href=\"/erp/users-app\">Users</a>", chrome, StringComparison.Ordinal);
    }

    [Fact]
    public void CatalogMarksUserWritesLiveGated()
    {
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/users-app").Status);
        Assert.Equal("write-live-gated", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/users/prepare").Status);
        Assert.Contains("department", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/users/prepare").Notes, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PHP remains authoritative", SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/users/prepare").Notes, StringComparison.Ordinal);
    }

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
