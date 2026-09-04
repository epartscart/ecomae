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
