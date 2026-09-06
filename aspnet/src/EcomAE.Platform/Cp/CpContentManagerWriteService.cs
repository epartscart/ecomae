using System.Globalization;
using System.Net;
using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>content_manager.php</c> / <c>content.php</c> / <c>content_create_edit.php</c> twins
/// for publish, main, body save, and no-tree metadata create/edit.
/// TinyMCE image upload and system-content config stay PHP.
/// Always refuse <c>system_flag=1</c> (do not invent <c>DP_Config-&gt;allow_edit_system_content</c>).
/// </summary>
public interface ICpContentManagerWriteService
{
    Task<ErpSimpleWriteResult> SetPublishedAsync(long contentId, int publishedFlag, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetMainAsync(long contentId, int isFrontend, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveBodyAsync(CpContentBodySaveRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveMetaAsync(CpContentMetaSaveRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveTreeAsync(CpContentTreeSaveRequest request, CancellationToken cancellationToken = default);
}

public sealed record CpContentBodySaveRequest(
    long ContentId,
    string? ContentType = null,
    string? Content = null,
    string? ContentLangStrId = null,
    string? LangCode = null,
    string? DomainPath = null);

public sealed record CpContentMetaSaveRequest(
    long ContentId = 0,
    string? Alias = null,
    string? Value = null,
    long Parent = 0,
    string? Description = null,
    int IsFrontend = 1,
    string? ContentType = null,
    string? Content = null,
    string? TitleTag = null,
    string? DescriptionTag = null,
    string? KeywordsTag = null,
    string? AuthorTag = null,
    int MainFlag = 0,
    string? CssJs = null,
    string? RobotsTag = null,
    int PublishedFlag = 1,
    string? GroupsAccess = null,
    string? ValueLangStrId = null,
    string? DescriptionLangStrId = null,
    string? ContentLangStrId = null,
    string? TitleLangStrId = null,
    string? DescriptionTagLangStrId = null,
    string? KeywordsLangStrId = null,
    string? AuthorLangStrId = null,
    string? LangCode = null,
    string? DomainPath = null,
    string? CheckHash = null,
    string? SecretSuccession = null);

public sealed record CpContentTreeSaveRequest(
    string? TreeJson = null,
    int IsFrontend = 1,
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
                    "CONTENT EDITING",
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

    public async Task<ErpSimpleWriteResult> SaveMetaAsync(
        CpContentMetaSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryNormalizeAlias(request.Alias, out var alias, out var aliasError))
        {
            return ErpSimpleWriteResult.Fail("invalid", aliasError);
        }

        if (!TryNormalizeType(request.ContentType, out var contentType))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Content type must be text or php.");
        }

        if (request.Parent < 0 || request.Parent == request.ContentId && request.ContentId > 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Parent page is not valid.");
        }

        var groups = ParseGroups(request.GroupsAccess);
        if (groups.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", groups.Error);
        }

        string storedContent;
        if (contentType == "php")
        {
            if (!TryNormalizePhpPath(request.Content, out storedContent, out var pathError))
            {
                return ErpSimpleWriteResult.Fail("invalid", pathError);
            }
        }
        else
        {
            storedContent = StripPhp(request.Content);
            if (storedContent.Length > 1_000_000)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Content body is too long.");
            }
        }

        var checkHash = (request.CheckHash ?? string.Empty).Trim();
        if (checkHash.Length > 0)
        {
            var expected = ComputeCheckHash(request.ContentId, request.IsFrontend > 0 ? 1 : 0, request.SecretSuccession);
            if (!string.Equals(checkHash, expected, StringComparison.OrdinalIgnoreCase))
            {
                return ErpSimpleWriteResult.Fail("invalid", "Content check hash is not valid.");
            }
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var isFrontend = request.IsFrontend > 0 ? 1 : 0;
        var mainFlag = request.MainFlag > 0 ? 1 : 0;
        var published = request.PublishedFlag > 0 ? 1 : 0;
        var lang = NormalizeLang(request.LangCode);
        var caption = HtmlEncode(request.Value);
        var title = HtmlEncode(request.TitleTag);
        var descriptionTag = HtmlEncode(request.DescriptionTag);
        var keywords = HtmlEncode(request.KeywordsTag);
        var author = HtmlEncode(request.AuthorTag);
        var robots = HtmlEncode(request.RobotsTag);
        var description = request.Description ?? string.Empty;
        var cssJs = request.CssJs ?? string.Empty;
        const string langDescription = "NO TREE EDITOR CONTENT EDITING";

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var creating = request.ContentId <= 0;
            long contentId = request.ContentId;
            long currentParent = 0;
            var currentMain = 0L;
            if (!creating)
            {
                var found = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT `id` FROM `content` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    contentId).ConfigureAwait(false);
                if (found <= 0)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Fail("invalid", "Content page was not found.");
                }

                var systemFlag = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT `system_flag` FROM `content` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    contentId).ConfigureAwait(false);
                if (systemFlag > 0)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Fail("invalid", "System pages cannot be edited here.");
                }

