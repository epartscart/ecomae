using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Regression: uppercase /IP twins and /lifeos/ trailing-slash twins caused AmbiguousMatchException → HTTP 500.
/// </summary>
public sealed class IpLifeOsAmbiguousRouteFixTests
{
    [Fact]
    public void IpPagesDoNotRegisterUppercaseRouteTwins()
    {
        var ipApp = Read("aspnet/src/EcomAE.Platform/Components/Pages/IpApp.razor");
        var ipLogin = Read("aspnet/src/EcomAE.Platform/Components/Pages/IpLoginApp.razor");
        Assert.DoesNotContain("@page \"/IP\"", ipApp, StringComparison.Ordinal);
        Assert.DoesNotContain("@page \"/IP/", ipApp, StringComparison.Ordinal);
        Assert.DoesNotContain("@page \"/IP/login\"", ipLogin, StringComparison.Ordinal);
        Assert.Contains("@page \"/ip\"", ipApp, StringComparison.Ordinal);
        Assert.Contains("@page \"/ip/login\"", ipLogin, StringComparison.Ordinal);
    }

    [Fact]
    public void LifeOsHomeDoesNotRegisterTrailingSlashTwin()
    {
        var home = Read("aspnet/src/EcomAE.Platform/Components/Pages/LifeOsHomeApp.razor");
        Assert.Contains("@page \"/lifeos\"", home, StringComparison.Ordinal);
        Assert.DoesNotContain("@page \"/lifeos/\"", home, StringComparison.Ordinal);
    }

    [Fact]
    public void CpLifeOsGuideAppIsWired()
    {
        var guide = Read("aspnet/src/EcomAE.Platform/Components/Pages/CpLifeOsGuideApp.razor");
        Assert.Contains("@page \"/cp/lifeos-guide-app\"", guide, StringComparison.Ordinal);
        Assert.Contains("LifeOsLinkCatalog", guide, StringComparison.Ordinal);
        Assert.Contains("System architecture", guide, StringComparison.Ordinal);
        Assert.Contains("Frontend links", guide, StringComparison.Ordinal);
        Assert.Contains("Backend links", guide, StringComparison.Ordinal);
        // Must not wrap in tenant EPARTS CP chrome (ERP Operations / STOREFRONT / Commerce menu).
        Assert.DoesNotContain("PhpCpDesktopChrome", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("PhpEpartsCartAnimatedLogo", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("ERP Operations", guide, StringComparison.Ordinal);
        Assert.Contains("ecomae_mark.svg", guide, StringComparison.Ordinal);
        Assert.Contains("LifeOsProductLayout", guide, StringComparison.Ordinal);

        var routes = Read("aspnet/src/EcomAE.Platform/Routing/EcomAeRoutes.cs");
        Assert.Contains("ControlPanelLifeOsGuideApp", routes, StringComparison.Ordinal);
        Assert.Contains("/cp/lifeos-guide-app", routes, StringComparison.Ordinal);

        var nav = Read("aspnet/src/EcomAE.Platform/Presentation/LegacyChromeNavCatalog.cs");
        Assert.Contains("/cp/lifeos-guide-app", nav, StringComparison.Ordinal);
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
