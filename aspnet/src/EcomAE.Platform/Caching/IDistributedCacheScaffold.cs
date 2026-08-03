namespace EcomAE.Platform.Caching;

/// <summary>
/// Unwired Redis/distributed-cache contract for Enterprise BOS scaffolding.
/// Not registered in DI; PHP cookies remain authoritative until staging cookie parity.
/// </summary>
public interface IDistributedCacheScaffold
{
    Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default);

    Task SetStringAsync(string key, string value, TimeSpan? ttl, CancellationToken cancellationToken = default);
}
