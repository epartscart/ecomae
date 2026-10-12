using EcomAE.Platform.Configuration;
using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontUsersRegisterOverlayTests
{
    [Fact]
    public void OverlaySetsContinueFlagWhenAspNetIsProductPrimary()
    {
        var config = new Dictionary<string, string>(StringComparer.Ordinal) { ["site_name"] = "Parts" };
        var overlay = StorefrontUsersRegisterEndpoints.OverlayAspNetPrimaryRegisterConfig(
            config,
            new PhpReferenceOptions { PreferAspNetStorefrontApps = true });
        Assert.Equal("1", overlay["registration_continue_if_confirm_email_fails"]);
        Assert.Equal("Parts", overlay["site_name"]);
        Assert.False(config.ContainsKey("registration_continue_if_confirm_email_fails"));
    }

    [Fact]
    public void OverlaySetsContinueFlagWhenPhpServingIsPaused()
    {
        var overlay = StorefrontUsersRegisterEndpoints.OverlayAspNetPrimaryRegisterConfig(
            new Dictionary<string, string>(StringComparer.Ordinal),
            new PhpReferenceOptions { PreferAspNetStorefrontApps = false, TemporarilyDeactivatePhpServing = true });
        Assert.Equal("1", overlay["registration_continue_if_confirm_email_fails"]);
    }

    [Fact]
    public void OverlayLeavesPhpGoldensAloneWhenNeitherFlagIsOn()
    {
        var config = new Dictionary<string, string>(StringComparer.Ordinal);
        var overlay = StorefrontUsersRegisterEndpoints.OverlayAspNetPrimaryRegisterConfig(
            config,
            new PhpReferenceOptions { PreferAspNetStorefrontApps = false, TemporarilyDeactivatePhpServing = false });
        Assert.Same(config, overlay);
        Assert.False(overlay.ContainsKey("registration_continue_if_confirm_email_fails"));
    }
}
