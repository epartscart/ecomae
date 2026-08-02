using System.Text.Json;

namespace EcomAE.Platform.Api.Catalog;

public sealed class CatalogOfflineCacheService : ICatalogOfflineCacheService
{
    private readonly ICatalogOfflineCacheRepository _repository;

    public CatalogOfflineCacheService(ICatalogOfflineCacheRepository repository)
    {
        _repository = repository;
    }

    public async Task<CatalogVinLookupResult> LookupVinAsync(string? vin, string? language, string? region, CancellationToken cancellationToken = default)
    {
        var normalized = UmapiCacheKeyBuilder.NormalizeVin(vin);
        if (normalized.Length is < 11 or > 17)
        {
            return new CatalogVinLookupResult(false, 400, "invalid_vin", "Valid VIN is required (11–17 characters).", null);
        }

        var lang = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim();
        var reg = string.IsNullOrWhiteSpace(region) ? "WWW" : region.Trim();
        var row = await _repository.FindVinAsync(normalized, lang, reg, cancellationToken).ConfigureAwait(false);
        if (row is null || row.VehicleCount <= 0 || string.IsNullOrWhiteSpace(row.ResponseJson))
        {
            return new CatalogVinLookupResult(false, 404, "vin_cache_miss", "No saved VIN decode for this key; PHP/UMAPI remains authoritative.", null);
        }

        var payload = DecodeObject(row.ResponseJson);
        if (payload is null)
        {
            return new CatalogVinLookupResult(false, 404, "vin_cache_invalid", "Saved VIN payload could not be decoded.", null);
        }

        return new CatalogVinLookupResult(true, 200, string.Empty, string.Empty, new
        {
            ok = true,
            source = "database",
            stale = true,
            cached_at = row.UpdatedAt,
            vin = row.Vin,
            language = row.Language,
            region = row.Region,
            vehicle_count = row.VehicleCount,
            manufacturer = row.Manufacturer,
            model_label = row.ModelLabel,
            payload
        });
    }

    public Task<CatalogActionCacheLookupResult> LookupEnginesAsync(string? section, int mfaId, string? language, string? region, CancellationToken cancellationToken = default)
    {
        if (mfaId <= 0)
        {
            return Task.FromResult(new CatalogActionCacheLookupResult(
                false, 400, "missing_params", "Manufacturer ID (MFA_ID) is required.", "engines", section ?? "passenger", null, 0, true, "rejected"));
        }

        var parameters = new Dictionary<string, object?> { ["MFA_ID"] = mfaId };
        return LookupActionAsync("engines", section, language, region, parameters, cancellationToken);
    }

    public Task<CatalogActionCacheLookupResult> LookupAnalogsAsync(string? section, string? article, string? brand, string? language, string? region, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(article) || string.IsNullOrWhiteSpace(brand))
        {
            return Task.FromResult(new CatalogActionCacheLookupResult(
                false, 400, "missing_params", "Article and brand are required.", "analogs", section ?? "passenger", null, 0, true, "rejected"));
        }

        var parameters = new Dictionary<string, object?>
        {
            ["article"] = article.Trim(),
            ["brand"] = brand.Trim()
        };
        return LookupActionAsync("analogs", section, language, region, parameters, cancellationToken);
    }

    public Task<CatalogActionCacheLookupResult> LookupArticleBrandsAsync(string? section, string? article, string? language, string? region, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(article))
        {
            return Task.FromResult(new CatalogActionCacheLookupResult(
                false, 400, "missing_params", "Article number is required.", "brands", section ?? "passenger", null, 0, true, "rejected"));
        }

        var parameters = new Dictionary<string, object?> { ["article"] = article.Trim() };
        return LookupActionAsync("brands", section, language, region, parameters, cancellationToken);
    }

    private async Task<CatalogActionCacheLookupResult> LookupActionAsync(
        string action,
        string? section,
        string? language,
        string? region,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        var normalizedSection = string.IsNullOrWhiteSpace(section) ? "passenger" : section.Trim().ToLowerInvariant();
        var lang = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim();
        var reg = string.IsNullOrWhiteSpace(region) ? "WWW" : region.Trim();
        var cacheKey = UmapiCacheKeyBuilder.Build(action, normalizedSection, lang, reg, parameters);
        var row = await _repository.FindActionCacheAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        if (row is null || string.IsNullOrWhiteSpace(row.ResponseJson))
        {
            return new CatalogActionCacheLookupResult(
                false,
                404,
                "cache_miss",
                $"No saved {action} cache row; PHP/UMAPI remains authoritative.",
                action,
                normalizedSection,
                null,
                0,
                true,
                "database-empty");
        }

        var payload = DecodeObject(row.ResponseJson);
        return new CatalogActionCacheLookupResult(
            true,
            200,
            string.Empty,
            string.Empty,
            action,
            normalizedSection,
            payload,
            row.RowsCount,
            true,
            "database");
    }

    private static object? DecodeObject(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
