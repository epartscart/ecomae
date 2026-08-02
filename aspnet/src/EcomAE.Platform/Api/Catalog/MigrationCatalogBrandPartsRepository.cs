namespace EcomAE.Platform.Api.Catalog;

public sealed class MigrationCatalogBrandPartsRepository : ICatalogBrandPartsRepository
{
    public Task<(int TotalRows, IReadOnlyList<CatalogBrandPartRow> Page)> FindByBrandAsync(
        string brandUpper,
        string brandCompact,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
        => Task.FromResult((0, (IReadOnlyList<CatalogBrandPartRow>)[]));
}
