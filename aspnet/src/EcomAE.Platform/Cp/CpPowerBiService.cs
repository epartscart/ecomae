using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live twin of the PHP loader behind <c>cp/content/control/portal/epc_power_bi.php</c>
/// (<c>epc_power_bi_ensure_schema</c>, <c>epc_power_bi_config_get</c>,
/// <c>epc_power_bi_reports_list</c>, <c>epc_power_bi_embed_resolve</c>).
/// </summary>
public interface ICpPowerBiService
{
    Task<CpPowerBiView> LoadAsync(
        string? siteKey,
        string? host,
        CancellationToken cancellationToken = default);
}

public sealed record CpPowerBiTenantOption(string SiteKey, string Hostname, string TradeName);

public sealed record CpPowerBiConfig(
    string SiteKey,
    string WorkspaceId,
    string AzureTenantId,
    string DefaultReportId,
    string DefaultDatasetId,
    string EmbedUrl,
    string EmbedMode,
    string Notes,
    bool Active)
{
    public static CpPowerBiConfig Empty(string siteKey)
        => new(siteKey, "", "", "", "", "", "none", "", false);
}

public sealed record CpPowerBiReport(
    long Id,
    string ReportId,
    string ReportName,
    string DatasetId,
    string Category,
    string EmbedUrl,
    bool Active,
    string CreatedAt);

public sealed record CpPowerBiView(
    string SiteKey,
    CpPowerBiConfig Config,
    IReadOnlyList<CpPowerBiReport> Reports,
    IReadOnlyList<CpPowerBiTenantOption> Tenants,
    PowerBiEmbedResolution Embed,
    string Source)
{
    public static CpPowerBiView Empty(string siteKey, string source)
        => new(
            siteKey,
            CpPowerBiConfig.Empty(siteKey),
            [],
            [],
            PowerBiCatalog.ResolveEmbed(false, "none", ""),
            source);
}

