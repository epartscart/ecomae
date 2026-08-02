namespace EcomAE.Platform.Api.Catalog;

public sealed class CatalogManufacturerService : ICatalogManufacturerService
{
    private readonly ICatalogManufacturerRepository _repository;

    public CatalogManufacturerService(ICatalogManufacturerRepository repository)
    {
        _repository = repository;
    }

    public async Task<CatalogManufacturersResult> GetBySectionAsync(string section, CancellationToken cancellationToken = default)
    {
        var normalized = string.IsNullOrWhiteSpace(section) ? "passenger" : section.Trim().ToLowerInvariant();
        var rows = await _repository.FindBySectionAsync(normalized, cancellationToken).ConfigureAwait(false);
        var data = new List<object>(rows.Count);
        foreach (var row in rows)
        {
            var decoded = DbCatalogManufacturerRepository.DecodeRawJson(row.RawJson);
            if (decoded is not null)
            {
                data.Add(decoded);
                continue;
            }

            data.Add(new
            {
                MFA_ID = row.MfaId,
                manufacturer = row.Manufacturer,
                manufacturer_ru = row.ManufacturerRu,
                type = row.Type,
                country = row.Country,
                popular = row.Popular,
                is_logo = row.IsLogo
            });
        }

        return new CatalogManufacturersResult(
            Ok: true,
            Section: normalized,
            Rows: data.Count,
            Source: data.Count > 0 ? "database" : "database-empty",
            Data: data,
            Message: data.Count > 0
                ? string.Empty
                : "No cached manufacturers for section; warm PHP offline catalog or check TenantRegistry DB.");
    }
}
