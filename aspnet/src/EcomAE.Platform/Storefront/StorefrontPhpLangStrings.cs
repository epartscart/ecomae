using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>cp/content/lang/ajax_get_text_strings.php</c>: the paged string list of the translation editor.
/// Same filters, sort whitelist, limits, and <c>lang_tabs_cols.php</c> table whitelist. The PHP
/// <c>SQL</c> echo is not returned; the editor page reads only <c>status</c>, <c>message</c>, and <c>items</c>.
/// </summary>
public static partial class StorefrontPhpAjax
{
    public const string CpTextStringsPath = "/cp/content/lang/ajax_get_text_strings.php";

    private static readonly string[] TextStringSortFields = ["str_key", "description", "current_lang_translation"];

    /// <summary>PHP <c>lang_tabs_cols.php</c>: tables and columns that may hold a <c>str_key</c>.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> LangTabsCols =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            ["config_groups"] = Cols(("caption", "text")),
            ["config_items"] = Cols(("caption", "text"), ("hint", "text")),
            ["content"] = Cols(("value", "text"), ("description", "text"), ("title_tag", "text"), ("description_tag", "text"), ("keywords_tag", "text"), ("author_tag", "text")),
            ["control_groups"] = Cols(("caption", "text")),
            ["control_items"] = Cols(("caption", "text")),
            ["groups"] = Cols(("value", "text"), ("description", "text")),
            ["menu"] = Cols(("caption", "text"), ("structure", "json")),
            ["metadata_handler_rules"] = Cols(("title_rule", "json")),
            ["modules"] = Cols(("prototype_name", "text"), ("caption", "text"), ("data", "json")),
            ["notifications_settings"] = Cols(("caption", "text"), ("description", "text"), ("event", "text"), ("email_subject", "text"), ("email_body", "text"), ("sms_body", "text"), ("default_email_subject", "text"), ("default_email_body", "text"), ("default_sms_body", "text"), ("vars", "json")),
            ["plugins"] = Cols(("caption", "text"), ("description", "text"), ("data_structure", "json"), ("data_value", "json")),
            ["reg_fields"] = Cols(("caption", "text")),
            ["reg_variants"] = Cols(("caption", "text")),
            ["shop_accounting_codes"] = Cols(("name", "text")),
            ["shop_catalogue_categories"] = Cols(("value", "text"), ("title_tag", "text"), ("description_tag", "text"), ("keywords_tag", "text")),
            ["shop_catalogue_products"] = Cols(("caption", "text"), ("title_tag", "text"), ("description_tag", "text"), ("keywords_tag", "text")),
            ["shop_categories_properties_map"] = Cols(("value", "text")),
            ["shop_currencies"] = Cols(("caption_short", "text")),
            ["shop_docpart_cars"] = Cols(("caption", "text")),
            ["shop_docpart_cars_catalogues"] = Cols(("caption", "text")),
            ["shop_docpart_prices_cols_types"] = Cols(("caption", "text")),
            ["shop_docpart_prices_load_modes"] = Cols(("name", "text")),
            ["shop_docpart_search_tabs"] = Cols(("caption", "text")),
            ["shop_geo"] = Cols(("value", "text")),
            ["shop_kkt_devices"] = Cols(("name", "text")),
            ["shop_kkt_interfaces_types"] = Cols(("description", "text")),
            ["shop_line_lists"] = Cols(("caption", "text")),
            ["shop_line_lists_items"] = Cols(("value", "text")),
            ["shop_main_page_groups"] = Cols(("caption", "text")),
            ["shop_obtaining_modes"] = Cols(("caption", "text")),
            ["shop_offices"] = Cols(("caption", "text"), ("country", "text"), ("region", "text"), ("city", "text"), ("address", "text"), ("description", "text"), ("timetable", "text")),
            ["shop_orders_items_statuses_ref"] = Cols(("name", "text")),
            ["shop_orders_statuses_ref"] = Cols(("name", "text")),
            ["shop_payment_systems"] = Cols(("name", "text"), ("description", "text")),
            ["shop_print_docs"] = Cols(("caption", "text"), ("description", "text")),
            ["shop_products_stickers"] = Cols(("value", "text"), ("description", "text")),
        };

    public sealed record TextStringsQuery(
        string? ItemsFilter,
        string? ItemsSort,
        string? LimitFrom,
        string? LimitCount,
        string? ItemsNew,
        string? LeftLang,
        string? RightLang,
        string? LangCpCookie);

    public sealed record TextStringsBody(
        [property: JsonPropertyName("debug")] string Debug,
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("items")] IReadOnlyList<JsonObject> Items);

    public static async Task<object> TextStringsAsync(
        DbConnection connection,
        CpLangRequest cpLang,
        string? adminSession,
        string? adminUser,
        string? postedCsrf,
        IReadOnlyDictionary<string, string> config,
        TextStringsQuery query,
        CancellationToken cancellationToken)
        => await LangAdminAsync(
            connection,
            cpLang,
            adminSession,
            adminUser,
            postedCsrf,
            async () =>
            {
                try
                {
                    return await TextStringsCoreAsync(connection, config, query, cancellationToken).ConfigureAwait(false);
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, LangStringsMissing);
                }
            },
            cancellationToken).ConfigureAwait(false);

    private static async Task<object> TextStringsCoreAsync(
        DbConnection connection,
        IReadOnlyDictionary<string, string> config,
        TextStringsQuery query,
        CancellationToken cancellationToken)
    {
        var filter = JsonObjectOrNull(query.ItemsFilter);
        var sort = JsonObjectOrNull(query.ItemsSort);
        var itemsNew = JsonArrayOrNull(query.ItemsNew);

        var languages = new List<string>();
        await using (var langs = connection.CreateCommand())
        {
            langs.CommandText = "SELECT `lang_code` FROM `lang_languages`";
            await using var reader = await langs.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var code = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                if (Regex.IsMatch(code, "^[A-Za-z0-9_-]{1,16}$"))
                {
                    languages.Add(code);
                }
            }
        }

        var sql = new StringBuilder("SELECT *");
        foreach (var code in languages)
        {
            sql.Append(", (SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `lang_code` = '")
                .Append(code)
                .Append("' AND `str_key` = `lang_text_strings`.`str_key`) AS `has_")
                .Append(code)
                .Append('`');
        }

        sql.Append(", (SELECT `value` FROM `lang_text_strings_translation` WHERE `lang_code` = ? AND `str_key` = `lang_text_strings`.`str_key`) AS `current_lang_translation`");
        var parameters = new List<object?> { await CpWorkLangAsync(connection, config, query.LangCpCookie, cancellationToken).ConfigureAwait(false) };

        var leftLang = query.LeftLang ?? string.Empty;
        var rightLang = query.RightLang ?? string.Empty;
        if (leftLang.Length > 0 && rightLang.Length > 0)
        {
            sql.Append(", (SELECT `value` FROM `lang_text_strings_translation` WHERE `lang_code` = ? AND `str_key` = `lang_text_strings`.`str_key`) AS `left_lang_translation`");
            sql.Append(", (SELECT `value` FROM `lang_text_strings_translation` WHERE `lang_code` = ? AND `str_key` = `lang_text_strings`.`str_key`) AS `right_lang_translation`");
            parameters.Add(leftLang);
            parameters.Add(rightLang);
        }

        sql.Append(" FROM `lang_text_strings`");

        var where = new List<string>();
        var strKey = FilterText(filter, "str_key");
        if (strKey.Length > 0)
        {
            Like(where, parameters, "`str_key`", strKey, FilterText(filter, "str_key_like") == "1");
        }

        var description = FilterText(filter, "description");
        if (description.Length > 0)
        {
            Like(where, parameters, "`description`", description, FilterText(filter, "description_like") == "1");
        }

        var translation = FilterText(filter, "translation");
        if (translation.Length > 0)
        {
            var like = FilterText(filter, "translation_like") == "1";
            where.Add("(SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `value` " + (like ? "LIKE" : "=") + " ? AND `str_key` = `lang_text_strings`.`str_key`) > ?");
            parameters.Add(like ? "%" + translation + "%" : translation);
            parameters.Add(0);
        }

        const string translations = "(SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `str_key` = `lang_text_strings`.`str_key`)";
        switch (FilterText(filter, "translation_progress"))
        {
            case "1":
                where.Add(translations + " = (SELECT COUNT(*) FROM `lang_languages`)");
                break;
            case "2":
                where.Add(translations + " < (SELECT COUNT(*) FROM `lang_languages`) AND " + translations + " != 0");
                break;
            case "3":
                where.Add(translations + " = 0");
                break;
            case "4":
                where.Add(translations + " < (SELECT COUNT(*) FROM `lang_languages`)");
                break;
        }

        var noTranslationIn = FilterText(filter, "no_translation_in");
        if (noTranslationIn.Length > 0 && noTranslationIn != "0")
        {
            where.Add("(SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `str_key` = `lang_text_strings`.`str_key` AND `lang_code` = ?) = 0");
            parameters.Add(noTranslationIn);
        }

        var hasTranslationIn = FilterText(filter, "has_translation_in");
        if (hasTranslationIn.Length > 0 && hasTranslationIn != "0")
        {
            where.Add("(SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `str_key` = `lang_text_strings`.`str_key` AND `lang_code` = ?) = 1");
            parameters.Add(hasTranslationIn);
        }

        var same = FilterText(filter, "same");
        if (same.Length > 0 && same != "0")
        {
            if (same == "1")
            {
                where.Add("`same` IS NOT NULL");
            }
            else if (same == "2")
            {
                where.Add("`same` IS NULL");
            }
            else
            {
                where.Add("`same` = ?");
                parameters.Add(same);
            }
        }

        var isCustom = FilterText(filter, "is_custom");
        if (isCustom is "0" or "1")
        {
            where.Add("`is_custom` = ?");
            parameters.Add(isCustom);
        }

        var usedFound = FilterText(filter, "used_found");
        if (usedFound is "0" or "1" or "2")
        {
            where.Add("`used_found` = ?");
            parameters.Add(usedFound);
        }

        var isError = FilterText(filter, "is_error");
        if (isError.Length > 0 && isError != "0")
        {
            where.Add("`is_error` = ?");
            parameters.Add(isError == "2" ? "0" : isError);
        }

        var table = FilterText(filter, "table");
        if (table.Length > 0 && table != "0")
        {
            if (!LangTabsCols.TryGetValue(table, out var tableCols))
            {
                return string.Empty;
            }

            var column = FilterText(filter, "column");
            IEnumerable<KeyValuePair<string, string>> searched = tableCols;
            if (column.Length > 0 && column != "0")
            {
                if (!tableCols.TryGetValue(column, out var type))
                {
                    return string.Empty;
                }

                searched = [new KeyValuePair<string, string>(column, type)];
            }

            where.Add("(" + string.Join(
                " OR ",
                searched.Select(c => c.Value == "text"
                    ? "(SELECT COUNT(*) FROM `" + table + "` WHERE `" + c.Key + "` = `lang_text_strings`.`str_key`) > 0"
                    : "(SELECT COUNT(*) FROM `" + table + "` WHERE `" + c.Key + "` LIKE CONCAT('%\"',`lang_text_strings`.`str_key`,'\"%')) > 0")) + ")");
        }

        if (itemsNew is { Count: > 0 })
        {
            where.Add("`str_key` NOT IN (" + string.Join(", ", itemsNew.Select(_ => "?")) + ")");
            parameters.AddRange(itemsNew.Select(n => (object?)NodeText(n)));
        }

        if (EditorRestricted(config))
        {
            where.Add("`is_custom` = 0");
        }

        if (where.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", where));
        }

        if (!PhpWholeNumber(query.LimitFrom, out var limitFrom)
            || !PhpWholeNumber(query.LimitCount, out var limitCount)
            || limitFrom < 0
            || limitCount <= 0
            || limitCount > 5000)
        {
            return string.Empty;
        }

        var sortField = sort?["field"] is JsonValue f ? NodeText(f) : string.Empty;
        var sortDirection = sort?["asc_desc"] is JsonValue d ? NodeText(d) : string.Empty;
        if (!TextStringSortFields.Contains(sortField, StringComparer.Ordinal) || sortDirection is not ("asc" or "desc"))
        {
            return string.Empty;
        }

        sql.Append(" ORDER BY `").Append(sortField).Append("` ").Append(sortDirection)
            .Append(" LIMIT ").Append(limitFrom.ToString(CultureInfo.InvariantCulture))
            .Append(", ").Append(limitCount.ToString(CultureInfo.InvariantCulture));

        var items = new List<JsonObject>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(sql.ToString());
            ErpDb.AddParameters(command, parameters.ToArray());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                var item = RowObject(row);
                item["description"] = PhpHtmlEntities(row.GetValueOrDefault("description") as string);
                item["current_lang_translation"] = PhpHtmlEntities(row.GetValueOrDefault("current_lang_translation") as string);
                items.Add(item);
            }
        }

        return new TextStringsBody(filter?.ToJsonString() ?? "null", true, "OK", items);
    }

    /// <summary>PHP <c>multilang_init</c> backend branch: forced <c>backend_ui_lang</c>, then the <c>lang_cp</c> cookie, then the active default.</summary>
    public static async Task<string> CpWorkLangAsync(
        DbConnection connection,
        IReadOnlyDictionary<string, string> config,
        string? langCpCookie,
        CancellationToken cancellationToken)
    {
        var multilang = config.TryGetValue("multilang", out var flag) && flag.Length > 0 && flag != "0" && !flag.Equals("false", StringComparison.OrdinalIgnoreCase);
        if (multilang)
        {
            var forced = config.TryGetValue("backend_ui_lang", out var backend) ? backend.Trim() : string.Empty;
            foreach (var candidate in new[] { forced, langCpCookie ?? string.Empty })
            {
                if (candidate.Length > 0 && await ErpDb.LongAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT COUNT(*) FROM `lang_languages` WHERE `active` = 1 AND `lang_code` = ?"),
                        cancellationToken,
                        candidate).ConfigureAwait(false) > 0)
                {
                    return candidate;
                }
            }
        }

        return await ErpDb.StringAsync(
            connection,
            null,
            "SELECT `lang_code` FROM `lang_languages` WHERE `active` = 1 AND `is_default` = 1 LIMIT 1",
            cancellationToken).ConfigureAwait(false) ?? string.Empty;
    }

    /// <summary>PHP 8 <c>htmlentities</c> default flags for ASCII markup: quotes become <c>&amp;quot;</c> and <c>&amp;#039;</c>.</summary>
    public static string PhpHtmlEntities(string? value)
        => string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("\"", "&quot;", StringComparison.Ordinal)
                .Replace("'", "&#039;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal);

    private static IReadOnlyDictionary<string, string> Cols(params (string Column, string Type)[] columns)
        => columns.ToDictionary(c => c.Column, c => c.Type, StringComparer.Ordinal);

    private static void Like(List<string> where, List<object?> parameters, string column, string value, bool like)
    {
        where.Add(column + (like ? " LIKE ?" : " = ?"));
        parameters.Add(like ? "%" + value + "%" : value);
    }

    private static string FilterText(JsonObject? filter, string name)
        => filter?[name] is JsonValue value ? NodeText(value) : string.Empty;

    private static string NodeText(JsonNode? node)
        => node is JsonValue value
            ? value.GetValueKind() switch
            {
                JsonValueKind.String => value.GetValue<string>(),
                JsonValueKind.Number => value.ToJsonString(),
                JsonValueKind.True => "1",
                _ => string.Empty,
            }
            : string.Empty;

    private static bool PhpWholeNumber(string? raw, out long value)
    {
        value = 0;
        var text = (raw ?? string.Empty).Trim();
        return text.Length > 0 && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
    }

    private static JsonObject? JsonObjectOrNull(string? raw)
    {
        try
        {
            return string.IsNullOrWhiteSpace(raw) ? null : JsonNode.Parse(raw) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonArray? JsonArrayOrNull(string? raw)
    {
        try
        {
            return string.IsNullOrWhiteSpace(raw) ? null : JsonNode.Parse(raw) as JsonArray;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
