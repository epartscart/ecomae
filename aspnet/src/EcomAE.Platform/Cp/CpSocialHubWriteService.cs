using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_epc_social_media.php</c> twin of <c>save_draft</c> / <c>epc_social_save_draft</c>.
/// Publish, generate caption, test account, and schema-ensure stay Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpSocialHubWriteService
{
    Task<ErpSimpleWriteResult> SaveDraftAsync(
        CpSocialHubSaveDraftRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CpSocialHubSaveDraftRequest(
    long Id,
    string? SiteKey,
    string? Platform,
    string? Title,
    string? Caption,
    string? Hashtags,
    string? MediaUrl,
    bool SuperCpHost,
    string? RequestHost);

public sealed class CpSocialHubWriteService : ICpSocialHubWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PlatformSafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex HostSafe = new("[^a-z0-9_-]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static readonly HashSet<string> Platforms = new(StringComparer.Ordinal)
    {
        "instagram", "tiktok", "facebook", "linkedin", "x",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public CpSocialHubWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string Clip(string? raw, int max)
    {
        var value = (raw ?? string.Empty).Trim();
        return value.Length <= max ? value : value[..max];
    }

    public static string NormalizePlatform(string? platform)
        => PlatformSafe.Replace((platform ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    /// <summary>PHP Super-CP GET site_key or <c>platform</c>; tenant host fallback.</summary>
    public static string ResolveSiteKey(string? posted, bool superCpHost, string? requestHost)
    {
        var key = SiteKeySafe.Replace((posted ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);
        if (key.Length > 0)
        {
            return key;
        }

        if (superCpHost)
        {
            return "platform";
        }

        var host = (requestHost ?? string.Empty).Trim().ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        var colon = host.IndexOf(':');
        if (colon > 0)
        {
            host = host[..colon];
        }

        return HostSafe.Replace(host.Replace('.', '-'), string.Empty);
    }

    public static string NormalizeTitle(string? title)
    {
        var value = Clip(title, 255);
        return value.Length == 0 ? "Untitled draft" : value;
    }

    public async Task<ErpSimpleWriteResult> SaveDraftAsync(
        CpSocialHubSaveDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var siteKey = ResolveSiteKey(request.SiteKey, request.SuperCpHost, request.RequestHost);
        if (siteKey.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid site_key");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var platform = NormalizePlatform(request.Platform);
        var title = NormalizeTitle(request.Title);
        var caption = (request.Caption ?? string.Empty).Trim();
        var hashtags = (request.Hashtags ?? string.Empty).Trim();
        var mediaUrl = Clip(request.MediaUrl, 512);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (request.Id > 0)
            {
                var existing = await ErpDb.LongAsync(
                    connection, null,
                    ErpDb.Positional("SELECT `id` FROM `epc_social_post_drafts` WHERE `id`=? AND `site_key`=? LIMIT 1"),
                    cancellationToken, request.Id, siteKey).ConfigureAwait(false);
                if (existing <= 0)
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Draft not found");
                }

                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("UPDATE `epc_social_post_drafts` SET `platform`=?, `title`=?, `caption`=?, `hashtags`=?, `media_url`=?, `updated_at`=? WHERE `id`=? AND `site_key`=?"),
                    cancellationToken, platform, title, caption, hashtags, mediaUrl, now, request.Id, siteKey).ConfigureAwait(false);
                return ErpSimpleWriteResult.Ok("Draft saved.", request.Id);
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `epc_social_post_drafts` (`site_key`, `platform`, `title`, `caption`, `hashtags`, `media_url`, `status`, `created_at`, `updated_at`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)"),
                cancellationToken, siteKey, platform, title, caption, hashtags, mediaUrl, "draft", now, now).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Draft saved.", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Social drafts table is missing — schema-ensure stays Classic.");
        }
    }
}
