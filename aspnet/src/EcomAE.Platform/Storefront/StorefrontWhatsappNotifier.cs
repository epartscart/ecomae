using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Storefront;

/// <summary>One channel outcome of a PHP dispatch person (<c>contacts.phone</c> / <c>contacts.whatsapp</c>).</summary>
public sealed record StorefrontNotifyChannel(bool TriedToSend, bool Status, string Error)
{
    public static StorefrontNotifyChannel None { get; } = new(false, false, string.Empty);
}

/// <summary>The <c>notifications_settings</c> fields PHP <c>epc_wa_should_send_for_person()</c> reads.</summary>
public sealed record StorefrontWhatsappNotify(string Name, bool EmailOn, bool SmsOn);

/// <summary>
/// PHP content/notifications/epc_whatsapp_notify.php: the Meta WhatsApp Cloud API fan-out of
/// <c>docpart_dispatch_notification()</c>. Settings come from <c>config.php</c> (<c>epc_whatsapp_api_enabled</c>,
/// <c>epc_whatsapp_api_token</c>, <c>epc_whatsapp_phone_number_id</c>, <c>epc_whatsapp_api_version</c>,
/// <c>epc_whatsapp_notify_names</c>, <c>epc_whatsapp_bilingual_notify</c>); every attempt is written to
/// <c>epc_whatsapp_notify_log</c>.
/// </summary>
public interface IStorefrontWhatsappNotifier
{
    Task<StorefrontNotifyChannel> DispatchForPersonAsync(
        DbConnection connection,
        IReadOnlyDictionary<string, string> config,
        StorefrontWhatsappNotify notify,
        IReadOnlyDictionary<string, string> vars,
        string smsBody,
        string emailBody,
        string phone,
        CancellationToken cancellationToken = default);
}

public sealed class StorefrontWhatsappNotifier : IStorefrontWhatsappNotifier
{
    public const string HttpClientName = "epc-whatsapp";
    public const int MaxBodyBytes = 3500;

    private const string DefaultNotifyNames =
        "new_order_to_user,new_order_to_manager,order_status_to_customer,order_status_to_manager,order_message_to_customer,order_message_to_manager,epc_customer_login";

