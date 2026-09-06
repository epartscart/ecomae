using System.Globalization;
using System.Net;
using EcomAE.Platform.Auth;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>offices_cash.php</c> action=add and <c>offices_cash_editor.php</c> add/del twins.
/// </summary>
public interface IErpOfficesCashWriteService
{
    Task<ErpSimpleWriteResult> AddEntryAsync(
        long managerId,
        long officeId,
        int income,
        decimal amount,
        long operationCodeId,
        string? comment,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddCodeAsync(
        long managerId,
        long officeId,
        int income,
        string? name,
        string? langStrId,
        string? langCode,
        string? domainPath,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteCodeAsync(
        long managerId,
        long officeId,
        long codeId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpOfficesCashWriteService : IErpOfficesCashWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private int _createdStrings;

    public ErpOfficesCashWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddEntryAsync(
        long managerId,
        long officeId,
        int income,
        decimal amount,
        long operationCodeId,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        if (managerId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A signed-in manager is required.");
        }

        if (officeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "An office is required.");
        }

        if (amount <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Amount must be greater than zero.");
        }

        if (operationCodeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A cash operation code is required.");
        }

        var flag = income > 0 ? 1 : 0;
        var note = SanitizeComment(comment);

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var office = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_offices` WHERE `id` = ? AND `users` LIKE ? LIMIT 1"),
            cancellationToken,
            officeId,
            "%\"" + managerId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\"%");
        if (office <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "This office is not assigned to the signed-in manager.");
        }

        var code = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_offices_cash_codes` WHERE `income` = ? AND `id` = ? AND `office_id` = ? LIMIT 1"),
            cancellationToken,
            flag, operationCodeId, officeId);
        if (code <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "The cash operation code is not valid for this office.");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `shop_offices_cash` (`id`, `office_id`, `manager_id`, `time`, `income`, `amount`, `operation_code`, `comment`) "
                + "VALUES (NULL, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            officeId,
            managerId,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            flag,
            amount,
            operationCodeId,
            note);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Cash entry saved.", id);
    }

    public async Task<ErpSimpleWriteResult> AddCodeAsync(
        long managerId,
        long officeId,
        int income,
        string? name,
        string? langStrId,
        string? langCode,
        string? domainPath,
        CancellationToken cancellationToken = default)
    {
        if (managerId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A signed-in manager is required.");
        }

        if (officeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "An office is required.");
        }

