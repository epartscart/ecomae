using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Configuration;
using EcomAE.Platform.Erp;
using Microsoft.Extensions.Options;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Communications desk — typed twin of PHP <c>cp/content/control/communications/communications.php</c>:
/// SMTP completeness from <c>config.php</c>, <c>debug_results</c> freshness pills for e-mail and SMS,
/// the active <c>sms_api</c> operator with its sender number, and the admin contact defaults used to
/// prefill the two test forms. Operator credentials and the SMTP password never leave the service.
/// </summary>
public interface ICpCommunicationsDeskService
{
    Task<CpCommunicationsView> LoadAsync(int adminUserId, string sessionEmail, CancellationToken cancellationToken = default);
}

/// <summary>PHP <c>$formatDebug()</c> output for one <c>debug_results</c> channel.</summary>
public sealed record CpCommunicationsDebug(string Pill, string Label, string Meta, string AgeClass)
{
    public static CpCommunicationsDebug Idle()
        => new("idle", "Not tested", "No successful test recorded yet. Send a test below.", string.Empty);
}

public sealed record CpCommunicationsView(
    bool EmailConfigured,
    string FromEmail,
    string SmtpHost,
    string SmtpPort,
    CpCommunicationsDebug EmailDebug,
    bool SmsConfigured,
    string SmsOperatorName,
    string SmsHandler,
    string SmsSender,
    CpCommunicationsDebug SmsDebug,
    string TestEmail,
    string TestPhone,
    string Source,
    string Error)
{
    public static CpCommunicationsView Empty(string source, string error)
        => new(
            false, string.Empty, string.Empty, string.Empty, CpCommunicationsDebug.Idle(),
            false, string.Empty, string.Empty, string.Empty, CpCommunicationsDebug.Idle(),
            string.Empty, string.Empty, source, error);
}

public sealed class CpCommunicationsDeskService : ICpCommunicationsDeskService
{
    /// <summary>PHP <c>$email_settings_pointed</c> — every one of these <c>DP_Config</c> fields must be non-empty.</summary>
    public static readonly string[] SmtpFields =
        ["from_name", "from_email", "smtp_mode", "smtp_encryption", "smtp_host", "smtp_port", "smtp_username", "smtp_password"];

    /// <summary>PHP sender precedence inside <c>sms_api.parameters_values</c>.</summary>
    public static readonly string[] SenderKeys = ["sender_number", "from", "sender", "sender_id"];

    private const long StaleSeconds = 86400;
    private const long OldSeconds = 604800;

    private readonly IErpWriteConnectionFactory _connections;
    private readonly PhpReferenceOptions _reference;

    public CpCommunicationsDeskService(
        IErpWriteConnectionFactory connections,
        IOptions<PhpReferenceOptions> reference)
    {
        _connections = connections;
        _reference = reference.Value;
    }

    public string ConfigPath
    {
        get
        {
            var root = (_reference.PhpDocRoot ?? Environment.GetEnvironmentVariable("ECOMAE_PHP_DOCROOT") ?? string.Empty).Trim();
            return root.Length == 0 ? string.Empty : Path.Combine(root, "config.php");
        }
    }

