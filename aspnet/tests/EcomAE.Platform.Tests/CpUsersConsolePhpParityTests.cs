using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards /cp/users-app against inventing a thin list without PHP user_manager / user.php detail.
/// </summary>
public sealed class CpUsersConsolePhpParityTests
{
    [Fact]
    public void CpUsersApp_IsLiveUserManagerAndUserEditorTwin()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpUsersApp.razor"));
        Assert.Contains("PhpCpModulePageHeader", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-kpi__card", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-data-table", text, StringComparison.Ordinal);
        Assert.Contains("epc-users-page", text, StringComparison.Ordinal);
        Assert.Contains("CpUsersConsoleStylesheets", text, StringComparison.Ordinal);
        Assert.Contains("ICpUserEditorService", text, StringComparison.Ordinal);
        Assert.Contains("Users.ListAsync(", text, StringComparison.Ordinal);
        Assert.Contains("Users.OpenAsync(", text, StringComparison.Ordinal);
        Assert.Contains("CpUserFilter(", text, StringComparison.Ordinal);
        foreach (var marker in new[]
        {
            "f_user_id", "f_group_id", "f_email", "f_phone", "f_unlocked", "ff_",
            "SortLink(l, \"user_id\"", "SortLink(l, \"balance\"", "SortLink(l, \"unlocked\"", "s_page=",
            "l.TableColumns", "r.ProfileValues", "r.Groups", "RegVariantCaption",
            "reg_variant_selector", "additional_fields_div", "regenerateFields", "groups_tree", "RenderGroupTree",
            "fields_json", "name=\"groups\"", "email_confirmed", "phone_confirmed", "save_action",
            "/cp/users/create", "/cp/users/update", "/cp/users/set-password", "/cp/users/delete", "/cp/users/set-unlocked", "/cp/users/set-comment",
            "name=\"confirmWrites\" value=\"true\"", "delete_users()", "user_id=",
            "/CP/users/usermanager", "/cp/groups-app", "/cp/orders", "/cp/credit-limits-app"
        })
        {
            Assert.Contains(marker, text, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("PhpParityModuleBody", text, StringComparison.Ordinal);
        Assert.DoesNotContain("GetCpUserDetailAsync", text, StringComparison.Ordinal);
        Assert.DoesNotContain("GetCpUsersAsync", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(phpHref)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void CpUserServices_ExposeListEditorUpdateDelete()
    {
        var reader = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Cp/CpUserEditorService.cs"));
        Assert.Contains("interface ICpUserEditorService", reader, StringComparison.Ordinal);
        Assert.Contains("Task<CpUserList> ListAsync(", reader, StringComparison.Ordinal);
        Assert.Contains("Task<CpUserEditor?> OpenAsync(", reader, StringComparison.Ordinal);
        Assert.Contains("shop_users_accounting", reader, StringComparison.Ordinal);
        Assert.Contains("users_profiles", reader, StringComparison.Ordinal);
        Assert.Contains("users_groups_bind", reader, StringComparison.Ordinal);
        Assert.Contains("reg_fields", reader, StringComparison.Ordinal);
        Assert.Contains("reg_variants", reader, StringComparison.Ordinal);

        var writer = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Cp/CpUserWriteService.cs"));
        Assert.Contains("Task<ErpSimpleWriteResult> UpdateAsync(", writer, StringComparison.Ordinal);
        Assert.Contains("Task<ErpSimpleWriteResult> DeleteAsync(", writer, StringComparison.Ordinal);

        var routes = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs"));
        Assert.Contains("CpUsersUpdate = \"/cp/users/update\"", routes, StringComparison.Ordinal);
        Assert.Contains("CpUsersDelete = \"/cp/users/delete\"", routes, StringComparison.Ordinal);

        var module = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        Assert.Contains("EcomAeRoutes.CpUsersUpdate", module, StringComparison.Ordinal);
        Assert.Contains("EcomAeRoutes.CpUsersDelete", module, StringComparison.Ordinal);

        var program = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("ICpUserEditorService, EcomAE.Platform.Cp.CpUserEditorService", program, StringComparison.Ordinal);
    }

    [Fact]
    public void CpUsersConsoleStylesheets_ArePlatformAssets()
    {
        Assert.Contains(
            LegacyPresentationAssets.CpUsersConsoleStylesheets,
            href => href.Contains("/platform-assets/epc_users_cp.css", StringComparison.Ordinal));
        Assert.True(File.Exists(FindRepoFile("cp/content/users/epc_users_cp.css")));
    }

    [Fact]
    public void PhpLegacyAssetBridge_MapsUsersCss()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs"));
        Assert.Contains("/platform-assets/epc_users_cp.css", text, StringComparison.Ordinal);
        Assert.Contains("cp/content/users/epc_users_cp.css", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Reporter_ExposesUserDetailDigest()
    {
        var iface = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Migration/ISurfaceDashboardSummaryReporter.cs"));
        Assert.Contains("GetCpUserDetailAsync", iface, StringComparison.Ordinal);
        var sql = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Migration/LegacySurfaceDashboardSql.cs"));
        Assert.Contains("SelectCpUserById", sql, StringComparison.Ordinal);
        Assert.Contains("SelectCpUserGroups", sql, StringComparison.Ordinal);
        Assert.Contains("SelectCpUserBalance", sql, StringComparison.Ordinal);
        var models = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardModels.cs"));
        Assert.Contains("CpUserDetailDigest", models, StringComparison.Ordinal);
        Assert.Contains("CpUserGroupDigest", models, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/CP/control/users?user_id=42", "/cp/users-app?user_id=42")]
    [InlineData("/CP/users/usermanager/user?user_id=7", "/cp/users-app?user_id=7")]
    [InlineData("/CP/users/user?user_id=9", "/cp/users-app?user_id=9")]
    [InlineData("/CP/control/users", "/cp/users-app")]
    public void AspNetPrimaryHref_PreservesUserIdOnUsersApp(string phpHref, string expected)
    {
        Assert.Equal(expected, PhpSurfaceLinkMap.AspNetPrimaryHref(phpHref));
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

            var alt = Path.GetFullPath(Path.Combine(dir.FullName, "..", "..", "..", "..", "..", relative));
            if (File.Exists(alt))
            {
                return alt;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
