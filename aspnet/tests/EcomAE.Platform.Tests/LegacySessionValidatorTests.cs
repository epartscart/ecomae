using EcomAE.Platform.Auth;
using EcomAE.Platform.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacySessionValidatorTests
{
    [Fact]
    public async Task AdminCookiesMapToAdminPermissions()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "admin_session=abc; admin_u_id=42";
        var validator = new HttpLegacySessionValidator();

        var session = await validator.ValidateAsync(context);

        Assert.Equal(LegacySessionKind.Admin, session.Kind);
        Assert.True(session.IsAuthenticated);
        Assert.Contains(EcomAePermissions.SuperCpAccess, session.Permissions);
        Assert.Contains(EcomAePermissions.TenantErpAccess, session.Permissions);
    }

    [Fact]
    public async Task ApiKeyHeaderMapsToApiPermission()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-API-Key"] = "epc_catalog_test_key";
        var validator = new HttpLegacySessionValidator();

        var session = await validator.ValidateAsync(context);

        Assert.Equal(LegacySessionKind.ApiKey, session.Kind);
        Assert.True(session.IsAuthenticated);
        Assert.Contains(EcomAePermissions.ApiAccess, session.Permissions);
    }
}
