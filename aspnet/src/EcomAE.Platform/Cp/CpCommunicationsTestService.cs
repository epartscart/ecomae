using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Erp;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live twin of PHP <c>cp/content/control/communications/ajax_test_notification.php</c> →
/// <c>send_notify('test_email' | 'test_phone')</c>: contact validated against the tenant
/// <c>reg_fields</c> regex, body taken from the <c>notifications_settings</c> template, delivery through the
/// tenant SMTP (e-mail) or the active <c>sms_api</c> operator, and the outcome written back to
/// <c>debug_results</c> so the status pills on the page reflect the real last attempt.
/// </summary>
public interface ICpCommunicationsTestService
{
    Task<ErpSimpleWriteResult> SendTestAsync(string type, string contact, CancellationToken cancellationToken = default);
}

public sealed class CpCommunicationsTestService : ICpCommunicationsTestService
{
    private static readonly TimeSpan RegexBudget = TimeSpan.FromMilliseconds(250);

    private readonly IErpWriteConnectionFactory _connections;
    private readonly ICpSmsGateway _sms;
    private readonly PhpReferenceOptions _reference;

    public CpCommunicationsTestService(
        IErpWriteConnectionFactory connections,
        ICpSmsGateway sms,
        IOptions<PhpReferenceOptions> reference)
    {
        _connections = connections;
        _sms = sms;
        _reference = reference.Value;
    }

    /// <summary>PHP accepts exactly <c>email</c> and <c>phone</c>.</summary>
    public static bool IsKnownType(string type)
        => type is "email" or "phone";

