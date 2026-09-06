using System.Globalization;
using System.Net;
using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>line_list.php</c> save_action create/edit and <c>line_lists_manager.php</c> delete twins.
/// Drag-tree item editor stays on the Classic twin. Manufacturers list id 10 cannot be deleted.
/// </summary>
public interface ICpLineListWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(CpLineListSaveRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(string? idsJson, CancellationToken cancellationToken = default);
}

public sealed record CpLineListSaveRequest(
    string? Action = null,
    long ListId = 0,
    string? Caption = null,
    string? CaptionLangStrId = null,
    int Type = 1,
    string? DataType = null,
    string? AutoSort = null,
    string? ItemsJson = null,
    string? LangCode = null,
    string? DomainPath = null);

public sealed record CpLineListItem(long Id, string Value, string ValueLangStrId, bool IsNew);

public sealed class CpLineListWriteService : ICpLineListWriteService
{
    public const long ManufacturerListId = 10;

    private readonly IErpWriteConnectionFactory _connections;
    private int _createdStrings;

    public CpLineListWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(CpLineListSaveRequest request, CancellationToken cancellationToken = default)
    {
        var action = NormalizeAction(request.Action);
        if (action is not ("create" or "edit"))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Action must be create or edit.");
        }

        if (action == "edit" && request.ListId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A line-list id is required to edit.");
        }

