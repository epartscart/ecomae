using System.Globalization;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP twins for lang editor ajax:
/// <c>ajax_set_is_custom.php</c>, <c>ajax_set_is_error.php</c>,
/// <c>ajax_set_same.php</c>, <c>ajax_set_used_found.php</c>,
/// <c>ajax_save_translation.php</c>, <c>ajax_save_description.php</c>,
/// <c>ajax_delete_not_used.php</c>, and <c>ajax_create_new_string.php</c>.
/// Restricted-mode config and used-found filesystem scan are not invented here.
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

    Task<ErpSimpleWriteResult> CreateStringAsync(CpLangCreateStringWriteRequest request, CancellationToken cancellationToken = default);
}

public sealed record CpLangCreateStringWriteRequest(
    string? Description = null,
    string? Same = null,
    int IsError = 0,
    int IsCustom = 0,
    int UsedFound = 0,
    string? DomainPath = null);

public sealed class CpLangWriteService : ICpLangWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private int _createdStrings;

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

    public async Task<ErpSimpleWriteResult> CreateStringAsync(
        CpLangCreateStringWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        var description = (request.Description ?? string.Empty).Trim();
        if (description.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Empty value does not acceptable");
        }

        if (description.Length > 255)
        {
            description = description[..255];
        }

        if (request.IsError is not 0 and not 1)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Incorrect value of is_error");
        }

        if (request.IsCustom is not 0 and not 1)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Incorrect value of is_custom");
        }

        if (request.UsedFound is not 0 and not 1 and not 2)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Incorrect value of used_found");
        }

        if (!TryNormalizeSame(request.Same, out var same, out var sameError))
        {
            return ErpSimpleWriteResult.Fail("invalid", sameError);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (same is not null)
        {
            var langs = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `lang_languages` WHERE `lang_code` = ?"),
                cancellationToken,
                same).ConfigureAwait(false);
            if (langs != 1)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Incorrect value of same");
            }
        }

        var languageCount = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `lang_languages`"),
            cancellationToken).ConfigureAwait(false);
        if (languageCount <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "No languages are configured.");
        }

        try
        {
            var key = await AllocateStrKeyAsync(connection, request.DomainPath, cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `lang_text_strings`
                    (`description`, `same`, `is_error`, `str_key`, `used_found`, `is_custom`)
                    VALUES (?, ?, ?, ?, ?, ?)
                    """),
                cancellationToken,
                description,
                same,
                request.IsError,
                key,
                request.UsedFound,
                request.IsCustom).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Language string created (" + key + ").", id > 0 ? id : 0);
        }
        catch (ErpWriteException ex)
        {
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
        catch (System.Data.Common.DbException)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Could not create the language string.");
        }
    }

    /// <summary>PHP ajax_create_new_string.php: <c>same</c> is <c>no</c> (null) or an existing lang code.</summary>
    public static bool TryNormalizeSame(string? raw, out string? same, out string error)
    {
        var text = (raw ?? string.Empty).Trim();
        same = null;
        error = string.Empty;
        if (text.Length == 0 || text.Equals("no", StringComparison.Ordinal))
        {
            return true;
        }

        if (text.Length is < 2 or > 16 || text.Any(ch => !char.IsLetterOrDigit(ch) && ch is not '-' and not '_'))
        {
            error = "Incorrect value of same";
            return false;
        }

        same = text;
        return true;
    }

    /// <summary>PHP <c>get_next_str_key()</c>: <c>time()_count_md5(domain_path)</c>.</summary>
    public static string NextStrKey(string? domainPath, int createdCount)
    {
        var count = createdCount < 1 ? 1 : createdCount;
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
               + "_"
               + count.ToString(CultureInfo.InvariantCulture)
               + "_"
               + LegacyPasswordVerifier.Md5Hex(domainPath ?? string.Empty);
    }

    private async Task<string> AllocateStrKeyAsync(
        System.Data.Common.DbConnection connection,
        string? domainPath,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 80; attempt++)
        {
            _createdStrings++;
            var key = NextStrKey(domainPath, _createdStrings);
            var found = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings` WHERE `str_key` = ?"),
                cancellationToken,
                key).ConfigureAwait(false);
            if (found == 0)
            {
                return key;
            }
        }

        throw new ErpWriteException("Could not allocate a language string key.");
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
