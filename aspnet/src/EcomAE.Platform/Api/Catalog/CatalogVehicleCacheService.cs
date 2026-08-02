namespace EcomAE.Platform.Api.Catalog;

public sealed class CatalogVehicleCacheService : ICatalogVehicleCacheService
{
    private readonly ICatalogVehicleCacheRepository _repository;

    public CatalogVehicleCacheService(ICatalogVehicleCacheRepository repository)
    {
        _repository = repository;
    }

    public async Task<CatalogCacheListResult> GetModelsAsync(string section, int mfaId, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSection(section);
        if (mfaId <= 0)
        {
            return Empty("models", normalized, "Query param mfa_id (MFA_ID) is required and must be > 0.", mfaId: mfaId);
        }

        var rows = await _repository.FindModelsAsync(normalized, mfaId, cancellationToken).ConfigureAwait(false);
        var data = rows.Select(row => DecodeOrFallback(row.RawJson, new
        {
            MFA_ID = row.MfaId,
            MS_ID = row.MsId,
            model_series = row.ModelSeries,
            year_from = row.YearFrom,
            year_to = row.YearTo
        })).ToArray();

        return ListResult("models", normalized, data, mfaId: mfaId);
    }

    public async Task<CatalogCacheListResult> GetModificationsAsync(string section, int msId, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSection(section);
        if (msId <= 0)
        {
            return Empty("modifications", normalized, "Query param ms_id (MS_ID) is required and must be > 0.", msId: msId);
        }

        var rows = await _repository.FindModificationsAsync(normalized, msId, cancellationToken).ConfigureAwait(false);
        var data = rows.Select(row => DecodeOrFallback(row.RawJson, new
        {
            MS_ID = row.MsId,
            modification_id = row.ModificationId,
            title = row.Title,
            year_from = row.YearFrom,
            year_to = row.YearTo,
            power_kw = row.PowerKw,
            capacity_lt = row.CapacityLt,
            fuel_type = row.FuelType
        })).ToArray();

        return ListResult("modifications", normalized, data, msId: msId);
    }

    public async Task<CatalogCacheListResult> GetBrandsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _repository.FindBrandsAsync(cancellationToken).ConfigureAwait(false);
        var data = rows.Select(row => DecodeOrFallback(row.RawJson, new
        {
            sup_id = row.SupId,
            brand = row.Brand,
            full_name = row.FullName
        })).ToArray();

        return ListResult("brands", "all", data);
    }

    private static string NormalizeSection(string section)
        => string.IsNullOrWhiteSpace(section) ? "passenger" : section.Trim().ToLowerInvariant();

    private static object DecodeOrFallback(string? rawJson, object fallback)
        => DbCatalogVehicleCacheRepository.DecodeRawJson(rawJson) ?? fallback;

    private static CatalogCacheListResult Empty(string action, string section, string message, int? mfaId = null, int? msId = null)
        => new(false, action, section, 0, "rejected", [], message, mfaId, msId);

    private static CatalogCacheListResult ListResult(string action, string section, IReadOnlyList<object> data, int? mfaId = null, int? msId = null)
        => new(
            true,
            action,
            section,
            data.Count,
            data.Count > 0 ? "database" : "database-empty",
            data,
            data.Count > 0 ? string.Empty : $"No cached {action} rows; warm PHP offline catalog or check TenantRegistry DB.",
            mfaId,
            msId);
}
