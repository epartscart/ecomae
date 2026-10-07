using System.Data.Common;
using System.Text.Json.Serialization;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public sealed record BroadcastBody(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("message")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Message = null);

    public sealed record BroadcastCountBody(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("count")] int Count);

    public sealed record BroadcastEmailPreviewBody(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("preview")] string Preview,
        [property: JsonPropertyName("body_html")] string BodyHtml);

    public sealed record BroadcastWhatsappPreviewBody(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("body_text")] string BodyText);

    public static async Task<object> MarketingBroadcastAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        IReadOnlyDictionary<string, string> fields,
        ICpMarketingBroadcastService broadcasts,
        CancellationToken cancellationToken)
    {
        var denied = await StaffAsync(
            connection,
            adminSession,
            adminUser,
            new CodedJson(403, new BroadcastBody(false, "Forbidden")),
            cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied is FlagBody flag ? new BroadcastBody(false, flag.Message) : denied;
        }

        var action = OmsField(fields, "action").Trim().ToLowerInvariant();
        if (action == "count_recipients")
        {
            var count = await broadcasts.CountRecipientsAsync(
                OmsField(fields, "audience_mode"),
                OmsField(fields, "audience_meta"),
                OmsField(fields, "channel"),
                cancellationToken).ConfigureAwait(false);
            return new BroadcastCountBody(true, count);
        }

        if (action == "template_preview")
        {
            var channel = OmsField(fields, "channel").Trim().ToLowerInvariant();
            var key = OmsField(fields, "template_key");
            if (channel == "whatsapp")
            {
                var template = MarketingBroadcastCatalog.WhatsappTemplate(key.Length == 0 ? "blank" : key);
                return new BroadcastWhatsappPreviewBody(true, template.Body);
            }

            var email = MarketingBroadcastCatalog.EmailTemplate(key.Length == 0 ? "blank" : key);
            return new BroadcastEmailPreviewBody(true, email.Subject, email.Preview, email.Html);
        }

        return new BroadcastBody(false, "Unknown action");
    }
}
