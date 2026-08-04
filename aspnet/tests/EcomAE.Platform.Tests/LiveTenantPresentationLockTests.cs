using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LiveTenantPresentationLockTests
{
    [Fact]
    public void NamedLiveTenantsAreLocked()
    {
        Assert.Equal(5, LiveTenantPresentationLock.Tenants.Count);
        Assert.Contains(LiveTenantPresentationLock.Tenants, t => t.Id == "epartscart");
        Assert.Contains(LiveTenantPresentationLock.Tenants, t => t.Id == "electronicae");
        Assert.Contains(LiveTenantPresentationLock.Tenants, t => t.Id == "stylenlook");
        Assert.Contains(LiveTenantPresentationLock.Tenants, t => t.Id == "thejewellerytrend");
        Assert.Contains(LiveTenantPresentationLock.Tenants, t => t.Id == "taxofinca");
    }

    [Theory]
    [InlineData("epartscart.com")]
    [InlineData("www.electronicae.com")]
    [InlineData("www.stylenlook.com")]
    [InlineData("www.thejewellerytrend.com")]
    [InlineData("www.taxofinca.com")]
    public void IsLockedHostRecognizesLiveTenants(string host)
    {
        Assert.True(LiveTenantPresentationLock.IsLockedHost(host));
    }

    [Fact]
    public void SummaryKeepsCutoverClosedButTargetsAspNet()
    {
        var summary = LiveTenantPresentationLock.BuildSummary();
        Assert.Equal(false, summary["cutoverAllowed"]);
        Assert.Equal(false, summary["readyForPhpRemoval"]);
        Assert.Equal(5, summary["tenantCount"]);
        Assert.Equal("100%-aspnet-core-0-php", summary["targetEndState"]);
        Assert.Equal("parity-gate-until-aspnet-same-to-same-then-cutover", summary["policy"]);
        Assert.Contains("100% ASP.NET", summary["mandate"]!.ToString()!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("same-to-same", summary["mandate"]!.ToString()!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW", summary["parityShadowConfirmEnv"]);
        Assert.NotEmpty((IReadOnlyList<string>)summary["unlockCriteria"]!);
    }
}
