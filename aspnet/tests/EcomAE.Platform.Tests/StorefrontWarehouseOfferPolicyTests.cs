using EcomAE.Platform.Storefront;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class StorefrontWarehouseOfferPolicyTests
{
    [Fact]
    public void Caption_UsesShortNameForPublicAndFullNameForOfficeManager()
    {
        Assert.Equal("SHJ", StorefrontWarehouseOfferPolicy.Caption(false, "Sharjah Industrial 6 warehouse", "SHJ"));
        Assert.Equal("Sharjah Industrial 6 warehouse", StorefrontWarehouseOfferPolicy.Caption(true, "Sharjah Industrial 6 warehouse", "SHJ"));
        Assert.Equal("Detroit USA consolidator", StorefrontWarehouseOfferPolicy.Caption(false, "Detroit USA consolidator", ""));
    }

    [Fact]
    public void DeliveryDays_AddsOfficeHoursAsWholeDays()
    {
        Assert.Equal(3, StorefrontWarehouseOfferPolicy.DeliveryDays("1", 48));
        Assert.Equal(8, StorefrontWarehouseOfferPolicy.DeliveryDays("5", 72));
        Assert.Equal(0, StorefrontWarehouseOfferPolicy.DeliveryDays("0", 2));
    }

    [Fact]
    public void FormatTerm_MatchesPhpInWarehouseAndDays()
    {
        Assert.Equal("In warehouse", StorefrontWarehouseOfferPolicy.FormatTerm(0, 0, ""));
        Assert.Equal("In warehouse · SHJ", StorefrontWarehouseOfferPolicy.FormatTerm(0, 0, "SHJ"));
        Assert.Equal("3 days.", StorefrontWarehouseOfferPolicy.FormatTerm(3, 3, "SHJ"));
        Assert.Equal("1 day.", StorefrontWarehouseOfferPolicy.FormatTerm(1, 1, "SHJ"));
        Assert.Equal("2-5 days.", StorefrontWarehouseOfferPolicy.FormatTerm(2, 5, "SHJ"));
    }

    [Fact]
    public void PublicStorage_HidesHiddenAndPaused()
    {
        Assert.False(StorefrontWarehouseOfferPolicy.IsPublicStorage(hidden: true, paused: false));
        Assert.False(StorefrontWarehouseOfferPolicy.IsPublicStorage(hidden: false, paused: true));
        Assert.True(StorefrontWarehouseOfferPolicy.IsPublicStorage(hidden: false, paused: false));
        Assert.True(StorefrontWarehouseOfferPolicy.OfficeUsersContain("[\"1\"]", 1));
        Assert.False(StorefrontWarehouseOfferPolicy.OfficeUsersContain("[\"1\"]", 2));
    }
}
