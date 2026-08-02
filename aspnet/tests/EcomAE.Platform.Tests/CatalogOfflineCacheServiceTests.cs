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
    public void EmptyParamsCacheKeyMatchesPhpEmptyArray()
    {
        var key = UmapiCacheKeyBuilder.Build("article", "passenger", "en", "WWW", new Dictionary<string, object?>());
        // sha1("article|passenger|en|WWW|[]")
        Assert.Equal(
            Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(
                System.Text.Encoding.UTF8.GetBytes("article|passenger|en|WWW|[]"))).ToLowerInvariant(),
            key);
    }

    [Fact]
    public async Task LookupArticleBrandsRequiresArticle()
    {
        var service = new CatalogOfflineCacheService(new StaticOfflineRepo());
        var result = await service.LookupArticleBrandsAsync("passenger", " ", "en", "WWW");
        Assert.False(result.Ok);
        Assert.Equal("missing_params", result.Code);
    }

    [Fact]
    public async Task LookupArticleBrandsReturnsCacheHit()
    {
        var cacheKey = UmapiCacheKeyBuilder.Build(
            "brands",
            "passenger",
            "en",
            "WWW",
            new Dictionary<string, object?> { ["article"] = "0986424590" });
        var service = new CatalogOfflineCacheService(new StaticOfflineRepo(
            action: new CatalogActionCacheRow(
                cacheKey,
                "brands",
                "passenger",
                "en",
                "WWW",
                JsonSerializer.Serialize(new { data = new[] { new { brand = "BOSCH" } } }),
                1,
                200,
                1710000000)));

        var result = await service.LookupArticleBrandsAsync("passenger", "0986424590", "en", "WWW");
        Assert.True(result.Ok);
        Assert.Equal("brands", result.Action);
        Assert.Equal(1, result.Rows);
    }

    [Fact]
    public void CategoriesCacheKeyUsesStringIdAndVehicleType()
    {
        var key = UmapiCacheKeyBuilder.Build(
            "categories",
            "passenger",
            "en",
            "WWW",
            new Dictionary<string, object?> { ["ID"] = "10", ["type"] = "PC" });
        Assert.Equal(
            Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(
                System.Text.Encoding.UTF8.GetBytes("categories|passenger|en|WWW|{\"ID\":\"10\",\"type\":\"PC\"}"))).ToLowerInvariant(),
            key);
    }

    [Fact]
    public async Task LookupCategoriesDefaultsTypeFromSection()
    {
        var cacheKey = UmapiCacheKeyBuilder.Build(
            "categories",
            "commercial",
            "en",
            "WWW",
            new Dictionary<string, object?> { ["type"] = "CV" });
        var service = new CatalogOfflineCacheService(new StaticOfflineRepo(
            action: new CatalogActionCacheRow(
                cacheKey,
                "categories",
                "commercial",
                "en",
                "WWW",
                JsonSerializer.Serialize(new { data = new[] { new { name = "Engine" } } }),
                1,
                200,
                1710000000)));

        var result = await service.LookupCategoriesAsync("commercial", null, null, "en", "WWW");
        Assert.True(result.Ok);
        Assert.Equal("categories", result.Action);
    }

    [Fact]
    public async Task LookupProductsReturnsCacheMiss()
    {
        var service = new CatalogOfflineCacheService(new StaticOfflineRepo());
        var result = await service.LookupProductsAsync("passenger", "1", "2", "PC", "en", "WWW");
        Assert.False(result.Ok);
        Assert.Equal("cache_miss", result.Code);
    }

    [Fact]
    public async Task LookupEngineSearchRejectsShortCode()
    {
        var service = new CatalogOfflineCacheService(new StaticOfflineRepo());
        var result = await service.LookupEngineSearchAsync("passenger", "X", 0, "en", "WWW");
        Assert.False(result.Ok);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task LookupEngineSearchUsesPhpParamShape()
    {
        var cacheKey = UmapiCacheKeyBuilder.Build(
            "engine_search",
            "passenger",
            "en",
            "WWW",
            new Dictionary<string, object?> { ["code"] = "3L", ["MFA_ID"] = 0 });
        var service = new CatalogOfflineCacheService(new StaticOfflineRepo(
            action: new CatalogActionCacheRow(
                cacheKey,
                "engine_search",
                "passenger",
                "en",
                "WWW",
                JsonSerializer.Serialize(new { code = "3L", matches = new[] { new { ENGINE_CODE = "3L" } } }),
                1,
                200,
                1710000000)));

        var result = await service.LookupEngineSearchAsync("passenger", "3L", 0, "en", "WWW");
        Assert.True(result.Ok);
        Assert.Equal("engine_search", result.Action);
    }

    [Fact]
    public async Task LookupArticleLinksRequiresId()
    {
        var service = new CatalogOfflineCacheService(new StaticOfflineRepo());
        var result = await service.LookupArticleLinksAsync("passenger", 0, "en", "WWW");
        Assert.False(result.Ok);
        Assert.Equal("missing_params", result.Code);
    }

    [Fact]
    public async Task LookupArticleUsesIdMatchedCacheRow()
    {
        var service = new CatalogOfflineCacheService(new StaticOfflineRepo(
            article: new CatalogActionCacheRow(
                "key",
                "article",
                "passenger",
                "en",
                "WWW",
                JsonSerializer.Serialize(new { ART_ID = 55, TITLE = "Pad" }),
                1,
                200,
                1710000000)));

        var result = await service.LookupArticleAsync("passenger", 55, "en", "WWW");
        Assert.True(result.Ok);
        Assert.Equal("article", result.Action);
    }


    [Fact]
    public async Task LookupArticlesBuildsCacheKeyAndMissesWhenEmpty()
    {
        var service = new CatalogOfflineCacheService(new StaticOfflineRepo());
        var result = await service.LookupArticlesAsync("passenger", null, "1", null, null, null, null, "en", "WWW");
        Assert.False(result.Ok);
        Assert.Equal("articles", result.Action);
        Assert.Equal("cache_miss", result.Code);
    }

    [Fact]
    public async Task LookupEngineUsesIdMatchedCacheRow()
    {
        var service = new CatalogOfflineCacheService(new StaticOfflineRepo(
            engine: new CatalogActionCacheRow(
                "key",
                "engine",
                "passenger",
                "en",
                "WWW",
                JsonSerializer.Serialize(new { ENG_ID = 10, CODE = "3L" }),
                1,
                200,
                1710000000)));

        var result = await service.LookupEngineAsync("passenger", 10, "en", "WWW");
        Assert.True(result.Ok);
        Assert.Equal("engine", result.Action);
    }

    [Fact]
    public void LegacySqlIncludesEngineByIdSelectOnly()
    {
        Assert.StartsWith("SELECT", LegacyUmapiActionCacheSql.SelectEngineById.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", LegacyUmapiActionCacheSql.SelectEngineById, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LegacySqlContractsAreSelectOnly()
    {
        Assert.Equal("epc_umapi_vin_cache", LegacyCatalogVinSql.SourceTable);
        Assert.Equal("epc_umapi_cache", LegacyUmapiActionCacheSql.SourceTable);
        Assert.StartsWith("SELECT", LegacyCatalogVinSql.SelectByVinLanguageRegion.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("SELECT", LegacyUmapiActionCacheSql.SelectByCacheKey.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("SELECT", LegacyUmapiActionCacheSql.SelectArticleById.Trim(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT", LegacyCatalogVinSql.SelectByVinLanguageRegion, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE", LegacyUmapiActionCacheSql.SelectByCacheKey, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StaticOfflineRepo : ICatalogOfflineCacheRepository
    {
        private readonly CatalogVinCacheRow? _vin;
        private readonly CatalogActionCacheRow? _action;
        private readonly CatalogActionCacheRow? _article;
        private readonly CatalogActionCacheRow? _engine;

        public StaticOfflineRepo(
            CatalogVinCacheRow? vin = null,
            CatalogActionCacheRow? action = null,
            CatalogActionCacheRow? article = null,
            CatalogActionCacheRow? engine = null)
        {
            _vin = vin;
            _action = action;
            _article = article;
            _engine = engine;
        }

        public Task<CatalogVinCacheRow?> FindVinAsync(string vin, string language, string region, CancellationToken cancellationToken = default)
            => Task.FromResult(_vin);

        public Task<CatalogActionCacheRow?> FindActionCacheAsync(string cacheKey, CancellationToken cancellationToken = default)
            => Task.FromResult(_action is not null && _action.CacheKey == cacheKey ? _action : null);

        public Task<CatalogActionCacheRow?> FindArticleByIdAsync(string section, string language, string region, int articleId, CancellationToken cancellationToken = default)
            => Task.FromResult(_article);

        public Task<CatalogActionCacheRow?> FindEngineByIdAsync(string section, string language, string region, int engineId, CancellationToken cancellationToken = default)
            => Task.FromResult(_engine);
    }
}
