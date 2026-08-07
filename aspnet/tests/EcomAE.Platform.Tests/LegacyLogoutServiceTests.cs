using EcomAE.Platform.Auth;
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
