namespace EcomAE.Platform.Data.Scaffolding;

/// <summary>
/// EF Core stub entity for future TenantRegistry bounded context. Not production-mapped.
/// </summary>
public sealed class TenantRegistryStub
{
    public int Id { get; set; }

    public string SiteKey { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public int IsActive { get; set; }
}
