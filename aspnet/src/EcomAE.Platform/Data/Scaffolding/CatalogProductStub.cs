namespace EcomAE.Platform.Data.Scaffolding;

/// <summary>
/// EF Core stub entity for future Catalog bounded context. Not production-mapped.
/// </summary>
public sealed class CatalogProductStub
{
    public int Id { get; set; }

    public string Article { get; set; } = string.Empty;

    public string Brand { get; set; } = string.Empty;
}
