namespace EcomAE.Platform.Services;

public interface ITenantRegistry
{
    ValueTask<TenantRegistryRecord?> FindByHostAsync(string host, CancellationToken cancellationToken = default);
}
