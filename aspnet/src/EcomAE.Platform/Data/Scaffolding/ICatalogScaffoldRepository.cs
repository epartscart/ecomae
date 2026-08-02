namespace EcomAE.Platform.Data.Scaffolding;

/// <summary>
/// Unwired repository contract for Enterprise BOS EF Core cutover.
/// Not registered in DI and must not be used for production reads/writes yet.
/// </summary>
public interface ICatalogScaffoldRepository
{
    Task<IReadOnlyList<CatalogBrandStub>> ListBrandsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogProductStub>> ListProductsAsync(CancellationToken cancellationToken = default);
}
