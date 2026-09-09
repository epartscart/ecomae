using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_marketplace.php</c> twin of <c>epc_marketplace_install</c>,
/// <c>epc_marketplace_uninstall</c>, and <c>epc_marketplace_add_review</c>.
/// Config, seed, and schema-ensure stay Classic. This service does not invent a send.
/// </summary>
public interface ICpMarketplaceAppsWriteService
{
    Task<ErpSimpleWriteResult> InstallAsync(
        CpMarketplaceInstallRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> UninstallAsync(
        CpMarketplaceInstallRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddReviewAsync(
        CpMarketplaceReviewRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CpMarketplaceInstallRequest(
    long AppId,
    string? SiteKey);

public sealed record CpMarketplaceReviewRequest(
    long AppId,
    string? SiteKey,
    int? Rating,
    string? Title,
    string? ReviewText,
    string? ReviewerName);

public sealed class CpMarketplaceAppsWriteService : ICpMarketplaceAppsWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_-]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;

    public CpMarketplaceAppsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeSiteKey(string? raw)
        => Clip(SiteKeySafe.Replace((raw ?? string.Empty).Trim().ToLowerInvariant(), string.Empty), 64);

    public static string Clip(string? raw, int max)
    {
        var text = raw ?? string.Empty;
        return text.Length <= max ? text : text[..max];
    }

    public static int ClampRating(int? raw)
    {
        var rating = raw ?? 5;
        if (rating < 1)
        {
            return 1;
        }

        return rating > 5 ? 5 : rating;
    }

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

    public async Task<ErpSimpleWriteResult> AddReviewAsync(
        CpMarketplaceReviewRequest request,
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

        var rating = ClampRating(request.Rating);
        var title = Clip(request.Title, 128);
        var reviewText = Clip(request.ReviewText, 512);
        var reviewerName = Clip(request.ReviewerName, 128);

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `epc_marketplace_reviews` (`app_id`,`site_key`,`rating`,`title`,`review_text`,`reviewer_name`) VALUES (?,?,?,?,?,?)"),
                cancellationToken, request.AppId, siteKey, rating, title, reviewText, reviewerName).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);

            var avg = await ErpDb.DecimalAsync(
                connection, null,
                ErpDb.Positional("SELECT AVG(`rating`) FROM `epc_marketplace_reviews` WHERE `app_id`=?"),
                cancellationToken, request.AppId).ConfigureAwait(false);
            var count = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_marketplace_reviews` WHERE `app_id`=?"),
                cancellationToken, request.AppId).ConfigureAwait(false);
            var avgRounded = Math.Round(avg, 1, MidpointRounding.AwayFromZero);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_marketplace_apps` SET `avg_rating`=?, `review_count`=? WHERE `id`=?"),
                cancellationToken, avgRounded, count, request.AppId).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Review added.", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Marketplace table is missing — schema-ensure stays Classic.");
        }
    }
}
