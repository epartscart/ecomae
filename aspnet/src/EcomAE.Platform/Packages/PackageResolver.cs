using EcomAE.Platform.Erp;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    // The revision row is re-read at most once per second per instance: deactivation on
    // any instance is visible to every other instance within ~1s (fail-closed applies).
    private static readonly TimeSpan RevisionCacheLifetime = TimeSpan.FromSeconds(1);

    private readonly IErpWriteConnectionFactory? _connections;
    private readonly IMemoryCache _cache;
    private readonly ILogger<PackageResolver> _log;
    private readonly Func<string, CancellationToken, Task<long>>? _revisionLoader;
    private readonly Func<string, CancellationToken, Task<IReadOnlySet<string>>>? _setLoader;
    private readonly TimeSpan _revisionCacheLifetime;

    public PackageResolver(
        IErpWriteConnectionFactory connections,
        IMemoryCache cache,
        ILogger<PackageResolver> log)
    {
        _connections = connections;
        _cache = cache;
        _log = log;
        _revisionCacheLifetime = RevisionCacheLifetime;
    }

    /// <summary>Test seam: simulates separate app instances sharing one registry store.</summary>
    internal PackageResolver(
        Func<string, CancellationToken, Task<long>> revisionLoader,
        Func<string, CancellationToken, Task<IReadOnlySet<string>>> setLoader,
        IMemoryCache cache,
        TimeSpan revisionCacheLifetime)
    {
        _revisionLoader = revisionLoader;
        _setLoader = setLoader;
        _cache = cache;
        _log = NullLogger<PackageResolver>.Instance;
        _revisionCacheLifetime = revisionCacheLifetime;
    }

    public void Invalidate(string siteKey)
        => _cache.Remove(RevisionCacheKey(_dbTag ?? string.Empty, siteKey)); // re-reads epc_package_revision on next resolve

    private string _dbTag = string.Empty; // captured from the open connection: prevents cross-DB cache bleed

    private static string CacheKey(string dbTag, string siteKey, long revision) => $"pkg-resolve:{dbTag}:{siteKey}:{revision}";
    private static string RevisionCacheKey(string dbTag, string siteKey) => $"pkg-revision:{dbTag}:{siteKey}";

    public async Task<IReadOnlySet<string>> GetEnabledAsync(string siteKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(siteKey))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var revision = await GetRevisionAsync(siteKey, cancellationToken).ConfigureAwait(false);
        if (revision < 0)
        {
            // Revision unverifiable → fail closed: never serve potentially stale state.
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var key = CacheKey(_dbTag, siteKey, revision);
        if (_cache.TryGetValue(key, out IReadOnlySet<string>? cached) && cached is not null)
        {
            return cached;
        }

        var resolved = await LoadAsync(siteKey, cancellationToken).ConfigureAwait(false);
        _cache.Set(key, resolved, CacheLifetime);
        return resolved;
    }

    private async Task<long> GetRevisionAsync(string siteKey, CancellationToken cancellationToken)
    {
        var key = RevisionCacheKey(_dbTag, siteKey);
        if (_cache.TryGetValue(key, out long cached))
        {
            return cached;
        }

        var revision = await LoadRevisionAsync(siteKey, cancellationToken).ConfigureAwait(false);
        _cache.Set(key, revision, _revisionCacheLifetime);
        return revision;
    }

    private async Task<long> LoadRevisionAsync(string siteKey, CancellationToken cancellationToken)
    {
        if (_revisionLoader is not null)
        {
            try
            {
                return await _revisionLoader(siteKey, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                return -1;
            }
        }

        if (_connections is null || !_connections.IsConfigured)
        {
            return -1;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            _dbTag = connection.Database ?? string.Empty;
            var value = await ErpDb.ScalarAsync(
                connection, null,
                "SELECT `revision` FROM `epc_package_revision` WHERE `site_key` = ? LIMIT 1",
                cancellationToken, siteKey).ConfigureAwait(false);
            var revision = value is null or DBNull ? 0L : Convert.ToInt64(value);
            // Cache under the resolved DB tag — the first lookup ran with a blank tag.
            _cache.Set(RevisionCacheKey(_dbTag, siteKey), revision, _revisionCacheLifetime);
            return revision;
        }
        catch
        {
            return -1; // fail closed: unknown revision still resolves the set, never a stale enabled entry
        }
    }

    private async Task<IReadOnlySet<string>> LoadAsync(string siteKey, CancellationToken cancellationToken)
    {
        if (_setLoader is not null)
        {
            try
            {
                return await _setLoader(siteKey, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase); // fail closed
            }
        }

        var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_connections is null || !_connections.IsConfigured)
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
