using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class PremiumEnterpriseShellTests
{
    [Fact]
    public void PremiumCssFileExistsAndCoversCpBosSurfaces()
    {
        var css = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/wwwroot/css/epc-premium-enterprise-shell.css"));
        Assert.Contains("epc-premium-shell", css, StringComparison.Ordinal);
        Assert.Contains(".epc-cp-header-bar", css, StringComparison.Ordinal);
        Assert.Contains(".epc-boc.epc-premium-shell", css, StringComparison.Ordinal);
        Assert.Contains(".bos-body--topnav", css, StringComparison.Ordinal);
        Assert.Contains(".epc-bosapp-hero", css, StringComparison.Ordinal);
        Assert.Contains(".epc-module-visual-frame", css, StringComparison.Ordinal);
        Assert.Contains(".hpanel", css, StringComparison.Ordinal);
    }

    [Fact]
    public void CpAndBosChromeWirePremiumShellClass()
    {
        var cp = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpCpDesktopChrome.razor"));
        var bos = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpBosDesktopChrome.razor"));
        var boc = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Components/Shared/Desktop/PhpSuperCpCommandCentre.razor"));
        Assert.Contains("epc-premium-shell", cp, StringComparison.Ordinal);
        Assert.Contains("epc-premium-shell", bos, StringComparison.Ordinal);
        Assert.Contains("epc-premium-shell", boc, StringComparison.Ordinal);
        Assert.Contains("/cp/super-mail-app", cp, StringComparison.Ordinal);
        Assert.Contains("/bos/mail-app", bos, StringComparison.Ordinal);
        Assert.Contains("/cp/super-mail-app", boc, StringComparison.Ordinal);
    }

    [Fact]
    public void AssetBridgeServesWwwrootCssBecauseStaticFilesAreOff()
    {
        var bridge = File.ReadAllText(Find("aspnet/src/EcomAE.Platform/Presentation/PhpLegacyAssetBridge.cs"));
        Assert.Contains("/css/{fileName}.css", bridge, StringComparison.Ordinal);
        Assert.Contains("ServeWwwrootCss", bridge, StringComparison.Ordinal);
    }

    [Fact]
    public void PremiumCssIsLastOnCpAndBosStylesheetLists()
    {
        Assert.Equal(
            "epc-premium-enterprise-shell.css",
            Path.GetFileName(LegacyPresentationAssets.ControlPanelStylesheets[^1].Split('?', 2)[0]));
        Assert.Equal(
            "epc-premium-enterprise-shell.css",
            Path.GetFileName(LegacyPresentationAssets.BosStylesheets[^1].Split('?', 2)[0]));
    }

    private static string Find(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(relative);
    }
}
