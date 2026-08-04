namespace EcomAE.Platform.Data.Scaffolding;

/// <summary>
/// Unwired repository contract for Enterprise BOS EF Core TenantRegistry cutover.
/// Not registered in DI and must not be used for production reads/writes yet.
/// </summary>
public interface ITenantRegistryScaffoldRepository
{
    Task<IReadOnlyList<TenantRegistryStub>> ListTenantsAsync(CancellationToken cancellationToken = default);
}
