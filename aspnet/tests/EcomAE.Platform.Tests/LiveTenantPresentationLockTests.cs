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
    public void SummaryKeepsCutoverClosed()
    {
        var summary = LiveTenantPresentationLock.BuildSummary();
        Assert.Equal(false, summary["cutoverAllowed"]);
        Assert.Equal(false, summary["readyForPhpRemoval"]);
        Assert.Equal(5, summary["tenantCount"]);
        Assert.Contains("same-to-same", summary["mandate"]!.ToString()!, StringComparison.OrdinalIgnoreCase);
    }
}
