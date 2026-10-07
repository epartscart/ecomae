using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>docpart_dispatch_notification()</c>: the <c>notifications_settings</c> template (language-resolved),
/// <c>%var%</c> substitution of the declared text vars, the content/notifications/template.php wrapper, and per
/// <c>user_id</c> / <c>direct_contact</c> person the e-mail (<c>email_on</c>, <c>email_confirmed</c> or
/// <c>send_for_not_confirmed</c>) through the platform SMTP (<c>DocpartMailer</c>), the SMS (<c>sms_on</c>,
/// <c>phone_confirmed</c> or <c>send_for_not_confirmed</c>) through the active <c>sms_api</c> operator, and the
/// WhatsApp Cloud API message (epc_whatsapp_notify.php) to the same phone.
/// </summary>
public interface IStorefrontNotifyDispatcher
{
    Task<StorefrontNotifyAnswer> SendAsync(
        DbConnection connection,
        string name,
        IReadOnlyDictionary<string, string> vars,
        IReadOnlyList<StorefrontNotifyPerson> persons,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string>? statusRef = null);

    Task<StorefrontNotifyOutcome> SendDirectEmailAsync(
        DbConnection connection,
        string name,
        IReadOnlyDictionary<string, string> vars,
        string email,
        CancellationToken cancellationToken = default);
}

/// <summary>A PHP <c>$persons[]</c> entry: <c>user_id</c> (contacts from <c>users</c>) or <c>direct_contact</c>.</summary>
public sealed record StorefrontNotifyPerson(string Type, int UserId, string Email, string Phone = "")
{
    public const string UserIdType = "user_id";
    public const string DirectContactType = "direct_contact";

    public static StorefrontNotifyPerson User(int userId) => new(UserIdType, userId, string.Empty);

    public static StorefrontNotifyPerson Direct(string email, string phone = "")
        => new(DirectContactType, 0, email ?? string.Empty, phone ?? string.Empty);
}

/// <summary><c>TriedToSend</c> / <c>Status</c> / <c>Message</c> are the e-mail flags; <c>Sms</c> and <c>WhatsApp</c> the phone channels.</summary>
public sealed record StorefrontNotifyPersonResult(StorefrontNotifyPerson Person, bool TriedToSend, bool Status, string Message)
{
    public StorefrontNotifyChannel Sms { get; init; } = StorefrontNotifyChannel.None;

    public StorefrontNotifyChannel WhatsApp { get; init; } = StorefrontNotifyChannel.None;
}

/// <summary>PHP dispatch answer: <c>status</c>, <c>message</c> and the per-person e-mail flags.</summary>
public sealed record StorefrontNotifyAnswer(bool Found, string Message, IReadOnlyList<StorefrontNotifyPersonResult> Persons)
{
    /// <summary>
    /// PHP <c>epc_notify_email_status()</c>: the first tried person matching <paramref name="match"/>
    /// (direct e-mail, or user id as a string) decides; <c>null</c> when nobody was tried.
    /// </summary>
    public bool? EmailStatus(string match = "")
    {
        if (!Found)
        {
            return null;
        }

        match = (match ?? string.Empty).Trim().ToLowerInvariant();
        foreach (var person in Persons)
        {
            if (!person.TriedToSend)
            {
                continue;
            }

            if (match.Length > 0)
            {
                if (person.Person.Type == StorefrontNotifyPerson.DirectContactType
                    && person.Person.Email.Trim().ToLowerInvariant() != match)
                {
                    continue;
                }

                if (person.Person.Type == StorefrontNotifyPerson.UserIdType
                    && person.Person.UserId.ToString(CultureInfo.InvariantCulture) != match)
                {
                    continue;
                }
            }

            return person.Status;
        }

        return null;
    }
}

/// <summary><c>Found</c> mirrors the PHP <c>status</c>; <c>EmailSent</c> mirrors <c>epc_notify_email_status()</c>.</summary>
public sealed record StorefrontNotifyOutcome(bool Found, bool EmailSent, string Message);

