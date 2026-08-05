using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacyPresentationAssetsLookParityTests
{
    [Fact]
    public void CpBodyClassMatchesPhpDesktopShellTokens()
    {
        var body = LegacyPresentationAssets.BodyClassFor("cp");
        Assert.Contains("epc-cp-shell", body);
        Assert.Contains("epc-cp-topnav-only", body);
        Assert.Contains("epc-cp--blue-theme", body);
        Assert.Contains("epc-cp-modern", body);
        Assert.Contains("fixed-navbar", body);
    }

    [Fact]
    public void BosAndErpBodyClassesMatchPhpShellTokens()
    {
        Assert.Contains("bos-body--topnav", LegacyPresentationAssets.BodyClassFor("bos"));
        Assert.Contains("epc-erp-standalone", LegacyPresentationAssets.BodyClassFor("erp"));
        Assert.Contains("bos-body--login", LegacyPresentationAssets.LoginBodyClassFor("bos"));
        Assert.Contains("epc-cp--blue-theme", LegacyPresentationAssets.LoginBodyClassFor("cp"));
        Assert.DoesNotContain("fixed-sidebar", LegacyPresentationAssets.LoginBodyClassFor("cp"));
    }

    [Fact]
    public void CommandDashboardCssIsInCpStylesheetList()
    {
        Assert.Contains(
            LegacyPresentationAssets.ControlPanelStylesheets,
            href => href.Contains("epc_cp_command_dashboard", StringComparison.OrdinalIgnoreCase));
    }
}
