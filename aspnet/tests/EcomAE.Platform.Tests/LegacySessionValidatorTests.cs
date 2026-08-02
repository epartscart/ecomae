using EcomAE.Platform.Auth;
using EcomAE.Platform.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacySessionValidatorTests
{
    [Fact]
    public async Task AdminCookiesMapToAdminPermissionsWhenDbNotConfigured()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "admin_session=abc; admin_u_id=42";
        var validator = new DbBackedLegacySessionValidator(new MigrationLegacySessionStore());

        var session = await validator.ValidateAsync(context);

        Assert.Equal(LegacySessionKind.Admin, session.Kind);
        Assert.True(session.IsAuthenticated);
        Assert.Contains(EcomAePermissions.SuperCpAccess, session.Permissions);
        Assert.Contains(EcomAePermissions.TenantErpAccess, session.Permissions);
        Assert.True(session.HasBackendAccess);
    }

    [Fact]
    public async Task AdminCookiesRejectedWhenDbSaysMissing()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "admin_session=abc; admin_u_id=42";
        var validator = new DbBackedLegacySessionValidator(new StaticSessionStore(configured: true, exists: false));

        var session = await validator.ValidateAsync(context);

        Assert.Equal(LegacySessionKind.Anonymous, session.Kind);
        Assert.False(session.IsAuthenticated);
    }

    [Fact]
    public async Task AdminCookiesAcceptedWhenDbConfirmsRowAndBackendGroup()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "admin_session=abc; admin_u_id=42";
        var validator = new DbBackedLegacySessionValidator(new StaticSessionStore(configured: true, exists: true, hasBackend: true));

        var session = await validator.ValidateAsync(context);

        Assert.Equal(LegacySessionKind.Admin, session.Kind);
        Assert.Equal(42, session.UserId);
        Assert.Equal("admin@example.com", session.Email);
        Assert.Contains(3, session.Groups);
        Assert.True(session.HasBackendAccess);
    }

    [Fact]
    public async Task AdminCookiesRejectedWhenBackendGroupMissing()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "admin_session=abc; admin_u_id=42";
        var validator = new DbBackedLegacySessionValidator(new StaticSessionStore(configured: true, exists: true, hasBackend: false));

        var session = await validator.ValidateAsync(context);

        Assert.Equal(LegacySessionKind.Anonymous, session.Kind);
    }

    [Fact]
    public async Task CustomerCookiesRejectedWhenDbSaysMissing()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "session=cust; u_id=9";
        var validator = new DbBackedLegacySessionValidator(new StaticSessionStore(configured: true, exists: false));

        var session = await validator.ValidateAsync(context);

        Assert.Equal(LegacySessionKind.Anonymous, session.Kind);
    }

    [Fact]
    public async Task CustomerCookiesAcceptedWhenDbConfirmsRow()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "session=cust; u_id=9";
        var validator = new DbBackedLegacySessionValidator(new StaticSessionStore(configured: true, exists: true));

        var session = await validator.ValidateAsync(context);

        Assert.Equal(LegacySessionKind.Customer, session.Kind);
        Assert.Equal(9, session.UserId);
    }

    [Fact]
    public async Task ApiKeyHeaderMapsToApiPermission()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-API-Key"] = "epc_catalog_test_key";
        var validator = new DbBackedLegacySessionValidator(new MigrationLegacySessionStore());

        var session = await validator.ValidateAsync(context);

        Assert.Equal(LegacySessionKind.ApiKey, session.Kind);
        Assert.True(session.IsAuthenticated);
        Assert.Contains(EcomAePermissions.ApiAccess, session.Permissions);
    }

    [Fact]
    public void LegacySessionSqlIsSelectOnly()
    {
        Assert.Equal("sessions", LegacySessionSql.SourceTable);
        Assert.StartsWith("SELECT", LegacySessionSql.CountAdminSession.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("SELECT", LegacySessionSql.CountCustomerSession.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("SELECT", LegacySessionSql.SelectBackendGroupIds.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("`type` = 1", LegacySessionSql.CountAdminSession, StringComparison.Ordinal);
        Assert.Contains("`for_backend` = 1", LegacySessionSql.SelectBackendGroupIds, StringComparison.Ordinal);
        Assert.DoesNotContain("`type`", LegacySessionSql.CountCustomerSession, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT", LegacySessionSql.CountAdminSession, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", LegacySessionSql.SelectUserGroupIds, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StaticSessionStore : ILegacySessionStore
    {
        private readonly bool _exists;
        private readonly bool _hasBackend;

        public StaticSessionStore(bool configured, bool exists, bool hasBackend = true)
        {
            IsConfigured = configured;
            _exists = exists;
            _hasBackend = hasBackend;
        }

        public bool IsConfigured { get; }

        public Task<bool> AdminSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
            => Task.FromResult(_exists);

        public Task<bool> CustomerSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
            => Task.FromResult(_exists);

        public Task<LegacyAdminIdentity?> GetAdminIdentityAsync(int userId, CancellationToken cancellationToken = default)
            => Task.FromResult<LegacyAdminIdentity?>(
                _exists
                    ? new LegacyAdminIdentity("admin@example.com", [3], _hasBackend)
                    : null);
    }
}
