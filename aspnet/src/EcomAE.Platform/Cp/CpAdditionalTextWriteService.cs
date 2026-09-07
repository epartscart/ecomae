using System.Globalization;
using System.Net;
using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>text_for_url.php</c> save and <c>text_for_url_list.php</c> delete twins.</summary>
public interface ICpAdditionalTextWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(CpAdditionalTextSaveRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(string? idsJson, CancellationToken cancellationToken = default);
}

public sealed record CpAdditionalTextSaveRequest(
    string? Url = null,
    string? Content = null,
    int BeforeMain = 0,
    string? TitleTag = null,
    string? DescriptionTag = null,
    string? KeywordsTag = null,
    string? ContentLangStrId = null,
    string? TitleLangStrId = null,
    string? DescriptionLangStrId = null,
    string? KeywordsLangStrId = null,
    string? LangCode = null,
    string? DomainPath = null);

public sealed class CpAdditionalTextWriteService : ICpAdditionalTextWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private int _createdStrings;

    public CpAdditionalTextWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(CpAdditionalTextSaveRequest request, CancellationToken cancellationToken = default)
    {
        var url = (request.Url ?? string.Empty).Trim();
        if (url.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A URL is required.");
        }

        if (url.Length > 500)
        {
            return ErpSimpleWriteResult.Fail("invalid", "URL is too long.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var content = StripPhp(request.Content);
        var title = HtmlEncode(request.TitleTag);
        var description = HtmlEncode(request.DescriptionTag);
        var keywords = HtmlEncode(request.KeywordsTag);
        var before = request.BeforeMain == 1 ? 1 : 0;
        var lang = NormalizeLang(request.LangCode);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existingId = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT `id` FROM `text_for_url` WHERE `url` = ? LIMIT 1"),
                cancellationToken,
                url).ConfigureAwait(false);
            var creating = existingId <= 0;
            var descriptionLabel = creating ? "TEXT FOR URL CREATING" : "TEXT FOR URL EDITING";
            var contentKey = await RequireTranslationAsync(
                connection, transaction, request.ContentLangStrId, content, lang, request.DomainPath, descriptionLabel, cancellationToken).ConfigureAwait(false);
            var titleKey = await RequireTranslationAsync(
                connection, transaction, request.TitleLangStrId, title, lang, request.DomainPath, descriptionLabel, cancellationToken).ConfigureAwait(false);
            var descriptionKey = await RequireTranslationAsync(
                connection, transaction, request.DescriptionLangStrId, description, lang, request.DomainPath, descriptionLabel, cancellationToken).ConfigureAwait(false);
            var keywordsKey = await RequireTranslationAsync(
                connection, transaction, request.KeywordsLangStrId, keywords, lang, request.DomainPath, descriptionLabel, cancellationToken).ConfigureAwait(false);

            if (creating)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        """
                        INSERT INTO `text_for_url`
                        (`content`, `url`, `before_main`, `title_tag`, `description_tag`, `keywords_tag`)
                        VALUES (?, ?, ?, ?, ?, ?)
                        """),
                    cancellationToken,
                    contentKey,
                    url,
                    before,
                    titleKey,
                    descriptionKey,
                    keywordsKey).ConfigureAwait(false);
                existingId = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT `id` FROM `text_for_url` WHERE `url` = ? LIMIT 1"),
                    cancellationToken,
                    url).ConfigureAwait(false);
            }
            else
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        """
                        UPDATE `text_for_url`
                        SET `content` = ?, `before_main` = ?, `title_tag` = ?, `description_tag` = ?, `keywords_tag` = ?
                        WHERE `id` = ?
                        """),
                    cancellationToken,
                    contentKey,
                    before,
                    titleKey,
                    descriptionKey,
                    keywordsKey,
                    existingId).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(creating ? "Additional text created." : "Additional text saved.", existingId);
        }
        catch (ErpWriteException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not save the additional text.");
        }
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(string? idsJson, CancellationToken cancellationToken = default)
    {
        var parsed = ParseIds(idsJson);
        if (parsed.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", parsed.Error);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var placeholders = string.Join(",", parsed.Ids.Select(_ => "?"));
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `text_for_url` WHERE `id` IN (" + placeholders + ")"),
            cancellationToken,
            parsed.Ids.Cast<object?>().ToArray()).ConfigureAwait(false);
        return rows > 0
            ? new ErpSimpleWriteResult(true, "ok", "Additional texts deleted.", parsed.Ids[0], rows)
            : ErpSimpleWriteResult.Fail("not_found", "No additional texts were deleted.");
    }

    /// <summary>PHP text_for_url_list.php urls_to_del JSON array or csv of ids.</summary>
    public static (IReadOnlyList<long> Ids, string? Error) ParseIds(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return ([], "At least one additional-text id is required.");
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

            return csv.Count == 0 ? ([], "At least one additional-text id is required.") : (csv.Distinct().Take(80).ToList(), null);
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ([], "urls_to_del JSON is not valid.");
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

            return ids.Count == 0 ? ([], "At least one additional-text id is required.") : (ids.Distinct().Take(80).ToList(), null);
        }
        catch (JsonException)
        {
            return ([], "urls_to_del JSON is not valid.");
        }
    }

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

    public static string HtmlEncode(string? raw)
        => WebUtility.HtmlEncode((raw ?? string.Empty).Trim());

    private async Task<string> RequireTranslationAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string? langStrId,
        string value,
        string langCode,
        string? domainPath,
        string description,
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
                description,
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

        throw new ErpWriteException("Could not allocate an additional-text translation key.");
    }

    private static string NormalizeLang(string? langCode)
    {
        var lang = (langCode ?? string.Empty).Trim().ToLowerInvariant();
        return lang.Length is < 2 or > 16 ? "en" : lang;
    }
}