    /// <summary>PHP <c>DP_User::check_contact_by_regexp()</c>: the stored pattern must match the whole contact.</summary>
    public static bool ContactMatches(string contact, string pattern)
    {
        if (contact.Length == 0)
        {
            return false;
        }

        if (pattern.Length == 0)
        {
            return true;
        }

        try
        {
            var match = Regex.Match(contact, pattern, RegexOptions.CultureInvariant, RegexBudget);
            return match.Success && match.Value == contact;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    public async Task<ErpSimpleWriteResult> SendTestAsync(string type, string contact, CancellationToken cancellationToken = default)
    {
        var channel = (type ?? string.Empty).Trim().ToLowerInvariant();
        if (!IsKnownType(channel))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Incorrect type");
        }

        var target = (contact ?? string.Empty).Trim();
        if (target.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "No params");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "Tenant database is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

            var pattern = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT IFNULL(`regexp`,'') FROM `reg_fields` WHERE `name` = ? LIMIT 1"),
                cancellationToken,
                channel).ConfigureAwait(false) ?? string.Empty;
            if (!ContactMatches(target, pattern))
            {
                return ErpSimpleWriteResult.Fail("invalid", "The contact does not match the required format.");
            }

            var template = await ReadTemplateAsync(connection, "test_" + channel, cancellationToken).ConfigureAwait(false);
            var outcome = channel == "email"
                ? await SendEmailAsync(target, template, cancellationToken).ConfigureAwait(false)
                : await SendSmsAsync(connection, target, template, cancellationToken).ConfigureAwait(false);

            await RecordDebugAsync(
                connection,
                channel == "email" ? "email" : "sms",
                outcome.Ok,
                outcome.Message,
                cancellationToken).ConfigureAwait(false);

            return outcome.Ok
                ? ErpSimpleWriteResult.Ok(
                    channel == "email"
                        ? "Test e-mail sent to " + target + "."
                        : "Test SMS sent to " + target + ".",
                    1)
                : ErpSimpleWriteResult.Fail("send_failed", outcome.Message);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Tenant database unavailable.");
        }
    }

    /// <summary>PHP notification template: <c>email_subject</c> / <c>email_body</c> / <c>sms_body</c> resolved through the language strings.</summary>
    private static async Task<(string Subject, string EmailBody, string SmsBody)> ReadTemplateAsync(
        DbConnection connection,
        string name,
        CancellationToken cancellationToken)
    {
        var subject = string.Empty;
        var emailBody = string.Empty;
        var smsBody = string.Empty;

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(
                "SELECT IFNULL(`email_subject`,''), IFNULL(`email_body`,''), IFNULL(`sms_body`,'') "
                + "FROM `notifications_settings` WHERE `name` = ? LIMIT 1");
            ErpDb.AddParameters(command, name);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                subject = reader.GetString(0);
                emailBody = reader.GetString(1);
                smsBody = reader.GetString(2);
            }
        }

        subject = await TranslateAsync(connection, subject, cancellationToken).ConfigureAwait(false);
        emailBody = await TranslateAsync(connection, emailBody, cancellationToken).ConfigureAwait(false);
        smsBody = await TranslateAsync(connection, smsBody, cancellationToken).ConfigureAwait(false);

        if (subject.Length == 0) { subject = "Test message"; }
        if (emailBody.Length == 0) { emailBody = "This is a test message from the Control Panel."; }
        if (smsBody.Length == 0) { smsBody = "Test message from the Control Panel."; }

        return (subject, emailBody, smsBody);
    }

    private static async Task<string> TranslateAsync(DbConnection connection, string key, CancellationToken cancellationToken)
    {
        if (key.Length == 0)
        {
            return string.Empty;
        }

        var value = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = 'en' LIMIT 1"),
            cancellationToken,
            key).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    private async Task<CpSmsSendOutcome> SendEmailAsync(
        string to,
        (string Subject, string EmailBody, string SmsBody) template,
        CancellationToken cancellationToken)
    {
        var config = ReadConfig();
        if (!CpCommunicationsDeskService.SmtpComplete(config))
        {
            return CpSmsSendOutcome.Fail("SMTP is not fully configured — fill the e-mail group in Configuration first.");
        }

        var host = config["smtp_host"].Trim();
        if (!int.TryParse(config["smtp_port"].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
            || port is < 1 or > 65535)
        {
            return CpSmsSendOutcome.Fail("SMTP port is invalid.");
        }

        var fromEmail = config["from_email"].Trim();
        if (!CpTenantEmailWriteService.IsEmail(fromEmail))
        {
            return CpSmsSendOutcome.Fail("From e-mail in Configuration is not a valid address.");
        }

        try
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(fromEmail, config["from_name"].Trim()),
                Subject = template.Subject,
                Body = template.EmailBody,
                IsBodyHtml = true,
            };
            mail.To.Add(to);

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = CpTenantEmailWriteService.NormalizeEncryption(config["smtp_encryption"]) is "tls" or "ssl",
                Timeout = 20_000,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Credentials = new NetworkCredential(config["smtp_username"].Trim(), config["smtp_password"]),
            };
            await client.SendMailAsync(mail, cancellationToken).ConfigureAwait(false);
            return new CpSmsSendOutcome(true, string.Empty);
        }
        catch (SmtpException ex)
        {
            return CpSmsSendOutcome.Fail(ex.Message);
        }
        catch (FormatException ex)
        {
            return CpSmsSendOutcome.Fail(ex.Message);
        }
    }

    private async Task<CpSmsSendOutcome> SendSmsAsync(
        DbConnection connection,
        string phone,
        (string Subject, string EmailBody, string SmsBody) template,
        CancellationToken cancellationToken)
    {
        var handler = string.Empty;
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT IFNULL(`handler`,''), IFNULL(`parameters_values`,'') FROM `sms_api` WHERE `active` = 1 LIMIT 1";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                handler = reader.GetString(0);
                parameters = ParseParameters(reader.GetString(1));
            }
        }

        if (handler.Length == 0)
        {
            return CpSmsSendOutcome.Fail("No SMS operator is active — activate one under SMS operators.");
        }

        return await _sms.SendAsync(handler, parameters, phone, template.SmsBody, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Flattens <c>sms_api.parameters_values</c> (PHP stores scalars as JSON strings or numbers).</summary>
    public static Dictionary<string, string> ParseParameters(string json)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return map;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return map;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                map[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "1",
                    JsonValueKind.False => string.Empty,
                    _ => string.Empty,
                };
            }
        }
        catch (JsonException)
        {
            return map;
        }

        return map;
    }

    /// <summary>PHP <c>send_notify.php</c> upsert of the channel row in <c>debug_results</c>.</summary>
    private static async Task RecordDebugAsync(
        DbConnection connection,
        string name,
        bool ok,
        string detail,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var text = detail.Length > 240 ? detail[..240] : detail;
        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `debug_results` WHERE `name` = ?"),
            cancellationToken,
            name).ConfigureAwait(false);

        if (existing > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `debug_results` SET `status` = ?, `debug_result` = ?, `time` = ? WHERE `name` = ?"),
                cancellationToken,
                ok ? 1 : 0,
                text,
                now,
                name).ConfigureAwait(false);
            return;
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `debug_results` (`name`, `status`, `debug_result`, `time`) VALUES (?,?,?,?)"),
            cancellationToken,
            name,
            ok ? 1 : 0,
            text,
            now).ConfigureAwait(false);
    }

    private IReadOnlyDictionary<string, string> ReadConfig()
    {
        var root = (_reference.PhpDocRoot ?? Environment.GetEnvironmentVariable("ECOMAE_PHP_DOCROOT") ?? string.Empty).Trim();
        if (root.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var path = Path.Combine(root, "config.php");
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            return PhpConfigFile.Values(PhpConfigFile.Parse(File.ReadAllText(path)));
        }
        catch (IOException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (UnauthorizedAccessException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
