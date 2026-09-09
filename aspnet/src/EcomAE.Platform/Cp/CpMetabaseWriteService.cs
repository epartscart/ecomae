using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_metabase_embed.php</c> twin of <c>epc_metabase_configure</c> (URL only)
/// and <c>epc_metabase_register_dashboard</c>. Secret key, JWT embed, and schema-ensure
/// stay Classic. This service does not invent a send.
/// </summary>
public interface ICpMetabaseWriteService
{
    Task<ErpSimpleWriteResult> SaveConfigAsync(
        CpMetabaseSaveConfigRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddDashboardAsync(
        CpMetabaseAddDashboardRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CpMetabaseSaveConfigRequest(
    string? SiteKey,
    string? MetabaseUrl);

public sealed record CpMetabaseAddDashboardRequest(
    string? SiteKey,
    int DashboardId,
    string? DashboardName,
    string? Category);

public sealed class CpMetabaseWriteService : ICpMetabaseWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_-]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public CpMetabaseWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    public static string Clip(string? raw, int max)
    {
        var text = (raw ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }

    public async Task<ErpSimpleWriteResult> SaveConfigAsync(
        CpMetabaseSaveConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var siteKey = NormalizeSiteKey(request.SiteKey);
        if (siteKey.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Site key is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `epc_metabase_config` (`site_key`,`metabase_url`,`secret_key`,`active`) VALUES (?,?,'',1) ON DUPLICATE KEY UPDATE `metabase_url`=VALUES(`metabase_url`), `active`=1"),
                cancellationToken,
                siteKey,
                Clip(request.MetabaseUrl, 256)).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Metabase configured for " + siteKey + ".", 0);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Metabase table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> AddDashboardAsync(
        CpMetabaseAddDashboardRequest request,
        CancellationToken cancellationToken = default)
    {
        var siteKey = NormalizeSiteKey(request.SiteKey);
        if (siteKey.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Site key is required");
        }

        if (request.DashboardId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Dashboard id is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var name = Clip(request.DashboardName, 128);
        if (name.Length == 0)
        {
            name = "Dashboard";
        }

        var category = Clip(request.Category, 32);
        if (category.Length == 0)
        {
            category = "finance";
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `epc_metabase_dashboards` (`site_key`,`dashboard_id`,`dashboard_name`,`category`) VALUES (?,?,?,?)"),
                cancellationToken,
                siteKey,
                request.DashboardId,
                name,
                category).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Dashboard registered.", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Metabase table is missing — schema-ensure stays Classic.");
        }
    }
}
