using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_integrations.php</c> twin of <c>save_feature_flags</c> / <c>epc_integrations_save_feature_flags</c>.
/// Schema-ensure stays Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpTenantFeaturesWriteService
{
    Task<ErpSimpleWriteResult> SaveFlagsAsync(
        CpTenantFeaturesSaveRequest request,
        CancellationToken cancellationToken = default);
}

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
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Feature-flags table is missing — schema-ensure stays Classic.");
        }
    }
}
