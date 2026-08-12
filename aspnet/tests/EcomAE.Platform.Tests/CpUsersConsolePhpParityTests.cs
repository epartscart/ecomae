using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards /cp/users-app against inventing a thin list without PHP user_manager / user.php detail.
/// </summary>
public sealed class CpUsersConsolePhpParityTests
{
    [Fact]
    public void CpUsersApp_EmitsDualPaneConsoleMarkers()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Components/Pages/CpUsersApp.razor"));
        Assert.Contains("PhpCpModulePageHeader", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-kpi", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-kpi__card", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-table-card", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-data-table", text, StringComparison.Ordinal);
        Assert.Contains("epc-users-page", text, StringComparison.Ordinal);
        Assert.Contains("epc-users-page__hero", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-users-workspace", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-users-workspace__list", text, StringComparison.Ordinal);
        Assert.Contains("epc-scp-users-workspace__detail", text, StringComparison.Ordinal);
        Assert.Contains("epc-ud", text, StringComparison.Ordinal);
        Assert.Contains("user_id=", text, StringComparison.Ordinal);
        Assert.Contains("GetCpUserDetailAsync", text, StringComparison.Ordinal);
        Assert.Contains("CpUsersConsoleStylesheets", text, StringComparison.Ordinal);
        Assert.Contains("/cp/groups-app", text, StringComparison.Ordinal);
        Assert.Contains("/cp/orders", text, StringComparison.Ordinal);
        Assert.Contains("/cp/credit-limits-app", text, StringComparison.Ordinal);
        Assert.DoesNotContain("AspNetPrimaryHref(phpHref)", text, StringComparison.Ordinal);
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
