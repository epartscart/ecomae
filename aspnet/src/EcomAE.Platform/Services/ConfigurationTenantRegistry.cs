using EcomAE.Platform.Configuration;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Services;

public sealed class ConfigurationTenantRegistry : ITenantRegistry
{
    private readonly IReadOnlyDictionary<string, TenantRegistryRecord> _recordsByHost;

    public ConfigurationTenantRegistry(IOptions<EcomAeOptions> options)
    {
        _recordsByHost = options.Value.SeedTenants
            .Where(row => !string.IsNullOrWhiteSpace(row.Host))
            .Select(row => new TenantRegistryRecord(
                NormalizeHost(row.Host),
                row.Mode,
                NormalizeSiteKey(row.SiteKey),
                string.IsNullOrWhiteSpace(row.DatabaseName) ? null : row.DatabaseName.Trim(),
                row.StorefrontEnabled,
                row.ErpEnabled,
                row.ControlPanelEnabled,
                row.BosEnabled,
                string.IsNullOrWhiteSpace(row.DbUser) ? null : row.DbUser.Trim(),
                string.IsNullOrWhiteSpace(row.DbPassword) ? null : row.DbPassword,
                row.DedicatedDb || string.Equals(row.ScalePolicy, "dedicated_mysql", StringComparison.OrdinalIgnoreCase),
                string.IsNullOrWhiteSpace(row.ScalePolicy) ? null : row.ScalePolicy.Trim()))
            .GroupBy(row => row.Host, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public ValueTask<TenantRegistryRecord?> FindByHostAsync(string host, CancellationToken cancellationToken = default)
    {
        _recordsByHost.TryGetValue(NormalizeHost(host), out var record);
        return ValueTask.FromResult(record);
    }

    private static string NormalizeHost(string host)
    {
        return host.Trim().TrimEnd('.').ToLowerInvariant();
    }

    private static string? NormalizeSiteKey(string? siteKey)
    {
        if (string.IsNullOrWhiteSpace(siteKey))
        {
            return null;
        }

        var chars = siteKey.Trim().ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || ch == '_').ToArray();
        return chars.Length == 0 ? null : new string(chars);
    }
}
