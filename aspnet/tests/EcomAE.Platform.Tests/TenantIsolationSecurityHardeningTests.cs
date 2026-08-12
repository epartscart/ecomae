using EcomAE.Platform.Auth;
using EcomAE.Platform.Services;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class TenantIsolationSecurityHardeningTests
{
    [Fact]
    public void TenantContextPublicDto_OmitsDbCredentials()
    {
        var tenant = TenantContext.ForKnownTenant(
            "epartscart",
            "www.epartscart.com",
            TenantMode.LiveTenant,
            TenantSurface.Storefront,
            "/",
            databaseName: "docpart",
            dbUser: "secret_user",
            dbPassword: "secret_pass",
            dedicatedDb: false);

        var dto = tenant.ToPublicDto();
        var json = System.Text.Json.JsonSerializer.Serialize(dto);

        Assert.True(dto.HasTenantDatabase);
        Assert.True(dto.HasDbCredentials);
        Assert.Equal("epartscart", dto.SiteKey);
        Assert.DoesNotContain("secret_user", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret_pass", json, StringComparison.Ordinal);
        Assert.DoesNotContain("DbPassword", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DbUser", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LegacyAdminPermissionSets_SplitByHost()
    {
        var super = LegacyAdminPermissionSets.ForRequestHost("www.ecomae.com");
        var tenant = LegacyAdminPermissionSets.ForRequestHost("www.epartscart.com");

        Assert.Contains(EcomAE.Platform.Security.EcomAePermissions.SuperBosAccess, super);
        Assert.DoesNotContain(EcomAE.Platform.Security.EcomAePermissions.SuperBosAccess, tenant);
        Assert.Contains(EcomAE.Platform.Security.EcomAePermissions.TenantCpAccess, tenant);
    }

    [Fact]
    public void ControlPanelFleetDigests_RequireSuperCpHostGate()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Modules/ControlPanelModule.cs"));
        var tenantsIdx = text.IndexOf("ControlPanelTenants,", StringComparison.Ordinal);
        var demoIdx = text.IndexOf("ControlPanelDemoTenants,", StringComparison.Ordinal);
        Assert.True(tenantsIdx > 0);
        Assert.True(demoIdx > 0);

        // Gate appears before auth check in each MapGet body.
        var tenantsSlice = text.Substring(tenantsIdx, Math.Min(900, text.Length - tenantsIdx));
        var demoSlice = text.Substring(demoIdx, Math.Min(900, text.Length - demoIdx));
        Assert.Contains("SuperCpHostGate.IsAllowed", tenantsSlice, StringComparison.Ordinal);
        Assert.Contains("SuperCpHostGate.IsAllowed", demoSlice, StringComparison.Ordinal);

        // Web tracker must not promote tenant admins via bos capability.
        Assert.DoesNotContain(
            "Capabilities.Contains(\"bos\") || PlatformHostPolicy.IsSuperCpHost",
            text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProgramTenantContext_UsesPublicDto()
    {
        var text = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Program.cs"));
        var idx = text.IndexOf("TenantContext,", StringComparison.Ordinal);
        Assert.True(idx > 0);
        var slice = text.Substring(idx, Math.Min(500, text.Length - idx));
        Assert.Contains("ToPublicDto()", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("Results.Ok(tenant)", slice, StringComparison.Ordinal);
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
