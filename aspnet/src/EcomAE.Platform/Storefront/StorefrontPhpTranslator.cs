using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>translate_str_by_key()</c> / <c>translate_str_by_id()</c> (lang/dp_lang.php, cached branch) for one
/// connection: <c>is_error</c> strings resolve in English with the storefront <c>ERROR STR_KEY</c> prefix, a
/// non-empty <c>same</c> redirects the language, and a missing translation is <c>null</c> (echoed as empty).
/// </summary>
public sealed class StorefrontPhpTranslator
{
    private readonly DbConnection _connection;
    private readonly string _lang;
    private readonly Dictionary<string, string?> _cache = new(StringComparer.Ordinal);

    public StorefrontPhpTranslator(DbConnection connection, string lang = "en")
    {
        _connection = connection;
        _lang = string.IsNullOrWhiteSpace(lang) ? "en" : lang;
    }

    /// <summary>The PHP return value: <c>null</c> when the key has no translation.</summary>
    public async Task<string?> RawAsync(string? key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        string? value;
        try
        {
            value = await LookupAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            value = null;
        }

        _cache[key] = value;
        return value;
    }

    /// <summary><c>echo translate_str_by_id($key)</c>.</summary>
    public async Task<string> TextAsync(string? key, CancellationToken cancellationToken = default)
        => await RawAsync(key, cancellationToken).ConfigureAwait(false) ?? string.Empty;

    public Task<string> TextAsync(int id, CancellationToken cancellationToken = default)
        => TextAsync(id.ToString(CultureInfo.InvariantCulture), cancellationToken);

    private async Task<string?> LookupAsync(string key, CancellationToken cancellationToken)
    {
        var isError = false;
        string? same = null;
        try
        {
            await using var command = _connection.CreateCommand();
            command.CommandText = ErpDb.Positional("SELECT IFNULL(`is_error`,0), `same` FROM `lang_text_strings` WHERE `str_key` = ? LIMIT 1");
            ErpDb.AddParameters(command, key);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                isError = Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture) == 1;
                same = reader.IsDBNull(1) ? null : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture);
            }
        }
        catch (DbException)
        {
        }

        var lang = isError ? "en" : !string.IsNullOrEmpty(same) ? same : _lang;
        var value = await ErpDb.StringAsync(
            _connection,
            null,
            ErpDb.Positional("SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = ? LIMIT 1"),
            cancellationToken,
            key,
            lang).ConfigureAwait(false);
        return isError ? "ERROR STR_KEY: " + key + ". " + value : value;
    }
}
