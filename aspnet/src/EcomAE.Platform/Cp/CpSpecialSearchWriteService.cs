using System.Globalization;
using System.Net;
using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>special_search.php</c> create/edit and <c>special_searches.php</c>
/// <c>delete_special_searches</c> twin. Image upload stays on the Classic twin.
/// </summary>
public interface ICpSpecialSearchWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(CpSpecialSearchSaveRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(string? searchesIdsJson, CancellationToken cancellationToken = default);
}

public sealed record CpSpecialSearchSaveRequest(
    long SearchId = 0,
    string? Caption = null,
    string? CaptionLangStrId = null,
    string? Title = null,
    string? TitleLangStrId = null,
    string? Description = null,
    string? DescriptionLangStrId = null,
    string? Keywords = null,
    string? KeywordsLangStrId = null,
    string? Robots = null,
    string? Alias = null,
    int Order = 0,
    int Active = 0,
    string? TreeJson = null,
    string? DeletedStepsJson = null,
    string? LangCode = null,
    string? DomainPath = null);

public sealed record CpSpecialSearchLevel(
    string Value,
    string ValueLangStrId,
    string H1,
    string H1LangStrId,
    string Title,
    string TitleLangStrId,
    string Description,
    string DescriptionLangStrId,
    string Keywords,
    string KeywordsLangStrId,
    string Robots);

public sealed record CpSpecialSearchStep(
    long Id,
    bool IsNew,
    string Value,
    string ValueLangStrId,
    string Alias,
    int Type,
    IReadOnlyList<int> Objects,
    IReadOnlyList<CpSpecialSearchLevel> Levels);

public sealed class CpSpecialSearchWriteService : ICpSpecialSearchWriteService
{
    public const string LangCreating = "SPECIAL SEARCH CREATING";
    public const string LangEditing = "SPECIAL SEARCH EDITING";

    private readonly IErpWriteConnectionFactory _connections;
    private int _createdStrings;

    public CpSpecialSearchWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        CpSpecialSearchSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var parsed = ParseSteps(request.TreeJson);
        if (parsed.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", parsed.Error);
        }

        var deleted = ParseIds(request.DeletedStepsJson);
        if (deleted.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", deleted.Error);
        }