    public static bool SmtpComplete(IReadOnlyDictionary<string, string> config)
        => SmtpFields.All(key => config.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value));

    /// <summary>PHP <c>$sms_sender</c> lookup: first non-empty of sender_number / from / sender / sender_id.</summary>
    public static string SenderNumber(string parametersValuesJson)
    {
        if (string.IsNullOrWhiteSpace(parametersValuesJson))
        {
            return string.Empty;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(parametersValuesJson);
        }
        catch (JsonException)
        {
            return string.Empty;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return string.Empty;
            }

            foreach (var key in SenderKeys)
            {
                if (document.RootElement.TryGetProperty(key, out var property)
                    && property.ValueKind == JsonValueKind.String)
                {
                    var value = (property.GetString() ?? string.Empty).Trim();
                    if (value.Length > 0)
                    {
                        return value;
                    }
                }
            }
        }

        return string.Empty;
    }

    /// <summary>PHP <c>$formatDebug()</c>: idle when never tested, green with a stale/old age class, red with the stored error.</summary>
    public static CpCommunicationsDebug FormatDebug(bool found, long status, long time, string debugResult, long nowUnix)
    {
        if (!found)
        {
            return CpCommunicationsDebug.Idle();
        }

        var when = DateTimeOffset.FromUnixTimeSeconds(time).UtcDateTime.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        if (status == 1)
        {
            var age = nowUnix - time;
            var ageClass = age >= OldSeconds ? "is-old" : age >= StaleSeconds ? "is-stale" : string.Empty;
            var note = ageClass switch
            {
                "is-old" => " · older than a week",
                "is-stale" => " · older than a day",
                _ => string.Empty,
            };
            return new("ok", "Last test succeeded", "Last successful test: " + when + note, ageClass);
        }

        return new("bad", "Last test failed", "Last attempt: " + when + " — " + debugResult, string.Empty);
    }

    public async Task<CpCommunicationsView> LoadAsync(int adminUserId, string sessionEmail, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpCommunicationsView.Empty("unconfigured", "Tenant database unavailable.");
        }

        var config = ReadConfig();
        var emailConfigured = SmtpComplete(config);

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var emailDebug = emailConfigured
                ? await ReadDebugAsync(connection, "email", now, cancellationToken).ConfigureAwait(false)
                : CpCommunicationsDebug.Idle();

            var operatorName = string.Empty;
            var handler = string.Empty;
            var sender = string.Empty;
            var smsConfigured = false;
            await using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT `name`, IFNULL(`handler`,''), IFNULL(`parameters_values`,'') FROM `sms_api` WHERE `active` = 1 LIMIT 1";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    smsConfigured = true;
                    operatorName = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                    handler = reader.GetString(1);
                    sender = SenderNumber(reader.GetString(2));
                }
            }

            var smsDebug = smsConfigured
                ? await ReadDebugAsync(connection, "sms", now, cancellationToken).ConfigureAwait(false)
                : CpCommunicationsDebug.Idle();

            var (profileEmail, profilePhone) = await ReadAdminProfileAsync(connection, adminUserId, cancellationToken).ConfigureAwait(false);
            var testEmail = profileEmail.Length > 0
                ? profileEmail
                : sessionEmail.Trim().Length > 0
                    ? sessionEmail.Trim()
                    : config.TryGetValue("from_email", out var fromEmail) ? fromEmail : string.Empty;

            return new CpCommunicationsView(
                emailConfigured,
                config.TryGetValue("from_email", out var from) ? from : string.Empty,
                config.TryGetValue("smtp_host", out var host) ? host : string.Empty,
                config.TryGetValue("smtp_port", out var port) ? port : string.Empty,
                emailDebug,
                smsConfigured,
                operatorName,
                handler,
                sender,
                smsDebug,
                testEmail,
                profilePhone,
                "database",
                string.Empty);
        }
        catch (DbException)
        {
            return CpCommunicationsView.Empty("database-error", "Tenant database unavailable.");
        }
    }

    private IReadOnlyDictionary<string, string> ReadConfig()
    {
        var path = ConfigPath;
        if (path.Length == 0 || !File.Exists(path))
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

    private static async Task<CpCommunicationsDebug> ReadDebugAsync(
        DbConnection connection,
        string name,
        long nowUnix,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT IFNULL(`status`,0), IFNULL(`time`,0), IFNULL(`debug_result`,'') FROM `debug_results` WHERE `name` = ? LIMIT 1");
        ErpDb.AddParameters(command, name);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return CpCommunicationsDebug.Idle();
        }

        return FormatDebug(
            true,
            Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
            Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
            reader.GetString(2),
            nowUnix);
    }

    private static async Task<(string Email, string Phone)> ReadAdminProfileAsync(
        DbConnection connection,
        int adminUserId,
        CancellationToken cancellationToken)
    {
        if (adminUserId <= 0)
        {
            return (string.Empty, string.Empty);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT IFNULL(`email`,''), IFNULL(`phone`,'') FROM `users` WHERE `user_id` = ? LIMIT 1");
        ErpDb.AddParameters(command, adminUserId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return (string.Empty, string.Empty);
        }

        return (reader.GetString(0).Trim(), reader.GetString(1).Trim());
    }
}