    private static readonly Regex Tags = new("<!--.*?-->|<[^>]*>", RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex Blanks = new("[ \t]+", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ManyNewlines = new("\n{3,}", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex NonDigits = new("[^0-9]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IHttpClientFactory _clients;

    public StorefrontWhatsappNotifier(IHttpClientFactory clients)
    {
        _clients = clients;
    }

    /// <summary>PHP <c>epc_wa_cfg()</c>: a missing or empty config value yields the default.</summary>
    public static string Cfg(IReadOnlyDictionary<string, string> config, string key, string fallback = "")
        => config.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : fallback;

    /// <summary>PHP <c>epc_wa_api_enabled()</c>.</summary>
    public static bool ApiEnabled(IReadOnlyDictionary<string, string> config)
        => Cfg(config, "epc_whatsapp_api_enabled", "0") == "1"
           && Cfg(config, "epc_whatsapp_api_token").Length > 0
           && Cfg(config, "epc_whatsapp_phone_number_id").Length > 0;

    /// <summary>PHP <c>epc_wa_notify_names()</c>.</summary>
    public static IReadOnlyList<string> NotifyNames(IReadOnlyDictionary<string, string> config)
    {
        var parts = Cfg(config, "epc_whatsapp_notify_names", DefaultNotifyNames)
            .Split(',')
            .Select(p => p.Trim(' ', '\t', '\n', '\r', '\0', '\x0B'))
            .Where(p => p.Length > 0 && p != "0")
            .ToList();
        return parts.Count > 0 ? parts : ["new_order_to_user", "order_status_to_customer"];
    }

    /// <summary>PHP <c>epc_wa_should_send_for_person()</c>; status-ref suppression only applies to order status notifications.</summary>
    public static bool ShouldSend(StorefrontWhatsappNotify notify, IReadOnlyDictionary<string, string> config, bool hasPhone)
        => hasPhone
           && ApiEnabled(config)
           && (notify.EmailOn || notify.SmsOn)
           && NotifyNames(config).Contains(notify.Name, StringComparer.Ordinal);

    /// <summary>PHP <c>epc_wa_plain_from_html()</c>.</summary>
    public static string PlainFromHtml(string html)
    {
        var text = WebUtility.HtmlDecode(Tags.Replace(html ?? string.Empty, string.Empty));
        text = Blanks.Replace(text, " ");
        text = ManyNewlines.Replace(text, "\n\n");
        return PhpTrim(text);
    }

    /// <summary>PHP <c>epc_wa_digits()</c>.</summary>
    public static string Digits(string phone) => NonDigits.Replace(phone ?? string.Empty, string.Empty);

    /// <summary>PHP <c>epc_wa_notify_ar_snippet()</c>.</summary>
    public static string ArabicSnippet(string notifyName, IReadOnlyDictionary<string, string> vars, string siteName)
    {
        var orderId = vars.TryGetValue("order_id", out var raw) ? PhpInt(raw) : 0;
        if (notifyName.Contains("order", StringComparison.Ordinal) && orderId > 0)
        {
            return "مرحباً من " + siteName + " — طلب #" + orderId.ToString(CultureInfo.InvariantCulture) + ". للاستفسار ردّوا على هذه الرسالة.";
        }

        if (notifyName == "epc_customer_login")
        {
            return "تسجيل دخول عميل — " + siteName;
        }

        return "رسالة من " + siteName + ".";
    }

    /// <summary>PHP <c>epc_wa_build_notify_body()</c>: SMS text, else the plain e-mail, else the order text, else a stub; then the Arabic line.</summary>
    public static string BuildBody(
        string smsBody,
        string emailBody,
        string notifyName,
        IReadOnlyDictionary<string, string> vars,
        string siteName,
        bool bilingual)
    {
        var body = PhpTrim(smsBody ?? string.Empty);
        if (body.Length == 0)
        {
            body = PlainFromHtml(emailBody ?? string.Empty);
        }

        if (body.Length == 0 && vars.TryGetValue("order_text", out var orderText) && PhpNonEmpty(orderText))
        {
            body = PlainFromHtml(orderText);
        }

        if (body.Length == 0 && vars.TryGetValue("order_id", out var orderIdRaw) && PhpNonEmpty(orderIdRaw))
        {
            body = siteName + " — order #" + PhpInt(orderIdRaw).ToString(CultureInfo.InvariantCulture);
        }

        if (Encoding.UTF8.GetByteCount(body) > MaxBodyBytes)
        {
            body = Utf8Prefix(body, MaxBodyBytes - 1) + "…";
        }

        if (bilingual && body.Length > 0)
        {
            var ar = ArabicSnippet(notifyName, vars, siteName);
            if (ar.Length > 0)
            {
                body = Bilingual(body, ar);
            }
        }

        return body;
    }

    /// <summary>PHP <c>epc_wa_bilingual()</c>.</summary>
    public static string Bilingual(string en, string ar)
    {
        en = PhpTrim(en);
        ar = PhpTrim(ar);
        if (en.Length == 0)
        {
            return ar;
        }

        return ar.Length == 0 ? en : en + "\n" + ar;
    }

    /// <summary>
    /// PHP <c>epc_wa_site_name()</c> → <c>epc_brand_trade_name()</c>: the site contact <c>trade_name</c>, then the
    /// site <c>hub_name</c>, then <c>ecomae</c> (the PHP hub default, so <c>from_name</c> is never reached).
    /// </summary>
    public static async Task<string> SiteNameAsync(DbConnection connection, IReadOnlyDictionary<string, string> config, CancellationToken cancellationToken)
    {
        var host = StorefrontOrderNotificationService.HostOf(config.TryGetValue("domain_path", out var d) ? d : string.Empty);
        try
        {
            var aliases = PlatformHostPolicy.NormalizeHostAliases(host);
            if (aliases.Count > 0)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM `epc_portal_site_settings` WHERE `host` IN ("
                    + string.Join(", ", aliases.Select((_, i) => "@p" + i.ToString(CultureInfo.InvariantCulture)))
                    + ") ORDER BY `id` ASC LIMIT 1";
                ErpDb.AddParameters(command, aliases.Cast<object?>().ToArray());
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var contact = CpIndustrySettingsService.ParseContact(Column(reader, "contact_json"), null);
                    if (contact.TradeName.Length > 0)
                    {
                        return contact.TradeName;
                    }

                    var hub = Column(reader, "hub_name");
                    if (hub.Length > 0)
                    {
                        return hub;
                    }
                }
            }
        }
        catch (DbException)
        {
        }

        return "ecomae";
    }

    public async Task<StorefrontNotifyChannel> DispatchForPersonAsync(
        DbConnection connection,
        IReadOnlyDictionary<string, string> config,
        StorefrontWhatsappNotify notify,
        IReadOnlyDictionary<string, string> vars,
        string smsBody,
        string emailBody,
        string phone,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldSend(notify, config, phone.Length > 0))
        {
            return StorefrontNotifyChannel.None;
        }

        var siteName = await SiteNameAsync(connection, config, cancellationToken).ConfigureAwait(false);
        var body = BuildBody(smsBody, emailBody, notify.Name, vars, siteName, Cfg(config, "epc_whatsapp_bilingual_notify", "1") == "1");
        if (body.Length == 0)
        {
            return StorefrontNotifyChannel.None;
        }

        var (ok, response, error) = await SendTextAsync(phone, body, config, cancellationToken).ConfigureAwait(false);
        await LogAttemptAsync(connection, notify.Name, phone, ok, body, response, cancellationToken).ConfigureAwait(false);
        return new StorefrontNotifyChannel(true, ok, error);
    }

    /// <summary>PHP <c>epc_wa_api_send_text()</c>: Graph API <c>/{version}/{phone_number_id}/messages</c> text message.</summary>
    private async Task<(bool Ok, string ResponseJson, string Error)> SendTextAsync(
        string phone,
        string text,
        IReadOnlyDictionary<string, string> config,
        CancellationToken cancellationToken)
    {
        var digits = Digits(phone);
        if (digits.Length == 0 || PhpTrim(text).Length == 0)
        {
            return (false, "[]", "empty phone or body");
        }

        var token = Cfg(config, "epc_whatsapp_api_token");
        var phoneId = Cfg(config, "epc_whatsapp_phone_number_id");
        var version = Cfg(config, "epc_whatsapp_api_version", "v21.0");
        var url = "https://graph.facebook.com/" + Uri.EscapeDataString(version) + "/" + Uri.EscapeDataString(phoneId) + "/messages";
        var payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["to"] = digits,
            ["type"] = "text",
            ["text"] = new Dictionary<string, object> { ["preview_url"] = false, ["body"] = text },
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);

        var client = _clients.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromSeconds(30);
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return (false, JsonSerializer.Serialize(new Dictionary<string, string> { ["curl_error"] = ex.Message }), ex.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            const string Timeout = "Operation timed out";
            return (false, JsonSerializer.Serialize(new Dictionary<string, string> { ["curl_error"] = Timeout }), Timeout);
        }

        using (response)
        {
            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var code = (int)response.StatusCode;
            JsonElement? root = null;
            try
            {
                using var doc = JsonDocument.Parse(raw.Length == 0 ? "null" : raw);
                if (doc.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    root = doc.RootElement.Clone();
                }
            }
            catch (JsonException)
            {
            }

            var hasMessages = root is { ValueKind: JsonValueKind.Object } obj
                && obj.TryGetProperty("messages", out var messages)
                && messages.ValueKind == JsonValueKind.Array
                && messages.GetArrayLength() > 0;
            var ok = code is >= 200 and < 300 && hasMessages;
            var apiError = root is { ValueKind: JsonValueKind.Object } errObj
                && errObj.TryGetProperty("error", out var errEl)
                && errEl.ValueKind == JsonValueKind.Object
                && errEl.TryGetProperty("message", out var msgEl)
                && msgEl.ValueKind == JsonValueKind.String
                    ? msgEl.GetString() ?? string.Empty
                    : string.Empty;
            var responseJson = root is null
                ? JsonSerializer.Serialize(new Dictionary<string, string> { ["raw"] = raw.Length > 500 ? raw[..500] : raw })
                : root.Value.GetRawText();
            if (!ok && apiError.Length == 0)
            {
                apiError = "HTTP " + code.ToString(CultureInfo.InvariantCulture);
            }

            return (ok, responseJson, ok ? string.Empty : apiError);
        }
    }

    /// <summary>PHP <c>epc_wa_log_attempt()</c>: best effort, never fails the dispatch.</summary>
    private static async Task LogAttemptAsync(
        DbConnection connection,
        string notifyName,
        string phone,
        bool ok,
        string preview,
        string responseJson,
        CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_whatsapp_notify_log` ("
            + "`id` INT UNSIGNED NOT NULL AUTO_INCREMENT, `created_at` INT UNSIGNED NOT NULL, `notify_name` VARCHAR(64) NOT NULL, "
            + "`phone` VARCHAR(32) NOT NULL, `status` TINYINT(1) NOT NULL DEFAULT 0, `message_preview` VARCHAR(500) NOT NULL DEFAULT '', "
            + "`response` TEXT, PRIMARY KEY (`id`), KEY `created_at` (`created_at`), KEY `notify_name` (`notify_name`)) ENGINE=InnoDB DEFAULT CHARSET=utf8",
            cancellationToken).ConfigureAwait(false);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "INSERT INTO `epc_whatsapp_notify_log` (`created_at`, `notify_name`, `phone`, `status`, `message_preview`, `response`) VALUES (?, ?, ?, ?, ?, ?)");
            ErpDb.AddParameters(
                command,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                notifyName,
                phone,
                ok ? 1 : 0,
                Utf8Prefix(preview, 500),
                responseJson);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    /// <summary>The longest prefix of at most <paramref name="maxBytes"/> UTF-8 bytes that ends on a character boundary.</summary>
    public static string Utf8Prefix(string value, int maxBytes)
    {
        if (Encoding.UTF8.GetByteCount(value) <= maxBytes)
        {
            return value;
        }

        var bytes = 0;
        var end = 0;
        while (end < value.Length)
        {
            var width = char.IsHighSurrogate(value[end]) && end + 1 < value.Length ? 2 : 1;
            var size = Encoding.UTF8.GetByteCount(value.AsSpan(end, width));
            if (bytes + size > maxBytes)
            {
                break;
            }

            bytes += size;
            end += width;
        }

        return value[..end];
    }

    private static string Column(DbDataReader reader, string name)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), name, StringComparison.OrdinalIgnoreCase))
            {
                return reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static string PhpTrim(string value) => value.Trim(' ', '\t', '\n', '\r', '\0', '\x0B');

    private static bool PhpNonEmpty(string? value) => !string.IsNullOrEmpty(value) && value != "0";

    private static int PhpInt(string? raw)
    {
        raw = (raw ?? string.Empty).Trim();
        var end = 0;
        while (end < raw.Length && (char.IsAsciiDigit(raw[end]) || (end == 0 && raw[end] is '-' or '+')))
        {
            end++;
        }

        return int.TryParse(raw[..end], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }
}
