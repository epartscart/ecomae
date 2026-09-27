using System.Data.Common;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Cp;

public sealed record CpMarketingSendRequest(
    string? Channel,
    string? TemplateKey,
    string? Subject,
    string? Preview,
    string? BodyHtml,
    string? BodyText,
    string? AudienceMode,
    string? AudienceMeta,
    string? AudienceMetaGroup,
    string? AudienceMetaManual,
    string? BatchLimit,
    int OperatorId,
    string? RequestHost);

public sealed record CpMarketingWaLink(string Name, string Phone, string Link);

public sealed record CpMarketingSendResult(
    bool Succeeded,
    string Code,
    string Message,
    long CampaignId,
    int SentOk,
    int SentFail,
    IReadOnlyList<CpMarketingWaLink> WaLinks)
{
    public static CpMarketingSendResult Fail(string code, string message)
        => new(false, code, message, 0, 0, 0, []);
}

/// <summary>
/// Live twin of the PHP campaign senders
/// (<c>epc_mb_send_email_campaign</c> / <c>epc_mb_send_whatsapp_campaign</c> plus
/// <c>epc_mb_create_campaign</c>, <c>epc_mb_log_recipient</c>, <c>epc_mb_update_campaign_counts</c>).
/// WhatsApp Cloud API delivery stays on the Classic twin; here the API mode is reported, not faked.
/// </summary>
public interface ICpMarketingBroadcastWriteService
{
    Task<CpMarketingSendResult> SendEmailCampaignAsync(CpMarketingSendRequest request, CancellationToken cancellationToken = default);

    Task<CpMarketingSendResult> SendWhatsappCampaignAsync(CpMarketingSendRequest request, CancellationToken cancellationToken = default);
}

