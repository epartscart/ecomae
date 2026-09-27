using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_integrations.php</c> twin of <c>save_feature_flags</c> / <c>epc_integrations_save_feature_flags</c>,
/// including the <c>epc_integrations_ensure_schema</c> table guard and the
/// <c>epc_portal_list_tenants</c> selector feed used by <c>epc_tenant_features.php</c>.
/// </summary>
public interface ICpTenantFeaturesWriteService
{
    Task<ErpSimpleWriteResult> SaveFlagsAsync(
        CpTenantFeaturesSaveRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>PHP <c>epc_portal_list_tenants()</c> ordered by hostname.</summary>
    Task<IReadOnlyList<CpTenantFeatureTenantOption>> ListTenantsAsync(CancellationToken cancellationToken = default);
}

public sealed record CpTenantFeatureTenantOption(string SiteKey, string Label);

public sealed record CpTenantFeaturesSaveRequest(
    string? SiteKey,
    IReadOnlyDictionary<string, bool>? Features);

public sealed class CpTenantFeaturesWriteService : ICpTenantFeaturesWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_-]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex FeatureKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<CpIntegrationsHubCatalogEntry> SaveableCatalog { get; } =
        CpIntegrationsHubCatalog.All.Where(item => item.Key != "tenant_registry").ToArray();

    public static readonly HashSet<string> SaveableKeys = new(
        SaveableCatalog.Select(item => item.Key),
        StringComparer.Ordinal);

    private readonly IErpWriteConnectionFactory _connections;

    public CpTenantFeaturesWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP <c>preg_replace('/[^a-z0-9_-]/', '', $site_key)</c> after lowercasing.</summary>
    public static string NormalizeSiteKey(string? siteKey)
        => SiteKeySafe.Replace((siteKey ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    /// <summary>PHP <c>preg_replace('/[^a-z0-9_]/', '', $featureKey)</c> after lowercasing.</summary>
    public static string NormalizeFeatureKey(string? featureKey)
        => FeatureKeySafe.Replace((featureKey ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    public static bool IsSaveable(string? featureKey)
    {
        var key = NormalizeFeatureKey(featureKey);
        return key.Length > 0 && SaveableKeys.Contains(key);
    }

    public static IReadOnlyDictionary<string, bool> NormalizeFeatures(IReadOnlyDictionary<string, bool>? posted)
    {
        var flags = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var key in SaveableKeys)
        {
            flags[key] = posted is not null && posted.TryGetValue(key, out var on) && on;
        }

        return flags;
    }

    public async Task<IReadOnlyList<CpTenantFeatureTenantOption>> ListTenantsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT `site_key`, IFNULL(`trade_name`, ''), IFNULL(`hostname`, '') FROM `epc_portal_tenants` "
                + "WHERE IFNULL(`site_key`, '') <> '' ORDER BY `hostname` ASC";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var rows = new List<CpTenantFeatureTenantOption>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var key = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                var trade = (reader.IsDBNull(1) ? string.Empty : reader.GetString(1)).Trim();
                rows.Add(new CpTenantFeatureTenantOption(key, (trade.Length > 0 ? trade : key) + " (" + key + ")"));
            }

            return rows;
        }
        catch (DbException)
        {
            return [];
        }
    }

    /// <summary>PHP <c>epc_integrations_ensure_schema()</c> flag table guard.</summary>
    private static async Task EnsureFlagTableAsync(DbConnection connection, CancellationToken cancellationToken)
        => await ErpDb.ExecuteAsync(
            connection, null,
            ErpDb.Positional(
                "CREATE TABLE IF NOT EXISTS `epc_tenant_feature_flags` ("
                + "`site_key` VARCHAR(64) NOT NULL, `feature_key` VARCHAR(64) NOT NULL, "
                + "`enabled` TINYINT(1) NOT NULL DEFAULT 1, `config_json` TEXT NULL, "
                + "`updated_at` INT NOT NULL DEFAULT 0, PRIMARY KEY (`site_key`, `feature_key`), "
                + "KEY `feature_key` (`feature_key`)) ENGINE=InnoDB DEFAULT CHARSET=utf8"),
            cancellationToken).ConfigureAwait(false);

    public async Task<ErpSimpleWriteResult> SaveFlagsAsync(
        CpTenantFeaturesSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var siteKey = NormalizeSiteKey(request.SiteKey);
        if (siteKey.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid site_key");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var flags = NormalizeFeatures(request.Features);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureFlagTableAsync(connection, cancellationToken).ConfigureAwait(false);
            var saved = 0L;
            foreach (var pair in flags)
            {
                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional(
                        "INSERT INTO `epc_tenant_feature_flags` (`site_key`, `feature_key`, `enabled`, `updated_at`) VALUES (?, ?, ?, ?) ON DUPLICATE KEY UPDATE `enabled` = VALUES(`enabled`), `updated_at` = VALUES(`updated_at`)"),
                    cancellationToken,
                    siteKey, pair.Key, pair.Value ? 1 : 0, now).ConfigureAwait(false);
                saved++;
            }

            return ErpSimpleWriteResult.Ok("Saved " + saved.ToString(System.Globalization.CultureInfo.InvariantCulture) + " feature flags.", saved);
        }
        catch (DbException ex)
        {
            return ErpSimpleWriteResult.Fail("db", "Feature-flag save failed: " + ex.Message);
        }
    }
}
