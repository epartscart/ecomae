using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class IpOsDesktopShellTests
{
    [Fact]
    public void IpAppIsOperatingSystemDesktopNotBosChrome()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Pages/IpApp.razor");
        Assert.Contains("Intelligence Platform OS", text, StringComparison.Ordinal);
        Assert.Contains("App management system", text, StringComparison.Ordinal);
        Assert.Contains("Multiple client management", text, StringComparison.Ordinal);
        Assert.Contains("LifeOS™ operating system", text, StringComparison.Ordinal);
        Assert.Contains("ipos-desktop", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpBosDesktopChrome", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-ip-hub", text, StringComparison.Ordinal);
    }

    [Fact]
    public void IpLoginIsOsBootNotBosLoginShell()
    {
        var text = Read("aspnet/src/EcomAE.Platform/Components/Pages/IpLoginApp.razor");
        Assert.Contains("Intelligence Platform OS", text, StringComparison.Ordinal);
        Assert.Contains("ipos-login", text, StringComparison.Ordinal);
        Assert.Contains("Enter Intelligence Platform OS", text, StringComparison.Ordinal);
        Assert.DoesNotContain("epc-login-bos", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpBosLoginAtmosphere", text, StringComparison.Ordinal);
        Assert.DoesNotContain("bos-login__role-tab", text, StringComparison.Ordinal);
    }

    [Fact]
    public void LifeOsNginxHostExampleDoesNotProxyHomeToMarketing()
    {
        var text = Read("deploy/aspnet/nginx-lifeos-host-aspnet-primary-example.conf");
        Assert.Contains("lifeos-host-home", text, StringComparison.Ordinal);
        Assert.Contains("proxy_pass http://127.0.0.1:5100/;", text, StringComparison.Ordinal);
        Assert.DoesNotContain("proxy_pass http://127.0.0.1:5100/marketing/app", text, StringComparison.Ordinal);
    }

    private static string Read(string relative)
    {
        var root = FindRepoRoot();
        return File.ReadAllText(Path.Combine(root, relative));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
