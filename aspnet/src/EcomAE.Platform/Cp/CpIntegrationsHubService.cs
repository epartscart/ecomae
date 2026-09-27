using System.Data.Common;
using EcomAE.Platform.Data;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Cp;

/// <summary>PHP <c>epc_integrations_hub_rows()</c> row.</summary>
public sealed record CpIntegrationsHubCard(
    string Key,
    string Label,
    string Icon,
    string Color,
    string Category,
    string Blurb,
    bool Active,
    string ConfigureUrl,
    string Guide,
    bool SuperOnly)
{
    /// <summary>PHP <c>$searchHay</c> — lowercase label + blurb + key + category label.</summary>
    public string SearchHay(string categoryLabel)
        => (Label + " " + Blurb + " " + Key + " " + categoryLabel).ToLowerInvariant();
}

/// <summary>PHP <c>epc_integrations_hub.php</c> page state.</summary>
public sealed record CpIntegrationsHubView(
    bool IsSuper,
    string MarketLabel,
    IReadOnlyList<CpIntegrationsHubCard> Rows,
    int ActiveCount,
    int TotalCount,
    int GuideCount);

public interface ICpIntegrationsHubService
{
    Task<CpIntegrationsHubView> LoadAsync(bool isSuper, CancellationToken cancellationToken = default);
}

/// <summary>
/// Integrations hub reads: catalog rows overlaid with <c>epc_tenant_feature_flags</c> for the
/// request tenant (PHP <c>epc_integrations_features_for_site</c>) plus the tenant market label.
/// Read-only — configuration lives on each integration's own module.
/// </summary>
public sealed class CpIntegrationsHubService : ICpIntegrationsHubService
{
    private const string SelectFlags =
        "SELECT `feature_key`, `enabled` FROM `epc_tenant_feature_flags` WHERE `site_key` = ? LIMIT 2000";

    private const string SelectCountry =
        "SELECT `setting_value` FROM `epc_price_settings` WHERE `setting_key` = 'company_country_code' LIMIT 1";

    private readonly ITenantDbConnectionFactory _registry;
    private readonly IErpWriteConnectionFactory _tenant;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public CpIntegrationsHubService(
        ITenantDbConnectionFactory registry,
        IErpWriteConnectionFactory tenant,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _registry = registry;
        _tenant = tenant;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>PHP <c>epc_apai_country_meta()['label']</c>.</summary>
    public static string MarketLabel(string? countryCode)
    {
        var code = new string((countryCode ?? string.Empty).Where(char.IsAsciiLetter).ToArray()).ToUpperInvariant();
        if (code.Length > 2)
        {
            code = code == "UAE" ? "AE" : code[..2];
        }

        return code switch
        {
            "" or "AE" => "United Arab Emirates",
            "PK" => "Pakistan",
            "IN" => "India",
            "SA" => "Saudi Arabia",
            "OM" => "Oman",
            "GB" => "United Kingdom",
            "US" => "United States",
            _ => code,
        };
    }

    public async Task<CpIntegrationsHubView> LoadAsync(bool isSuper, CancellationToken cancellationToken = default)
    {
        var tenant = _httpContextAccessor?.HttpContext?.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        var siteKey = isSuper ? "platform" : (tenant?.SiteKey ?? string.Empty);

        var flags = isSuper ? null : await LoadFlagsAsync(siteKey, cancellationToken).ConfigureAwait(false);
        var rows = CpIntegrationsHubCatalog.BuildHubCards(isSuper, flags);
        var market = isSuper ? string.Empty : await LoadMarketLabelAsync(tenant, cancellationToken).ConfigureAwait(false);

        return new CpIntegrationsHubView(
            isSuper,
            market,
            rows,
            rows.Count(row => row.Active),
            rows.Count,
            rows.Count(row => !string.IsNullOrWhiteSpace(row.Guide)));
    }

    private async Task<IReadOnlyDictionary<string, bool>?> LoadFlagsAsync(
        string siteKey,
        CancellationToken cancellationToken)
    {
        if (siteKey.Length == 0 || !_registry.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _registry.OpenRegistryAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(SelectFlags);
            ErpDb.AddParameters(command, siteKey);

            var flags = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var key = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                if (key.Length == 0)
                {
                    continue;
                }

                flags[key] = !reader.IsDBNull(1) && reader.GetInt32(1) != 0;
            }

            return flags.Count > 0 ? flags : null;
        }
        catch (DbException)
        {
            // PHP falls back to catalog defaults when the flag table is unavailable.
            return null;
        }
    }

    private async Task<string> LoadMarketLabelAsync(TenantContext? tenant, CancellationToken cancellationToken)
    {
        if (TenantDataGuard.IsContained(tenant) || !_tenant.IsConfigured)
        {
            return MarketLabel(null);
        }

        try
        {
            await using var connection = await _tenant.OpenAsync(cancellationToken).ConfigureAwait(false);
            var code = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional(SelectCountry),
                cancellationToken).ConfigureAwait(false);
            return MarketLabel(code);
        }
        catch (DbException)
        {
            return MarketLabel(null);
        }
    }
}
