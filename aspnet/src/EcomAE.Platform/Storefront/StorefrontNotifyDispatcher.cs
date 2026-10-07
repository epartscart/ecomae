using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>docpart_dispatch_notification()</c> e-mail branch for a <c>direct_contact</c> person:
/// the <c>notifications_settings</c> template (language-resolved), <c>%var%</c> substitution of the
/// declared text vars, the <c>email_on</c> / <c>send_for_not_confirmed</c> gates, and delivery through
/// the platform SMTP (<c>DocpartMailer</c>). SMS / WhatsApp fan-out stays with the Classic dispatcher.
/// </summary>
public interface IStorefrontNotifyDispatcher
{
    Task<StorefrontNotifyOutcome> SendDirectEmailAsync(
        DbConnection connection,
        string name,
        IReadOnlyDictionary<string, string> vars,
        string email,
        CancellationToken cancellationToken = default);
}

/// <summary><c>Found</c> mirrors the PHP <c>status</c>; <c>EmailSent</c> mirrors <c>epc_notify_email_status()</c>.</summary>
public sealed record StorefrontNotifyOutcome(bool Found, bool EmailSent, string Message);

public sealed class StorefrontNotifyDispatcher : IStorefrontNotifyDispatcher
{
    private readonly ICpPlatformMailer _mailer;

    public StorefrontNotifyDispatcher(ICpPlatformMailer mailer)
    {
        _mailer = mailer;
    }

    public async Task<StorefrontNotifyOutcome> SendDirectEmailAsync(
        DbConnection connection,
        string name,
        IReadOnlyDictionary<string, string> vars,
        string email,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(vars);

        var found = false;
        var emailOn = false;
        var sendForNotConfirmed = false;
        var subject = string.Empty;
        var body = string.Empty;
        var varsJson = string.Empty;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(
                "SELECT IFNULL(`email_on`,0), IFNULL(`send_for_not_confirmed`,0), IFNULL(`email_subject`,''), IFNULL(`email_body`,''), IFNULL(`vars`,'') "
                + "FROM `notifications_settings` WHERE `name` = ? LIMIT 1");
            ErpDb.AddParameters(command, name);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                found = true;
                emailOn = Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture) != 0;
                sendForNotConfirmed = Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture) != 0;
                subject = Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty;
                body = Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? string.Empty;
                varsJson = Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }

        if (!found)
        {
            return new StorefrontNotifyOutcome(false, false, "Notification not found");
        }

        var to = (email ?? string.Empty).Trim();
        if (to.Length == 0 || !sendForNotConfirmed || !emailOn)
        {
            return new StorefrontNotifyOutcome(true, false, "E-mail not sent for this notification");
        }

        subject = Render(await TranslateAsync(connection, subject, cancellationToken).ConfigureAwait(false), varsJson, vars);
        body = Render(await TranslateAsync(connection, body, cancellationToken).ConfigureAwait(false), varsJson, vars);

        var sent = await _mailer.SendHtmlAsync(to, subject, body, cancellationToken).ConfigureAwait(false);
        return new StorefrontNotifyOutcome(true, sent.Ok, sent.Message);
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

    private static async Task<string> TranslateAsync(DbConnection connection, string key, CancellationToken cancellationToken)
    {
        if (key.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            var value = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = 'en' LIMIT 1"),
                cancellationToken,
                key).ConfigureAwait(false);
            return string.IsNullOrEmpty(value) ? key : value;
        }
        catch (DbException)
        {
            return key;
        }
    }
}
