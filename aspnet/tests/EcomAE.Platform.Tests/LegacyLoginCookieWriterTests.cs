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

    [Fact]
    public void ApplyMarksSecureOnHttps()
    {
        var context = new DefaultHttpContext();
        var success = new LegacyLoginSuccess(7, "a@b.c", "tok-admin", "csrf", true, "/erp");

        LegacyLoginCookieWriter.Apply(context.Response, success, rememberMe: false, secure: true);

        var headers = context.Response.GetTypedHeaders().SetCookie;
        Assert.NotEmpty(headers);
        Assert.All(headers, c => Assert.True(c.Secure));
    }

    [Theory]
    [InlineData("www.ecomae.com", ".ecomae.com")]
    [InlineData("www.epartscart.com", ".epartscart.com")]
    [InlineData("electronicae.com", ".electronicae.com")]
    public void ClearAllOnHttpsExpiresHostAndParentDomainSecureCookies(string host, string parentDomain)
    {
        var context = new DefaultHttpContext();
        LegacyLoginCookieWriter.ClearAll(context.Response, host, https: true);

        var setCookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains("admin_session", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("admin_u_id", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("epc_platform_erp", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("epc_erp_tenant", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(parentDomain, setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RequestIsHttpsHonorsForwardedProto()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-Proto"] = "https";
        Assert.True(LegacyLoginCookieWriter.RequestIsHttps(context.Request));
    }
}