        var parsed = ParseItems(request.ItemsJson);
        if (parsed.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", parsed.Error);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var caption = HtmlEncode(request.Caption);
        var dataType = HtmlEncode(string.IsNullOrWhiteSpace(request.DataType) ? "text" : request.DataType.Trim());
        var autoSort = HtmlEncode(string.IsNullOrWhiteSpace(request.AutoSort) ? "no" : request.AutoSort.Trim());
        var type = request.Type;
        var lang = NormalizeLang(request.LangCode);
        var description = action == "edit" ? "LINE LIST EDITING" : "LINE LIST CREATING";
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            long listId = request.ListId;
            if (action == "edit")
            {
                var found = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT `id` FROM `shop_line_lists` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    listId).ConfigureAwait(false);
                if (found <= 0)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Fail("invalid", "Line list was not found.");
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

            if (action == "create")
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        """
                        INSERT INTO `shop_line_lists` (`caption`, `type`, `data_type`, `auto_sort`)
                        VALUES (?, ?, ?, ?)
                        """),
                    cancellationToken,
                    captionKey,
                    type,
                    dataType,
                    autoSort).ConfigureAwait(false);
                listId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                var order = 1;
                foreach (var item in parsed.Items)
                {
                    var valueKey = await RequireTranslationAsync(
                        connection,
                        transaction,
                        item.ValueLangStrId,
                        HtmlEncode(item.Value),
                        lang,
                        request.DomainPath,
                        description,
                        cancellationToken).ConfigureAwait(false);
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            "INSERT INTO `shop_line_lists_items` (`line_list_id`, `value`, `order`) VALUES (?, ?, ?)"),
                        cancellationToken,
                        listId,
                        valueKey,
                        order).ConfigureAwait(false);
                    order++;
                }
            }
            else
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        """
                        UPDATE `shop_line_lists`
                        SET `caption` = ?, `type` = ?, `data_type` = ?, `auto_sort` = ?
                        WHERE `id` = ?
                        """),
                    cancellationToken,
                    captionKey,
                    type,
                    dataType,
                    autoSort,
                    listId).ConfigureAwait(false);

                var keep = new HashSet<long>(parsed.Items.Where(i => !i.IsNew && i.Id > 0).Select(i => i.Id));
                var existing = await LoadItemIdsAsync(connection, transaction, listId, cancellationToken).ConfigureAwait(false);
                foreach (var existingId in existing)
                {
                    if (keep.Contains(existingId))
                    {
                        continue;
                    }

                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional("DELETE FROM `shop_line_lists_items` WHERE `id` = ?"),
                        cancellationToken,
                        existingId).ConfigureAwait(false);
                }

                var order = 1;
                foreach (var item in parsed.Items)
                {
                    var valueKey = await RequireTranslationAsync(
                        connection,
                        transaction,
                        item.ValueLangStrId,
                        HtmlEncode(item.Value),
                        lang,
                        request.DomainPath,
                        description,
                        cancellationToken).ConfigureAwait(false);
                    if (item.IsNew || item.Id <= 0)
                    {
                        await ErpDb.ExecuteAsync(
                            connection,
                            transaction,
                            ErpDb.Positional(
                                "INSERT INTO `shop_line_lists_items` (`line_list_id`, `value`, `order`) VALUES (?, ?, ?)"),
                            cancellationToken,
                            listId,
                            valueKey,
                            order).ConfigureAwait(false);
                    }
                    else
                    {
                        await ErpDb.ExecuteAsync(
                            connection,
                            transaction,
                            ErpDb.Positional("UPDATE `shop_line_lists_items` SET `value` = ?, `order` = ? WHERE `id` = ?"),
                            cancellationToken,
                            valueKey,
                            order,
                            item.Id).ConfigureAwait(false);
                    }

                    order++;
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(action == "create" ? "Line list created." : "Line list saved.", listId);
        }
        catch (ErpWriteException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not save the line list.");
        }
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(string? idsJson, CancellationToken cancellationToken = default)
    {
        var parsed = ParseIds(idsJson);
        if (parsed.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", parsed.Error);
        }

        if (parsed.Ids.Contains(ManufacturerListId))
        {
            return ErpSimpleWriteResult.Fail("invalid", "The manufacturers line list cannot be deleted.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var placeholders = string.Join(",", parsed.Ids.Select(_ => "?"));
        var idArgs = parsed.Ids.Cast<object?>().ToArray();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var propertyIds = await LoadPropertyIdsAsync(connection, transaction, placeholders, idArgs, cancellationToken)
                .ConfigureAwait(false);

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `shop_line_lists_items` WHERE `line_list_id` IN (" + placeholders + ")"),
                cancellationToken,
                idArgs).ConfigureAwait(false);

            var rows = await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `shop_line_lists` WHERE `id` IN (" + placeholders + ")"),
                cancellationToken,
                idArgs).ConfigureAwait(false);

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "DELETE FROM `shop_categories_properties_map` WHERE `property_type_id` = 5 AND `list_id` IN ("
                    + placeholders
                    + ")"),
                cancellationToken,
                idArgs).ConfigureAwait(false);

            if (propertyIds.Count > 0)
            {
                var propertyPlaceholders = string.Join(",", propertyIds.Select(_ => "?"));
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "DELETE FROM `shop_properties_values_list` WHERE `property_id` IN (" + propertyPlaceholders + ")"),
                    cancellationToken,
                    propertyIds.Cast<object?>().ToArray()).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return rows > 0
                ? new ErpSimpleWriteResult(true, "ok", "Line lists deleted.", parsed.Ids[0], rows)
                : ErpSimpleWriteResult.Fail("not_found", "No line lists were deleted.");
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not delete the line lists.");
        }
    }

    /// <summary>PHP line_list.php json_decode(tree_json). Empty becomes no items. Invalid JSON is refused.</summary>
    public static (IReadOnlyList<CpLineListItem> Items, string? Error) ParseItems(string? raw)
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

        if (text[0] != '[')
        {
            return ([], "tree_json must be a JSON array.");
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ([], "tree_json must be a JSON array.");
            }

            var items = new List<CpLineListItem>();
            foreach (var node in doc.RootElement.EnumerateArray())
            {
                if (node.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var id = ReadLong(node, "id");
                var value = ReadString(node, "value");
                var langStrId = ReadString(node, "value_lang_str_id");
                var isNew = ReadFlag(node, "is_new");
                items.Add(new CpLineListItem(id, value, langStrId, isNew));
                if (items.Count > 400)
                {
                    return ([], "tree_json has too many items.");
                }
            }

            return (items, null);
        }
        catch (JsonException)
        {
            return ([], "tree_json is not valid JSON.");
        }
    }

    /// <summary>PHP line_lists_manager.php line_lists JSON array or csv of ids.</summary>
    public static (IReadOnlyList<long> Ids, string? Error) ParseIds(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return ([], "At least one line-list id is required.");
        }

        if (text[0] == '{')
        {
            return ([], "line_lists JSON is not valid.");
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

            return csv.Count == 0 ? ([], "At least one line-list id is required.") : (csv.Distinct().Take(80).ToList(), null);
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ([], "line_lists JSON is not valid.");
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

            return ids.Count == 0 ? ([], "At least one line-list id is required.") : (ids.Distinct().Take(80).ToList(), null);
        }
        catch (JsonException)
        {
            return ([], "line_lists JSON is not valid.");
        }
    }

    public static string HtmlEncode(string? raw)
        => WebUtility.HtmlEncode(raw ?? string.Empty);

    public static string NormalizeAction(string? raw)
    {
        var action = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return action switch
        {
            "create" or "save_create" or "save_action_create" => "create",
            "edit" or "update" or "save_edit" or "save_update" => "edit",
            "delete" or "delete_line_lists" or "del" => "delete",
            _ => action
        };
    }

    private static async Task<List<long>> LoadItemIdsAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        long listId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional("SELECT `id` FROM `shop_line_lists_items` WHERE `line_list_id` = ?");
        ErpDb.AddParameters(command, listId);
        var ids = new List<long>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ids.Add(reader.GetInt64(0));
        }

        return ids;
    }

    private static async Task<List<long>> LoadPropertyIdsAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string placeholders,
        object?[] idArgs,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional(
            "SELECT `id` FROM `shop_categories_properties_map` WHERE `property_type_id` = 5 AND `list_id` IN ("
            + placeholders
            + ")");
        ErpDb.AddParameters(command, idArgs);
        var ids = new List<long>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ids.Add(reader.GetInt64(0));
        }

        return ids;
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

        throw new ErpWriteException("Could not allocate a line-list translation key.");
    }

    private static string NormalizeLang(string? langCode)
    {
        var lang = (langCode ?? string.Empty).Trim().ToLowerInvariant();
        return lang.Length is < 2 or > 16 ? "en" : lang;
    }
}
