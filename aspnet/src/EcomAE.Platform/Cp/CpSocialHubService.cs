using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live twin of the PHP social hub loader (<c>epc_social_brand_context</c>, <c>epc_social_list_accounts</c>,
/// <c>epc_social_account_public_meta</c>, <c>epc_social_list_drafts</c> in
/// content/social_media/epc_social_media_helpers.php) behind
/// <c>cp/content/control/portal/epc_social_media_hub.php</c>.
/// </summary>
public interface ICpSocialHubService
{
    Task<CpSocialHubView> LoadAsync(
        string? siteKey,
        bool superCpHost,
        string? host,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CpSocialTenantOption>> ListTenantsAsync(CancellationToken cancellationToken = default);
}

public sealed record CpSocialTenantOption(string SiteKey, string Hostname, string TradeName);

/// <summary>PHP account row without <c>encrypted_credentials</c> plus <c>epc_social_account_public_meta</c>.</summary>
public sealed record CpSocialAccount(
    long Id,
    string Platform,
    string AccountLabel,
    string Username,
    string Status,
    long LastTestAt,
    bool LastTestOk,
    string PageId,
    string IgUserId,
    string OpenId,
    string PrivacyLevel,
    bool HasToken);

public sealed record CpSocialDraft(
    long Id,
    string Platform,
    string Title,
    string Caption,
    string Hashtags,
    string MediaUrl,
    string Status,
    long ScheduledAt,
    string ExternalPostId,
    long PublishedAt,
    string LastError,
    long UpdatedAt);

public sealed record CpSocialHubView(
    string SiteKey,
    SocialBrandContext Brand,
    IReadOnlyList<CpSocialAccount> Accounts,
    IReadOnlyList<CpSocialDraft> Drafts,
    string Source)
{
    public static CpSocialHubView Empty(string siteKey, SocialBrandContext brand, string source) =>
        new(siteKey, brand, [], [], source);

    public CpSocialAccount? Account(string platform)
        => Accounts.FirstOrDefault(a => a.Platform == platform);
}

public sealed class CpSocialHubService : ICpSocialHubService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpSocialHubService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpSocialHubView> LoadAsync(
        string? siteKey,
        bool superCpHost,
        string? host,
        CancellationToken cancellationToken = default)
    {
        var key = CpSocialHubWriteService.ResolveSiteKey(siteKey, superCpHost, host);
        if (!_connections.IsConfigured)
        {
            return CpSocialHubView.Empty(key, DefaultBrand(key), "unconfigured");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var brand = await LoadBrandAsync(connection, key, cancellationToken).ConfigureAwait(false);
            var accounts = await LoadAccountsAsync(connection, key, cancellationToken).ConfigureAwait(false);
            var drafts = await LoadDraftsAsync(connection, key, cancellationToken).ConfigureAwait(false);
            return new CpSocialHubView(key, brand, accounts, drafts, "db");
        }
        catch (DbException)
        {
            return CpSocialHubView.Empty(key, DefaultBrand(key), "db-error");
        }
    }

    public async Task<IReadOnlyList<CpSocialTenantOption>> ListTenantsAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            return await LoadTenantsAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return [];
        }
    }

    /// <summary>PHP platform defaults from <c>epc_social_brand_context()</c>.</summary>
    public static SocialBrandContext DefaultBrand(string siteKey)
        => new(
            siteKey,
            "ECOM AE",
            "ecomae.official",
            "https://www.ecomae.com",
            "ecomae.com",
            siteKey == "platform" || siteKey.Length == 0 ? "platform" : "auto_parts",
            "UAE",
            "GCC",
            siteKey == "platform" || siteKey.Length == 0);

    private static async Task<IReadOnlyList<CpSocialTenantOption>> LoadTenantsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = new List<CpSocialTenantOption>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT `site_key`, IFNULL(`hostname`,''), IFNULL(`trade_name`,'') FROM `epc_portal_tenants` "
            + "WHERE IFNULL(`site_key`, '') <> '' AND COALESCE(`is_active`, 1) = 1 ORDER BY `hostname` ASC";
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CpSocialTenantOption(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }

        return rows;
    }

    /// <summary>PHP <c>epc_social_brand_context()</c> for the resolved site key.</summary>
    private static async Task<SocialBrandContext> LoadBrandAsync(
        DbConnection connection,
        string siteKey,
        CancellationToken cancellationToken)
    {
        var fallback = DefaultBrand(siteKey);
        if (fallback.IsPlatform)
        {
            return fallback;
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT IFNULL(`trade_name`,''), IFNULL(`hub_name`,''), IFNULL(`hostname`,''), IFNULL(`industry_code`,'') "
            + "FROM `epc_portal_tenants` WHERE `site_key` = @p0 LIMIT 1";
        ErpDb.AddParameters(cmd, [siteKey]);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return fallback;
        }

        var tradeName = reader.GetString(0).Trim();
        var hubName = reader.GetString(1).Trim();
        var brandName = tradeName.Length > 0 ? tradeName : (hubName.Length > 0 ? hubName : siteKey);
        var hostname = reader.GetString(2).Trim().ToLowerInvariant();
        var industry = reader.GetString(3).Trim();
        var website = fallback.Website;
        var domain = fallback.Domain;
        if (hostname.Length > 0)
        {
            domain = hostname.StartsWith("www.", StringComparison.Ordinal) ? hostname[4..] : hostname;
            website = "https://www." + domain;
        }

        return fallback with
        {
            BrandName = brandName,
            Handle = SocialMediaPackCatalog.Handle(brandName, siteKey),
            Website = website,
            Domain = domain,
            Industry = industry.Length > 0 ? industry : "auto_parts",
        };
    }

    private static async Task<IReadOnlyList<CpSocialAccount>> LoadAccountsAsync(
        DbConnection connection,
        string siteKey,
        CancellationToken cancellationToken)
    {
        var accounts = new List<CpSocialAccount>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT `id`, `platform`, IFNULL(`account_label`,''), IFNULL(`username`,''), IFNULL(`status`,'pending'), "
            + "IFNULL(`last_test_at`,0), IFNULL(`last_test_ok`,0), IFNULL(`encrypted_credentials`,'') "
            + "FROM `epc_social_accounts` WHERE `site_key` = @p0 ORDER BY `platform`";
        ErpDb.AddParameters(cmd, [siteKey]);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var credentials = ParseCredentials(reader.GetString(7), siteKey);
            accounts.Add(new CpSocialAccount(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                Convert.ToInt64(reader.GetValue(5), CultureInfo.InvariantCulture),
                Convert.ToInt32(reader.GetValue(6), CultureInfo.InvariantCulture) == 1,
                credentials.PageId,
                credentials.IgUserId,
                credentials.OpenId,
                credentials.PrivacyLevel,
                credentials.HasToken));
        }

        return accounts;
    }

    /// <summary>PHP <c>epc_social_account_public_meta()</c> — identifiers only, never the token itself.</summary>
    public static (string PageId, string IgUserId, string OpenId, string PrivacyLevel, bool HasToken) ParseCredentials(
        string? encrypted,
        string siteKey)
    {
        var plain = CpSocialCrypto.Decrypt(encrypted, siteKey);
        if (plain.Length == 0)
        {
            return (string.Empty, string.Empty, string.Empty, "SELF_ONLY", false);
        }

        try
        {
            using var doc = JsonDocument.Parse(plain);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (string.Empty, string.Empty, string.Empty, "SELF_ONLY", false);
            }

            return (
                Text(root, "page_id"),
                Text(root, "ig_user_id"),
                Text(root, "open_id"),
                Text(root, "privacy_level") is { Length: > 0 } privacy ? privacy : "SELF_ONLY",
                Text(root, "access_token").Length > 0 || Text(root, "api_key").Length > 0);
        }
        catch (JsonException)
        {
            return (string.Empty, string.Empty, string.Empty, "SELF_ONLY", false);
        }
    }

    private static string Text(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static async Task<IReadOnlyList<CpSocialDraft>> LoadDraftsAsync(
        DbConnection connection,
        string siteKey,
        CancellationToken cancellationToken)
    {
        var drafts = new List<CpSocialDraft>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT `id`, IFNULL(`platform`,''), IFNULL(`title`,''), IFNULL(`caption`,''), IFNULL(`hashtags`,''), "
            + "IFNULL(`media_url`,''), IFNULL(`status`,'draft'), IFNULL(`scheduled_at`,0), IFNULL(`external_post_id`,''), "
            + "IFNULL(`published_at`,0), IFNULL(`last_error`,''), IFNULL(`updated_at`,0) "
            + "FROM `epc_social_post_drafts` WHERE `site_key` = @p0 ORDER BY `updated_at` DESC LIMIT 20";
        ErpDb.AddParameters(cmd, [siteKey]);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            drafts.Add(new CpSocialDraft(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                Convert.ToInt64(reader.GetValue(7), CultureInfo.InvariantCulture),
                reader.GetString(8),
                Convert.ToInt64(reader.GetValue(9), CultureInfo.InvariantCulture),
                reader.GetString(10),
                Convert.ToInt64(reader.GetValue(11), CultureInfo.InvariantCulture)));
        }

        return drafts;
    }
}
