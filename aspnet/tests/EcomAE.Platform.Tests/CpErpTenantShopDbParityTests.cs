using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Guards CP/ERP digests against the empty-data bug: ePartsCart shop opens must use
/// shared Model C <c>docpart</c> with registry credentials (not portal db_user override),
/// and portal fleet tables must use <c>OpenRegistryAsync</c>.
/// </summary>
public sealed class CpErpTenantShopDbParityTests
{
    [Fact]
    public void OpenTenantShopAsync_AlwaysForcesDocpartOnEpartsCart()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs"));
        Assert.Contains("private Task<DbConnection> OpenTenantShopAsync", text, StringComparison.Ordinal);
        Assert.Contains("IsEpartsCartRequest()", text, StringComparison.Ordinal);
        Assert.Contains("OpenAsync(\"docpart\"", text, StringComparison.Ordinal);
        // Must NOT require !HasTenantDatabase — portal row with db_name=docpart + bad db_user
        // previously skipped the force and emptied every module.
        var methodStart = text.IndexOf("private Task<DbConnection> OpenTenantShopAsync", StringComparison.Ordinal);
        Assert.True(methodStart >= 0);
        var methodEnd = text.IndexOf("private bool IsEpartsCartRequest", methodStart, StringComparison.Ordinal);
        Assert.True(methodEnd > methodStart);
        var method = text[methodStart..methodEnd];
        Assert.DoesNotContain("HasTenantDatabase", method, StringComparison.Ordinal);
        Assert.Contains("OpenAsync(\"docpart\"", method, StringComparison.Ordinal);
    }

    [Fact]
    public void Factory_DoesNotApplyTenantUserOnExplicitDatabaseOpen()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Data/MySqlTenantDbConnectionFactory.cs"));
        Assert.Contains("Explicit databaseName without userName", text, StringComparison.Ordinal);
        Assert.Contains("string.IsNullOrWhiteSpace(databaseName)", text, StringComparison.Ordinal);
        Assert.Contains("epc_portal_tenants.db_user must NOT", text, StringComparison.Ordinal);
        // Old bug: always `userName ?? tenant?.DbUser` even for OpenAsync("docpart").
        Assert.DoesNotContain(
            "var user = !string.IsNullOrWhiteSpace(userName) ? userName : tenant?.DbUser;",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ShopDigests_UseOpenTenantShopAsync_NotBareOpenAsyncNull()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs"));
        Assert.Contains("ListCpUsersAsync", text, StringComparison.Ordinal);
        Assert.Contains("ListCpOrdersAsync", text, StringComparison.Ordinal);
        Assert.Contains("BuildErpAsync", text, StringComparison.Ordinal);
        Assert.Contains("ListErpCashAccountsAsync", text, StringComparison.Ordinal);
        Assert.Contains("ListErpSuppliersAsync", text, StringComparison.Ordinal);
        // Only the non-ePartsCart fallback inside OpenTenantShopAsync may call OpenAsync(null).
        var opens = 0;
        var idx = 0;
        while ((idx = text.IndexOf("_connections.OpenAsync(null", idx, StringComparison.Ordinal)) >= 0)
        {
            opens++;
            idx += 1;
        }

        Assert.Equal(1, opens);
        Assert.True(text.Contains("OpenTenantShopAsync", StringComparison.Ordinal));
        Assert.True(text.Split("OpenTenantShopAsync", StringSplitOptions.None).Length > 50);
    }

    [Fact]
    public void PortalDigests_UseOpenRegistryAsync()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs"));
        foreach (var marker in new[]
                 {
                     "BuildCpPortalSettingsDigestAsync",
                     "ListCpDemoTenantsAsync",
                     "BuildCpPlatformCommunicationDigestAsync",
                     "BuildCpInfoBlocksDigestAsync",
                     "BuildCpFreeToolsDigestAsync",
                     "ListPortalTenantsAsync",
                 })
        {
            // Prefer the method declaration site (not call sites).
            var start = text.IndexOf(" " + marker + "(", StringComparison.Ordinal);
            if (start < 0)
            {
                start = text.IndexOf(marker + "(", StringComparison.Ordinal);
            }

            Assert.True(start >= 0, marker);
            var window = text.Substring(start, Math.Min(1800, text.Length - start));
            Assert.Contains("OpenRegistryAsync", window, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AuthGate_DoesNotJsonTrapDashboardSummaryApps()
    {
        Assert.False(EcomAE.Platform.Middleware.AdminSurfaceAuthGateMiddleware.IsJsonChallengePath("/cp/dashboard-summary-app"));
        Assert.False(EcomAE.Platform.Middleware.AdminSurfaceAuthGateMiddleware.IsJsonChallengePath("/erp/dashboard-summary-app"));
        Assert.True(EcomAE.Platform.Middleware.AdminSurfaceAuthGateMiddleware.IsJsonChallengePath("/cp/dashboard-summary"));
        Assert.True(EcomAE.Platform.Middleware.AdminSurfaceAuthGateMiddleware.IsJsonChallengePath("/erp/dashboard-summary"));
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
