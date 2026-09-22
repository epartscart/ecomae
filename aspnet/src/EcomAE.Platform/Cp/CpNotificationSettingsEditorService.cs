using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>One <c>notifications_settings</c> row as listed by PHP <c>notifications.php</c> (labels already translated).</summary>
public sealed record CpNotificationRow(
    long Id,
    string Name,
    string Caption,
    string Event,
    string Description,
    int EmailOn,
    int SmsOn,
    int ForeseenEmail,
    int ForeseenSms);

/// <summary>One placeholder from the <c>vars</c> JSON column (<c>%name%</c>).</summary>
public sealed record CpNotificationVar(string Caption, string Name);

/// <summary>The editor payload of PHP <c>notification.php</c>: translated templates plus factory defaults for the restore button.</summary>
public sealed record CpNotificationDetail(
    long Id,
    string Name,
    string Caption,
    string Event,
    string Description,
    int SendForNotConfirmed,
    int EmailOn,
    int SmsOn,
    int ForeseenEmail,
    int ForeseenSms,
    string EmailSubject,
    string EmailBody,
    string SmsBody,
    string DefaultEmailSubject,
    string DefaultEmailBody,
    string DefaultSmsBody,
    IReadOnlyList<CpNotificationVar> Vars);

public interface ICpNotificationSettingsEditorService
{
    Task<IReadOnlyList<CpNotificationRow>> ListAsync(string langCode, CancellationToken cancellationToken = default);

    Task<CpNotificationDetail?> OpenAsync(long id, string langCode, CancellationToken cancellationToken = default);
}

/// <summary>Read side of the CP notification settings twin (<c>control/notifications_settings/notifications.php</c> + <c>notification.php</c>).</summary>
public sealed class CpNotificationSettingsEditorService : ICpNotificationSettingsEditorService
{
    private const string Columns =
        "`id`, IFNULL(`name`,''), IFNULL(`caption`,''), IFNULL(`event`,''), IFNULL(`description`,''), " +
        "IFNULL(`email_on`,0), IFNULL(`sms_on`,0), IFNULL(`foreseen_email`,0), IFNULL(`foreseen_sms`,0), " +
        "IFNULL(`send_for_not_confirmed`,0), IFNULL(`email_subject`,''), IFNULL(`email_body`,''), IFNULL(`sms_body`,''), " +
        "IFNULL(`default_email_subject`,''), IFNULL(`default_email_body`,''), IFNULL(`default_sms_body`,''), IFNULL(`vars`,'')";

    private readonly IErpWriteConnectionFactory _connections;

    public CpNotificationSettingsEditorService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<IReadOnlyList<CpNotificationRow>> ListAsync(string langCode, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var raw = new List<string[]>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT " + Columns + " FROM `notifications_settings` ORDER BY `id` ASC";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    raw.Add(ReadRow(reader));
                }
            }

            var translate = Translator(connection, CpCustomTranslationWriter.NormalizeLang(langCode), cancellationToken);
            var rows = new List<CpNotificationRow>(raw.Count);
            foreach (var r in raw)
            {
                rows.Add(new CpNotificationRow(
                    long.Parse(r[0], CultureInfo.InvariantCulture),
                    r[1],
                    await translate(r[2]).ConfigureAwait(false),
                    await translate(r[3]).ConfigureAwait(false),
                    await translate(r[4]).ConfigureAwait(false),
                    Flag(r[5]),
                    Flag(r[6]),
                    Flag(r[7]),
                    Flag(r[8])));
            }

            return rows;
        }
        catch (DbException)
        {
            return [];
        }
    }

    public async Task<CpNotificationDetail?> OpenAsync(long id, string langCode, CancellationToken cancellationToken = default)
    {
        if (id <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            string[]? r = null;
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional("SELECT " + Columns + " FROM `notifications_settings` WHERE `id` = ? LIMIT 1");
                var p = cmd.CreateParameter();
                p.Value = id;
                cmd.Parameters.Add(p);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    r = ReadRow(reader);
                }
            }

            if (r is null)
            {
                return null;
            }

            var translate = Translator(connection, CpCustomTranslationWriter.NormalizeLang(langCode), cancellationToken);
            var vars = new List<CpNotificationVar>();
            foreach (var v in ParseVars(r[16]))
            {
                vars.Add(new CpNotificationVar(await translate(v.Caption).ConfigureAwait(false), v.Name));
            }

            return new CpNotificationDetail(
                id,
                r[1],
                await translate(r[2]).ConfigureAwait(false),
                await translate(r[3]).ConfigureAwait(false),
                await translate(r[4]).ConfigureAwait(false),
                Flag(r[9]),
                Flag(r[5]),
                Flag(r[6]),
                Flag(r[7]),
                Flag(r[8]),
                await translate(r[10]).ConfigureAwait(false),
                await translate(r[11]).ConfigureAwait(false),
                await translate(r[12]).ConfigureAwait(false),
                await translate(r[13]).ConfigureAwait(false),
                await translate(r[14]).ConfigureAwait(false),
                await translate(r[15]).ConfigureAwait(false),
                vars);
        }
        catch (DbException)
        {
            return null;
        }
    }

    /// <summary>PHP <c>vars</c> JSON: <c>[{"name":"order_id","caption":"2201","type":"text"}, ...]</c>.</summary>
    public static IReadOnlyList<CpNotificationVar> ParseVars(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var list = new List<CpNotificationVar>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = item.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "";
                var caption = item.TryGetProperty("caption", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? "" : "";
                list.Add(new CpNotificationVar(caption, name));
            }

            return list;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// PHP <c>translate_str_by_id()</c> for the notification tables: a value that is a <c>lang_text_strings</c> key resolves to its
    /// translation (requested language first, then any); anything else (setup scripts store literal text) is shown as-is.
    /// </summary>
    internal static Func<string, Task<string>> Translator(DbConnection connection, string langCode, CancellationToken cancellationToken)
    {
        var cache = new Dictionary<string, string>(StringComparer.Ordinal);
        return async key =>
        {
            key ??= string.Empty;
            if (key.Length == 0 || key.Length > 64 || key.Any(char.IsWhiteSpace))
            {
                return key;
            }

            if (!cache.TryGetValue(key, out var text))
            {
                text = await ErpDb.StringAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? ORDER BY `lang_code` = ? DESC, `lang_code` = 'en' DESC LIMIT 1"),
                    cancellationToken,
                    key,
                    langCode).ConfigureAwait(false) ?? key;
                cache[key] = text;
            }

            return text;
        };
    }

    private static string[] ReadRow(DbDataReader reader)
    {
        var r = new string[17];
        for (var i = 0; i < r.Length; i++)
        {
            r[i] = reader.IsDBNull(i) ? "" : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? "";
        }

        return r;
    }

    private static int Flag(string value)
        => value is "1" or "1.0" || (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) && d != 0) ? 1 : 0;
}
