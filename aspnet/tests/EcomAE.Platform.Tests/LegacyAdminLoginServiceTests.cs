using EcomAE.Platform.Auth;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Data;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
    public async Task TenantCpLoginFailsFastWhenShopDbUnbound()
    {
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        http.HttpContext.Request.Host = new HostString("www.epartscart.com");
        http.HttpContext.Items[TenantResolutionMiddleware.HttpContextItemKey] = new TenantContext(
            Host: "www.epartscart.com",
            Path: "/cp/login",
            Surface: TenantSurface.ControlPanel,
            Mode: TenantMode.LiveTenant,
            SiteKey: "epartscart",
            DatabaseName: null);

        var options = Options.Create(new EcomAeOptions { SecretSuccession = "test-secret" });
        var service = new DbLegacyAdminLoginService(
            new AlwaysConfiguredConnections(),
            new NoopSessionStore(),
            options,
            http,
            NullLogger<DbLegacyAdminLoginService>.Instance);

        var outcome = await service.LoginAsync(
            new LegacyLoginRequest("taxofin2025@gmail.com", "any", "email", false, LegacyLoginSurface.ControlPanel),
            "127.0.0.1",
            "test-agent");

        Assert.False(outcome.Ok);
        Assert.Equal("tenant_db_unbound", outcome.Failure?.Code);
    }

    [Fact]
    public async Task SuperCpHostAllowsRegistryFallbackWhenShopDbMissing()
    {
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        http.HttpContext.Request.Host = new HostString("www.ecomae.com");
        // No tenant item / no db_name — Super-CP must not hard-fail as tenant_db_unbound.
        var options = Options.Create(new EcomAeOptions { SecretSuccession = "test-secret" });
        var connections = new CapturingConnections();
        var service = new DbLegacyAdminLoginService(
            connections,
            new NoopSessionStore(),
            options,
            http,
            NullLogger<DbLegacyAdminLoginService>.Instance);

        var outcome = await service.LoginAsync(
            new LegacyLoginRequest("ops@ecomae.com", "x", "email", false, LegacyLoginSurface.ControlPanel),
            "127.0.0.1",
            "test-agent");

        Assert.NotEqual("tenant_db_unbound", outcome.Failure?.Code);
        Assert.True(connections.OpenForTenantCalled);
    }

    private sealed class AlwaysConfiguredConnections : ITenantDbConnectionFactory
    {
        public bool IsConfigured => true;

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("should not open when unbound");

        public Task<System.Data.Common.DbConnection> OpenAsync(
            string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("should not open when unbound");

        public Task<System.Data.Common.DbConnection> OpenForTenantAsync(
            TenantContext? tenant, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("should not open when unbound");

        public Task<System.Data.Common.DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("should not open when unbound");
    }

    private sealed class CapturingConnections : ITenantDbConnectionFactory
    {
        public bool OpenForTenantCalled { get; private set; }
        public bool IsConfigured => true;

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("capture-only");

        public Task<System.Data.Common.DbConnection> OpenAsync(
            string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("capture-only");

        public Task<System.Data.Common.DbConnection> OpenForTenantAsync(
            TenantContext? tenant, CancellationToken cancellationToken = default)
        {
            OpenForTenantCalled = true;
            throw new InvalidOperationException("expected-open-then-fail");
        }

        public Task<System.Data.Common.DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("capture-only");
    }

    private sealed class NoopSessionStore : ILegacySessionStore
    {
        public bool IsConfigured => true;

        public Task<bool> AdminSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<bool> CustomerSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<LegacyAdminIdentity?> GetAdminIdentityAsync(int userId, CancellationToken cancellationToken = default)
            => Task.FromResult<LegacyAdminIdentity?>(null);
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
    public void EmailLookupIsCaseInsensitiveAndProbeOmitsConfirmedFilter()
    {
        Assert.Contains("LOWER(`email`) = LOWER(@contact)", LegacyAdminLoginSql.SelectUserByEmail, StringComparison.Ordinal);
        Assert.Contains("`email_confirmed` = 1", LegacyAdminLoginSql.SelectUserByEmail, StringComparison.Ordinal);
        Assert.Contains("`unlocked` = 1", LegacyAdminLoginSql.SelectUserByEmail, StringComparison.Ordinal);
        Assert.Contains("LOWER(`email`) = LOWER(@contact)", LegacyAdminLoginSql.SelectUserProbeByEmail, StringComparison.Ordinal);
        Assert.DoesNotContain("`email_confirmed` = 1", LegacyAdminLoginSql.SelectUserProbeByEmail, StringComparison.Ordinal);
        Assert.Contains("email_confirmed", LegacyAdminLoginSql.SelectUserProbeByEmail, StringComparison.Ordinal);
        Assert.Contains("unlocked", LegacyAdminLoginSql.SelectUserProbeByEmail, StringComparison.Ordinal);
    }

    [Fact]
    public void LoginErrorHelperSurfacesTenantDbUnboundAndWrongHostHint()
    {
        var unbound = LoginErrorHelper.FromUri("https://www.epartscart.com/cp/login?error=tenant_db_unbound");
        Assert.NotNull(unbound);
        Assert.Contains("Shop database is not bound", unbound, StringComparison.OrdinalIgnoreCase);

        var wrong = LoginErrorHelper.FromUri("https://www.epartscart.com/cp/login?error=invalid_credentials");
        Assert.NotNull(wrong);
        Assert.Contains("taxofinca", wrong, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SecretSuccession", wrong, StringComparison.Ordinal);
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
