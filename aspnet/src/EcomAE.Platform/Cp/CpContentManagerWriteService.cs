using System.Globalization;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>content_manager.php</c> / <c>content.php</c> twins for single-id
/// <c>set_published_flag</c>, <c>set_main_flag</c>, and <c>save_content</c> body save.
/// TinyMCE image upload and system-content config stay PHP.
/// Always refuse <c>system_flag=1</c> (do not invent <c>DP_Config-&gt;allow_edit_system_content</c>).
/// </summary>
public interface ICpContentManagerWriteService
{
    Task<ErpSimpleWriteResult> SetPublishedAsync(long contentId, int publishedFlag, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetMainAsync(long contentId, int isFrontend, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveBodyAsync(CpContentBodySaveRequest request, CancellationToken cancellationToken = default);
}

public sealed record CpContentBodySaveRequest(
    long ContentId,
    string? ContentType = null,
    string? Content = null,
    string? ContentLangStrId = null,
    string? LangCode = null,
    string? DomainPath = null);

public sealed class CpContentManagerWriteService : ICpContentManagerWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private int _createdStrings;

    public CpContentManagerWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetPublishedAsync(
        long contentId,
        int publishedFlag,
        CancellationToken cancellationToken = default)
    {
        if (contentId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A content page id is required.");
        }

        var flag = publishedFlag > 0 ? 1 : 0;
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var found = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `content` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            contentId);
        if (found <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Content page was not found.");
        }

        var systemFlag = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `system_flag` FROM `content` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            contentId);
        if (systemFlag > 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "System pages cannot change publish state.");
        }

        if (flag == 0)
        {
            var mainFlag = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `main_flag` FROM `content` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                contentId);
            if (mainFlag > 0)
            {
                return ErpSimpleWriteResult.Fail("invalid", "The main page cannot be unpublished.");
            }
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `content` SET `published_flag` = ? WHERE `id` = ?"),
            cancellationToken,
            flag, contentId);
        return ErpSimpleWriteResult.Ok("Publish flag updated.", contentId);
    }

    public async Task<ErpSimpleWriteResult> SetMainAsync(
        long contentId,
        int isFrontend,
        CancellationToken cancellationToken = default)
    {
        if (contentId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A content page id is required.");
        }

        var frontend = isFrontend > 0 ? 1 : 0;
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var found = await ErpDb.LongAsync(
            connection,
            tx,
            ErpDb.Positional("SELECT `id` FROM `content` WHERE `id` = ? AND `is_frontend` = ? LIMIT 1"),
            cancellationToken,
            contentId, frontend);
        if (found <= 0)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Content page was not found.");
        }

        var level = await ErpDb.LongAsync(
            connection,
            tx,
            ErpDb.Positional("SELECT `level` FROM `content` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            contentId);
        if (level > 1)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Nested pages cannot be set as the main page.");
        }

        var published = await ErpDb.LongAsync(
            connection,
            tx,
            ErpDb.Positional("SELECT `published_flag` FROM `content` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            contentId);
        if (published == 0)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Unpublished pages cannot be set as the main page.");
        }

        var alreadyMain = await ErpDb.LongAsync(
            connection,
            tx,
            ErpDb.Positional("SELECT `main_flag` FROM `content` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            contentId);
        if (alreadyMain > 0)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "This page is already the main page.");
        }

        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("UPDATE `content` SET `main_flag` = 1 WHERE `id` = ?"),
            cancellationToken,
            contentId);
        await ErpDb.ExecuteAsync(
            connection,
            tx,
            ErpDb.Positional("UPDATE `content` SET `main_flag` = 0 WHERE `id` != ? AND `is_frontend` = ? AND `main_flag` = 1"),
            cancellationToken,
            contentId, frontend);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Main page updated.", contentId);
    }

    public async Task<ErpSimpleWriteResult> SaveBodyAsync(
        CpContentBodySaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ContentId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A content page id is required.");
        }

        if (!TryNormalizeType(request.ContentType, out var contentType))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Content type must be text or php.");
        }

        string stored;
        if (contentType == "php")
        {
            if (!TryNormalizePhpPath(request.Content, out stored, out var pathError))
            {
                return ErpSimpleWriteResult.Fail("invalid", pathError);
            }
        }
        else
        {
            stored = StripPhp(request.Content);
            if (stored.Length > 1_000_000)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Content body is too long.");
            }
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var found = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `content` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            request.ContentId).ConfigureAwait(false);
        if (found <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Content page was not found.");
        }

        var systemFlag = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `system_flag` FROM `content` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            request.ContentId).ConfigureAwait(false);
        if (systemFlag > 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "System pages cannot be edited here.");
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (contentType == "text")
            {
                stored = await RequireTranslationAsync(
                    connection,
                    transaction,
                    request.ContentLangStrId,
                    stored,
                    NormalizeLang(request.LangCode),
                    request.DomainPath,
                    cancellationToken).ConfigureAwait(false);
            }

            var edited = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var rows = await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    """
                    UPDATE `content`
                    SET `content_type` = ?, `content` = ?, `time_edited` = ?
                    WHERE `id` = ?
                      AND (`system_flag` IS NULL OR `system_flag` <> 1)
                    """),
                cancellationToken,
                contentType,
                stored,
                edited,
                request.ContentId).ConfigureAwait(false);
            if (rows <= 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("invalid", "Content page not found or is a locked system page.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok($"Content #{request.ContentId} body saved ({contentType}).", request.ContentId);
        }
        catch (ErpWriteException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not save the content body.");
        }
    }

    /// <summary>PHP content.php save_content accepts only exact <c>text</c> or <c>php</c> (case-insensitive).</summary>
    public static bool TryNormalizeType(string? raw, out string type)
    {
        type = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (type is "text" or "php")
        {
            return true;
        }

        type = string.Empty;
        return false;
    }

    /// <summary>PHP content.php: last <c>.</c> segment must be <c>php</c>. Also refuse <c>..</c> and oversize paths.</summary>
    public static bool TryNormalizePhpPath(string? raw, out string path, out string error)
    {
        path = (raw ?? string.Empty).Trim();
        error = string.Empty;
        if (path.Length == 0)
        {
            error = "PHP content path is required.";
            return false;
        }

        var lastDot = path.LastIndexOf('.');
        var extension = lastDot >= 0 ? path[(lastDot + 1)..] : path;
        if (path.Length > 500
            || path.Contains("..", StringComparison.Ordinal)
            || !extension.Equals("php", StringComparison.OrdinalIgnoreCase))
        {
            error = "PHP content must be a .php file path.";
            path = string.Empty;
            return false;
        }

        return true;
    }

    /// <summary>PHP content.php text-type strip of <c>&lt;?</c> / <c>?&gt;</c> (same as additional texts).</summary>
    public static string StripPhp(string? raw)
    {
        var text = raw ?? string.Empty;
        while (text.Contains("<?", StringComparison.Ordinal))
        {
            text = text.Replace("<?", "[CODE]", StringComparison.Ordinal);
        }

        while (text.Contains("?>", StringComparison.Ordinal))
        {
            text = text.Replace("?>", "[/CODE]", StringComparison.Ordinal);
        }

        return text;
    }

    private async Task<string> RequireTranslationAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string? langStrId,
        string value,
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
                    existingKey,
                    langCode).ConfigureAwait(false);
            }
        }

        string key;
        if (existingKey.Length == 0 || isCustom == 0)
        {
            key = await AllocateStrKeyAsync(connection, transaction, domainPath, cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `lang_text_strings` (`description`, `same`, `is_error`, `is_custom`, `str_key`) VALUES (?,?,?,?,?)"),
                cancellationToken,
                "CONTENT EDITING",
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
                value,
                key,
                langCode).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `lang_text_strings_translation` (`value`, `str_key`, `lang_code`) VALUES (?,?,?)"),
                cancellationToken,
                value,
                key,
                langCode).ConfigureAwait(false);
        }

        return key;
    }

    private async Task<string> AllocateStrKeyAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string? domainPath,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 80; attempt++)
        {
            _createdStrings++;
            var key = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
                      + "_"
                      + _createdStrings.ToString(CultureInfo.InvariantCulture)
                      + "_"
                      + LegacyPasswordVerifier.Md5Hex(domainPath ?? string.Empty);
            var found = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings` WHERE `str_key` = ?"),
                cancellationToken,
                key).ConfigureAwait(false);
            if (found == 0)
            {
                return key;
            }
        }

        throw new ErpWriteException("Could not allocate a content translation key.");
    }

    private static string NormalizeLang(string? langCode)
    {
        var lang = (langCode ?? string.Empty).Trim().ToLowerInvariant();
        return lang.Length is < 2 or > 16 ? "en" : lang;
    }
}
