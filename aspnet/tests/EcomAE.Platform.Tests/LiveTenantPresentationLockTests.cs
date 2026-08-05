using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LiveTenantPresentationLockTests
{
    [Fact]
    public void NamedLiveTenantsAreCatalogued()
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
        Assert.True(LiveTenantPresentationLock.IsProductTenantHost(host));
    }

    [Fact]
    public void SummaryIsAspNetPrimaryForAllProductTenants()
    {
        var summary = LiveTenantPresentationLock.BuildSummary();
        Assert.Equal(false, summary["cutoverAllowed"]);
        Assert.Equal(false, summary["readyForPhpRemoval"]);
        Assert.Equal(false, summary["phpPrimaryUntilParity"]);
        Assert.Equal("aspnet", summary["stackToday"]);
        Assert.Equal(5, summary["tenantCount"]);
        Assert.Equal("100%-aspnet-core-live-php-reference-kept", summary["targetEndState"]);
        Assert.Equal("aspnet-primary-all-product-tenants-php-reference-only", summary["policy"]);
        Assert.Contains("ASP.NET Core", summary["mandate"]!.ToString()!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/php-reference", summary["mandate"]!.ToString()!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("ECOMAE_CONFIRM_LIVE_TENANT_ASPNET_PARITY_SHADOW", summary["parityShadowConfirmEnv"]);
        Assert.Contains("epartscart|electronicae|stylenlook|thejewellerytrend|taxofinca",
            summary["nginxProductHostRegex"]!.ToString()!, StringComparison.Ordinal);
        Assert.NotEmpty((IReadOnlyList<string>)summary["unlockCriteria"]!);

        var tenants = (Array)summary["tenants"]!;
        Assert.Equal(5, tenants.Length);
        foreach (var raw in tenants)
        {
            var row = (IReadOnlyDictionary<string, object>)raw!;
            Assert.Equal("aspnet", row["stackToday"]);
            Assert.Contains("/php-reference", row["phpAccess"]!.ToString()!, StringComparison.Ordinal);
            Assert.Equal("super-cp-only-404-on-tenant", row["bos"]);
        }
    }
}
