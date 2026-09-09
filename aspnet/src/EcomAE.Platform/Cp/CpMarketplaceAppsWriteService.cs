using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_marketplace.php</c> twin of <c>epc_marketplace_install</c>
/// and <c>epc_marketplace_uninstall</c>. Review, config, seed, and schema-ensure
/// stay Classic. This service does not invent a send.
/// </summary>
public interface ICpMarketplaceAppsWriteService
{
    Task<ErpSimpleWriteResult> InstallAsync(
        CpMarketplaceInstallRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> UninstallAsync(
        CpMarketplaceInstallRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CpMarketplaceInstallRequest(
    long AppId,
    string? SiteKey);

public sealed class CpMarketplaceAppsWriteService : ICpMarketplaceAppsWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_-]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public CpMarketplaceAppsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    public async Task<ErpSimpleWriteResult> InstallAsync(
        CpMarketplaceInstallRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.AppId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "App id is required");
        }

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
            var version = await ErpDb.StringAsync(
                connection, null,
                ErpDb.Positional("SELECT IFNULL(`version`,'') FROM `epc_marketplace_apps` WHERE `id`=? AND `status`='published' LIMIT 1"),
                cancellationToken, request.AppId).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(version))
            {
                return ErpSimpleWriteResult.Fail("not_found", "App not found or not published");
            }

            if (version.Length > 16)
            {
                version = version[..16];
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `epc_marketplace_installs` (`app_id`,`site_key`,`installed_version`,`installed_by`) VALUES (?,?,?,0) ON DUPLICATE KEY UPDATE `status`='active', `installed_version`=VALUES(`installed_version`), `updated_at`=NOW()"),
                cancellationToken, request.AppId, siteKey, version).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_marketplace_apps` SET `downloads`=`downloads`+1 WHERE `id`=?"),
                cancellationToken, request.AppId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("App installed.", request.AppId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Marketplace table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> UninstallAsync(
        CpMarketplaceInstallRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.AppId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "App id is required");
        }

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
            var existing = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT `id` FROM `epc_marketplace_installs` WHERE `app_id`=? AND `site_key`=? LIMIT 1"),
                cancellationToken, request.AppId, siteKey).ConfigureAwait(false);
            if (existing <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Install not found");
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_marketplace_installs` SET `status`='uninstalled' WHERE `app_id`=? AND `site_key`=?"),
                cancellationToken, request.AppId, siteKey).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("App uninstalled.", request.AppId);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Marketplace table is missing — schema-ensure stays Classic.");
        }
    }
}
