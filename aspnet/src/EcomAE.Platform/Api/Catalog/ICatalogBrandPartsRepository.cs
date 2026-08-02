namespace EcomAE.Platform.Api.Catalog;

public interface ICatalogBrandPartsRepository
{
    Task<(int TotalRows, IReadOnlyList<CatalogBrandPartRow> Page)> FindByBrandAsync(
        string brandUpper,
        string brandCompact,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);
}
