using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpNotificationToggleRequest(long NotificationId, string? Type, int SetSend);

/// <summary>PHP <c>notification.php</c> <c>action=save</c> form (checkbox presence = on).</summary>
public sealed record CpNotificationSaveRequest(
    long NotificationId,
    bool EmailOn,
    string? EmailSubject,
    string? EmailBody,
    bool SmsOn,
    string? SmsBody,
    string? LangCode = null);

public interface ICpNotificationSettingsWriteService
{
    Task<ErpSimpleWriteResult> ToggleAsync(CpNotificationToggleRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> RestoreDefaultsAsync(IReadOnlyList<long> notificationIds, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveAsync(CpNotificationSaveRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Live PHP <c>notifications.php</c> (<c>action=set_send</c>, <c>action=set_default</c>) and <c>notification.php</c> (<c>action=save</c>).
/// Template text lives in <c>lang_text_strings_translation</c> when the column holds a string key (PHP <c>save_custom_translation(..., let_edit_not_custom=true)</c>);
/// rows seeded with literal text are edited in place.
/// </summary>
public sealed class CpNotificationSettingsWriteService : ICpNotificationSettingsWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpNotificationSettingsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string? NormalizeType(string? type)
    {
        var raw = (type ?? string.Empty).Trim().ToLowerInvariant();
        return raw is "email" or "sms" ? raw : null;
    }

    public static int NormalizeFlag(int setSend)
        => setSend == 0 ? 0 : 1;

    public async Task<ErpSimpleWriteResult> ToggleAsync(CpNotificationToggleRequest request, CancellationToken cancellationToken = default)
    {
        if (request.NotificationId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A notification id is required.");
        }

        var type = NormalizeType(request.Type);
        if (type is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Type must be email or sms.");
        }

        var flag = NormalizeFlag(request.SetSend);
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, "notifications_settings", cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("invalid", "notifications_settings table is not provisioned. Schema ensure stays on the Classic twin.");
            }

            var exists = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `notifications_settings` WHERE `id` = ?"),
                cancellationToken,
                request.NotificationId).ConfigureAwait(false);
            if (exists <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Notification setting was not found.");
            }

            if (flag == 1)
            {
                var foreseenCol = type == "email" ? "foreseen_email" : "foreseen_sms";
                var foreseen = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT IFNULL(`" + foreseenCol + "`, 0) FROM `notifications_settings` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    request.NotificationId).ConfigureAwait(false);
                if (foreseen == 0)
                {
                    return ErpSimpleWriteResult.Fail("not_foreseen", "This event does not support that channel.");
                }
            }

            var onCol = type == "email" ? "email_on" : "sms_on";
            var writes = await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `notifications_settings` SET `" + onCol + "` = ? WHERE `id` = ?"),
                cancellationToken,
                flag,
                request.NotificationId).ConfigureAwait(false);
            if (writes <= 0)
            {
                return ErpSimpleWriteResult.Fail("unchanged", "Channel flag was not updated.");
            }

