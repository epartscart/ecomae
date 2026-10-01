using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live twin of the PHP social hub writes (<c>epc_social_save_draft</c>, <c>epc_social_save_account</c>,
/// <c>epc_social_test_account</c>, <c>epc_social_delete_account</c> in
/// content/social_media/epc_social_media_helpers.php).
/// Live publishing to Meta/TikTok stays Classic — this service does not invent a send.
/// </summary>
public interface ICpSocialHubWriteService
{
    Task<ErpSimpleWriteResult> SaveDraftAsync(
        CpSocialHubSaveDraftRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveAccountAsync(
        CpSocialHubSaveAccountRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> TestAccountAsync(
        string? siteKey,
        string? platform,
        bool superCpHost,
        string? requestHost,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAccountAsync(
        string? siteKey,
        string? platform,
        bool superCpHost,
        string? requestHost,
        CancellationToken cancellationToken = default);
}

public sealed record CpSocialHubSaveAccountRequest(
    string? SiteKey,
    string? Platform,
    string? AccountLabel,
    string? Username,
    string? AccessToken,
    string? ApiKey,
    string? ApiSecret,
    string? PageId,
    string? IgUserId,
    string? OpenId,
    string? PrivacyLevel,
    bool SuperCpHost,
    string? RequestHost);

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
        if (superCpHost)
        {
            var key = SiteKeySafe.Replace((posted ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);
            if (key.Length > 0)
            {
                return key;
            }

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

    /// <summary>PHP <c>epc_social_save_account()</c> privacy whitelist.</summary>
    public static string NormalizePrivacy(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToUpperInvariant();
        return SocialMediaPackCatalog.TikTokPrivacyLevels.Any(p => p.Value == value) ? value : "SELF_ONLY";
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

    /// <summary>
    /// PHP <c>epc_social_save_account()</c>: AES-256-CBC credential vault keyed per tenant, blank fields
    /// keep the stored value, Instagram/TikTok fall back to the posted page id for their own identifier.
    /// </summary>
    public async Task<ErpSimpleWriteResult> SaveAccountAsync(
        CpSocialHubSaveAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var siteKey = ResolveSiteKey(request.SiteKey, request.SuperCpHost, request.RequestHost);
        if (siteKey.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid site_key");
        }

        var platform = NormalizePlatform(request.Platform);
        if (!Platforms.Contains(platform))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid platform.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var label = Clip(request.AccountLabel, 128);
        var username = Clip(request.Username, 128);
        var pageId = (request.PageId ?? string.Empty).Trim();
        var igUserId = (request.IgUserId ?? string.Empty).Trim();
        var openId = (request.OpenId ?? string.Empty).Trim();
        if (platform == "instagram" && igUserId.Length == 0 && pageId.Length > 0)
        {
            igUserId = pageId;
        }

        if (platform == "tiktok" && openId.Length == 0 && pageId.Length > 0)
        {
            openId = pageId;
        }

        var payload = new JsonObject
        {
            ["access_token"] = (request.AccessToken ?? string.Empty).Trim(),
            ["api_key"] = (request.ApiKey ?? string.Empty).Trim(),
            ["api_secret"] = (request.ApiSecret ?? string.Empty).Trim(),
            ["page_id"] = pageId,
            ["ig_user_id"] = igUserId,
            ["open_id"] = openId,
            ["privacy_level"] = NormalizePrivacy(request.PrivacyLevel),
            ["updated"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var existing = await LoadAccountAsync(connection, siteKey, platform, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                MergeStoredCredentials(payload, existing.Value.Credentials, siteKey, request);
            }

            var encrypted = CpSocialCrypto.Encrypt(payload.ToJsonString(), siteKey);
            if (existing is not null)
            {
                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("UPDATE `epc_social_accounts` SET `account_label`=?, `username`=?, `encrypted_credentials`=?, `status`=?, `updated_at`=? WHERE `id`=?"),
                    cancellationToken, label, username, encrypted, "connected", now, existing.Value.Id).ConfigureAwait(false);
                return ErpSimpleWriteResult.Ok(Title(platform) + " account saved securely.", existing.Value.Id);
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `epc_social_accounts` (`site_key`, `platform`, `account_label`, `username`, `encrypted_credentials`, `status`, `created_at`, `updated_at`) VALUES (?, ?, ?, ?, ?, ?, ?, ?)"),
                cancellationToken, siteKey, platform, label, username, encrypted, "connected", now, now).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(Title(platform) + " account saved securely.", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Social accounts table is missing — schema-ensure stays Classic.");
        }
    }

    /// <summary>
    /// PHP <c>epc_social_test_account()</c> vault fallback: live Meta/TikTok probing stays Classic, so the
    /// twin verifies the stored token plus username and records <c>last_test_at</c>/<c>status</c> like PHP.
    /// </summary>
    public async Task<ErpSimpleWriteResult> TestAccountAsync(
        string? siteKey,
        string? platform,
        bool superCpHost,
        string? requestHost,
        CancellationToken cancellationToken = default)
    {
        var key = ResolveSiteKey(siteKey, superCpHost, requestHost);
        var plat = NormalizePlatform(platform);
        if (key.Length == 0 || plat.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid platform.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var existing = await LoadAccountAsync(connection, key, plat, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                return ErpSimpleWriteResult.Fail("not_found", "No account configured for " + plat + ".");
            }

            var meta = CpSocialHubService.ParseCredentials(existing.Value.Credentials, key);
            var ok = meta.HasToken && existing.Value.Username.Length > 0;
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_social_accounts` SET `last_test_at`=?, `last_test_ok`=?, `status`=? WHERE `site_key`=? AND `platform`=?"),
                cancellationToken, now, ok ? 1 : 0, ok ? "verified" : "pending", key, plat).ConfigureAwait(false);

            return ok
                ? ErpSimpleWriteResult.Ok("Credential vault OK (live API module stays Classic).", existing.Value.Id)
                : ErpSimpleWriteResult.Fail("incomplete", "Credentials incomplete. Add username and access token.");
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Social accounts table is missing — schema-ensure stays Classic.");
        }
    }

    /// <summary>PHP <c>epc_social_delete_account()</c>.</summary>
    public async Task<ErpSimpleWriteResult> DeleteAccountAsync(
        string? siteKey,
        string? platform,
        bool superCpHost,
        string? requestHost,
        CancellationToken cancellationToken = default)
    {
        var key = ResolveSiteKey(siteKey, superCpHost, requestHost);
        var plat = NormalizePlatform(platform);
        if (key.Length == 0 || plat.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid platform.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var rows = await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("DELETE FROM `epc_social_accounts` WHERE `site_key`=? AND `platform`=?"),
                cancellationToken, key, plat).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Account removed.", rows);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Social accounts table is missing — schema-ensure stays Classic.");
        }
    }

    /// <summary>PHP keeps stored secrets when the posted field is blank.</summary>
    public static void MergeStoredCredentials(
        JsonObject payload,
        string? storedEncrypted,
        string siteKey,
        CpSocialHubSaveAccountRequest request)
    {
        var plain = CpSocialCrypto.Decrypt(storedEncrypted, siteKey);
        if (plain.Length == 0)
        {
            return;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(plain);
        }
        catch (JsonException)
        {
            return;
        }

        if (node is not JsonObject old)
        {
            return;
        }

        Keep(payload, old, "access_token");
        Keep(payload, old, "api_key");
        Keep(payload, old, "api_secret");
        Keep(payload, old, "page_id");
        Keep(payload, old, "ig_user_id");
        Keep(payload, old, "open_id");
        if ((request.PrivacyLevel ?? string.Empty).Trim().Length == 0)
        {
            var stored = (old["privacy_level"]?.GetValue<string>() ?? string.Empty).Trim();
            if (stored.Length > 0)
            {
                payload["privacy_level"] = stored;
            }
        }
    }

    private static void Keep(JsonObject payload, JsonObject old, string field)
    {
        if ((payload[field]?.GetValue<string>() ?? string.Empty).Length > 0)
        {
            return;
        }

        payload[field] = old[field]?.GetValue<string>() ?? string.Empty;
    }

    private static string Title(string platform)
        => platform.Length == 0 ? platform : char.ToUpperInvariant(platform[0]) + platform[1..];

    private static async Task<(long Id, string Username, string Credentials)?> LoadAccountAsync(
        DbConnection connection,
        string siteKey,
        string platform,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT `id`, IFNULL(`username`,''), IFNULL(`encrypted_credentials`,'') FROM `epc_social_accounts` "
            + "WHERE `site_key` = @p0 AND `platform` = @p1 LIMIT 1";
        ErpDb.AddParameters(cmd, [siteKey, platform]);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return (
            Convert.ToInt64(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture),
            reader.GetString(1).Trim(),
            reader.GetString(2));
    }
}
