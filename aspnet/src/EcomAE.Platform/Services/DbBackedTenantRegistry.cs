using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Data;
using Microsoft.Extensions.Caching.Memory;

namespace EcomAE.Platform.Services;

/// <summary>
/// Live platform-registry lookup by hostname with seed fallback.
/// Credentials are loaded for connection open only — never logged.
/// </summary>
public sealed class DbBackedTenantRegistry : ITenantRegistry
{
    private readonly ITenantDbConnectionFactory _connections;
    private readonly ConfigurationTenantRegistry _seed;
    private readonly IMemoryCache _cache;

    public DbBackedTenantRegistry(
        ITenantDbConnectionFactory connections,
        ConfigurationTenantRegistry seed,
        IMemoryCache cache)
    {
        _connections = connections;
        _seed = seed;
        _cache = cache;
    }

    public async ValueTask<TenantRegistryRecord?> FindByHostAsync(string host, CancellationToken cancellationToken = default)
    {
        var aliases = PlatformHostPolicy.NormalizeHostAliases(host);
        if (aliases.Count == 0)
        {
            return null;
        }

        var normalized = aliases[0];

        if (!_connections.IsConfigured)
        {
            return await _seed.FindByHostAsync(normalized, cancellationToken).ConfigureAwait(false);
        }

        var cacheKey = "tenant-registry:host:" + normalized;
        if (_cache.TryGetValue(cacheKey, out TenantRegistryRecord? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            await using var connection = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = PortalTenantSql.SelectActiveTenantByHosts;

            var h0 = command.CreateParameter();
            h0.ParameterName = "@h0";
            h0.Value = aliases[0];
            command.Parameters.Add(h0);

            var h1 = command.CreateParameter();
            h1.ParameterName = "@h1";
            h1.Value = aliases.Count > 1 ? aliases[1] : aliases[0];
            command.Parameters.Add(h1);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var row = ReadRow(reader);
                var record = row.ToTenantRegistryRecord() with { Host = PlatformHostPolicy.NormalizeHost(row.Hostname) };
                _cache.Set(cacheKey, record, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
                });
                return record;
            }
        }
        catch
        {
            // Fall back to seed — fail closed on data opens happens in the connection factory.
        }

        return await _seed.FindByHostAsync(normalized, cancellationToken).ConfigureAwait(false);
    }

    private static PortalTenantRow ReadRow(DbDataReader reader)
    {
        return new PortalTenantRow(
            SiteKey: Convert.ToString(reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty,
            Hostname: Convert.ToString(reader["hostname"], CultureInfo.InvariantCulture) ?? string.Empty,
            DatabaseName: Convert.ToString(reader["db_name"], CultureInfo.InvariantCulture) ?? string.Empty,
            DbUser: Convert.ToString(reader["db_user"], CultureInfo.InvariantCulture) ?? string.Empty,
            DbPassword: Convert.ToString(reader["db_password"], CultureInfo.InvariantCulture) ?? string.Empty,
            Status: Convert.ToString(reader["status"], CultureInfo.InvariantCulture) ?? string.Empty,
            IsDemo: ToBool(reader["is_demo"]),
            ErpOnlyShared: ToBool(reader["erp_only_shared"]),
            IsActive: ToBool(reader["is_active"], defaultTrue: true),
            DedicatedDb: ToBool(reader["dedicated_db"]),
            ScalePolicy: Convert.ToString(reader["scale_policy"], CultureInfo.InvariantCulture) ?? string.Empty);
    }

    private static bool ToBool(object? value, bool defaultTrue = false)
    {
        if (value is null or DBNull)
        {
            return defaultTrue;
        }

        return Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0;
    }
}