            return ErpSimpleWriteResult.Ok(
                flag == 1 ? "Channel enabled." : "Channel disabled.",
                request.NotificationId);
        }
        catch (Exception ex)
        {
            return ErpSimpleWriteResult.Fail("db", ex.Message);
        }
    }

    public static IReadOnlyList<long> ParseIds(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.StartsWith('['))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                return doc.RootElement.EnumerateArray()
                    .Select(e => e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out var n) ? n : long.TryParse(e.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : 0)
                    .Where(v => v > 0)
                    .Distinct()
                    .ToList();
            }
            catch (JsonException)
            {
                return [];
            }
        }

        return text
            .Split([',', ' ', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0)
            .Where(v => v > 0)
            .Distinct()
            .ToList();
    }

    /// <summary>PHP <c>set_default</c>: copy every language of the factory string onto the edited string, then <c>email_on = foreseen_email, sms_on = foreseen_sms</c>.</summary>
    public async Task<ErpSimpleWriteResult> RestoreDefaultsAsync(IReadOnlyList<long> notificationIds, CancellationToken cancellationToken = default)
    {
        if (notificationIds.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select at least one notification.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, "notifications_settings", cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("invalid", "notifications_settings table is not provisioned.");
            }

            var langs = await LanguagesAsync(connection, cancellationToken).ConfigureAwait(false);
            await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var writes = 0;
            foreach (var id in notificationIds)
            {
                var n = await ReadTemplateAsync(connection, tx, id, cancellationToken).ConfigureAwait(false);
                if (n is null)
                {
                    await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Fail("not_found", "Notification not found");
                }

                if (n.ForeseenEmail == 1)
                {
                    writes += await RestoreFieldAsync(connection, tx, id, "email_subject", n.EmailSubject, n.DefaultEmailSubject, langs, cancellationToken).ConfigureAwait(false);
                    writes += await RestoreFieldAsync(connection, tx, id, "email_body", n.EmailBody, n.DefaultEmailBody, langs, cancellationToken).ConfigureAwait(false);
                }

                if (n.ForeseenSms == 1)
                {
                    writes += await RestoreFieldAsync(connection, tx, id, "sms_body", n.SmsBody, n.DefaultSmsBody, langs, cancellationToken).ConfigureAwait(false);
                }

                writes += await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional("UPDATE `notifications_settings` SET `email_on` = `foreseen_email`, `sms_on` = `foreseen_sms` WHERE `id` = ?"),
                    cancellationToken,
                    id).ConfigureAwait(false);
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(true, "ok", $"Factory settings restored for {notificationIds.Count} notification(s).", notificationIds[0], writes);
        }
        catch (Exception ex)
        {
            return ErpSimpleWriteResult.Fail("db", ex.Message);
        }
    }

    /// <summary>PHP <c>notification.php</c> <c>action=save</c> inside one transaction.</summary>
    public async Task<ErpSimpleWriteResult> SaveAsync(CpNotificationSaveRequest request, CancellationToken cancellationToken = default)
    {
        if (request.NotificationId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A notification id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var lang = CpCustomTranslationWriter.NormalizeLang(request.LangCode);
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, "notifications_settings", cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("invalid", "notifications_settings table is not provisioned.");
            }

            await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var n = await ReadTemplateAsync(connection, tx, request.NotificationId, cancellationToken).ConfigureAwait(false);
            if (n is null)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("not_found", "Notification setting was not found.");
            }

            var writes = 0;
            var emailOn = 0;
            var smsOn = 0;
            if (n.ForeseenEmail == 1)
            {
                emailOn = request.EmailOn ? 1 : 0;
                writes += await SaveFieldAsync(connection, tx, request.NotificationId, "email_subject", n.EmailSubject, request.EmailSubject ?? "", lang, cancellationToken).ConfigureAwait(false);
                writes += await SaveFieldAsync(connection, tx, request.NotificationId, "email_body", n.EmailBody, request.EmailBody ?? "", lang, cancellationToken).ConfigureAwait(false);
            }

            if (n.ForeseenSms == 1)
            {
                smsOn = request.SmsOn ? 1 : 0;
                writes += await SaveFieldAsync(connection, tx, request.NotificationId, "sms_body", n.SmsBody, request.SmsBody ?? "", lang, cancellationToken).ConfigureAwait(false);
            }

            writes += await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional("UPDATE `notifications_settings` SET `email_on` = ?, `sms_on` = ? WHERE `id` = ?"),
                cancellationToken,
                emailOn,
                smsOn,
                request.NotificationId).ConfigureAwait(false);

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(true, "ok", "Notification saved.", request.NotificationId, Math.Max(1, writes));
        }
        catch (Exception ex)
        {
            return ErpSimpleWriteResult.Fail("db", ex.Message);
        }
    }

    private sealed record Template(int ForeseenEmail, int ForeseenSms, string EmailSubject, string EmailBody, string SmsBody, string DefaultEmailSubject, string DefaultEmailBody, string DefaultSmsBody);

    private static async Task<Template?> ReadTemplateAsync(DbConnection connection, DbTransaction tx, long id, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = ErpDb.Positional(
            "SELECT IFNULL(`foreseen_email`,0), IFNULL(`foreseen_sms`,0), IFNULL(`email_subject`,''), IFNULL(`email_body`,''), IFNULL(`sms_body`,''), " +
            "IFNULL(`default_email_subject`,''), IFNULL(`default_email_body`,''), IFNULL(`default_sms_body`,'') FROM `notifications_settings` WHERE `id` = ? LIMIT 1");
        var p = cmd.CreateParameter();
        p.Value = id;
        cmd.Parameters.Add(p);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new Template(
            Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
            Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
            Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? "",
            Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture) ?? "",
            Convert.ToString(reader.GetValue(4), CultureInfo.InvariantCulture) ?? "",
            Convert.ToString(reader.GetValue(5), CultureInfo.InvariantCulture) ?? "",
            Convert.ToString(reader.GetValue(6), CultureInfo.InvariantCulture) ?? "",
            Convert.ToString(reader.GetValue(7), CultureInfo.InvariantCulture) ?? "");
    }

    private static async Task<IReadOnlyList<string>> LanguagesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var langs = new List<string>();
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT `lang_code` FROM `lang_languages`";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var code = Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? "";
                if (code.Length > 0)
                {
                    langs.Add(code);
                }
            }
        }
        catch (DbException)
        {
        }

        return langs.Count == 0 ? ["en"] : langs;
    }

    private static async Task<bool> IsStringKeyAsync(DbConnection connection, DbTransaction tx, string key, CancellationToken cancellationToken)
    {
        if (key.Length == 0 || key.Length > 64 || key.Any(char.IsWhiteSpace))
        {
            return false;
        }

        return await ErpDb.LongAsync(
            connection,
            tx,
            ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings` WHERE `str_key` = ?"),
            cancellationToken,
            key).ConfigureAwait(false) > 0;
    }

    /// <summary>PHP restore query per language: <c>UPDATE lang_text_strings_translation SET value = (default value) WHERE lang_code = ? AND str_key = ?</c>.</summary>
    private static async Task<int> RestoreFieldAsync(
        DbConnection connection,
        DbTransaction tx,
        long id,
        string column,
        string key,
        string defaultKey,
        IReadOnlyList<string> langs,
        CancellationToken cancellationToken)
    {
        if (await IsStringKeyAsync(connection, tx, key, cancellationToken).ConfigureAwait(false)
            && await IsStringKeyAsync(connection, tx, defaultKey, cancellationToken).ConfigureAwait(false))
        {
            var writes = 0;
            foreach (var lang in langs)
            {
                var value = await ErpDb.StringAsync(
                    connection,
                    tx,
                    ErpDb.Positional("SELECT `value` FROM `lang_text_strings_translation` WHERE `lang_code` = ? AND `str_key` = ? LIMIT 1"),
                    cancellationToken,
                    lang,
                    defaultKey).ConfigureAwait(false);
                if (value is null)
                {
                    continue;
                }

                writes += await UpsertTranslationAsync(connection, tx, key, lang, value, cancellationToken).ConfigureAwait(false);
            }

            return writes;
        }

        return await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("UPDATE `notifications_settings` SET `" + column + "` = `default_" + column + "` WHERE `id` = ?"),
            cancellationToken,
            id).ConfigureAwait(false);
    }

    /// <summary>PHP <c>save_custom_translation(key, value, null, let_edit_not_custom=true)</c>: the key never changes, only its work-language value.</summary>
    private static async Task<int> SaveFieldAsync(
        DbConnection connection,
        DbTransaction tx,
        long id,
        string column,
        string key,
        string value,
        string lang,
        CancellationToken cancellationToken)
    {
        if (await IsStringKeyAsync(connection, tx, key, cancellationToken).ConfigureAwait(false))
        {
            return await UpsertTranslationAsync(connection, tx, key, lang, value, cancellationToken).ConfigureAwait(false);
        }

        return await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("UPDATE `notifications_settings` SET `" + column + "` = ? WHERE `id` = ?"),
            cancellationToken,
            value,
            id).ConfigureAwait(false);
    }

    private static async Task<int> UpsertTranslationAsync(DbConnection connection, DbTransaction tx, string key, string lang, string value, CancellationToken cancellationToken)
    {
        var updated = await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("UPDATE `lang_text_strings_translation` SET `value` = ? WHERE `str_key` = ? AND `lang_code` = ?"),
            cancellationToken,
            value,
            key,
            lang).ConfigureAwait(false);
        if (updated > 0)
        {
            return updated;
        }

        var exists = await ErpDb.LongAsync(
            connection,
            tx,
            ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = ?"),
            cancellationToken,
            key,
            lang).ConfigureAwait(false);
        if (exists > 0)
        {
            return 1;
        }

        return await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("INSERT INTO `lang_text_strings_translation` (`value`, `str_key`, `lang_code`) VALUES (?,?,?)"),
            cancellationToken,
            value,
            key,
            lang).ConfigureAwait(false);
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }
}
