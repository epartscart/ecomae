using EcomAE.Platform.Auth;
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
    public void ChromeComponentsEmitLogoutAffordances()
    {
        Assert.Contains("/cp/logout", Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor"));
        Assert.Contains("/erp/logout", Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor"));
        Assert.Contains("/bos/logout", Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpBosDesktopChrome.razor"));
        Assert.Contains("/storefront/logout", Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpStorefrontDesktopChrome.razor"));
        Assert.Contains("ILegacySessionValidator", Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor"));
        Assert.Contains("Log out", Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor"));
        Assert.Contains("data-enhance-nav=\"false\"", Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor"));
        Assert.Contains("LogoutHref", Read("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpErpDesktopChrome.razor"));
    }

    [Fact]
    public void RedirectAfterLogout_UsesClientErpLoginWhenBound()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Host = new HostString("www.ecomae.com");
        ctx.Items[ClientErpPath.HttpContextItemKey] = "indj";
        Assert.Equal("/cp/client-erp/indj/", LegacyLogoutService.RedirectAfterLogout(ctx, "erp", null));
    }

    [Fact]
    public void NginxLogoutSnippetDoesNotEscapeHostVariables()
    {
        var text = Read("scripts/cloudpanel_STOP_PRODUCT_PHP_NOW.sh");
        var start = text.IndexOf("location = /erp/logout", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var chunk = text.Substring(start, 420);
        Assert.Contains("proxy_set_header Host $host;", chunk, StringComparison.Ordinal);
        Assert.DoesNotContain(@"\$host", chunk, StringComparison.Ordinal);
        Assert.Contains("cloudpanel_repair_erp_logout_nginx.sh", Read("scripts/cloudpanel_deploy_main_gated.sh"), StringComparison.Ordinal);
    }

    [Fact]
    public void LogoutRoutesDisableAntiforgerySoPostDoesNot400()
    {
        var program = Read("aspnet/src/EcomAE.Platform/Program.cs");
        foreach (var route in new[]
                 {
                     "EcomAeRoutes.LegacyLogout",
                     "EcomAeRoutes.ControlPanelLogout",
                     "EcomAeRoutes.ErpLogout",
                     "EcomAeRoutes.BosLogout",
                     "EcomAeRoutes.StorefrontLogout",
                 })
        {
            Assert.Contains(route, program, StringComparison.Ordinal);
        }

        Assert.Contains("=> PerformLegacyLogout(context, logout, \"erp\", null)).DisableAntiforgery();", program, StringComparison.Ordinal);
        Assert.Contains("=> PerformLegacyLogout(context, logout, \"cp\", null)).DisableAntiforgery();", program, StringComparison.Ordinal);
        Assert.Contains("}).DisableAntiforgery();", program, StringComparison.Ordinal);
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

}
