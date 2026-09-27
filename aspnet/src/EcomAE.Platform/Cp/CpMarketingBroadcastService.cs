using System.Data.Common;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>PHP <c>epc_mb_dashboard_stats()</c>.</summary>
public sealed record CpMarketingBroadcastStats(
    int EmailRecipients,
    int WhatsappRecipients,
    int EmailsSent,
    int WhatsappSent,
    int Campaigns);

/// <summary>PHP <c>epc_marketing_broadcast_campaigns</c> row.</summary>
public sealed record CpMarketingCampaign(
    long Id,
    long CreatedAt,
    string Channel,
    string TemplateKey,
    string Subject,
    string AudienceMode,
    int TotalTargets,
    int SentOk,
    int SentFail,
    string Status);

public sealed record CpMarketingGroup(int Id, string Name);

/// <summary>PHP <c>epc_mb_shop_context()</c>.</summary>
public sealed record CpMarketingShopContext(string ShopName, string ShopUrl);

public sealed record CpMarketingBroadcastView(
    CpMarketingShopContext Shop,
    CpMarketingBroadcastStats Stats,
    IReadOnlyList<CpMarketingCampaign> Campaigns,
    IReadOnlyList<CpMarketingGroup> Groups,
    bool SmtpReady,
    IReadOnlyList<string> SmtpIssues,
    bool WhatsappApiEnabled,
    string Source)
{
    public static CpMarketingBroadcastView Empty(string source, string shopName, string shopUrl)
        => new(
            new CpMarketingShopContext(shopName, shopUrl),
            new CpMarketingBroadcastStats(0, 0, 0, 0, 0),
            [],
            [],
            false,
            ["Tenant SMTP is not readable."],
            false,
            source);
}

/// <summary>
/// Live twin of the PHP marketing broadcast loader
/// (<c>epc_mb_dashboard_stats</c>, <c>epc_mb_list_campaigns</c>, <c>epc_mb_list_groups</c>,
/// <c>epc_mb_shop_context</c>, <c>epc_auth_smtp_diagnose</c>, <c>epc_wa_api_enabled</c>).
/// </summary>
public interface ICpMarketingBroadcastService
{
    Task<CpMarketingBroadcastView> LoadAsync(string? host, CancellationToken cancellationToken = default);

    Task<int> CountRecipientsAsync(string mode, string meta, string channel, CancellationToken cancellationToken = default);
}

