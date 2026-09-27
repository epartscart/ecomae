using EcomAE.Platform.Cp;
using EcomAE.Platform.Services;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpTenantDashboardStatsTests
{
    [Fact]
    public void Empty_stats_are_zero_safe_with_seven_dynamic_days()
    {
        var stats = CpDashboardStats.Empty(new DateTime(2026, 3, 3));

        Assert.Equal(0, stats.OrdersToday);
        Assert.Equal(0, stats.Clients);
        Assert.Equal(7, stats.DayLabels.Count);
        Assert.Equal(7, stats.DayCounts.Count);
        Assert.All(stats.DayCounts, c => Assert.Equal(0, c));
        Assert.Equal("Wed 25", stats.DayLabels[0]);
        Assert.Equal("Tue 3", stats.DayLabels[6]);
    }

    [Theory]
    [InlineData("shop.example.com", "docpart", null, true)]
    [InlineData("shop.example.com", "tenant_shop", null, false)]
    [InlineData("www.epartscart.com", "docpart", "epartscart", false)]
    [InlineData("cp.ecomae.com", "docpart", null, false)]
    public void Containment_matches_php_tenant_data_guard(string host, string database, string? siteKey, bool contained)
    {
        var tenant = new TenantContext(
            host,
            "/cp",
            TenantSurface.ControlPanel,
            TenantMode.LiveTenant,
            siteKey,
            database);

        Assert.Equal(contained, TenantDataGuard.IsContained(tenant));
    }
}
