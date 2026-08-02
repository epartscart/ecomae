using EcomAE.Platform.Auth;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacyApiClientPolicyTests
{
    [Fact]
    public void ProductAllowedMatchesPhpBothSemantics()
    {
        var both = Client(product: "both");
        var catalog = Client(product: "catalog");

        Assert.True(LegacyApiClientPolicy.ProductAllowed(both, "price_pro"));
        Assert.True(LegacyApiClientPolicy.ProductAllowed(catalog, "catalog"));
        Assert.False(LegacyApiClientPolicy.ProductAllowed(catalog, "price_pro"));
    }

    [Theory]
    [InlineData("[\"manufacturers\",\"vin\"]", "vin", true)]
    [InlineData("manufacturers, models vin", "models", true)]
    [InlineData("manufacturers", "price_lookup", false)]
    [InlineData("*", "anything", true)]
    public void ActionAllowedMatchesPhpActionListSemantics(string allowedActions, string action, bool expected)
    {
        Assert.Equal(expected, LegacyApiClientPolicy.ActionAllowed(Client(allowedActionsJson: allowedActions), action));
    }

    [Fact]
    public void QuotaAvailableRequiresCallsBelowLimit()
    {
        Assert.True(LegacyApiClientPolicy.QuotaAvailable(Client(dailyLimit: 10, callsToday: 9)));
        Assert.False(LegacyApiClientPolicy.QuotaAvailable(Client(dailyLimit: 10, callsToday: 10)));
    }

    private static LegacyApiClientRecord Client(string product = "catalog", string allowedActionsJson = "*", int dailyLimit = 1000, int callsToday = 0) => new(
        1,
        new string('a', 64),
        "epc_catalog_sample",
        product,
        "Test client",
        true,
        dailyLimit,
        callsToday,
        DateOnly.FromDateTime(DateTime.UtcNow),
        allowedActionsJson);
}
