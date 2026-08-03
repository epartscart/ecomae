using EcomAE.Platform.Auth;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacyLoginCookieWriterTests
{
    [Fact]
    public void ApplyWritesAdminCookiesWithoutExpiresWhenNotRemembered()
    {
        var context = new DefaultHttpContext();
        var success = new LegacyLoginSuccess(7, "a@b.c", "tok-admin", "csrf", true, "/cp/app");

        LegacyLoginCookieWriter.Apply(context.Response, success, rememberMe: false);

        var headers = context.Response.GetTypedHeaders().SetCookie;
        Assert.Contains(headers, c => c.Name == "admin_session" && c.Value == "tok-admin");
        Assert.Contains(headers, c => c.Name == "admin_u_id" && c.Value == "7");
        Assert.DoesNotContain(headers, c => c.Name == "session");
        Assert.DoesNotContain(headers, c => c.Expires.HasValue);
        Assert.All(headers, c => Assert.True(c.HttpOnly));
        Assert.All(headers, c => Assert.Equal("/", c.Path.ToString()));
    }

    [Fact]
    public void ApplyWritesCustomerCookiesAndRememberExpiry()
    {
        var context = new DefaultHttpContext();
        var success = new LegacyLoginSuccess(9, "c@d.e", "tok-cust", "csrf", false, "/storefront/app");

        LegacyLoginCookieWriter.Apply(context.Response, success, rememberMe: true);

        var headers = context.Response.GetTypedHeaders().SetCookie;
        Assert.Contains(headers, c => c.Name == "session" && c.Value == "tok-cust");
        Assert.Contains(headers, c => c.Name == "u_id" && c.Value == "9");
        Assert.DoesNotContain(headers, c => c.Name == "admin_session");
        Assert.Contains(headers, c => c.Expires.HasValue);
    }
}
