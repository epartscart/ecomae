using System.Data.Common;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Presentation;

public sealed record ErpNavTenantRuleSet(
    IReadOnlySet<string> DisabledTabIds,
    IReadOnlySet<string> EnabledModuleIds)
{
    public static ErpNavTenantRuleSet Empty { get; } =
        new(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
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
        var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var knownModuleIds = KnownModuleIds();
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                """
                SELECT `feature_key`, NULL AS `modules`
                FROM `epc_tenant_feature_flags`
                WHERE `site_key` = ?
                  AND IFNULL(`enabled`,1) = 0
                  AND `feature_key` LIKE 'erp.nav.disabled.%'
                UNION ALL
                SELECT NULL AS `feature_key`, p.`modules`
                FROM `epc_tenant_pack_assignments` a
                INNER JOIN `epc_industry_packs` p ON p.`pack_key` = a.`pack_key`
                WHERE a.`site_key` = ?
                  AND IFNULL(p.`active`,1) = 1
                """);
            ErpDb.AddParameters(command, normalizedSiteKey, normalizedSiteKey);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (!reader.IsDBNull(0))
                {
                    var key = Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture);
                    var tabId = DisabledTabId(key);
                    if (tabId.Length > 0)
                    {
                        disabled.Add(tabId);
                    }
                }

                if (!reader.IsDBNull(1))
                {
                    AddExplicitModuleIds(
                        Convert.ToString(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture),
                        knownModuleIds,
                        enabled);
                }
            }
        }
        catch (DbException)
        {
            return ErpNavTenantRuleSet.Empty;
        }

        return new ErpNavTenantRuleSet(disabled, enabled);
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

    private static IReadOnlySet<string> KnownModuleIds()
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var area in PhpModuleCatalog.ErpAreas)
        {
            values.Add(area.Id);
        }

        foreach (var tab in PhpModuleCatalog.ErpTabs)
        {
            values.Add(tab.Id);
            values.Add(TabKey(tab));
            if (!string.IsNullOrWhiteSpace(tab.Group))
            {
                values.Add(tab.Group);
            }
        }

        return values;
    }

    private static void AddExplicitModuleIds(
        string? modulesJson,
        IReadOnlySet<string> knownModuleIds,
        ISet<string> destination)
    {
        if (string.IsNullOrWhiteSpace(modulesJson))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(modulesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var value = item.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(value) && knownModuleIds.Contains(value))
                {
                    destination.Add(value);
                }
            }
        }
        catch (JsonException)
        {
        }
    }

    public static IReadOnlySet<string> ExplicitModuleIds(string? modulesJson)
    {
        var destination = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddExplicitModuleIds(modulesJson, KnownModuleIds(), destination);
        return destination;
    }

    private static string TabKey(PhpModuleCatalog.ModuleLink tab)
    {
        var slash = tab.Id.LastIndexOf('/');
        return slash >= 0 && slash < tab.Id.Length - 1 ? tab.Id[(slash + 1)..] : tab.Id;
    }
}
