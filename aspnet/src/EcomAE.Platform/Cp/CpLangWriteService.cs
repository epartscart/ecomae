using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP twins for lang editor flag ajax:
/// <c>ajax_set_is_custom.php</c>, <c>ajax_set_is_error.php</c>,
/// <c>ajax_set_same.php</c>, <c>ajax_set_used_found.php</c>.
/// Restricted-mode config is not invented here.
/// </summary>
public interface ICpLangWriteService
{
    Task<ErpSimpleWriteResult> SetIsCustomAsync(string? strKey, int isCustom, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetIsErrorAsync(string? strKey, int isError, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetSameAsync(string? strKey, string? same, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetUsedFoundAsync(string? strKey, int usedFound, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveTranslationAsync(string? strKey, string? langCode, string? value, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveDescriptionAsync(string? strKey, string? value, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteUnusedCustomAsync(CancellationToken cancellationToken = default);
}

public sealed class CpLangWriteService : ICpLangWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpLangWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public Task<ErpSimpleWriteResult> SetIsCustomAsync(string? strKey, int isCustom, CancellationToken cancellationToken = default)
        => SetFlagAsync(strKey, isCustom, 0, 1, "is_custom", "is_custom", cancellationToken);

    public Task<ErpSimpleWriteResult> SetIsErrorAsync(string? strKey, int isError, CancellationToken cancellationToken = default)
        => SetFlagAsync(strKey, isError, 0, 1, "is_error", "is_error", cancellationToken);

    public Task<ErpSimpleWriteResult> SetUsedFoundAsync(string? strKey, int usedFound, CancellationToken cancellationToken = default)
        => SetFlagAsync(strKey, usedFound, 0, 2, "used_found", "used_found", cancellationToken);

    public async Task<ErpSimpleWriteResult> SetSameAsync(
        string? strKey,
        string? same,
        CancellationToken cancellationToken = default)
    {
        var key = NormalizeKey(strKey);
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A string key is required.");
        }

        var raw = (same ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A same value of no or a language code is required.");
        }

        string? value;
        if (raw.Equals("no", StringComparison.Ordinal))
        {
            value = null;
        }
        else if (raw.Length is < 2 or > 16 || raw.Any(ch => !char.IsLetterOrDigit(ch) && ch is not '-' and not '_'))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Incorrect value of same.");
        }
        else
        {
            value = raw;
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (value is not null)
        {
            var langs = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `lang_languages` WHERE `lang_code` = ?"),
                cancellationToken,
                value);
            if (langs != 1)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Incorrect value of same.");
            }
        }

        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings` WHERE `str_key` = ?"),
            cancellationToken,
            key);
        if (exists != 1)
        {
            return ErpSimpleWriteResult.Fail("not_found", "No such string.");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `lang_text_strings` SET `same` = ? WHERE `str_key` = ?"),
            cancellationToken,
            value, key);
        return ErpSimpleWriteResult.Ok("Language same flag saved.", 0);
    }

    public async Task<ErpSimpleWriteResult> SaveTranslationAsync(
        string? strKey,
        string? langCode,
        string? value,
        CancellationToken cancellationToken = default)
    {
        var key = NormalizeKey(strKey);
        var lang = NormalizeLang(langCode);
        var text = value ?? string.Empty;
        if (key.Length == 0 || lang.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A string key and language code are required.");
        }

        if (text.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Empty value does not acceptable.");
        }

        if (text.Length > 8000)
        {
            text = text[..8000];
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var strings = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings` WHERE `str_key` = ?"),
            cancellationToken,
            key);
        if (strings != 1)
        {
            return ErpSimpleWriteResult.Fail("not_found", "String not found.");
        }

        var langs = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `lang_languages` WHERE `lang_code` = ?"),
            cancellationToken,
            lang);
        if (langs != 1)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Language not found.");
        }

        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = ?"),
            cancellationToken,
            key, lang);
        if (existing == 1)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `lang_text_strings_translation` SET `value` = ? WHERE `str_key` = ? AND `lang_code` = ?"),
                cancellationToken,
                text, key, lang);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `lang_text_strings_translation` (`str_key`,`lang_code`,`value`) VALUES (?,?,?)"),
                cancellationToken,
                key, lang, text);
        }

        return ErpSimpleWriteResult.Ok("Translation saved.", 0);
    }

    public async Task<ErpSimpleWriteResult> SaveDescriptionAsync(
        string? strKey,
        string? value,
        CancellationToken cancellationToken = default)
    {
        var key = NormalizeKey(strKey);
        var text = value ?? string.Empty;
        if (key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A string key is required.");
        }

        if (text.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Empty value does not acceptable.");
        }

        if (text.Length > 8000)
        {
            text = text[..8000];
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings` WHERE `str_key` = ?"),
            cancellationToken,
            key);
        if (exists != 1)
        {
            return ErpSimpleWriteResult.Fail("not_found", "String not found.");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `lang_text_strings` SET `description` = ? WHERE `str_key` = ?"),
            cancellationToken,
            text, key);
        return ErpSimpleWriteResult.Ok("String description saved.", 0);
    }

    public async Task<ErpSimpleWriteResult> DeleteUnusedCustomAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var translations = await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("DELETE FROM `lang_text_strings_translation` WHERE `str_key` IN (SELECT `str_key` FROM `lang_text_strings` WHERE `is_custom` = ? AND `used_found` = ?)"),
            cancellationToken,
            1, 2);
        var strings = await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("DELETE FROM `lang_text_strings` WHERE `is_custom` = ? AND `used_found` = ?"),
            cancellationToken,
            1, 2);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ErpSimpleWriteResult(true, "ok", "Unused custom strings deleted.", 0, Math.Max(translations + strings, 1));
    }

    private static string NormalizeLang(string? langCode)
    {
        var lang = (langCode ?? string.Empty).Trim();
        if (lang.Length is < 2 or > 16 || lang.Any(ch => !char.IsLetterOrDigit(ch) && ch is not '-' and not '_'))
        {
            return string.Empty;
        }

        return lang;
    }

    private async Task<ErpSimpleWriteResult> SetFlagAsync(
        string? strKey,
        int flag,
        int min,
        int max,
        string column,
        string label,
        CancellationToken cancellationToken)
    {
        var key = NormalizeKey(strKey);
        if (key.Length == 0 || flag < min || flag > max)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A string key and a valid " + label + " flag are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings` WHERE `str_key` = ?"),
            cancellationToken,
            key);
        if (exists != 1)
        {
            return ErpSimpleWriteResult.Fail("not_found", "No such string.");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `lang_text_strings` SET `" + column + "` = ? WHERE `str_key` = ?"),
            cancellationToken,
            flag, key);
        return ErpSimpleWriteResult.Ok("Language " + label + " flag saved.", 0);
    }

    private static string NormalizeKey(string? strKey)
    {
        var key = (strKey ?? string.Empty).Trim();
        if (key.Length > 190)
        {
            key = key[..190];
        }

        return key;
    }
}