public sealed class CpPowerBiService : ICpPowerBiService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IHttpContextAccessor? _http;

    public CpPowerBiService(IErpWriteConnectionFactory connections, IHttpContextAccessor? httpContextAccessor = null)
    {
        _connections = connections;
        _http = httpContextAccessor;
    }

    public async Task<CpPowerBiView> LoadAsync(
        string? siteKey,
        string? host,
        CancellationToken cancellationToken = default)
    {
        var requested = CpPowerBiWriteService.NormalizeSiteKey(siteKey);
        var scope = RequestScope();
        if (scope.Confined && scope.SiteKey.Length == 0)
        {
            return CpPowerBiView.Empty(requested, "confined");
        }

        if (!_connections.IsConfigured)
        {
            return CpPowerBiView.Empty(requested, "unconfigured");
        }

        var hotKey = CacheKey(host, requested, scope.SiteKey, scope.DatabaseName);
        if (ErpHotReadCache.TryGet(hotKey, out CpPowerBiView? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await CpPowerBiWriteService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

            var tenants = await LoadTenantsAsync(connection, cancellationToken).ConfigureAwait(false);
            if (scope.Confined)
            {
                tenants = VisibleTenants(tenants, scope.SiteKey);
            }

            var key = ResolveSiteKey(requested, host, tenants, scope.Confined ? scope.SiteKey : null);
            var config = await LoadConfigAsync(connection, key, cancellationToken).ConfigureAwait(false);
            var reports = await LoadReportsAsync(connection, key, cancellationToken).ConfigureAwait(false);
            var embed = PowerBiCatalog.ResolveEmbed(config.Active, config.EmbedMode, config.EmbedUrl);

            var view = new CpPowerBiView(key, config, reports, tenants, embed, "db");
            ErpHotReadCache.Set(hotKey, view, TimeSpan.FromSeconds(20));
            return view;
        }
        catch (DbException)
        {
            return CpPowerBiView.Empty(requested, "db-error");
        }
    }

    /// <summary>
    /// Cache identity includes the company and its database. The host alone is shared
    /// by every company opened on www.ecomae.com, so it must not be the whole key.
    /// </summary>
    public static string CacheKey(string? host, string requested, string? siteKey, string? database)
        => "pbi:"
            + CpPowerBiWriteService.NormalizeSiteKey(siteKey)
            + "@"
            + (database ?? string.Empty).Trim()
            + "|"
            + (host ?? string.Empty).Trim()
            + "|"
            + requested;

    /// <summary>A bound company sees its own registry row. The platform workspace sees the directory.</summary>
    public static IReadOnlyList<CpPowerBiTenantOption> VisibleTenants(
        IReadOnlyList<CpPowerBiTenantOption> tenants,
        string? confineToSite)
    {
        var confined = CpPowerBiWriteService.NormalizeSiteKey(confineToSite);
        if (confined.Length == 0)
        {
            return tenants;
        }

        return tenants
            .Where(tenant => string.Equals(CpPowerBiWriteService.NormalizeSiteKey(tenant.SiteKey), confined, StringComparison.Ordinal))
            .ToArray();
    }

    /// <summary>PHP host-match fallback then first tenant. A confined company never falls through to another tenant.</summary>
    public static string ResolveSiteKey(
        string requested,
        string? host,
        IReadOnlyList<CpPowerBiTenantOption> tenants,
        string? confineToSite = null)
    {
        var confined = CpPowerBiWriteService.NormalizeSiteKey(confineToSite);
        if (confined.Length > 0)
        {
            return confined;
        }

        if (requested.Length > 0)
        {
            return requested;
        }

        var current = Strip((host ?? string.Empty).Split(':')[0]);
        if (current.Length > 0)
        {
            foreach (var tenant in tenants)
            {
                if (Strip(tenant.Hostname) == current)
                {
                    return tenant.SiteKey;
                }
            }
        }

        return tenants.Count > 0 ? tenants[0].SiteKey : "epartscart";
    }

    private (string SiteKey, string DatabaseName, bool Confined) RequestScope()
    {
        var http = _http?.HttpContext;
        if (http is null || !LegacyAdminPermissionSets.IsCompanyRequest(http))
        {
            var platformDb = http?.Items[TenantResolutionMiddleware.HttpContextItemKey] is TenantContext platform
                ? platform.DatabaseName ?? string.Empty
                : string.Empty;
            return (string.Empty, platformDb.Trim(), false);
        }

        var tenant = http.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        var site = ClientErpPath.NormalizeSiteKey(tenant?.SiteKey);
        if (site.Length == 0
            && http.Items.TryGetValue(ClientErpPath.HttpContextItemKey, out var raw))
        {
            site = ClientErpPath.NormalizeSiteKey(raw as string);
        }

        if (site.Length == 0 && ClientErpPath.TryParse(http.Request.Path.Value, out var parsed))
        {
            site = parsed.SiteKey;
        }

        return (site, (tenant?.DatabaseName ?? string.Empty).Trim(), true);
    }

    private static string Strip(string hostname)
    {
        var host = hostname.Trim().ToLowerInvariant();
        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }

    private static async Task<IReadOnlyList<CpPowerBiTenantOption>> LoadTenantsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = new List<CpPowerBiTenantOption>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT `site_key`, IFNULL(`hostname`,''), IFNULL(`trade_name`,'') FROM `epc_portal_tenants` "
            + "WHERE IFNULL(`site_key`, '') <> '' AND COALESCE(`is_active`, 1) = 1 ORDER BY `hostname` ASC";
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CpPowerBiTenantOption(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }

        return rows;
    }

    private static async Task<CpPowerBiConfig> LoadConfigAsync(
        DbConnection connection,
        string siteKey,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT IFNULL(`workspace_id`,''), IFNULL(`azure_tenant_id`,''), IFNULL(`default_report_id`,''), "
            + "IFNULL(`default_dataset_id`,''), IFNULL(`embed_url`,''), IFNULL(`embed_mode`,'none'), "
            + "IFNULL(`notes`,''), IFNULL(`active`,0) FROM `epc_power_bi_config` WHERE `site_key` = @p0 LIMIT 1";
        ErpDb.AddParameters(cmd, [siteKey]);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return CpPowerBiConfig.Empty(siteKey);
        }

        return new CpPowerBiConfig(
            siteKey,
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            Convert.ToInt32(reader.GetValue(7), CultureInfo.InvariantCulture) == 1);
    }

    private static async Task<IReadOnlyList<CpPowerBiReport>> LoadReportsAsync(
        DbConnection connection,
        string siteKey,
        CancellationToken cancellationToken)
    {
        var rows = new List<CpPowerBiReport>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT `id`, IFNULL(`report_id`,''), IFNULL(`report_name`,''), IFNULL(`dataset_id`,''), "
            + "IFNULL(`category`,''), IFNULL(`embed_url`,''), IFNULL(`active`,0), IFNULL(`created_at`,'') "
            + "FROM `epc_power_bi_reports` WHERE `site_key` = @p0 ORDER BY `category` ASC, `id` DESC LIMIT 200";
        ErpDb.AddParameters(cmd, [siteKey]);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CpPowerBiReport(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture) == 1,
                reader.GetValue(7)?.ToString() ?? string.Empty));
        }

        return rows;
    }
}
