using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

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

    public CpPowerBiService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpPowerBiView> LoadAsync(
        string? siteKey,
        string? host,
        CancellationToken cancellationToken = default)
    {
        var requested = CpPowerBiWriteService.NormalizeSiteKey(siteKey);
        if (!_connections.IsConfigured)
        {
            return CpPowerBiView.Empty(requested, "unconfigured");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await CpPowerBiWriteService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

            var tenants = await LoadTenantsAsync(connection, cancellationToken).ConfigureAwait(false);
            var key = ResolveSiteKey(requested, host, tenants);
            var config = await LoadConfigAsync(connection, key, cancellationToken).ConfigureAwait(false);
            var reports = await LoadReportsAsync(connection, key, cancellationToken).ConfigureAwait(false);
            var embed = PowerBiCatalog.ResolveEmbed(config.Active, config.EmbedMode, config.EmbedUrl);

            return new CpPowerBiView(key, config, reports, tenants, embed, "db");
        }
        catch (DbException)
        {
            return CpPowerBiView.Empty(requested, "db-error");
        }
    }

    /// <summary>PHP host-match fallback then first tenant.</summary>
    public static string ResolveSiteKey(
        string requested,
        string? host,
        IReadOnlyList<CpPowerBiTenantOption> tenants)
    {
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
