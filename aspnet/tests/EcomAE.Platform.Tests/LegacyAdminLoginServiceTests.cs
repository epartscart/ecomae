using EcomAE.Platform.Auth;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacyAdminLoginServiceTests
{
    [Fact]
    public async Task UnconfiguredServiceForcesPhpFallback()
    {
        var service = new UnconfiguredLegacyAdminLoginService();
        Assert.False(service.IsConfigured);

        var outcome = await service.LoginAsync(
            new LegacyLoginRequest("a@b.c", "x", "email", false, LegacyLoginSurface.ControlPanel),
            "127.0.0.1",
            "test-agent");

        Assert.False(outcome.Ok);
        Assert.Equal("bridge_not_configured", outcome.Failure?.Code);
    }

    [Fact]
    public void LoginSqlInsertsAreExplicitAndSeparateFromReadOnlySessionSql()
    {
        Assert.Contains("INSERT INTO `sessions`", LegacyAdminLoginSql.InsertAdminSession, StringComparison.Ordinal);
        Assert.Contains("`type`", LegacyAdminLoginSql.InsertAdminSession, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `sessions`", LegacyAdminLoginSql.InsertCustomerSession, StringComparison.Ordinal);
        Assert.Contains("`last_activiti_time`", LegacyAdminLoginSql.InsertCustomerSession, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT", LegacySessionSql.CountAdminSession, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", LegacySessionSql.CountAdminSession, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", LegacySessionSql.CountAdminSession, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SurfaceParserMapsKnownKeys()
    {
        Assert.Equal(LegacyLoginSurface.Erp, LegacyLoginSurfaceParser.Parse("ERP"));
        Assert.Equal(LegacyLoginSurface.Bos, LegacyLoginSurfaceParser.Parse("bos"));
        Assert.Equal(LegacyLoginSurface.Storefront, LegacyLoginSurfaceParser.Parse("storefront"));
        Assert.Equal(LegacyLoginSurface.ControlPanel, LegacyLoginSurfaceParser.Parse("nope"));
        Assert.Equal("cp", LegacyLoginSurfaceParser.Key(null));
    }
}