        var caption = SanitizeCaption(name);
        if (caption.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A cash operation name is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var flag = income > 0 ? 1 : 0;
        var lang = NormalizeLang(langCode);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var office = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_offices` WHERE `id` = ? AND `users` LIKE ? LIMIT 1"),
            cancellationToken,
            officeId,
            "%\"" + managerId.ToString(CultureInfo.InvariantCulture) + "\"%");
        if (office <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "This office is not assigned to the signed-in manager.");
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        string key;
        try
        {
            var saved = await SaveCustomTranslationAsync(
                connection,
                transaction,
                langStrId,
                caption,
                lang,
                domainPath,
                cancellationToken).ConfigureAwait(false);
            if (saved.Error is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("invalid", saved.Error);
            }

            key = saved.Key!;
            var exists = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `id` FROM `shop_offices_cash_codes` WHERE `income` = ? AND `name` = ? AND `office_id` = ? LIMIT 1"),
                cancellationToken,
                flag, key, officeId).ConfigureAwait(false);
            if (exists > 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("exists", "A cash operation code with this name already exists for the office.");
            }

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `shop_offices_cash_codes` (`id`, `income`, `name`, `office_id`) VALUES (NULL, ?, ?, ?)"),
                cancellationToken,
                flag, key, officeId).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not save the cash operation code.");
        }

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return new ErpSimpleWriteResult(true, "ok", "Cash operation code saved.", id, 1);
    }

    public async Task<ErpSimpleWriteResult> DeleteCodeAsync(
        long managerId,
        long officeId,
        long codeId,
        CancellationToken cancellationToken = default)
    {
        if (managerId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A signed-in manager is required.");
        }

        if (officeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "An office is required.");
        }

        if (codeId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A cash operation code is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var office = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_offices` WHERE `id` = ? AND `users` LIKE ? LIMIT 1"),
            cancellationToken,
            officeId,
            "%\"" + managerId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\"%");
        if (office <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "This office is not assigned to the signed-in manager.");
        }

        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_offices_cash_codes` WHERE `id` = ? AND `office_id` = ? LIMIT 1"),
            cancellationToken,
            codeId, officeId);
        if (existing <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Cash operation code was not found for this office.");
        }

        var used = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_offices_cash` WHERE `operation_code` = ? LIMIT 1"),
            cancellationToken,
            codeId);
        if (used > 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "This cash operation code is used by existing cash entries.");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `shop_offices_cash_codes` WHERE `id` = ?"),
            cancellationToken,
            codeId);
        return ErpSimpleWriteResult.Ok("Cash operation code deleted.", codeId);
    }

    /// <summary>PHP offices_cash_editor name: strip quotes/backslash/CR/LF/tab, htmlentities.</summary>
    public static string SanitizeCaption(string? raw)
    {
        var name = (raw ?? string.Empty).Trim();
        name = name
            .Replace("\"", string.Empty, StringComparison.Ordinal)
            .Replace("\\", string.Empty, StringComparison.Ordinal)
            .Replace("'", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\t", string.Empty, StringComparison.Ordinal);
        return WebUtility.HtmlEncode(name);
    }

    /// <summary>PHP <c>get_next_str_key</c>: <c>time()_count_md5(domain_path)</c>.</summary>
    public static string NextStrKey(string? domainPath, int createdCount)
        => DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
           + "_"
           + Math.Max(1, createdCount).ToString(CultureInfo.InvariantCulture)
           + "_"
           + LegacyPasswordVerifier.Md5Hex(domainPath ?? string.Empty);

    /// <summary>PHP offices_cash comment: strip quotes/backslash/CR/tab, htmlentities, newline → br.</summary>
    internal static string SanitizeComment(string? raw)
    {
        var comment = (raw ?? string.Empty).Trim();
        comment = comment
            .Replace("\"", string.Empty, StringComparison.Ordinal)
            .Replace("\\", string.Empty, StringComparison.Ordinal)
            .Replace("'", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\t", string.Empty, StringComparison.Ordinal);
        comment = WebUtility.HtmlEncode(comment);
        return comment.Replace("\n", "<br/>", StringComparison.Ordinal);
    }

    private async Task<(string? Key, string? Error)> SaveCustomTranslationAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string? langStrId,
        string caption,
        string langCode,
        string? domainPath,
        CancellationToken cancellationToken)
    {
        var existingKey = (langStrId ?? string.Empty).Trim();
        if (existingKey is "0")
        {
            existingKey = string.Empty;
        }

        var isCustom = 0L;
        var hasTranslation = 0L;
        if (existingKey.Length > 0)
        {
            isCustom = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `is_custom` FROM `lang_text_strings` WHERE `str_key` = ? LIMIT 1"),
                cancellationToken,
                existingKey).ConfigureAwait(false);
            if (isCustom == 0)
            {
                var found = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings` WHERE `str_key` = ?"),
                    cancellationToken,
                    existingKey).ConfigureAwait(false);
                if (found == 0)
                {
                    existingKey = string.Empty;
                }
            }

            if (existingKey.Length > 0)
            {
                hasTranslation = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = ?"),
                    cancellationToken,
                    existingKey, langCode).ConfigureAwait(false);
            }
        }

        string key;
        if (existingKey.Length == 0 || isCustom == 0)
        {
            _createdStrings++;
            key = NextStrKey(domainPath, _createdStrings);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `lang_text_strings` (`description`, `same`, `is_error`, `is_custom`, `str_key`) VALUES (?,?,?,?,?)"),
                cancellationToken,
                existingKey.Length == 0
                    ? "CASH OPERATIONS TYPES EDITING CUSTOM CREATED"
                    : "CASH OPERATIONS TYPES EDITING CUSTOM COPIED",
                null,
                0,
                1,
                key).ConfigureAwait(false);
            hasTranslation = 0;
        }
        else
        {
            key = existingKey;
        }

        if (hasTranslation > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `lang_text_strings_translation` SET `value` = ? WHERE `str_key` = ? AND `lang_code` = ?"),
                cancellationToken,
                caption, key, langCode).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `lang_text_strings_translation` (`value`, `str_key`, `lang_code`) VALUES (?,?,?)"),
                cancellationToken,
                caption, key, langCode).ConfigureAwait(false);
        }

        return (key, null);
    }

    private static string NormalizeLang(string? langCode)
    {
        var lang = (langCode ?? string.Empty).Trim().ToLowerInvariant();
        if (lang.Length is < 2 or > 16)
        {
            return "en";
        }

        return lang;
    }
}
