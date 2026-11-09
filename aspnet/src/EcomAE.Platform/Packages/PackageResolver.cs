using EcomAE.Platform.Erp;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace EcomAE.Platform.Packages;

/// <summary>Resolves a tenant's enabled packages. Fail-closed: unverifiable ⇒ empty set.</summary>
public interface IPackageResolver
{
    Task<IReadOnlySet<string>> GetEnabledAsync(string siteKey, CancellationToken cancellationToken = default);
    void Invalidate(string siteKey);
}

/// <summary>Runtime gate: package execution is allowed only when enabled for the tenant.</summary>
public interface IPackageGate
{
    Task<bool> IsEnabledAsync(HttpContext? context, string packageId, CancellationToken cancellationToken = default);
    Task<bool> IsEnabledAsync(string siteKey, string packageId, CancellationToken cancellationToken = default);
}

public sealed class PackageResolver : IPackageResolver, IPackageGate
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(60);

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PackageResolver> _log;

    public PackageResolver(
        IErpWriteConnectionFactory connections,
        IMemoryCache cache,
        ILogger<PackageResolver> log)
    {
        _connections = connections;
        _cache = cache;
        _log = log;
    }

    public void Invalidate(string siteKey)
        => _cache.Remove(CacheKey(siteKey));

    private static string CacheKey(string siteKey) => "pkg-resolve:" + siteKey;

    public async Task<IReadOnlySet<string>> GetEnabledAsync(string siteKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(siteKey))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var key = CacheKey(siteKey);
        if (_cache.TryGetValue(key, out IReadOnlySet<string>? cached) && cached is not null)
        {
            return cached;
        }

        var resolved = await LoadAsync(siteKey, cancellationToken).ConfigureAwait(false);
        _cache.Set(key, resolved, CacheLifetime);
        return resolved;
    }

    private async Task<IReadOnlySet<string>> LoadAsync(string siteKey, CancellationToken cancellationToken)
    {
        var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!_connections.IsConfigured)
        {
            return enabled; // fail closed
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT `package_id` FROM `epc_tenant_package_installs` WHERE `site_key` = ? AND `enabled` = 1 AND `install_status` = 'installed'";
            var parameter = command.CreateParameter();
            parameter.Value = siteKey;
            command.Parameters.Add(parameter);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!reader.IsDBNull(0))
                {
                    enabled.Add(reader.GetString(0));
                }
            }
        }
        catch (Exception ex)
        {
            // Fail closed on any registry failure: unknown state must never allow execution.
            _log.LogWarning(ex, "Package resolve failed siteKey={SiteKey} (fail-closed)", siteKey);
        }

        return enabled;
    }

    public async Task<bool> IsEnabledAsync(string siteKey, string packageId, CancellationToken cancellationToken = default)
    {
        if (string.Equals(packageId, PackageCatalog.CorePackageId, StringComparison.OrdinalIgnoreCase))
        {
            return true; // core is always on
        }

        var set = await GetEnabledAsync(siteKey, cancellationToken).ConfigureAwait(false);
        return set.Contains(packageId);
    }

    public async Task<bool> IsEnabledAsync(HttpContext? context, string packageId, CancellationToken cancellationToken = default)
    {
        var tenant = context?.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        var siteKey = tenant?.SiteKey ?? "ecomae";
        return await IsEnabledAsync(siteKey, packageId, cancellationToken).ConfigureAwait(false);
    }
}
