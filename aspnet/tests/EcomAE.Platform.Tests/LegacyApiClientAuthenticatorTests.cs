using EcomAE.Platform.Auth;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LegacyApiClientAuthenticatorTests
{
    private static readonly TimeProvider FixedTime = new FixedTimeProvider(new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task RequireAsyncRejectsMissingApiKey()
    {
        var authenticator = CreateAuthenticator(CreatePriceProClient());
        var result = await authenticator.RequireAsync(new DefaultHttpContext().Request, "price_pro", "lookup");

        Assert.False(result.Succeeded);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal("missing_api_key", result.Code);
    }

    [Fact]
    public async Task RequireAsyncRejectsInvalidKeyFormat()
    {
        var authenticator = CreateAuthenticator(CreatePriceProClient());
        var request = new DefaultHttpContext().Request;
        request.Headers["X-API-Key"] = "epc_test_key";

        var result = await authenticator.RequireAsync(request, "price_pro", "lookup");

        Assert.False(result.Succeeded);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal("invalid_key_format", result.Code);
    }

    [Fact]
    public async Task RequireAsyncRejectsUnknownHash()
    {
        var authenticator = CreateAuthenticator(CreatePriceProClient());
        var request = new DefaultHttpContext().Request;
        request.Headers["X-API-Key"] = "epc_pricepro_unknownkey01";

        var result = await authenticator.RequireAsync(request, "price_pro", "lookup");

        Assert.False(result.Succeeded);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal("invalid_api_key", result.Code);
    }

    [Fact]
    public async Task RequireAsyncRejectsWrongProductKey()
    {
        var catalogKey = "epc_catalog_abc123def456";
        var catalog = CreateClient(catalogKey, "catalog");
        var authenticator = CreateAuthenticator(catalog);
        var request = new DefaultHttpContext().Request;
        request.Headers["X-API-Key"] = catalogKey;

        var result = await authenticator.RequireAsync(request, "price_pro", "lookup");

        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("wrong_product_key", result.Code);
    }

    [Fact]
    public async Task RequireAsyncRejectsDisallowedAction()
    {
        var key = "epc_pricepro_actiondeny01";
        var client = CreateClient(key, "price_pro", allowedActionsJson: "[\"status\"]");
        var authenticator = CreateAuthenticator(client);
        var request = new DefaultHttpContext().Request;
        request.Headers["X-API-Key"] = key;

        var result = await authenticator.RequireAsync(request, "price_pro", "lookup");

        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal("action_not_allowed", result.Code);
    }

    [Fact]
    public async Task RequireAsyncRejectsDailyQuotaExceededAndLogsUsage()
    {
        var key = "epc_pricepro_quota000001";
        var client = CreateClient(key, "price_pro", dailyLimit: 1, callsToday: 1, callsResetDate: new DateOnly(2026, 8, 2));
        var usage = new MigrationLegacyApiUsageLogger();
        var authenticator = new LegacyApiClientAuthenticator(new InMemoryLegacyApiClientStore(client), usage, FixedTime);
        var request = new DefaultHttpContext().Request;
        request.Headers["X-API-Key"] = key;

        var result = await authenticator.RequireAsync(request, "price_pro", "lookup");

        Assert.False(result.Succeeded);
        Assert.Equal(429, result.StatusCode);
        Assert.Equal("daily_quota_exceeded", result.Code);
        Assert.Single(usage.Entries);
        Assert.True(usage.Entries[0].QuotaBlocked);
    }

    [Fact]
    public async Task RequireAsyncAcceptsValidPriceProKeyAndConsumesQuota()
    {
        var key = "epc_pricepro_valid000001";
        var client = CreateClient(key, "price_pro", dailyLimit: 5, callsToday: 1, callsResetDate: new DateOnly(2026, 8, 2));
        var store = new InMemoryLegacyApiClientStore(client);
        var authenticator = new LegacyApiClientAuthenticator(store, new MigrationLegacyApiUsageLogger(), FixedTime);
        var request = new DefaultHttpContext().Request;
        request.Headers.Authorization = "Bearer " + key;

        var result = await authenticator.RequireAsync(request, "price_pro", "lookup");

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Client);
        Assert.Equal("price_pro", result.KeyProduct);
        var updated = await store.FindActiveByHashAsync(client.ClientKeyHash);
        Assert.Equal(2, updated!.CallsToday);
    }

    [Fact]
    public async Task RequireAsyncReturns503WhenStoreNotConfigured()
    {
        var authenticator = new LegacyApiClientAuthenticator(
            new InMemoryLegacyApiClientStore { IsConfigured = false },
            new MigrationLegacyApiUsageLogger(),
            FixedTime);

        var result = await authenticator.RequireAsync(new DefaultHttpContext().Request, "price_pro", "lookup");

        Assert.Equal(503, result.StatusCode);
        Assert.Equal("platform_db_unavailable", result.Code);
    }

    private static LegacyApiClientAuthenticator CreateAuthenticator(params LegacyApiClientRecord[] clients)
    {
        return new LegacyApiClientAuthenticator(
            new InMemoryLegacyApiClientStore(clients),
            new MigrationLegacyApiUsageLogger(),
            FixedTime);
    }

    private static LegacyApiClientRecord CreatePriceProClient()
    {
        return CreateClient("epc_pricepro_seed0000001", "price_pro");
    }

    private static LegacyApiClientRecord CreateClient(
        string rawKey,
        string product,
        int dailyLimit = 1000,
        int callsToday = 0,
        DateOnly? callsResetDate = null,
        string allowedActionsJson = "*")
    {
        var parsed = LegacyApiClientKeyParser.Parse(rawKey) ?? throw new InvalidOperationException(rawKey);
        return new LegacyApiClientRecord(
            Id: 7,
            ClientKeyHash: parsed.Sha256Hash,
            ClientKeyPrefix: parsed.Prefix,
            Product: product,
            Label: "test-client",
            Active: true,
            DailyLimit: dailyLimit,
            CallsToday: callsToday,
            CallsResetDate: callsResetDate,
            AllowedActionsJson: allowedActionsJson);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
