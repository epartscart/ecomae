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
        Assert.Contains("cp", session.Capabilities);
        Assert.Contains("erp", session.Capabilities);
        Assert.Contains("bos", session.Capabilities);
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
    public async Task StaleAdminCookiesFallThroughToCustomerSession()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "admin_session=stale; admin_u_id=42; session=cust; u_id=9";
        var validator = new DbBackedLegacySessionValidator(
            new StaticSessionStore(configured: true, adminExists: false, customerExists: true));

        var session = await validator.ValidateAsync(context);

        Assert.Equal(LegacySessionKind.Customer, session.Kind);
        Assert.Equal(9, session.UserId);
        Assert.True(session.IsAuthenticated);
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
    public async Task ValidateCustomerAsync_PrefersCustomerCookiesWhenAdminAlsoPresent()
    {
        var context = new DefaultHttpContext();
        // Chrome may show signed-in via admin cookies while storefront cart needs customer.
        context.Request.Headers.Cookie = "admin_session=adm; admin_u_id=1; session=cust; u_id=9";
        var validator = new DbBackedLegacySessionValidator(new DualSessionStore());

        var adminFirst = await validator.ValidateAsync(context);
        Assert.Equal(LegacySessionKind.Admin, adminFirst.Kind);

        var customer = await validator.ValidateCustomerAsync(context);
        Assert.Equal(LegacySessionKind.Customer, customer.Kind);
        Assert.Equal(9, customer.UserId);
    }

    [Fact]
    public async Task HttpValidator_ValidateCustomerAsync_IgnoresAdminCookies()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "admin_session=adm; admin_u_id=1; session=cust; u_id=9";
        var validator = new HttpLegacySessionValidator();

        var customer = await validator.ValidateCustomerAsync(context);
        Assert.Equal(LegacySessionKind.Customer, customer.Kind);
        Assert.Equal(9, customer.UserId);
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
        Assert.Contains("modules_access", LegacySessionSql.SelectModuleAccessForGroup, StringComparison.Ordinal);
        Assert.Contains("NOT EXISTS", LegacySessionSql.SelectOpenModules, StringComparison.Ordinal);
        Assert.Contains("`parent`", LegacySessionSql.SelectGroupParent, StringComparison.Ordinal);
        Assert.DoesNotContain("`type`", LegacySessionSql.CountCustomerSession, StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT", LegacySessionSql.CountAdminSession, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", LegacySessionSql.SelectUserGroupIds, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE", LegacySessionSql.SelectOpenModules, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StaticSessionStore : ILegacySessionStore
    {
        private readonly bool _adminExists;
        private readonly bool _customerExists;
        private readonly bool _hasBackend;

        public StaticSessionStore(bool configured, bool exists, bool hasBackend = true)
            : this(configured, adminExists: exists, customerExists: exists, hasBackend)
        {
        }

        public StaticSessionStore(bool configured, bool adminExists, bool customerExists, bool hasBackend = true)
        {
            IsConfigured = configured;
            _adminExists = adminExists;
            _customerExists = customerExists;
            _hasBackend = hasBackend;
        }

        public bool IsConfigured { get; }

        public Task<bool> AdminSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
            => Task.FromResult(_adminExists);

        public Task<bool> CustomerSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
            => Task.FromResult(_customerExists);

        public Task<LegacyAdminIdentity?> GetAdminIdentityAsync(int userId, CancellationToken cancellationToken = default)
            => Task.FromResult<LegacyAdminIdentity?>(
                _adminExists
                    ? new LegacyAdminIdentity("admin@example.com", [3], _hasBackend)
                    : null);
    }

    private sealed class DualSessionStore : ILegacySessionStore
    {
        public bool IsConfigured => true;

        public Task<bool> AdminSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<bool> CustomerSessionExistsAsync(string sessionToken, int userId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<LegacyAdminIdentity?> GetAdminIdentityAsync(int userId, CancellationToken cancellationToken = default)
            => Task.FromResult<LegacyAdminIdentity?>(new LegacyAdminIdentity("admin@example.com", [3], true));
    }
}
