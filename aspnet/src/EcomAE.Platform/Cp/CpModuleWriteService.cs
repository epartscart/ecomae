using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>edit_module.php</c> create/edit and <c>modules_manager.php</c> delete/activate twins.
/// Prototype CSS/JS is copied from the prototype row. TinyMCE upload stays PHP.
/// </summary>
public interface ICpModuleWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(CpModuleSaveRequest request, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetActivatedAsync(string? idsJson, int activatedFlag, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(string? idsJson, int isFrontend, CancellationToken cancellationToken = default);
}

public sealed record CpModuleSaveRequest(
    string? Action = null,
    long ModuleId = 0,
    long PrototypeId = 0,
    string? PrototypeNameLangStrId = null,
    string? Caption = null,
    string? CaptionLangStrId = null,
    string? ContentType = null,
    string? Content = null,
    string? ContentLangStrId = null,
    string? Position = null,
    int Activated = 1,
    string? DataJson = null,
    int ShowCaption = 0,
    int SortOrder = 0,
    int ForAll = 0,
    int IsFrontend = 1,
    string? ContentIds = null,
    string? GroupsAllowed = null,
    string? LangCode = null,
    string? DomainPath = null);

public sealed class CpModuleWriteService : ICpModuleWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private int _createdStrings;

    public CpModuleWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(CpModuleSaveRequest request, CancellationToken cancellationToken = default)
    {
        var action = NormalizeAction(request.Action);
        if (action is not ("create" or "edit"))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Action must be create or edit.");
        }

        if (action == "edit" && request.ModuleId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A module id is required to edit.");
        }

        if (!CpContentManagerWriteService.TryNormalizeType(request.ContentType, out var contentType))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Content type must be text or php.");
        }

        string storedContent;
        if (contentType == "php")
        {
            if (!CpContentManagerWriteService.TryNormalizePhpPath(request.Content, out var phpPath, out var phpError))
            {
                return ErpSimpleWriteResult.Fail("invalid", phpError ?? "PHP content must be a .php file path.");
            }

            storedContent = phpPath;
        }
        else
        {
            storedContent = CpContentManagerWriteService.StripPhp(request.Content);
        }

        var dataParsed = TryEncodeData(request.DataJson);
        if (dataParsed.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", dataParsed.Error);
        }

        var pages = ParseIds(request.ContentIds, "content_array");
        if (pages.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", pages.Error);
        }

        var groups = ParseIds(request.GroupsAllowed, "groups_allowed");
        if (groups.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", groups.Error);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var caption = HtmlEncode(request.Caption);
        var position = HtmlEncode(request.Position);
        var frontend = request.IsFrontend > 0 ? 1 : 0;
        var activated = request.Activated > 0 ? 1 : 0;
        var showCaption = request.ShowCaption > 0 ? 1 : 0;
        var forAll = request.ForAll > 0 ? 1 : 0;
        var order = request.SortOrder;
        var lang = NormalizeLang(request.LangCode);
        var description = action == "edit" ? "MODULE EDITING" : "MODULE CREATING";
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            long moduleId = request.ModuleId;
            var cssJs = "";
            if (request.PrototypeId > 0)
            {
                cssJs = await ErpDb.StringAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT IFNULL(`css_js`,'') FROM `modules` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    request.PrototypeId).ConfigureAwait(false) ?? "";
            }

            if (action == "edit")
            {
                var found = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT `id` FROM `modules` WHERE `id` = ? LIMIT 1"),
                    cancellationToken,
                    moduleId).ConfigureAwait(false);
                if (found <= 0)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Fail("invalid", "Module was not found.");
                }

                storedContent = LockProductPropertyContent(moduleId, storedContent);
            }

            var captionKey = await RequireTranslationAsync(
                connection, transaction, request.CaptionLangStrId, caption, lang, request.DomainPath, description, cancellationToken)
                .ConfigureAwait(false);
            if (contentType == "text")
            {
                storedContent = await RequireTranslationAsync(
                    connection, transaction, request.ContentLangStrId, storedContent, lang, request.DomainPath, description, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (action == "create")
            {
                var prototypeName = (request.PrototypeNameLangStrId ?? string.Empty).Trim();
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        """
                        INSERT INTO `modules`
                        (`is_frontend`, `is_prototype`, `prototype_id`, `prototype_name`, `caption`, `content_type`, `content`,
                         `position`, `activated`, `data`, `show_caption`, `order`, `css_js`, `for_all`)
                        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                        """),
                    cancellationToken,
                    frontend,
                    0,
                    request.PrototypeId,
                    prototypeName,
                    captionKey,
                    contentType,
                    storedContent,
                    position,
                    activated,
                    dataParsed.Json,
                    showCaption,
                    order,
                    cssJs,
                    forAll).ConfigureAwait(false);
                moduleId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        """
                        UPDATE `modules`
                        SET `caption` = ?, `content` = ?, `position` = ?, `activated` = ?, `data` = ?,
                            `show_caption` = ?, `order` = ?, `css_js` = ?, `for_all` = ?
                        WHERE `id` = ?
                        """),
                    cancellationToken,
                    captionKey,
                    storedContent,
                    position,
                    activated,
                    dataParsed.Json,
                    showCaption,
                    order,
                    cssJs,
                    forAll,
                    moduleId).ConfigureAwait(false);
            }

            await BindPagesAsync(connection, transaction, moduleId, frontend, pages.Ids, cancellationToken).ConfigureAwait(false);
            await ReplaceAccessAsync(connection, transaction, moduleId, groups.Ids, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(action == "create" ? "Module created." : "Module saved.", moduleId);
        }
        catch (ErpWriteException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not save the module.");
        }
    }

    public async Task<ErpSimpleWriteResult> SetActivatedAsync(
        string? idsJson,
        int activatedFlag,
        CancellationToken cancellationToken = default)
    {
        var parsed = ParseIds(idsJson, "modules_list");
        if (parsed.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", parsed.Error);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var flag = activatedFlag > 0 ? 1 : 0;
        var placeholders = string.Join(",", parsed.Ids.Select(_ => "?"));
        var args = new List<object?> { flag };
        args.AddRange(parsed.Ids.Cast<object?>());
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var rows = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `modules` SET `activated` = ? WHERE `id` IN (" + placeholders + ")"),
            cancellationToken,
            args.ToArray()).ConfigureAwait(false);
        return rows > 0
            ? new ErpSimpleWriteResult(true, "ok", "Module activation updated.", parsed.Ids[0], rows)
            : ErpSimpleWriteResult.Fail("not_found", "No modules were updated.");
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(
        string? idsJson,
        int isFrontend,
        CancellationToken cancellationToken = default)
    {
        var parsed = ParseIds(idsJson, "modules_list");
        if (parsed.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", parsed.Error);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var frontend = isFrontend > 0 ? 1 : 0;
        var placeholders = string.Join(",", parsed.Ids.Select(_ => "?"));
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var rows = await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `modules` WHERE `id` IN (" + placeholders + ")"),
                cancellationToken,
                parsed.Ids.Cast<object?>().ToArray()).ConfigureAwait(false);
            if (rows <= 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("not_found", "No modules were deleted.");
            }

            await UntiePagesAsync(connection, transaction, parsed.Ids, frontend, cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `modules_access` WHERE `module_id` IN (" + placeholders + ")"),
                cancellationToken,
                parsed.Ids.Cast<object?>().ToArray()).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(true, "ok", "Modules deleted.", parsed.Ids[0], rows);
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not delete the modules.");
        }
    }

    public static string NormalizeAction(string? raw)
    {
        var action = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return action switch
        {
            "create" or "module_create" => "create",
            "edit" or "update" or "module_edit" => "edit",
            "delete" => "delete",
            "activated" or "activate" => "activate",
            _ => action
        };
    }

    public static string HtmlEncode(string? raw)
        => WebUtility.HtmlEncode(raw ?? string.Empty);

    /// <summary>PHP modules_list / content_array / groups_allowed JSON array or csv. Empty is allowed for page/group lists.</summary>
    public static (IReadOnlyList<long> Ids, string? Error) ParseIds(string? raw, string fieldName = "modules_list")
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0 || text == "[]")
        {
            return fieldName == "modules_list"
                ? ([], "At least one module id is required.")
                : ([], null);
        }

        if (text[0] == '{')
        {
            return ([], fieldName + " JSON is not valid.");
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

            return fieldName == "modules_list" && csv.Count == 0
                ? ([], "At least one module id is required.")
                : (csv.Distinct().Take(80).ToList(), null);
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ([], fieldName + " JSON is not valid.");
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

            return fieldName == "modules_list" && ids.Count == 0
                ? ([], "At least one module id is required.")
                : (ids.Distinct().Take(80).ToList(), null);
        }
        catch (JsonException)
        {
            return ([], fieldName + " JSON is not valid.");
        }
    }

    public static (string Json, string? Error) TryEncodeData(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return ("[]", null);
        }

        if (text.Length > 200_000)
        {
            return ("", "data_value is too large.");
        }

        try
        {
            var node = JsonNode.Parse(text);
            if (node is null)
            {
                return ("[]", null);
            }

            EncodeDataNode(node);
            return (node.ToJsonString(JsonSerializerOptions.Default), null);
        }
        catch (JsonException)
        {
            return ("", "data_value is not valid JSON.");
        }
    }

    private static void EncodeDataNode(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                EncodeDataNode(child);
            }

            return;
        }

        if (node is not JsonObject obj)
        {
            return;
        }

        foreach (var key in obj.Select(p => p.Key).ToList())
        {
            if (HtmlEncode(key) != key)
            {
                obj.Remove(key);
                continue;
            }

            var child = obj[key];
            if (child is JsonArray or JsonObject)
            {
                EncodeDataNode(child);
                continue;
            }

            if (child is JsonValue value && value.GetValueKind() == JsonValueKind.String)
            {
                obj[key] = HtmlEncode(value.GetValue<string>());
            }
        }
    }

    private static string LockProductPropertyContent(long moduleId, string content)
        => moduleId switch
        {
            33 => "<div style=\"margin-left:20px; margin-top:10px;\"><div id=\"side_properties_widgets_div\" class=\"side_properties_widgets_div\" style=\"display: none;\"></div></div>",
            34 => "<div id=\"side_properties_widgets_div\" class=\"side_properties_widgets_div\" style=\"display:none\"></div>",
            _ => content
        };

    private static async Task BindPagesAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        long moduleId,
        int isFrontend,
        IReadOnlyList<long> contentIds,
        CancellationToken cancellationToken)
    {
        var wanted = contentIds.ToHashSet();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional("SELECT `id`, IFNULL(`modules_array`,'[]') FROM `content` WHERE `is_frontend` = ?");
        ErpDb.AddParameters(command, isFrontend);
        var rows = new List<(long Id, string Json)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add((reader.GetInt64(0), reader.GetString(1)));
            }
        }

        foreach (var row in rows)
        {
            var current = ParseIdArray(row.Json);
            var shouldHave = wanted.Contains(row.Id);
            var has = current.Contains(moduleId);
            if (shouldHave == has)
            {
                continue;
            }

            var next = shouldHave
                ? current.Append(moduleId).Distinct().ToList()
                : current.Where(id => id != moduleId).ToList();
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `content` SET `modules_array` = ? WHERE `id` = ?"),
                cancellationToken,
                JsonSerializer.Serialize(next),
                row.Id).ConfigureAwait(false);
        }
    }

    private static async Task UntiePagesAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        IReadOnlyList<long> moduleIds,
        int isFrontend,
        CancellationToken cancellationToken)
    {
        var remove = moduleIds.ToHashSet();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional("SELECT `id`, IFNULL(`modules_array`,'[]') FROM `content` WHERE `is_frontend` = ?");
        ErpDb.AddParameters(command, isFrontend);
        var rows = new List<(long Id, string Json)>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add((reader.GetInt64(0), reader.GetString(1)));
            }
        }

        foreach (var row in rows)
        {
            var current = ParseIdArray(row.Json);
            if (!current.Any(remove.Contains))
            {
                continue;
            }

            var next = current.Where(id => !remove.Contains(id)).ToList();
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `content` SET `modules_array` = ? WHERE `id` = ?"),
                cancellationToken,
                JsonSerializer.Serialize(next),
                row.Id).ConfigureAwait(false);
        }
    }

    private static async Task ReplaceAccessAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        long moduleId,
        IReadOnlyList<long> groupIds,
        CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("DELETE FROM `modules_access` WHERE `module_id` = ?"),
            cancellationToken,
            moduleId).ConfigureAwait(false);
        foreach (var groupId in groupIds)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `modules_access` (`module_id`, `group_id`) VALUES (?, ?)"),
                cancellationToken,
                moduleId,
                groupId).ConfigureAwait(false);
        }
    }

    private static List<long> ParseIdArray(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
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

            return ids;
        }
        catch (JsonException)
        {
            return [];
        }
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

        throw new ErpWriteException("Could not allocate a module translation key.");
    }

    private static string NormalizeLang(string? langCode)
    {
        var lang = (langCode ?? string.Empty).Trim().ToLowerInvariant();
        return lang.Length is < 2 or > 16 ? "en" : lang;
    }
}
