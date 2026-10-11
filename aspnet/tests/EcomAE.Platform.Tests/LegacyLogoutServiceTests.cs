using EcomAE.Platform.Auth;
using EcomAE.Platform.Data;
using EcomAE.Platform.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacyLogoutServiceTests
{
    [Theory]
    [InlineData(null, null, "/cp/login")]
    [InlineData("cp", null, "/cp/login")]
    [InlineData("erp", null, "/erp/login")]
    [InlineData("bos", null, "/bos/login")]
    [InlineData("ip", null, "/ip/login")]
    [InlineData("lifeos", null, "/lifeos/login")]
    [InlineData("storefront", null, "/storefront/login")]
    [InlineData("cp", "/erp", "/erp")]
    [InlineData("cp", "https://evil.example/", "/cp/login")] // reject absolute open redirect
    [InlineData("cp", "//evil.example", "/cp/login")]
    public void RedirectForSurface_IsSafe(string? surface, string? returnUrl, string expected)
    {
        Assert.Equal(expected, LegacyLogoutService.RedirectForSurface(surface, returnUrl));
    }

    [Fact]
    public void ClearAll_ExpiresAuthCookies()
    {
        var ctx = new DefaultHttpContext();
        LegacyLoginCookieWriter.ClearAll(ctx.Response);

        var setCookie = ctx.Response.Headers.SetCookie.ToString();
        Assert.Contains("admin_session", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("admin_u_id", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("session", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("u_id", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("epc_erp_shell", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LogoutOnHttpsSuperHostClearsSecureCookiesAndForgetsCache()
    {
        var invalidator = new RecordingInvalidator();
        var logout = new LegacyLogoutService(new UnconfiguredFactory(), invalidator);
        var ctx = new DefaultHttpContext();
        ctx.Request.Host = new HostString("www.ecomae.com");
        ctx.Request.Scheme = "https";
        ctx.Request.Headers["X-Forwarded-Proto"] = "https";
        ctx.Request.Headers.Cookie = "admin_session=tok-admin; admin_u_id=7";

        await logout.LogoutAsync(ctx);

        var setCookie = ctx.Response.Headers.SetCookie.ToString();
        Assert.Contains("admin_session", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".ecomae.com", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-store", ctx.Response.Headers.CacheControl.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("www.ecomae.com", invalidator.Host);
        Assert.Equal("tok-admin", invalidator.AdminSession);
        Assert.Equal(7, invalidator.AdminUser);
    }

    [Fact]
    public async Task LogoutOnTenantHostClearsParentDomainCookies()
    {
        var invalidator = new RecordingInvalidator();
        var logout = new LegacyLogoutService(new UnconfiguredFactory(), invalidator);
        var ctx = new DefaultHttpContext();
        ctx.Request.Host = new HostString("www.epartscart.com");
        ctx.Request.Headers["X-Forwarded-Proto"] = "https";
        ctx.Request.Headers.Cookie = "admin_session=tok-tenant; admin_u_id=3; session=cust; u_id=9";

        await logout.LogoutAsync(ctx);

        var setCookie = ctx.Response.Headers.SetCookie.ToString();
        Assert.Contains(".epartscart.com", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("epc_erp_tenant", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("www.epartscart.com", invalidator.Host);
        Assert.Equal("tok-tenant", invalidator.AdminSession);
        Assert.Equal(3, invalidator.AdminUser);
        Assert.Equal("cust", invalidator.CustomerSession);
        Assert.Equal(9, invalidator.CustomerUser);
    }

    [Fact]
    public void ChromeComponentsEmitLogoutAffordances()
    {
        Assert.Contains("/cp/logout", Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor"));
        Assert.Contains("/erp/logout", Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor"));
        Assert.Contains("/bos/logout", Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpBosDesktopChrome.razor"));
        Assert.Contains("/storefront/logout", Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor"));
        Assert.Contains("ILegacySessionValidator", Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor"));
        Assert.Contains("Log out", Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor"));
    }

    [Fact]
    public void DeploymentTemplatesOwnErpLogoutBeforePhpFallback()
    {
        foreach (var relative in new[]
        {
            "deploy/aspnet/nginx-presentation-app-shadow-example.conf",
            "deploy/aspnet/nginx-classic-entry-aspnet-primary-shadow-example.conf",
            "deploy/aspnet/nginx-classic-entry-industry-aspnet-primary-shadow-example.conf",
            "deploy/aspnet/nginx-classic-entry-tenant-aspnet-primary-shadow-example.conf",
            "deploy/aspnet/nginx-surface-entry-previews-example.conf"
        })
        {
            var text = Read(relative);
            Assert.Contains("location = /erp/logout", text, StringComparison.Ordinal);
            Assert.Contains("location = /cp/logout", text, StringComparison.Ordinal);
            Assert.Contains("127.0.0.1:5100", text, StringComparison.Ordinal);
            Assert.Contains("erp-logout", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string Read(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }

    private sealed class RecordingInvalidator : ILegacySessionInvalidator
    {
        public string Host { get; private set; } = "";
        public string AdminSession { get; private set; } = "";
        public int AdminUser { get; private set; }
        public string CustomerSession { get; private set; } = "";
        public int CustomerUser { get; private set; }

        public void Forget(string host, string adminSession, int adminUser, string customerSession, int customerUser)
        {
            Host = host;
            AdminSession = adminSession;
            AdminUser = adminUser;
            CustomerSession = customerSession;
            CustomerUser = customerUser;
        }
    }

    private sealed class UnconfiguredFactory : ITenantDbConnectionFactory
    {
        public bool IsConfigured => false;

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("not configured");

        public Task<System.Data.Common.DbConnection> OpenAsync(string? databaseName, string? userName, string? password, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("not configured");

        public Task<System.Data.Common.DbConnection> OpenForTenantAsync(TenantContext? tenant, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("not configured");

        public Task<System.Data.Common.DbConnection> OpenRegistryAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("not configured");
    }

}