public sealed class StorefrontNotifyDispatcher : IStorefrontNotifyDispatcher
{
    public const string NotFoundMessage = "Notification not found";
    public const string NotSentMessage = "E-mail not sent for this notification";

    private static readonly TimeZoneInfo PlatformZone = ResolveZone();

    private readonly ICpPlatformMailer _mailer;
    private readonly ICpSmsGateway? _sms;
    private readonly IStorefrontWhatsappNotifier? _whatsapp;
    private readonly string _docRoot;

    public StorefrontNotifyDispatcher(
        ICpPlatformMailer mailer,
        IOptions<PhpReferenceOptions>? reference = null,
        ICpSmsGateway? sms = null,
        IStorefrontWhatsappNotifier? whatsapp = null)
    {
        _mailer = mailer;
        _sms = sms;
        _whatsapp = whatsapp;
        _docRoot = (reference?.Value.PhpDocRoot ?? Environment.GetEnvironmentVariable("ECOMAE_PHP_DOCROOT") ?? string.Empty).Trim();
    }

    public async Task<StorefrontNotifyOutcome> SendDirectEmailAsync(
        DbConnection connection,
        string name,
        IReadOnlyDictionary<string, string> vars,
        string email,
        CancellationToken cancellationToken = default)
    {
        var answer = await SendAsync(connection, name, vars, [StorefrontNotifyPerson.Direct((email ?? string.Empty).Trim())], cancellationToken)
            .ConfigureAwait(false);
        if (!answer.Found)
        {
            return new StorefrontNotifyOutcome(false, false, answer.Message);
        }

        var person = answer.Persons[0];
        return person.TriedToSend
            ? new StorefrontNotifyOutcome(true, person.Status, person.Message)
            : new StorefrontNotifyOutcome(true, false, NotSentMessage);
    }

