using System.Text.Json;
using EcomAE.Platform.Api.Catalog;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CatalogOfflineCacheServiceTests
{
    [Fact]
    public void NormalizeVinStripsNonAlnumAndUppercases()
    {
        Assert.Equal("WBAXG1103CDW29096", UmapiCacheKeyBuilder.NormalizeVin("wba xg1103-cdw29096"));
        Assert.Equal(string.Empty, UmapiCacheKeyBuilder.NormalizeVin("   "));
    }

    [Fact]
    public void CacheKeyMatchesPhpSha1MaterialShape()
    {
        var key = UmapiCacheKeyBuilder.Build(
            "engines",
            "passenger",
            "en",
            "WWW",
            new Dictionary<string, object?> { ["MFA_ID"] = 10 });

        Assert.Equal(40, key.Length);
        Assert.Matches("^[a-f0-9]{40}$", key);

        // sha1("engines|passenger|en|WWW|{\"MFA_ID\":10}") — matches PHP epc_cache_key
        Assert.Equal("7b7ad71e2d5d20ae671d37b206971076a7a45cd5", key);
    }

    [Fact]
    public async Task LookupVinRejectsShortVin()
    {
        var service = new CatalogOfflineCacheService(new StaticOfflineRepo());
        var result = await service.LookupVinAsync("SHORT", "en", "WWW");
        Assert.False(result.Ok);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("invalid_vin", result.Code);
    }

    [Fact]
    public async Task LookupVinReturnsCacheHitPayload()
    {
        var payload = JsonSerializer.Serialize(new { data = new { matchingVehicles = new[] { new { carId = 1 } } } });
        var service = new CatalogOfflineCacheService(new StaticOfflineRepo(
            vin: new CatalogVinCacheRow(
                "WBAXG1103CDW29096",
                "en",
                "WWW",
                payload,
                1,
                "BMW",
                "3 Series",
                200,
                1710000000)));

        var result = await service.LookupVinAsync("WBAXG1103CDW29096", "en", "WWW");
        Assert.True(result.Ok);
        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Payload);
    }

    [Fact]
    public async Task LookupVinReturnsCacheMiss()
    {
        var service = new CatalogOfflineCacheService(new StaticOfflineRepo());
        var result = await service.LookupVinAsync("WBAXG1103CDW29096", "en", "WWW");
        Assert.False(result.Ok);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("vin_cache_miss", result.Code);
    }

    [Fact]
    public async Task LookupEnginesRequiresMfaId()
    {
        var service = new CatalogOfflineCacheService(new StaticOfflineRepo());
        var result = await service.LookupEnginesAsync("passenger", 0, "en", "WWW");
        Assert.False(result.Ok);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("missing_params", result.Code);
    }

    [Fact]
    public async Task LookupEnginesReturnsCacheHit()
    {
        var cacheKey = UmapiCacheKeyBuilder.Build(
            "engines",
            "passenger",
            "en",
            "WWW",
            new Dictionary<string, object?> { ["MFA_ID"] = 10 });
        var service = new CatalogOfflineCacheService(new StaticOfflineRepo(
            action: new CatalogActionCacheRow(
                cacheKey,
                "engines",
                "passenger",
                "en",
                "WWW",
                JsonSerializer.Serialize(new { data = new[] { new { ENG_CODE = "N47" } } }),
                1,
                200,
                1710000000)));

        var result = await service.LookupEnginesAsync("passenger", 10, "en", "WWW");
        Assert.True(result.Ok);
        Assert.Equal("database", result.Source);
        Assert.Equal(1, result.Rows);
    }

    [Fact]
    public async Task LookupAnalogsRequiresArticleAndBrand()
    {
        var service = new CatalogOfflineCacheService(new StaticOfflineRepo());
        var result = await service.LookupAnalogsAsync("passenger", "", "BOSCH", "en", "WWW");
        Assert.False(result.Ok);
        Assert.Equal("missing_params", result.Code);
    }

    [Fact]
    public async Task LookupAnalogsReturnsCacheMiss()
    {
        var service = new CatalogOfflineCacheService(new StaticOfflineRepo());
        var result = await service.LookupAnalogsAsync("passenger", "0986424590", "BOSCH", "en", "WWW");
        Assert.False(result.Ok);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("cache_miss", result.Code);
    }

    [Fact]
    public void LegacySqlContractsAreSelectOnly()
    {
        Assert.Equal("epc_umapi_vin_cache", LegacyCatalogVinSql.SourceTable);
        Assert.Equal("epc_umapi_cache", LegacyUmapiActionCacheSql.SourceTable);
        Assert.StartsWith("SELECT", LegacyCatalogVinSql.SelectByVinLanguageRegion.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("SELECT", LegacyUmapiActionCacheSql.SelectByCacheKey.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", LegacyCatalogVinSql.SelectByVinLanguageRegion, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", LegacyUmapiActionCacheSql.SelectByCacheKey, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StaticOfflineRepo : ICatalogOfflineCacheRepository
    {
        private readonly CatalogVinCacheRow? _vin;
        private readonly CatalogActionCacheRow? _action;

        public StaticOfflineRepo(CatalogVinCacheRow? vin = null, CatalogActionCacheRow? action = null)
        {
            _vin = vin;
            _action = action;
        }

        public Task<CatalogVinCacheRow?> FindVinAsync(string vin, string language, string region, CancellationToken cancellationToken = default)
            => Task.FromResult(_vin);

        public Task<CatalogActionCacheRow?> FindActionCacheAsync(string cacheKey, CancellationToken cancellationToken = default)
            => Task.FromResult(_action is not null && _action.CacheKey == cacheKey ? _action : null);
    }
}
