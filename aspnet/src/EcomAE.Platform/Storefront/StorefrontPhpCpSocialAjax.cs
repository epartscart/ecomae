using System.Data.Common;
using System.Text.Json.Serialization;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string SocialCaptionStaysClassic = "Caption generation stays on the classic helper.";
    public const string SocialPublishStaysClassic = "Publishing to Meta/TikTok stays Classic.";

    public sealed record SocialBody(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("message")] string Message);

    public static async Task<object> SocialMediaAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrfToken,
        IReadOnlyDictionary<string, string> fields,
        string? requestHost,
        ICpSocialHubWriteService social,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(connection, adminSession, adminUser, new SocialBody(false, "Admin required"), cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied is FlagBody flag ? new SocialBody(false, flag.Message) : denied;
        }

        if (!await SocialCsrfMatchesAsync(connection, adminSession, adminUser, csrfToken, cancellationToken).ConfigureAwait(false))
        {
            return new SocialBody(false, "CSRF failed");
        }

        var action = OmsField(fields, "action").Trim().ToLowerInvariant();
        if (action == "generate_caption")
        {
            return new SocialBody(false, SocialCaptionStaysClassic);
        }

        if (action is "test_account" or "publish_draft" or "publish_now")
        {
            return new SocialBody(false, SocialPublishStaysClassic);
        }

        if (action == "save_draft")
        {
            var written = await social.SaveDraftAsync(
                new CpSocialHubSaveDraftRequest(
                    ParseId(OmsField(fields, "id")),
                    OmsField(fields, "site_key"),
                    OmsField(fields, "platform"),
                    OmsField(fields, "title"),
                    OmsField(fields, "caption"),
                    OmsField(fields, "hashtags"),
                    OmsField(fields, "media_url"),
                    false,
                    requestHost),
                cancellationToken).ConfigureAwait(false);
            return new SocialBody(written.Succeeded, written.Message);
        }

        return new SocialBody(false, "Unknown action");
    }

    private static async Task<bool> SocialCsrfMatchesAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrfToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(csrfToken))
        {
            return false;
        }

        var stored = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT IFNULL(`csrf_guard_key`, '') FROM `sessions` WHERE `session` = ? AND `type` = 1 AND `user_id` = ? LIMIT 1"),
            cancellationToken,
            adminSession ?? string.Empty,
            ParseId(adminUser)).ConfigureAwait(false) ?? string.Empty;
        return string.Equals(stored, csrfToken, StringComparison.Ordinal);
    }
}