    public async Task<StorefrontNotifyAnswer> SendAsync(
        DbConnection connection,
        string name,
        IReadOnlyDictionary<string, string> vars,
        IReadOnlyList<StorefrontNotifyPerson> persons,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, string>? statusRef = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(vars);
        ArgumentNullException.ThrowIfNull(persons);

        var found = false;
        var emailOn = false;
        var smsOn = false;
        var sendForNotConfirmed = false;
        var subject = string.Empty;
        var body = string.Empty;
        var smsBody = string.Empty;
        var varsJson = string.Empty;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional("SELECT * FROM `notifications_settings` WHERE `name` = ? LIMIT 1");
            ErpDb.AddParameters(command, name);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                found = true;
                emailOn = PhpLong(Column(reader, "email_on")) != 0;
                smsOn = PhpLong(Column(reader, "sms_on")) != 0;
                sendForNotConfirmed = PhpLong(Column(reader, "send_for_not_confirmed")) != 0;
                subject = Column(reader, "email_subject");
                body = Column(reader, "email_body");
                smsBody = Column(reader, "sms_body");
                varsJson = Column(reader, "vars");
            }
        }

        if (!found)
        {
            return new StorefrontNotifyAnswer(false, NotFoundMessage, []);
        }

        var translator = new StorefrontPhpTranslator(connection);
        subject = Render(await TranslateOrRawAsync(translator, subject, cancellationToken).ConfigureAwait(false), varsJson, vars);
        body = Render(await TranslateOrRawAsync(translator, body, cancellationToken).ConfigureAwait(false), varsJson, vars);
        smsBody = Render(await TranslateOrRawAsync(translator, smsBody, cancellationToken).ConfigureAwait(false), varsJson, vars);
        string? wrapped = null;
        var smsApi = await ActiveSmsApiAsync(connection, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<string, string>? config = null;
        var (smsAllowed, whatsappAllowed) = (true, true);
        if (statusRef is not null)
        {
            config = _mailer.ReadConfig();
            (smsAllowed, whatsappAllowed) = StatusAllows(name, statusRef, config);
        }

        var waNotify = new StorefrontWhatsappNotify(name, emailOn, smsOn);

        var results = new List<StorefrontNotifyPersonResult>(persons.Count);
        foreach (var person in persons)
        {
            var contacts = await ContactsAsync(connection, person, sendForNotConfirmed, cancellationToken).ConfigureAwait(false);
            var phone = contacts.Phone;

            var result = new StorefrontNotifyPersonResult(person, false, false, NotSentMessage);
            if (contacts.Email.Length > 0 && emailOn)
            {
                wrapped ??= await WrapAsync(connection, translator, subject, body, cancellationToken).ConfigureAwait(false);
                var sent = await _mailer.SendHtmlAsync(contacts.Email, subject, wrapped, cancellationToken).ConfigureAwait(false);
                result = new StorefrontNotifyPersonResult(person, true, sent.Ok, sent.Message);
            }

            if (phone.Length > 0 && smsOn && smsAllowed && smsApi is { } api)
            {
                phone = SmsPhone(phone);
                var sms = _sms is null
                    ? CpSmsSendOutcome.Fail("SMS gateway is not available")
                    : await _sms.SendAsync(
                        api.Handler, api.Parameters, phone, smsBody, cancellationToken, CpSmsHandlerContext.For(connection, config ??= _mailer.ReadConfig())).ConfigureAwait(false);
                result = result with { Sms = new StorefrontNotifyChannel(true, sms.Ok, sms.Ok ? string.Empty : sms.Message) };
            }

            if (phone.Length > 0 && whatsappAllowed && _whatsapp is not null)
            {
                config ??= _mailer.ReadConfig();
                var wa = await _whatsapp.DispatchForPersonAsync(connection, config, waNotify, vars, smsBody, body, phone, cancellationToken)
                    .ConfigureAwait(false);
                result = result with { WhatsApp = wa };
            }

            results.Add(result);
        }

        return new StorefrontNotifyAnswer(true, string.Empty, results);
    }

    /// <summary>
    /// PHP <c>status_ref</c> rule (dispatch SMS gate and <c>epc_wa_status_allows_send()</c>): with
    /// <c>orders_statuses_notifications_settings</c> = 1, a status whose <c>to_manager_sms</c> / <c>to_customer_sms</c>
    /// is 0 sends no SMS; WhatsApp needs the flag missing or exactly 1.
    /// </summary>
    public static (bool Sms, bool WhatsApp) StatusAllows(string name, IReadOnlyDictionary<string, string> statusRef, IReadOnlyDictionary<string, string> config)
    {
        if (!config.TryGetValue("orders_statuses_notifications_settings", out var on) || on != "1")
        {
            return (true, true);
        }

        var key = name switch
        {
            "order_status_to_manager" or "order_item_status_to_manager" => "to_manager_sms",
            "order_status_to_customer" or "order_item_status_to_customer" => "to_customer_sms",
            _ => string.Empty,
        };
        if (key.Length == 0 || !statusRef.TryGetValue(key, out var flag))
        {
            return (true, true);
        }

        return (PhpInt(flag) != 0, PhpInt(flag) == 1);
    }

    /// <summary>PHP strips these before posting the number to the SMS handler; WhatsApp then gets the stripped number.</summary>
    public static string SmsPhone(string phone)
    {
        foreach (var token in new[] { " ", "+7", "(", ")", "-", "_", "+" })
        {
            phone = phone.Replace(token, string.Empty, StringComparison.Ordinal);
        }

        return phone;
    }

    private sealed record SmsApi(string Handler, IReadOnlyDictionary<string, string> Parameters);

    /// <summary>PHP <c>SELECT * FROM sms_api WHERE active = 1</c> (first row); no table or no active operator means no SMS.</summary>
    private static async Task<SmsApi?> ActiveSmsApiAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT * FROM `sms_api` WHERE `active` = ?");
            ErpDb.AddParameters(command, 1);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            return new SmsApi(Column(reader, "handler"), CpCommunicationsTestService.ParseParameters(Column(reader, "parameters_values")));
        }
        catch (DbException)
        {
            return null;
        }
    }

    /// <summary>PHP replaces <c>%name%</c> only for vars the template declares with <c>type = text</c>; undeclared vars stay literal.</summary>
    public static string Render(string template, string varsJson, IReadOnlyDictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(template) || string.IsNullOrWhiteSpace(varsJson))
        {
            return template ?? string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(varsJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return template;
            }

            foreach (var declared in doc.RootElement.EnumerateArray())
            {
                if (declared.ValueKind != JsonValueKind.Object
                    || !declared.TryGetProperty("type", out var type)
                    || type.ValueKind != JsonValueKind.String
                    || type.GetString() != "text"
                    || !declared.TryGetProperty("name", out var nameEl)
                    || nameEl.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var key = nameEl.GetString() ?? string.Empty;
                var value = vars.TryGetValue(key, out var v) ? v : string.Empty;
                template = template.Replace("%" + key + "%", value, StringComparison.Ordinal);
            }
        }
        catch (JsonException)
        {
        }

        return template;
    }

    /// <summary>The <c>templates</c> row fields content/notifications/template.php reads.</summary>
    public sealed record EmailTemplateSettings(string LogoFile, bool TransparentLogo, string MainColor, int Version)
    {
        public static EmailTemplateSettings None { get; } = new(string.Empty, false, string.Empty, 0);

        public static EmailTemplateSettings Parse(string? dataValue)
        {
            if (string.IsNullOrWhiteSpace(dataValue))
            {
                return None;
            }

            try
            {
                using var doc = JsonDocument.Parse(dataValue);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return None;
                }

                var root = doc.RootElement;
                return new EmailTemplateSettings(
                    Scalar(root, "logo_file"),
                    Scalar(root, "bg_transparent_logo") == "1",
                    Scalar(root, "main_color"),
                    PhpInt(Scalar(root, "version")));
            }
            catch (JsonException)
            {
                return None;
            }
        }
    }

    public static async Task<EmailTemplateSettings> LoadTemplateSettingsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            return EmailTemplateSettings.Parse(await ErpDb.StringAsync(
                connection,
                null,
                "SELECT `data_value` FROM `templates` WHERE `is_frontend` = 1 AND `current` = 1 LIMIT 1",
                cancellationToken).ConfigureAwait(false));
        }
        catch (DbException)
        {
            return EmailTemplateSettings.None;
        }
    }

    /// <summary>
    /// content/notifications/template.php: logo + subject/date header, the body, the 4929/4930 footer, then the
    /// obtain-mode caption and inline-style string replacements applied over the whole wrapped body.
    /// </summary>
    public static string Wrap(
        string subject,
        string body,
        EmailTemplateSettings template,
        bool logoExists,
        string domainPath,
        DateTimeOffset now,
        string footer1,
        string footer2,
        IReadOnlyList<string> obtainCaptions,
        string obtainLabel)
    {
        var html = new StringBuilder();
        html.Append("<div style=\"text-align:center; padding:50px 50px 50px 50px; background:#f5f5f5; border-radius: 15px; font-family:Calibri; font-size: 16px;\">\n");
        html.Append("\t<div style=\"text-align:left; max-width:1000px; display: block; padding: 30px; border-radius: 0px; background: white; margin: 50px auto; border-radius: 10px; border: 1px solid #cfcfcf; box-shadow: 0px 14px 28px #979797;\">\n\n");
        html.Append("\t\t<div style=\"text-align:left;\">\n");
        html.Append("\t\t\t<table border=\"0\" cellspacing=\"0\" cellpadding=\"0\" width=\"100%\" style=\"width: 100%; border: none; margin: 0; padding: 0;\">\n");
        html.Append("\t\t\t\t<tr>\n");
        html.Append("\t\t\t\t\t<td class=\"dp_email_logo\" valign=\"top\" style=\"border: none; margin: 0; padding: 0;\">\n");
        if (template.LogoFile.Length > 0 && logoExists)
        {
            html.Append("\t\t\t\t\t\t<a href=\"").Append(domainPath).Append("\" target=\"_blank\">\n");
            html.Append("\t\t\t\t\t\t\t<img style=\"max-height: 60px; max-width: 300px; margin-right: 50px;")
                .Append(template.TransparentLogo ? " background:" + template.MainColor + ";" : string.Empty)
                .Append(" image-rendering: -webkit-optimize-contrast;\" src=\"")
                .Append(domainPath).Append(template.LogoFile.Length > 1 ? template.LogoFile[1..] : string.Empty)
                .Append("?v=").Append(template.Version.ToString(CultureInfo.InvariantCulture))
                .Append("\" alt=\"logotype\"/>\n");
            html.Append("\t\t\t\t\t\t</a>\n");
        }

        html.Append("\t\t\t\t\t</td>\n");
        html.Append("\t\t\t\t\t<td class=\"dp_email_subject\" valign=\"top\" style=\"border: none; margin: 0; padding: 0; text-align:right; font-size: 18px; line-height: 16px;\">\n");
        html.Append("\t\t\t\t\t\t").Append(subject).Append("<br/><small style=\"font-size: 13px;\">")
            .Append(now.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)).Append("</small>\t\t\t\t\t</td>\n");
        html.Append("\t\t\t\t</tr>\n\t\t\t</table>\n\t\t</div>\n\t\t\n");
        html.Append("\t\t<div style=\"border-top: 1px solid #cfcfcf; margin: 22px 0px 18px 0px;\"></div>\n\t\t\n");
        html.Append("\t\t<div style=\"text-align:left;\">\n\t\t").Append(body).Append("\t\t</div>\n\t\t\n");
        html.Append("\t\t<div style=\"border-top: 1px solid #cfcfcf; margin: 22px 0px 18px 0px;\"></div>\n\t\t\n");
        html.Append("\t\t<p style=\"text-align: center; color: #5a5a5a; font-size: 11px; line-height: 1.1em; padding: 0; margin: 0; margin-bottom: -13px;\">\n");
        html.Append("\t\t\t").Append(footer1).Append(".\n\t\t\t<br/>\n\t\t\t").Append(footer2).Append(".\n\t\t</p>\n\t\t\n");
        html.Append("\t</div>\n</div>\n");
        html.Append("<style type=\"text/css\">\n@media screen and (max-width: 767px) {\n\t.dp_email_logo {\n\t\ttext-align: center !important;\n\t}\n");
        html.Append("\t.dp_email_subject {\n\t\tdisplay: none !important;\n\t}\n}\n</style>\n");

        var result = html.ToString()
            .Replace("<div class=\"collapse\" id=\"collapse_office_map_container\">", "<div style=\"display:none;\">", StringComparison.Ordinal);
        foreach (var caption in obtainCaptions)
        {
            var heading = "<h4>" + obtainLabel + " - " + caption + "</h4>";
            result = result
                .Replace("<p>" + obtainLabel + " - <b>" + caption + "</b></p>", heading, StringComparison.Ordinal)
                .Replace("<p class=\"lead\">" + obtainLabel + " - " + caption + "</p>", heading, StringComparison.Ordinal);
        }

        const string Cell = "border: 1px solid #cfcfcf; padding: 5px;";
        const string Table = "<table cellspacing=\"0\" style=\"border-collapse: collapse; padding: 0; margin: 0; margin-top: 0px; margin-right: 0px; margin-bottom: 0px; margin-left: 0px; border: 1px solid #cfcfcf; font-family: Calibri; font-size: 12px;\">";
        return result
            .Replace("<h4>", "<h4 style=\"font-family: Calibri; font-size: 16px; padding: 0; margin: 0; margin-top: 20px; margin-right: 0px; margin-bottom: 5px; margin-left: 0px; font-weight: bold; display: block;\">", StringComparison.Ordinal)
            .Replace("<table class=\"table\">", Table, StringComparison.Ordinal)
            .Replace("<table>", Table, StringComparison.Ordinal)
            .Replace("<tr>", "<tr style=\"border: 1px solid #cfcfcf;\">", StringComparison.Ordinal)
            .Replace("<th>", "<th style=\"" + Cell + "\">", StringComparison.Ordinal)
            .Replace("<th colspan=", "<th style=\"" + Cell + "\" colspan=", StringComparison.Ordinal)
            .Replace("<td>", "<td style=\"" + Cell + "\">", StringComparison.Ordinal)
            .Replace("<td colspan=", "<td style=\"" + Cell + "\" colspan=", StringComparison.Ordinal);
    }

    public static DateTimeOffset PlatformNow() => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, PlatformZone);

    public static DateTimeOffset PlatformTime(long unixSeconds)
        => TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(unixSeconds), PlatformZone);

    private async Task<string> WrapAsync(
        DbConnection connection,
        StorefrontPhpTranslator translator,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var template = await LoadTemplateSettingsAsync(connection, cancellationToken).ConfigureAwait(false);
        var domainPath = _mailer.ReadConfig().TryGetValue("domain_path", out var d) ? d.Trim() : string.Empty;
        var captions = new List<string>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT IFNULL(`caption`,'') FROM `shop_obtaining_modes`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                captions.Add(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty);
            }
        }
        catch (DbException)
        {
        }

        return Wrap(
            subject,
            body,
            template,
            LogoExists(template.LogoFile),
            domainPath,
            PlatformNow(),
            await translator.TextAsync(4929, cancellationToken).ConfigureAwait(false),
            await translator.TextAsync(4930, cancellationToken).ConfigureAwait(false),
            captions,
            await translator.TextAsync(3507, cancellationToken).ConfigureAwait(false));
    }

    private bool LogoExists(string logoFile)
    {
        if (logoFile.Length == 0 || _docRoot.Length == 0)
        {
            return false;
        }

        try
        {
            var full = Path.GetFullPath(Path.Combine(_docRoot, logoFile.TrimStart('/')));
            var root = Path.GetFullPath(_docRoot);
            return full.StartsWith(root, StringComparison.Ordinal) && File.Exists(full);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>The e-mail and phone PHP would send to (empty when the person does not qualify for that channel).</summary>
    private static async Task<(string Email, string Phone)> ContactsAsync(
        DbConnection connection,
        StorefrontNotifyPerson person,
        bool sendForNotConfirmed,
        CancellationToken cancellationToken)
    {
        if (person.Type == StorefrontNotifyPerson.DirectContactType)
        {
            return sendForNotConfirmed ? (person.Email.Trim(), person.Phone.Trim()) : (string.Empty, string.Empty);
        }

        if (person.Type != StorefrontNotifyPerson.UserIdType || person.UserId <= 0)
        {
            return (string.Empty, string.Empty);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT * FROM `users` WHERE `user_id` = ? LIMIT 1");
            ErpDb.AddParameters(command, person.UserId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return (string.Empty, string.Empty);
            }

            var email = Column(reader, "email").Trim();
            var emailConfirmed = PhpLong(Column(reader, "email_confirmed")) != 0;
            var phone = Column(reader, "phone");
            var phoneConfirmed = PhpLong(Column(reader, "phone_confirmed")) != 0;
            return (
                email.Length > 0 && (emailConfirmed || sendForNotConfirmed) ? email : string.Empty,
                phone.Length > 0 && phone != "0" && (phoneConfirmed || sendForNotConfirmed) ? phone : string.Empty);
        }
        catch (DbException)
        {
            return (string.Empty, string.Empty);
        }
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

    private static long PhpLong(string raw) => PhpInt(raw);

    /// <summary>PHP falls back to the raw template text when the translation is null / empty.</summary>
    private static async Task<string> TranslateOrRawAsync(StorefrontPhpTranslator translator, string key, CancellationToken cancellationToken)
    {
        if (key.Length == 0)
        {
            return string.Empty;
        }

        var value = await translator.RawAsync(key, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrEmpty(value) ? key : value;
    }

    private static string Scalar(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var v))
        {
            return string.Empty;
        }

        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? string.Empty,
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True => "1",
            _ => string.Empty,
        };
    }

    private static int PhpInt(string raw)
    {
        raw = raw.Trim();
        var end = 0;
        while (end < raw.Length && (char.IsAsciiDigit(raw[end]) || (end == 0 && raw[end] == '-')))
        {
            end++;
        }

        return int.TryParse(raw[..end], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static TimeZoneInfo ResolveZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Dubai");
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("Asia/Dubai", TimeSpan.FromHours(4), "Asia/Dubai", "Asia/Dubai");
        }
    }
}
