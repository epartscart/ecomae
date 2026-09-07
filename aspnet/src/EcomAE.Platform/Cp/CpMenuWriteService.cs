using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>menu_edit.php</c> save_action create/update and <c>menu_manager.php</c> delete twins.
/// Drag-tree UX stays on the Classic twin; this writes caption, UL attrs, and structure JSON.
/// </summary>
public interface ICpMenuWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(CpMenuSaveRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(string? idsJson, CancellationToken cancellationToken = default);
}

public sealed record CpMenuSaveRequest(
    string? Action = null,
    long MenuId = 0,
    string? Caption = null,
    string? CaptionLangStrId = null,
    string? MenuUlClass = null,
    string? MenuUlId = null,
    int IsFrontend = 1,
    string? TreeJson = null,
    string? LangCode = null,
    string? DomainPath = null);

public sealed class CpMenuWriteService : ICpMenuWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private int _createdStrings;

    public CpMenuWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(CpMenuSaveRequest request, CancellationToken cancellationToken = default)
    {
        var action = NormalizeAction(request.Action);
        if (action is not ("create" or "update"))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Action must be create or update.");
        }

        if (action == "update" && request.MenuId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A menu id is required to update.");
        }

        var treeText = (request.TreeJson ?? string.Empty).Trim();
        var keepExistingTree = action == "update" && treeText.Length == 0;
        JsonNode? tree = null;
        if (!keepExistingTree)
        {
            var parsed = TryParseTree(treeText);
            if (parsed.Error is not null)
            {
                return ErpSimpleWriteResult.Fail("invalid", parsed.Error);
            }

            tree = parsed.Tree;
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var caption = HtmlEncode(request.Caption);
        var ulClass = HtmlEncode(request.MenuUlClass);
        var ulId = HtmlEncode(request.MenuUlId);
        var frontend = request.IsFrontend > 0 ? 1 : 0;
        var lang = NormalizeLang(request.LangCode);
        var description = action == "update" ? "MENU EDITING" : "MENU CREATING";
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            long menuId = request.MenuId;
            if (action == "update")
            {
                var found = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT `id` FROM `menu` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    menuId).ConfigureAwait(false);
                if (found <= 0)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Fail("invalid", "Menu was not found.");
                }
            }

            var captionKey = await RequireTranslationAsync(
                connection,
                transaction,
                request.CaptionLangStrId,
                caption,
                lang,
                request.DomainPath,
                description,
                cancellationToken).ConfigureAwait(false);

            string structure;
            if (keepExistingTree)
            {
                structure = await ErpDb.StringAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT IFNULL(`structure`,'[]') FROM `menu` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    menuId).ConfigureAwait(false) ?? "[]";
            }
            else
            {
                await EncodeTreeAsync(tree, connection, transaction, lang, request.DomainPath, description, cancellationToken)
                    .ConfigureAwait(false);
                structure = tree?.ToJsonString(JsonSerializerOptions.Default) ?? "[]";
            }

            if (action == "create")
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        """
                        INSERT INTO `menu` (`is_frontend`, `caption`, `structure`, `menu_ul_class`, `menu_ul_id`)
                        VALUES (?, ?, ?, ?, ?)
                        """),
                    cancellationToken,
                    frontend,
                    captionKey,
                    structure,
                    ulClass,
                    ulId).ConfigureAwait(false);
                menuId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        """
                        UPDATE `menu`
                        SET `caption` = ?, `structure` = ?, `menu_ul_class` = ?, `menu_ul_id` = ?
                        WHERE `id` = ?
                        """),
                    cancellationToken,
                    captionKey,
                    structure,
                    ulClass,
                    ulId,
                    menuId).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(action == "create" ? "Menu created." : "Menu saved.", menuId);
        }
        catch (ErpWriteException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not save the menu.");
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
            ErpDb.Positional("DELETE FROM `menu` WHERE `id` IN (" + placeholders + ")"),
            cancellationToken,
            parsed.Ids.Cast<object?>().ToArray()).ConfigureAwait(false);
        return rows > 0
            ? new ErpSimpleWriteResult(true, "ok", "Menus deleted.", parsed.Ids[0], rows)
            : ErpSimpleWriteResult.Fail("not_found", "No menus were deleted.");
    }

    /// <summary>PHP menu_edit.php json_decode(menu_tree). Empty becomes <c>[]</c>. Invalid JSON is refused.</summary>
    public static (JsonNode? Tree, string? Error) TryParseTree(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return (new JsonArray(), null);
        }

        if (text.Length > 200_000)
        {
            return (null, "menu_tree is too large.");
        }

        try
        {
            var node = JsonNode.Parse(text);
            if (node is null)
            {
                return (new JsonArray(), null);
            }

            if (node is not (JsonArray or JsonObject))
            {
                return (null, "menu_tree must be a JSON array or object.");
            }

            var nodes = 0;
            if (!CountNodes(node, ref nodes) || nodes > 400)
            {
                return (null, "menu_tree has too many nodes.");
            }

            return (node, null);
        }
        catch (JsonException)
        {
            return (null, "menu_tree is not valid JSON.");
        }
    }

    /// <summary>PHP menu_manager.php menu_list JSON array or csv of ids.</summary>
    public static (IReadOnlyList<long> Ids, string? Error) ParseIds(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return ([], "At least one menu id is required.");
        }

        if (text[0] == '{')
        {
            return ([], "menu_list JSON is not valid.");
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

            return csv.Count == 0 ? ([], "At least one menu id is required.") : (csv.Distinct().Take(80).ToList(), null);
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ([], "menu_list JSON is not valid.");
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

            return ids.Count == 0 ? ([], "At least one menu id is required.") : (ids.Distinct().Take(80).ToList(), null);
        }
        catch (JsonException)
        {
            return ([], "menu_list JSON is not valid.");
        }
    }

    public static string HtmlEncode(string? raw)
        => WebUtility.HtmlEncode(raw ?? string.Empty);

    public static string NormalizeAction(string? raw)
    {
        var action = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return action switch
        {
            "create" or "save_create" => "create",
            "update" or "save_update" => "update",
            "delete" => "delete",
            _ => action
        };
    }

    private async Task EncodeTreeAsync(
        JsonNode? node,
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string langCode,
        string? domainPath,
        string description,
        CancellationToken cancellationToken)
    {
        if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                await EncodeTreeAsync(child, connection, transaction, langCode, domainPath, description, cancellationToken)
                    .ConfigureAwait(false);
            }

            return;
        }

        if (node is not JsonObject obj)
        {
            return;
        }

        var keys = obj.Select(p => p.Key).ToList();
        foreach (var key in keys)
        {
            if (HtmlEncode(key) != key)
            {
                obj.Remove(key);
                continue;
            }

            if (key is "value_lang_str_id" or "a_innerhtml_lang_str_id")
            {
                continue;
            }

            var child = obj[key];
            if (child is JsonArray or JsonObject)
            {
                await EncodeTreeAsync(child, connection, transaction, langCode, domainPath, description, cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (child is not JsonValue value)
            {
                continue;
            }

            var text = value.GetValueKind() switch
            {
                JsonValueKind.String => value.GetValue<string>() ?? string.Empty,
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToJsonString(),
                _ => string.Empty
            };
            var encoded = HtmlEncode(text);
            if (key is "value" or "a_innerhtml")
            {
                var langStrId = ReadLangStrId(obj, key + "_lang_str_id");
                var strKey = await RequireTranslationAsync(
                    connection,
                    transaction,
                    langStrId,
                    encoded,
                    langCode,
                    domainPath,
                    description,
                    cancellationToken).ConfigureAwait(false);
                obj[key] = strKey;
                obj[key + "_lang_str_id"] = strKey;
            }
            else if (value.GetValueKind() == JsonValueKind.String)
            {
                obj[key] = encoded;
            }
        }
    }

    private static string ReadLangStrId(JsonObject obj, string key)
    {
        if (!obj.TryGetPropertyValue(key, out var node) || node is null)
        {
            return string.Empty;
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
            {
                return text ?? string.Empty;
            }

            if (value.TryGetValue<long>(out var n))
            {
                return n.ToString(CultureInfo.InvariantCulture);
            }
        }

        return node.ToJsonString().Trim('"');
    }

    private static bool CountNodes(JsonNode? node, ref int count)
    {
        if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                if (!CountNodes(child, ref count))
                {
                    return false;
                }
            }

            return true;
        }

        if (node is not JsonObject obj)
        {
            return true;
        }

        var looksLikeNode =
            obj.ContainsKey("link_mode")
            || obj.ContainsKey("value")
            || obj.ContainsKey("caption")
            || obj.ContainsKey("name");
        if (looksLikeNode)
        {
            count++;
            if (count > 400)
            {
                return false;
            }
        }

        foreach (var property in obj)
        {
            if (property.Value is JsonArray or JsonObject
                && !CountNodes(property.Value, ref count))
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

        throw new ErpWriteException("Could not allocate a menu translation key.");
    }

    private static string NormalizeLang(string? langCode)
    {
        var lang = (langCode ?? string.Empty).Trim().ToLowerInvariant();
        return lang.Length is < 2 or > 16 ? "en" : lang;
    }
}