public sealed class CpMarketingBroadcastWriteService : ICpMarketingBroadcastWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly ICpTenantEmailWriteService _email;

    public CpMarketingBroadcastWriteService(IErpWriteConnectionFactory connections, ICpTenantEmailWriteService email)
    {
        _connections = connections;
        _email = email;
    }

    public async Task<CpMarketingSendResult> SendEmailCampaignAsync(
        CpMarketingSendRequest request,
        CancellationToken cancellationToken = default)
    {
        var mode = CpMarketingBroadcastRecipients.NormalizeMode(request.AudienceMode);
        var meta = CpMarketingBroadcastRecipients.ResolveMeta(mode, request.AudienceMeta, request.AudienceMetaGroup, request.AudienceMetaManual);
        var template = MarketingBroadcastCatalog.EmailTemplate(request.TemplateKey);
        var subject = (request.Subject ?? string.Empty).Trim();
        var preview = (request.Preview ?? string.Empty).Trim();
        var html = request.BodyHtml ?? string.Empty;
        if (html.Length == 0)
        {
            subject = subject.Length > 0 ? subject : template.Subject;
            preview = preview.Length > 0 ? preview : template.Preview;
            html = template.Html;
        }

        if (subject.Length == 0 || html.Length == 0)
        {
            return CpMarketingSendResult.Fail("invalid", "Subject and HTML body are required.");
        }

        if (!_connections.IsConfigured)
        {
            return CpMarketingSendResult.Fail("db", "Tenant database is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await CpMarketingBroadcastRecipients.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

            var recipients = await CpMarketingBroadcastRecipients
                .ResolveAsync(connection, mode, meta, "email", cancellationToken).ConfigureAwait(false);
            if (recipients.Count == 0)
            {
                return CpMarketingSendResult.Fail("empty", "No recipients with valid email addresses.");
            }

            var batch = CpMarketingBroadcastRecipients.NormalizeBatchLimit(request.BatchLimit);
            recipients = [.. recipients.Take(batch)];

            var shopName = await ShopNameAsync(connection, cancellationToken).ConfigureAwait(false);
            var shopUrl = CpMarketingBroadcastRecipients.ShopUrlFromHost(request.RequestHost);

            var campaignId = await CreateCampaignAsync(
                connection,
                "email",
                template.Key,
                subject,
                preview,
                html,
                string.Empty,
                mode,
                meta,
                recipients.Count,
                request.OperatorId,
                cancellationToken).ConfigureAwait(false);

            var ok = 0;
            var fail = 0;
            foreach (var recipient in recipients)
            {
                var name = recipient.Name.Length > 0 ? recipient.Name : "Customer";
                var subj = MarketingBroadcastCatalog.ApplyVars(subject, name, shopName, shopUrl);
                var body = MarketingBroadcastCatalog.ApplyVars(html, name, shopName, shopUrl);
                var result = await _email
                    .SendAsync(new CpTenantEmailMessage(recipient.Email, subj, body), cancellationToken)
                    .ConfigureAwait(false);
                if (result.Succeeded)
                {
                    ok++;
                }
                else
                {
                    fail++;
                }

                await LogRecipientAsync(
                    connection,
                    campaignId,
                    recipient,
                    result.Succeeded,
                    result.Message,
                    string.Empty,
                    cancellationToken).ConfigureAwait(false);
            }

            await UpdateCountsAsync(connection, campaignId, ok, fail, cancellationToken).ConfigureAwait(false);
            return new CpMarketingSendResult(
                true,
                "ok",
                $"Email campaign sent: {ok} OK, {fail} failed (batch limit {batch}).",
                campaignId,
                ok,
                fail,
                []);
        }
        catch (DbException ex)
        {
            return CpMarketingSendResult.Fail("db", ex.Message);
        }
    }

    public async Task<CpMarketingSendResult> SendWhatsappCampaignAsync(
        CpMarketingSendRequest request,
        CancellationToken cancellationToken = default)
    {
        var mode = CpMarketingBroadcastRecipients.NormalizeMode(request.AudienceMode);
        var meta = CpMarketingBroadcastRecipients.ResolveMeta(mode, request.AudienceMeta, request.AudienceMetaGroup, request.AudienceMetaManual);
        var template = MarketingBroadcastCatalog.WhatsappTemplate(request.TemplateKey);
        var bodyText = (request.BodyText ?? string.Empty).Trim();
        if (bodyText.Length == 0)
        {
            bodyText = template.Body;
        }

        if (bodyText.Length == 0)
        {
            return CpMarketingSendResult.Fail("invalid", "WhatsApp message body is required.");
        }

        if (!_connections.IsConfigured)
        {
            return CpMarketingSendResult.Fail("db", "Tenant database is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await CpMarketingBroadcastRecipients.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

            var recipients = await CpMarketingBroadcastRecipients
                .ResolveAsync(connection, mode, meta, "whatsapp", cancellationToken).ConfigureAwait(false);
            if (recipients.Count == 0)
            {
                return CpMarketingSendResult.Fail("empty", "No recipients with phone numbers.");
            }

            var batch = CpMarketingBroadcastRecipients.NormalizeBatchLimit(request.BatchLimit);
            recipients = [.. recipients.Take(batch)];

            var shopName = await ShopNameAsync(connection, cancellationToken).ConfigureAwait(false);
            var shopUrl = CpMarketingBroadcastRecipients.ShopUrlFromHost(request.RequestHost);

            var campaignId = await CreateCampaignAsync(
                connection,
                "whatsapp",
                template.Key,
                string.Empty,
                string.Empty,
                string.Empty,
                bodyText,
                mode,
                meta,
                recipients.Count,
                request.OperatorId,
                cancellationToken).ConfigureAwait(false);

            var ok = 0;
            var links = new List<CpMarketingWaLink>();
            foreach (var recipient in recipients)
            {
                var name = recipient.Name.Length > 0 ? recipient.Name : "Customer";
                var text = MarketingBroadcastCatalog.ApplyVars(bodyText, name, shopName, shopUrl);
                var link = CpMarketingBroadcastRecipients.WhatsappShareUrl(recipient.Phone, text);
                ok++;
                await LogRecipientAsync(
                    connection,
                    campaignId,
                    recipient,
                    true,
                    "wa.me link prepared",
                    link,
                    cancellationToken).ConfigureAwait(false);
                if (link.Length > 0)
                {
                    links.Add(new CpMarketingWaLink(name, recipient.Phone, link));
                }
            }

            await UpdateCountsAsync(connection, campaignId, ok, 0, cancellationToken).ConfigureAwait(false);
            return new CpMarketingSendResult(
                true,
                "ok",
                $"WhatsApp wa.me links prepared for {ok} customers (open links to send manually).",
                campaignId,
                ok,
                0,
                links);
        }
        catch (DbException ex)
        {
            return CpMarketingSendResult.Fail("db", ex.Message);
        }
    }

    private static async Task<string> ShopNameAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            var name = (await ErpDb.StringAsync(
                connection,
                null,
                "SELECT `value` FROM `config_items` WHERE `name` = @p0 LIMIT 1",
                cancellationToken,
                "site_name").ConfigureAwait(false) ?? string.Empty).Trim();
            return name.Length > 0 ? name : "Store";
        }
        catch (DbException)
        {
            return "Store";
        }
    }

    private static async Task<long> CreateCampaignAsync(
        DbConnection connection,
        string channel,
        string templateKey,
        string subject,
        string preview,
        string bodyHtml,
        string bodyText,
        string audienceMode,
        string audienceMeta,
        int totalTargets,
        int operatorId,
        CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_marketing_broadcast_campaigns` "
                + "(`created_at`, `channel`, `template_key`, `subject`, `preview`, `body_html`, `body_text`, "
                + "`audience_mode`, `audience_meta`, `total_targets`, `status`, `operator_id`) "
                + "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            channel,
            Cut(templateKey, 64),
            Cut(subject, 255),
            Cut(preview, 500),
            bodyHtml,
            bodyText,
            Cut(audienceMode, 32),
            Cut(audienceMeta, 255),
            totalTargets,
            "sending",
            operatorId).ConfigureAwait(false);

        return await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    private static Task LogRecipientAsync(
        DbConnection connection,
        long campaignId,
        CpMarketingRecipient recipient,
        bool ok,
        string detail,
        string waLink,
        CancellationToken cancellationToken)
    {
        var address = recipient.Email.Length > 0 ? recipient.Email : recipient.Phone;
        return ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_marketing_broadcast_log` "
                + "(`campaign_id`, `created_at`, `recipient`, `user_id`, `status`, `detail`, `wa_link`) "
                + "VALUES (?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            campaignId,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Cut(address, 255),
            recipient.UserId,
            ok ? 1 : 0,
            Cut(detail, 500),
            Cut(waLink, 500));
    }

    private static Task UpdateCountsAsync(
        DbConnection connection,
        long campaignId,
        int ok,
        int fail,
        CancellationToken cancellationToken)
        => ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_marketing_broadcast_campaigns` SET `sent_ok` = ?, `sent_fail` = ?, `status` = ? WHERE `id` = ?"),
            cancellationToken,
            ok,
            fail,
            "completed",
            campaignId);

    private static string Cut(string value, int length)
        => value.Length <= length ? value : value[..length];
}