public sealed class CpMarketingBroadcastService : ICpMarketingBroadcastService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpMarketingBroadcastService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpMarketingBroadcastView> LoadAsync(string? host, CancellationToken cancellationToken = default)
    {
        var fallbackUrl = CpMarketingBroadcastRecipients.ShopUrlFromHost(host);
        if (!_connections.IsConfigured)
        {
            return CpMarketingBroadcastView.Empty("unconfigured", "Store", fallbackUrl);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await CpMarketingBroadcastRecipients.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

            var shopName = await ConfigValueAsync(connection, "site_name", cancellationToken).ConfigureAwait(false);
            var shop = new CpMarketingShopContext(shopName.Length > 0 ? shopName : "Store", fallbackUrl);

            var emailRecipients = await CpMarketingBroadcastRecipients.CountAsync(connection, "all", string.Empty, "email", cancellationToken).ConfigureAwait(false);
            var waRecipients = await CpMarketingBroadcastRecipients.CountAsync(connection, "all", string.Empty, "whatsapp", cancellationToken).ConfigureAwait(false);
            var emailsSent = (int)await ErpDb.LongAsync(
                connection,
                null,
                "SELECT IFNULL(SUM(`sent_ok`),0) FROM `epc_marketing_broadcast_campaigns` WHERE `channel` = 'email'",
                cancellationToken).ConfigureAwait(false);
            var waSent = (int)await ErpDb.LongAsync(
                connection,
                null,
                "SELECT IFNULL(SUM(`sent_ok`),0) FROM `epc_marketing_broadcast_campaigns` WHERE `channel` = 'whatsapp'",
                cancellationToken).ConfigureAwait(false);
            var campaignCount = (int)await ErpDb.LongAsync(
                connection,
                null,
                "SELECT COUNT(*) FROM `epc_marketing_broadcast_campaigns`",
                cancellationToken).ConfigureAwait(false);

            var campaigns = await ListCampaignsAsync(connection, 15, cancellationToken).ConfigureAwait(false);
            var groups = await ListGroupsAsync(connection, cancellationToken).ConfigureAwait(false);
            var (smtpReady, issues) = await DiagnoseSmtpAsync(connection, cancellationToken).ConfigureAwait(false);
            var waApi = await WhatsappApiEnabledAsync(connection, cancellationToken).ConfigureAwait(false);

            return new CpMarketingBroadcastView(
                shop,
                new CpMarketingBroadcastStats(emailRecipients, waRecipients, emailsSent, waSent, campaignCount),
                campaigns,
                groups,
                smtpReady,
                issues,
                waApi,
                "db");
        }
        catch (DbException)
        {
            return CpMarketingBroadcastView.Empty("db-error", "Store", fallbackUrl);
        }
    }

    public async Task<int> CountRecipientsAsync(string mode, string meta, string channel, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return 0;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            return await CpMarketingBroadcastRecipients.CountAsync(connection, mode, meta, channel, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return 0;
        }
    }

    private static async Task<string> ConfigValueAsync(DbConnection connection, string name, CancellationToken cancellationToken)
    {
        try
        {
            return (await ErpDb.StringAsync(
                connection,
                null,
                "SELECT `value` FROM `config_items` WHERE `name` = @p0 LIMIT 1",
                cancellationToken,
                name).ConfigureAwait(false) ?? string.Empty).Trim();
        }
        catch (DbException)
        {
            return string.Empty;
        }
    }

    /// <summary>PHP <c>epc_wa_api_enabled()</c> — flag plus token and phone id.</summary>
    private static async Task<bool> WhatsappApiEnabledAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var enabled = await ConfigValueAsync(connection, "epc_whatsapp_api_enabled", cancellationToken).ConfigureAwait(false);
        if (enabled != "1")
        {
            return false;
        }

        var token = await ConfigValueAsync(connection, "epc_whatsapp_api_token", cancellationToken).ConfigureAwait(false);
        var phoneId = await ConfigValueAsync(connection, "epc_whatsapp_phone_number_id", cancellationToken).ConfigureAwait(false);
        return token.Length > 0 && phoneId.Length > 0;
    }

    /// <summary>PHP <c>epc_auth_smtp_diagnose()</c> shape over the tenant SMTP settings this app writes.</summary>
    private static async Task<(bool Ok, IReadOnlyList<string> Issues)> DiagnoseSmtpAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        string raw;
        try
        {
            raw = await ErpDb.StringAsync(
                connection,
                null,
                "SELECT IFNULL(`integrations_json`, '') FROM `epc_portal_site_settings` ORDER BY `id` ASC LIMIT 1",
                cancellationToken).ConfigureAwait(false) ?? string.Empty;
        }
        catch (DbException)
        {
            return (false, ["Tenant SMTP settings row is missing."]);
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return (false, ["Tenant SMTP is not configured."]);
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("smtp", out var smtp) || smtp.ValueKind != JsonValueKind.Object)
            {
                return (false, ["Tenant SMTP is not configured."]);
            }

            var issues = new List<string>();
            var useTenant = smtp.TryGetProperty("use_tenant_smtp", out var useEl)
                && (useEl.ValueKind == JsonValueKind.True
                    || (useEl.ValueKind == JsonValueKind.Number && useEl.GetInt32() != 0)
                    || (useEl.ValueKind == JsonValueKind.String && useEl.GetString() is "1" or "true"));
            if (!useTenant)
            {
                issues.Add("Use tenant SMTP is off.");
            }

            if (Value(smtp, "smtp_host").Length == 0)
            {
                issues.Add("SMTP host is empty.");
            }

            if (Value(smtp, "smtp_username").Length == 0)
            {
                issues.Add("SMTP username is empty.");
            }

            if (Value(smtp, "smtp_password").Length == 0)
            {
                issues.Add("SMTP password is empty.");
            }

            if (Value(smtp, "from_email").Length == 0)
            {
                issues.Add("From address is empty.");
            }

            return (issues.Count == 0, issues);
        }
        catch (JsonException)
        {
            return (false, ["Tenant SMTP settings are not valid JSON."]);
        }

        static string Value(JsonElement smtp, string name)
            => smtp.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
                ? (el.GetString() ?? string.Empty).Trim()
                : string.Empty;
    }

    private static async Task<IReadOnlyList<CpMarketingCampaign>> ListCampaignsAsync(
        DbConnection connection,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = new List<CpMarketingCampaign>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            "SELECT `id`, `created_at`, `channel`, IFNULL(`template_key`,''), IFNULL(`subject`,''), IFNULL(`audience_mode`,''), "
            + "`total_targets`, `sent_ok`, `sent_fail`, IFNULL(`status`,'') "
            + "FROM `epc_marketing_broadcast_campaigns` ORDER BY `id` DESC LIMIT " + Math.Clamp(limit, 1, 50).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CpMarketingCampaign(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                reader.GetInt32(8),
                reader.GetString(9)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<CpMarketingGroup>> ListGroupsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var rows = new List<CpMarketingGroup>();
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT `id`, IFNULL(`value`,'') AS `name` FROM `groups` ORDER BY `id` ASC";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = reader.GetInt32(0);
                var name = reader.GetString(1);
                rows.Add(new CpMarketingGroup(id, name.Length > 0 ? name : "Group #" + id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
        }
        catch (DbException)
        {
            return [];
        }

        return rows;
    }
}