        var creating = request.SearchId <= 0;
        if (!creating)
        {
            foreach (var step in parsed.Steps)
            {
                if (!step.IsNew && step.Id <= 0)
                {
                    return ErpSimpleWriteResult.Fail("invalid", "Each existing step needs a positive id.");
                }
            }
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var lang = NormalizeLang(request.LangCode);
        var langDescription = creating ? LangCreating : LangEditing;
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!creating)
            {
                var found = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT COUNT(*) FROM `shop_special_searches` WHERE `id` = ?"),
                    cancellationToken,
                    request.SearchId).ConfigureAwait(false);
                if (found == 0)
                {
                    throw new ErpWriteException("Special search not found.");
                }
            }

            // One translation pass per field. PHP edit re-runs save_custom_translation on
            // existing steps in both the insert and update loops; that can allocate a second
            // key when value_lang_str_id is 0. We keep the returned key and write once.
            var caption = await RequireTranslationAsync(
                connection, transaction, request.CaptionLangStrId, HtmlEncode(request.Caption),
                lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
            var title = await RequireTranslationAsync(
                connection, transaction, request.TitleLangStrId, HtmlEncode(request.Title),
                lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
            var description = await RequireTranslationAsync(
                connection, transaction, request.DescriptionLangStrId, HtmlEncode(request.Description),
                lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
            var keywords = await RequireTranslationAsync(
                connection, transaction, request.KeywordsLangStrId, HtmlEncode(request.Keywords),
                lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
            var robots = HtmlEncode(request.Robots);
            var alias = HtmlEncode(request.Alias);

            long searchId;
            if (creating)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `shop_special_searches` (`caption`, `alias`, `order`, `active`, `title`, `description`, `keywords`, `robots`) VALUES (?,?,?,?,?,?,?,?)"),
                    cancellationToken,
                    caption,
                    alias,
                    request.Order,
                    request.Active,
                    title,
                    description,
                    keywords,
                    robots).ConfigureAwait(false);
                searchId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                searchId = request.SearchId;
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "UPDATE `shop_special_searches` SET `caption` = ?, `alias` = ?, `order`=?, `active` = ?, `title`=?, `description`=?, `keywords`=?, `robots`=? WHERE `id` = ?"),
                    cancellationToken,
                    caption,
                    alias,
                    request.Order,
                    request.Active,
                    title,
                    description,
                    keywords,
                    robots,
                    searchId).ConfigureAwait(false);
            }

            var stepIds = new List<long>(parsed.Steps.Count);
            var order = 1;
            foreach (var step in parsed.Steps)
            {
                var stepCaption = await RequireTranslationAsync(
                    connection, transaction, step.ValueLangStrId, HtmlEncode(step.Value),
                    lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
                var stepAlias = HtmlEncode(step.Alias);
                var objects = JsonSerializer.Serialize(step.Objects);
                var insertNew = creating || step.IsNew;
                long stepId;
                if (insertNew)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            "INSERT INTO `shop_special_searches_steps` (`search_id`, `caption`, `alias`, `type`, `objects`, `order`) VALUES (?, ?, ?, ?, ?, ?)"),
                        cancellationToken,
                        searchId,
                        stepCaption,
                        stepAlias,
                        step.Type,
                        objects,
                        order).ConfigureAwait(false);
                    stepId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    stepId = step.Id;
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            "UPDATE `shop_special_searches_steps` SET `caption` = ?, `alias` = ?, `type`=?, `objects` = ?, `order` = ? WHERE `id` = ? AND `search_id` = ?"),
                        cancellationToken,
                        stepCaption,
                        stepAlias,
                        step.Type,
                        objects,
                        order,
                        stepId,
                        searchId).ConfigureAwait(false);
                }

                stepIds.Add(stepId);
                order++;
            }

            if (!creating)
            {
                foreach (var deletedId in deleted.Ids)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional("DELETE FROM `shop_special_searches_steps` WHERE `id` = ? AND `search_id` = ?"),
                        cancellationToken,
                        deletedId,
                        searchId).ConfigureAwait(false);
                }
            }

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `shop_special_searches_metadata` WHERE `search_id` = ?"),
                cancellationToken,
                searchId).ConfigureAwait(false);

            for (var i = 0; i < parsed.Steps.Count; i++)
            {
                var step = parsed.Steps[i];
                var stepId = stepIds[i];
                var levelNumber = 1;
                foreach (var level in step.Levels)
                {
                    var levelValue = await RequireTranslationAsync(
                        connection, transaction, level.ValueLangStrId, HtmlEncode(level.Value),
                        lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
                    var levelH1 = await RequireTranslationAsync(
                        connection, transaction, level.H1LangStrId, HtmlEncode(level.H1),
                        lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
                    var levelTitle = await RequireTranslationAsync(
                        connection, transaction, level.TitleLangStrId, HtmlEncode(level.Title),
                        lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
                    var levelDescription = await RequireTranslationAsync(
                        connection, transaction, level.DescriptionLangStrId, HtmlEncode(level.Description),
                        lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
                    var levelKeywords = await RequireTranslationAsync(
                        connection, transaction, level.KeywordsLangStrId, HtmlEncode(level.Keywords),
                        lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            "INSERT INTO `shop_special_searches_metadata` (`value`, `search_id`, `step_id`, `step_level`, `h1`, `title`, `description`, `keywords`, `robots`) VALUES (?,?,?,?,?,?,?,?,?)"),
                        cancellationToken,
                        levelValue,
                        searchId,
                        stepId,
                        levelNumber,
                        levelH1,
                        levelTitle,
                        levelDescription,
                        levelKeywords,
                        HtmlEncode(level.Robots)).ConfigureAwait(false);
                    levelNumber++;
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(creating ? "Special search created." : "Special search saved.", searchId);
        }
        catch (ErpWriteException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not save special search.");
        }
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(
        string? searchesIdsJson,
        CancellationToken cancellationToken = default)
    {
        var parsed = ParseIds(searchesIdsJson);
        if (parsed.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", parsed.Error);
        }

        if (parsed.Ids.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Need at least one special-search id.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // PHP manager delete removes searches + steps only. Image unlink is commented
            // out there; metadata orphans stay. File upload stays Classic.
            foreach (var id in parsed.Ids)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("DELETE FROM `shop_special_searches` WHERE `id` = ?"),
                    cancellationToken,
                    id).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("DELETE FROM `shop_special_searches_steps` WHERE `search_id` = ?"),
                    cancellationToken,
                    id).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Special searches deleted.", parsed.Ids[0]);
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not delete special searches.");
        }
    }

    public static (IReadOnlyList<CpSpecialSearchStep> Steps, string? Error) ParseSteps(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return ([], null);
        }

        if (text.Length > 200_000)
        {
            return ([], "tree_json is too large.");
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ([], "tree_json must be a JSON array.");
            }

            var steps = new List<CpSpecialSearchStep>();
            foreach (var node in document.RootElement.EnumerateArray())
            {
                if (node.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var type = ReadLong(node, "type");
                if (type is not (1 or 2))
                {
                    return ([], "Each special-search step type must be 1 or 2.");
                }

                var objects = ParseObjectIds(node);
                if (objects.Error is not null)
                {
                    return ([], objects.Error);
                }

                var levels = ParseLevels(node);
                if (levels.Error is not null)
                {
                    return ([], levels.Error);
                }

                steps.Add(new CpSpecialSearchStep(
                    ReadLong(node, "id"),
                    ReadFlag(node, "is_new"),
                    ReadString(node, "value"),
                    ReadString(node, "value_lang_str_id"),
                    ReadString(node, "alias"),
                    (int)type,
                    objects.Ids,
                    levels.Levels));
                if (steps.Count > 80)
                {
                    return ([], "tree_json has too many steps.");
                }
            }

            return (steps, null);
        }
        catch (JsonException)
        {
            return ([], "tree_json is not valid JSON.");
        }
    }

    public static (IReadOnlyList<long> Ids, string? Error) ParseIds(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return ([], null);
        }

        if (text.Length > 20_000)
        {
            return ([], "ids JSON is too large.");
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ([], "ids JSON must be an array.");
            }

            var ids = new List<long>();
            foreach (var node in document.RootElement.EnumerateArray())
            {
                var id = node.ValueKind switch
                {
                    JsonValueKind.Number when node.TryGetInt64(out var n) => n,
                    JsonValueKind.String when long.TryParse(
                        node.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                        => parsed,
                    _ => 0
                };
                if (id > 0)
                {
                    ids.Add(id);
                }

                if (ids.Count > 80)
                {
                    return ([], "ids JSON has too many entries.");
                }
            }

            return (ids, null);
        }
        catch (JsonException)
        {
            return ([], "ids JSON is not valid JSON.");
        }
    }

    public static string HtmlEncode(string? raw)
        => WebUtility.HtmlEncode((raw ?? string.Empty).Trim());

    public static string NormalizeAction(string? raw)
    {
        var action = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return action switch
        {
            "save" or "create" or "edit" or "update" or "action" => "save",
            "delete" or "delete_special_searches" => "delete",
            _ => action
        };
    }

    private static (IReadOnlyList<int> Ids, string? Error) ParseObjectIds(JsonElement node)
    {
        if (!node.TryGetProperty("objects", out var objects) || objects.ValueKind == JsonValueKind.Null)
        {
            return ([], null);
        }

        JsonElement array = objects;
        if (objects.ValueKind == JsonValueKind.String)
        {
            var text = (objects.GetString() ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return ([], null);
            }

            try
            {
                using var document = JsonDocument.Parse(text);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return ([], "Step objects must be a JSON array.");
                }

                return CollectObjectIds(document.RootElement);
            }
            catch (JsonException)
            {
                return ([], "Step objects must be a JSON array.");
            }
        }

        if (array.ValueKind != JsonValueKind.Array)
        {
            return ([], "Step objects must be a JSON array.");
        }

        return CollectObjectIds(array);
    }

    private static (IReadOnlyList<int> Ids, string? Error) CollectObjectIds(JsonElement array)
    {
        var ids = new List<int>();
        foreach (var item in array.EnumerateArray())
        {
            var id = item.ValueKind switch
            {
                JsonValueKind.Number when item.TryGetInt32(out var n) => n,
                JsonValueKind.String when int.TryParse(
                    item.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    => parsed,
                _ => 0
            };
            ids.Add(id);
            if (ids.Count > 80)
            {
                return ([], "A step has too many objects.");
            }
        }

        return (ids, null);
    }

    private static (IReadOnlyList<CpSpecialSearchLevel> Levels, string? Error) ParseLevels(JsonElement node)
    {
        if (!node.TryGetProperty("levels", out var levels) || levels.ValueKind == JsonValueKind.Null)
        {
            return ([], null);
        }

        if (levels.ValueKind != JsonValueKind.Array)
        {
            return ([], "Step levels must be a JSON array.");
        }

        var parsed = new List<CpSpecialSearchLevel>();
        foreach (var level in levels.EnumerateArray())
        {
            if (level.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            parsed.Add(new CpSpecialSearchLevel(
                ReadString(level, "value"),
                ReadString(level, "value_lang_str_id"),
                ReadString(level, "h1"),
                ReadString(level, "h1_lang_str_id"),
                ReadString(level, "title"),
                ReadString(level, "title_lang_str_id"),
                ReadString(level, "description"),
                ReadString(level, "description_lang_str_id"),
                ReadString(level, "keywords"),
                ReadString(level, "keywords_lang_str_id"),
                ReadString(level, "robots")));
            if (parsed.Count > 40)
            {
                return ([], "A step has too many levels.");
            }
        }

        return (parsed, null);
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
            var key = CpLangWriteService.NextStrKey(domainPath, _createdStrings);
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

        throw new ErpWriteException("Could not allocate a special-search translation key.");
    }

    private static string NormalizeLang(string? langCode)
    {
        var lang = (langCode ?? string.Empty).Trim().ToLowerInvariant();
        return lang.Length is < 2 or > 16 ? "en" : lang;
    }

    private static long ReadLong(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var n))
        {
            return n;
        }

        return value.ValueKind == JsonValueKind.String
               && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static string ReadString(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
            _ => string.Empty
        };
    }

    private static bool ReadFlag(JsonElement node, string name)
    {
        if (!node.TryGetProperty(name, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => value.TryGetInt64(out var n) && n != 0,
            JsonValueKind.String => value.GetString() is "1" or "true" or "yes" or "on",
            _ => false
        };
    }
}
