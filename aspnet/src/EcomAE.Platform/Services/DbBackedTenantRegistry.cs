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

        // Host pairs to try: (primary, www-alias) then (country base domain, www.base).
        // ae.epartscart.com resolves the same tenant row as epartscart.com.
        var pairs = new List<(string H0, string H1)> { (aliases[0], aliases.Count > 1 ? aliases[1] : aliases[0]) };
        if (aliases.Count > 2)
        {
            pairs.Add((aliases[2], aliases.Count > 3 ? aliases[3] : aliases[2]));
        }

        try
        {
            await using var connection = await _connections.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
            TenantRegistryRecord? record = null;
            foreach (var pair in pairs)
            {
                record = await TryReadByHostsAsync(
                        connection,
                        PortalTenantSql.SelectActiveTenantByHosts,
                        pair,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (record is not null)
                {
                    break;
                }
            }
            // Older registries may lack dedicated_db/scale_policy — retry core columns only.
            if (record is null)
            {
                foreach (var pair in pairs)
                {
                    record = await TryReadByHostsAsync(
                            connection,
                            PortalTenantSql.SelectActiveTenantByHostsMinimal,
                            pair,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (record is not null)
                    {
                        break;
                    }
                }
            }

            // Multi-domain tenants (auto247.ae / auto247.pk / auto247.com.pk → site_key auto247):
            // distinct domains can't alias through hostname normalization, so a registry-level
            // domain map binds each hostname to the tenant's site_key, then the tenant row
            // resolves exactly like a primary hostname match.
            if (record is null)
            {
                record = await TryReadByDomainMapAsync(connection, aliases, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (record is not null)
            {
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

    /// <summary>
    /// Domain map fallback: <c>epc_tenant_domains</c> binds arbitrary hostnames to a site_key,
    /// then the tenant row itself is read by site_key. A missing table (older registry) just
    /// misses like an unmapped host.
    /// </summary>
    private static async Task<TenantRegistryRecord?> TryReadByDomainMapAsync(
        DbConnection connection,
        IReadOnlyList<string> aliases,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var lookup = connection.CreateCommand();
            var names = new string[aliases.Count];
            for (var i = 0; i < aliases.Count; i++)
            {
                var name = "@d" + i;
                names[i] = name;
                var p = lookup.CreateParameter();
                p.ParameterName = name;
                p.Value = aliases[i];
                lookup.Parameters.Add(p);
            }

            lookup.CommandText = "SELECT `site_key`, `hostname` FROM `epc_tenant_domains` "
                + "WHERE `is_active` = 1 AND `hostname` IN (" + string.Join(",", names) + ") "
                + "ORDER BY CASE WHEN `hostname` = @d0 THEN 0 ELSE 1 END LIMIT 1";

            await using var reader = await lookup.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var siteKey = Convert.ToString(reader["site_key"], CultureInfo.InvariantCulture) ?? string.Empty;
            var mappedHost = Convert.ToString(reader["hostname"], CultureInfo.InvariantCulture) ?? string.Empty;
            if (siteKey.Length == 0)
            {
                return null;
            }

            await reader.DisposeAsync().ConfigureAwait(false);
            await using var byKey = connection.CreateCommand();
            byKey.CommandText = PortalTenantSql.SelectTenantBySiteKey;
            var sk = byKey.CreateParameter();
            sk.ParameterName = "@siteKey";
            sk.Value = siteKey;
            byKey.Parameters.Add(sk);
            await using var keyReader = await byKey.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await keyReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var row = ReadRow(keyReader);
            if (!row.IsActive || row.Status is not ("dns_pending" or "live"))
            {
                return null;
            }

            return row.ToTenantRegistryRecord() with { Host = PlatformHostPolicy.NormalizeHost(mappedHost) };
        }
        catch
        {
            return null;
        }
    }

    private static async Task<TenantRegistryRecord?> TryReadByHostsAsync(
        DbConnection connection,
        string sql,
        (string H0, string H1) hosts,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            var h0 = command.CreateParameter();
            h0.ParameterName = "@h0";
            h0.Value = hosts.H0;
            command.Parameters.Add(h0);

            var h1 = command.CreateParameter();
            h1.ParameterName = "@h1";
            h1.Value = hosts.H1;
            command.Parameters.Add(h1);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var row = ReadRow(reader);
            return row.ToTenantRegistryRecord() with { Host = PlatformHostPolicy.NormalizeHost(row.Hostname) };
        }
        catch
        {
            return null;
        }
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
