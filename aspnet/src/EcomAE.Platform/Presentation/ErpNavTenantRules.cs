using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Presentation;

public sealed record ErpNavTenantRuleSet(IReadOnlySet<string> DisabledTabIds)
{
    public static ErpNavTenantRuleSet Empty { get; } =
        new(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
}

public interface IErpNavTenantRules
{
    Task<ErpNavTenantRuleSet> ResolveAsync(
        string? siteKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only tenant overlay for ERP navigation. PHP remains authoritative:
/// only explicit <c>erp.nav.disabled.*</c> feature flags add restrictions.
/// Missing tables, rows, or configuration preserve the complete PHP tree.
/// </summary>
public sealed class ErpNavTenantRules : IErpNavTenantRules
{
    private const string DisabledPrefix = "erp.nav.disabled.";
    private readonly IErpWriteConnectionFactory _connections;

    public ErpNavTenantRules(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpNavTenantRuleSet> ResolveAsync(
        string? siteKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedSiteKey = NormalizeSiteKey(siteKey);
        if (!_connections.IsConfigured || normalizedSiteKey.Length == 0)
        {
            return ErpNavTenantRuleSet.Empty;
        }

        var disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                """
                SELECT `feature_key`
                FROM `epc_tenant_feature_flags`
                WHERE `site_key` = ?
                  AND IFNULL(`enabled`,1) = 0
                  AND `feature_key` LIKE 'erp.nav.disabled.%'
                """);
            ErpDb.AddParameters(command, normalizedSiteKey);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var key = Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture);
                var tabId = DisabledTabId(key);
                if (tabId.Length > 0)
                {
                    disabled.Add(tabId);
                }
            }
        }
        catch (DbException)
        {
            return ErpNavTenantRuleSet.Empty;
        }

        return new ErpNavTenantRuleSet(disabled);
    }

    public static string NormalizeSiteKey(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant();

    public static string DisabledTabId(string? featureKey)
    {
        var key = (featureKey ?? string.Empty).Trim();
        return key.StartsWith(DisabledPrefix, StringComparison.OrdinalIgnoreCase)
            ? key[DisabledPrefix.Length..].Trim()
            : string.Empty;
    }
}