                var currentFrontend = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT `is_frontend` FROM `content` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    contentId).ConfigureAwait(false);
                if (currentFrontend != isFrontend)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Fail("invalid", "Frontend/backend mode cannot change.");
                }

                currentParent = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT `parent` FROM `content` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    contentId).ConfigureAwait(false);
                currentMain = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT `main_flag` FROM `content` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    contentId).ConfigureAwait(false);
            }

            var level = 1L;
            var url = alias;
            if (request.Parent > 0)
            {
                var parentLevel = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT `level` FROM `content` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    request.Parent).ConfigureAwait(false);
                var parentUrl = await ErpDb.StringAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT `url` FROM `content` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    request.Parent).ConfigureAwait(false);
                var parentFrontend = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT `is_frontend` FROM `content` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    request.Parent).ConfigureAwait(false);
                if (string.IsNullOrEmpty(parentUrl) && parentLevel == 0)
                {
                    var parentFound = await ErpDb.LongAsync(
                        connection,
                        transaction,
                        ErpDb.Positional("SELECT `id` FROM `content` WHERE `id` = ? LIMIT 1"),
                        cancellationToken,
                        request.Parent).ConfigureAwait(false);
                    if (parentFound <= 0)
                    {
                        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        return ErpSimpleWriteResult.Fail("invalid", "Parent page was not found.");
                    }
                }

                if (parentFrontend != isFrontend)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Fail("invalid", "Parent must be in the same frontend/backend mode.");
                }

                level = parentLevel + 1;
                url = (parentUrl ?? string.Empty).TrimEnd('/') + "/" + alias;
            }

            var valueKey = await RequireTranslationAsync(
                connection, transaction, request.ValueLangStrId, caption, lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
            var descriptionKey = await RequireTranslationAsync(
                connection, transaction, request.DescriptionLangStrId, description, lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
            var titleKey = await RequireTranslationAsync(
                connection, transaction, request.TitleLangStrId, title, lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
            var descriptionTagKey = await RequireTranslationAsync(
                connection, transaction, request.DescriptionTagLangStrId, descriptionTag, lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
            var keywordsKey = await RequireTranslationAsync(
                connection, transaction, request.KeywordsLangStrId, keywords, lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
            var authorKey = await RequireTranslationAsync(
                connection, transaction, request.AuthorLangStrId, author, lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
            if (contentType == "text")
            {
                storedContent = await RequireTranslationAsync(
                    connection, transaction, request.ContentLangStrId, storedContent, lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (creating)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        """
                        INSERT INTO `content`
                        (`count`,`url`,`level`,`alias`,`value`,`parent`,`description`,`is_frontend`,`content_type`,`content`,`title_tag`,`description_tag`,`keywords_tag`,`author_tag`,`main_flag`,`modules_array`,`css_js`,`robots_tag`,`system_flag`,`published_flag`,`open`,`time_created`,`order`)
                        VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                        """),
                    cancellationToken,
                    0,
                    url,
                    level,
                    alias,
                    valueKey,
                    request.Parent,
                    descriptionKey,
                    isFrontend,
                    contentType,
                    storedContent,
                    titleKey,
                    descriptionTagKey,
                    keywordsKey,
                    authorKey,
                    mainFlag,
                    "[]",
                    cssJs,
                    robots,
                    0,
                    published,
                    0,
                    now,
                    1).ConfigureAwait(false);
                contentId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (contentId <= 0)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Fail("invalid", "Could not create the content page.");
                }

                if (request.Parent > 0)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional("UPDATE `content` SET `count` = `count` + 1 WHERE `id` = ?"),
                        cancellationToken,
                        request.Parent).ConfigureAwait(false);
                }
            }
            else
            {
                var rows = await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        """
                        UPDATE `content`
                        SET `url` = ?, `level` = ?, `alias` = ?, `value` = ?, `parent` = ?, `description` = ?,
                            `content_type` = ?, `content` = ?, `title_tag` = ?, `description_tag` = ?,
                            `keywords_tag` = ?, `author_tag` = ?, `main_flag` = ?, `css_js` = ?,
                            `robots_tag` = ?, `published_flag` = ?, `time_edited` = ?
                        WHERE `id` = ?
                          AND (`system_flag` IS NULL OR `system_flag` <> 1)
                        """),
                    cancellationToken,
                    url,
                    level,
                    alias,
                    valueKey,
                    request.Parent,
                    descriptionKey,
                    contentType,
                    storedContent,
                    titleKey,
                    descriptionTagKey,
                    keywordsKey,
                    authorKey,
                    mainFlag,
                    cssJs,
                    robots,
                    published,
                    now,
                    contentId).ConfigureAwait(false);
                if (rows <= 0)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Fail("invalid", "Content page not found or is a locked system page.");
                }

                if (request.Parent != currentParent)
                {
                    if (request.Parent > 0)
                    {
                        await ErpDb.ExecuteAsync(
                            connection,
                            transaction,
                            ErpDb.Positional("UPDATE `content` SET `count` = `count` + 1 WHERE `id` = ?"),
                            cancellationToken,
                            request.Parent).ConfigureAwait(false);
                    }

                    if (currentParent > 0)
                    {
                        await ErpDb.ExecuteAsync(
                            connection,
                            transaction,
                            ErpDb.Positional("UPDATE `content` SET `count` = `count` - 1 WHERE `id` = ?"),
                            cancellationToken,
                            currentParent).ConfigureAwait(false);
                    }
                }

                if (!await HandleChildNodesAsync(connection, transaction, contentId, cancellationToken).ConfigureAwait(false))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Fail("invalid", "Could not update nested pages.");
                }
            }

            if (mainFlag == 1 && (creating || currentMain == 0))
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("UPDATE `content` SET `main_flag` = 0 WHERE `main_flag` = 1 AND `id` != ? AND `is_frontend` = ?"),
                    cancellationToken,
                    contentId,
                    isFrontend).ConfigureAwait(false);
            }

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `content_access` WHERE `content_id` = ?"),
                cancellationToken,
                contentId).ConfigureAwait(false);
            foreach (var groupId in groups.Ids)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)"),
                    cancellationToken,
                    contentId,
                    groupId).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(
                creating ? $"Content page created (#{contentId})." : $"Content #{contentId} metadata saved.",
                contentId);
        }
        catch (ErpWriteException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not save the content page.");
        }
    }

    public async Task<ErpSimpleWriteResult> SaveTreeAsync(
        CpContentTreeSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var parsed = ParseTree(request.TreeJson);
        if (parsed.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", parsed.Error);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var frontend = request.IsFrontend > 0 ? 1 : 0;
        var lang = NormalizeLang(request.LangCode);
        const string langDescription = "CONTENT TREE EDITING";
        var keep = parsed.Nodes.Select(n => n.Id).ToHashSet();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = await ListFrontendPagesAsync(connection, transaction, frontend, cancellationToken).ConfigureAwait(false);
            if (existing.Any(p => p.System && !keep.Contains(p.Id)))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("invalid", "System pages cannot be removed from the tree.");
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var order = 0;
            foreach (var node in parsed.Nodes)
            {
                order++;
                var found = existing.FirstOrDefault(p => p.Id == node.Id);
                if (found.Id > 0 && found.System)
                {
                    continue;
                }

                var valueKey = await RequireTranslationAsync(
                    connection, transaction, node.ValueLangStrId, HtmlEncode(node.Value), lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
                var descriptionKey = await RequireTranslationAsync(
                    connection, transaction, node.DescriptionLangStrId, HtmlEncode(node.Description), lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
                var titleKey = await RequireTranslationAsync(
                    connection, transaction, node.TitleLangStrId, HtmlEncode(node.TitleTag), lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
                var descriptionTagKey = await RequireTranslationAsync(
                    connection, transaction, node.DescriptionTagLangStrId, HtmlEncode(node.DescriptionTag), lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
                var keywordsKey = await RequireTranslationAsync(
                    connection, transaction, node.KeywordsLangStrId, HtmlEncode(node.KeywordsTag), lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);

                if (found.Id > 0)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            """
                            UPDATE `content`
                            SET `count`=?, `url`=?, `level`=?, `alias`=?, `value`=?, `parent`=?, `description`=?,
                                `main_flag`=?, `title_tag`=?, `description_tag`=?, `keywords_tag`=?, `robots_tag`=?,
                                `published_flag`=?, `open`=?, `css_js`=?, `order`=?
                            WHERE `id`=?
                            """),
                        cancellationToken,
                        node.Count, node.Url, node.Level, node.Alias, valueKey, node.Parent, descriptionKey,
                        node.MainFlag, titleKey, descriptionTagKey, keywordsKey, HtmlEncode(node.RobotsTag),
                        node.PublishedFlag, node.Open, node.CssJs ?? string.Empty, order, node.Id).ConfigureAwait(false);
                }
                else
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            """
                            INSERT INTO `content`
                            (`id`,`count`,`url`,`level`,`alias`,`value`,`parent`,`description`,`is_frontend`,
                             `main_flag`,`content_type`,`title_tag`,`description_tag`,`keywords_tag`,`robots_tag`,
                             `modules_array`,`published_flag`,`open`,`css_js`,`time_created`,`order`)
                            VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
                            """),
                        cancellationToken,
                        node.Id, node.Count, node.Url, node.Level, node.Alias, valueKey, node.Parent, descriptionKey,
                        frontend, node.MainFlag, "text", titleKey, descriptionTagKey, keywordsKey, HtmlEncode(node.RobotsTag),
                        "[]", node.PublishedFlag, node.Open, node.CssJs ?? string.Empty, now, order).ConfigureAwait(false);
                }

                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("DELETE FROM `content_access` WHERE `content_id` = ?"),
                    cancellationToken,
                    node.Id).ConfigureAwait(false);
                foreach (var groupId in node.Groups)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional("INSERT INTO `content_access` (`content_id`, `group_id`) VALUES (?, ?)"),
                        cancellationToken,
                        node.Id,
                        groupId).ConfigureAwait(false);
                }
            }

            foreach (var page in existing.Where(p => !p.System && !keep.Contains(p.Id)))
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("DELETE FROM `content` WHERE `id` = ?"),
                    cancellationToken,
                    page.Id).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(true, "ok", "Content tree saved.", parsed.Nodes[0].Id, parsed.Nodes.Count);
        }
        catch (ErpWriteException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not save the content tree.");
        }
    }

    /// <summary>PHP content_tree.php Webix hierarchy. Empty tree refused.</summary>
    public static (IReadOnlyList<ContentTreeNode> Nodes, string? Error) ParseTree(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return ([], "tree_json is required.");
        }

        if (text.Length > 200_000)
        {
            return ([], "tree_json is too large.");
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            var nodes = new List<ContentTreeNode>();
            WalkTree(doc.RootElement, nodes);
            return nodes.Count == 0
                ? ([], "At least one content page is required.")
                : nodes.Count > 400
                    ? ([], "Too many content-tree nodes.")
                    : (nodes, null);
        }
        catch (JsonException)
        {
            return ([], "tree_json is not valid JSON.");
        }
    }

    public readonly record struct ContentTreeNode(
        long Id,
        int Count,
        string Url,
        int Level,
        string Alias,
        string Value,
        string ValueLangStrId,
        long Parent,
        string Description,
        string DescriptionLangStrId,
        int MainFlag,
        string TitleTag,
        string TitleLangStrId,
        string DescriptionTag,
        string DescriptionTagLangStrId,
        string KeywordsTag,
        string KeywordsLangStrId,
        string RobotsTag,
        int PublishedFlag,
        int Open,
        string? CssJs,
        IReadOnlyList<long> Groups);

    /// <summary>PHP content_create_edit.php alias becomes the URL segment.</summary>
    public static bool TryNormalizeAlias(string? raw, out string alias, out string error)
    {
        alias = (raw ?? string.Empty).Trim();
        error = string.Empty;
        if (alias.Length == 0)
        {
            error = "An alias is required.";
            return false;
        }

        if (alias.Length > 190
            || alias.Contains("..", StringComparison.Ordinal)
            || alias.Contains('/', StringComparison.Ordinal)
            || alias.Contains('\\', StringComparison.Ordinal))
        {
            error = "Alias must be a single URL segment.";
            alias = string.Empty;
            return false;
        }

        return true;
    }

    /// <summary>PHP content_create_edit.php groups_access JSON array or csv of group ids.</summary>
    public static (IReadOnlyList<long> Ids, string? Error) ParseGroups(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0 || text == "[]")
        {
            return ([], null);
        }

        if (text[0] != '[')
        {
            var csv = new List<long>();
            foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0)
                {
                    csv.Add(id);
                }
            }

            return csv.Count == 0 ? ([], "groups_access is not valid.") : (csv.Distinct().Take(80).ToList(), null);
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ([], "groups_access is not valid.");
            }

            var ids = new List<long>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var n) && n > 0)
                {
                    ids.Add(n);
                }
                else if (item.ValueKind == JsonValueKind.String
                         && long.TryParse(item.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                         && parsed > 0)
                {
                    ids.Add(parsed);
                }
            }

            return (ids.Distinct().Take(80).ToList(), null);
        }
        catch (JsonException)
        {
            return ([], "groups_access is not valid.");
        }
    }

    /// <summary>PHP md5(content_id + is_frontend + secret_succession).</summary>
    public static string ComputeCheckHash(long contentId, int isFrontend, string? secretSuccession)
        => LegacyPasswordVerifier.Md5Hex(
            contentId.ToString(CultureInfo.InvariantCulture)
            + (isFrontend > 0 ? 1 : 0).ToString(CultureInfo.InvariantCulture)
            + (secretSuccession ?? string.Empty));

    public static string HtmlEncode(string? raw)
        => WebUtility.HtmlEncode(raw ?? string.Empty);

    private static void WalkTree(JsonElement element, List<ContentTreeNode> nodes)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                WalkTree(item, nodes);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var id = ReadLong(element, "id");
        if (id <= 0)
        {
            return;
        }

        var data = GetProperty(element, "data");
        var childCount = data.ValueKind == JsonValueKind.Array
            ? data.GetArrayLength()
            : (int)ReadLong(element, "$count", "count");
        var groups = ReadGroups(element);
        nodes.Add(new ContentTreeNode(
            id,
            childCount,
            SqlQuote(ReadString(element, "url")),
            (int)Math.Max(1, ReadLong(element, "$level", "level")),
            ReadString(element, "alias").Replace("'", "''", StringComparison.Ordinal).ToLowerInvariant(),
            ReadString(element, "value"),
            ReadString(element, "value_lang_str_id", "valueLangStrId"),
            ReadLong(element, "$parent", "parent"),
            ReadString(element, "description"),
            ReadString(element, "description_lang_str_id", "descriptionLangStrId"),
            ReadLong(element, "main_flag", "mainFlag") > 0 ? 1 : 0,
            ReadString(element, "title_tag", "titleTag"),
            ReadString(element, "title_tag_lang_str_id", "titleLangStrId"),
            ReadString(element, "description_tag", "descriptionTag"),
            ReadString(element, "description_tag_lang_str_id", "descriptionTagLangStrId"),
            ReadString(element, "keywords_tag", "keywordsTag"),
            ReadString(element, "keywords_tag_lang_str_id", "keywordsLangStrId"),
            ReadString(element, "robots_tag", "robotsTag"),
            ReadLong(element, "published_flag", "publishedFlag") == 0 ? 0 : 1,
            ReadBoolFlag(element, "open"),
            ReadString(element, "css_js", "cssJs"),
            groups));

        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in data.EnumerateArray())
            {
                WalkTree(child, nodes);
            }
        }
    }

    private static IReadOnlyList<long> ReadGroups(JsonElement element)
    {
        if (!element.TryGetProperty("groups_access", out var groups)
            && !element.TryGetProperty("groupsAccess", out groups))
        {
            return [];
        }

        if (groups.ValueKind == JsonValueKind.String)
        {
            var parsed = ParseGroups(groups.GetString());
            return parsed.Error is null ? parsed.Ids : [];
        }

        if (groups.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var ids = new List<long>();
        foreach (var item in groups.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var n) && n > 0)
            {
                ids.Add(n);
            }
        }

        return ids.Distinct().Take(80).ToList();
    }

    private static JsonElement GetProperty(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) ? value : default;

    private static long ReadLong(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var n))
            {
                return n;
            }

            if (value.ValueKind == JsonValueKind.String
                && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            if (value.ValueKind == JsonValueKind.True)
            {
                return 1;
            }
        }

        return 0;
    }

    private static string ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString() ?? string.Empty;
            }

            if (element.TryGetProperty(name, out value) && value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            {
                return value.ToString();
            }
        }

        return string.Empty;
    }

    private static int ReadBoolFlag(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => 1,
            JsonValueKind.Number when value.TryGetInt64(out var n) && n > 0 => 1,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var b) && b => 1,
            JsonValueKind.String when value.GetString() is "1" or "true" or "yes" => 1,
            _ => 0
        };
    }

    private static string SqlQuote(string raw)
        => raw.Replace("'", "''", StringComparison.Ordinal);

    private readonly record struct ExistingPage(long Id, bool System);

    private static async Task<List<ExistingPage>> ListFrontendPagesAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        int isFrontend,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional("SELECT `id`, IFNULL(`system_flag`,0) FROM `content` WHERE `is_frontend` = ?");
        ErpDb.AddParameters(command, isFrontend);
        var rows = new List<ExistingPage>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ExistingPage(reader.GetInt64(0), reader.GetInt64(1) > 0));
        }

        return rows;
    }

    private async Task<bool> HandleChildNodesAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        long contentId,
        CancellationToken cancellationToken)
    {
        var count = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT `count` FROM `content` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            contentId).ConfigureAwait(false);
        if (count == 0)
        {
            return true;
        }

        var level = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT `level` FROM `content` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            contentId).ConfigureAwait(false);
        var url = await ErpDb.StringAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT `url` FROM `content` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            contentId).ConfigureAwait(false) ?? string.Empty;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional("SELECT `id`, IFNULL(`alias`,''), IFNULL(`count`,0) FROM `content` WHERE `parent` = ?");
        ErpDb.AddParameters(command, contentId);
        var children = new List<(long Id, string Alias, long ChildCount)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                children.Add((
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetInt64(2)));
            }
        }

        foreach (var child in children)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `content` SET `level` = ?, `url` = ? WHERE `id` = ?"),
                cancellationToken,
                level + 1,
                url.TrimEnd('/') + "/" + child.Alias,
                child.Id).ConfigureAwait(false);
            if (child.ChildCount > 0
                && !await HandleChildNodesAsync(connection, transaction, child.Id, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<string> RequireTranslationAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string? langStrId,
        string value,
        string langCode,
        string? domainPath,
        string langDescription,
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
                langDescription,
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
