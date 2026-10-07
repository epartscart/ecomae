using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string LangTranslationsMissing = "Language translations are not in this database.";
    public const string LanguagesMissing = "Languages are not in this database.";
    public const string ContentPagesMissing = "Content pages are not in this database.";
    public const string PricePagesMissing = "Price pages are not in this database.";
    public const string WarehouseGroupsMissing = "Warehouse groups are not in this database.";
    public const string WarehousesMissing = "Warehouses are not in this database.";
    public const string ManufacturersMissing = "Manufacturer synonyms are not in this database.";
    public const string StringNotFound = "String not found";
    public const string TooFewArguments = "Too few arguments";
    public const string EmptyValue = "Empty value does not acceptable";
    public const string NoSuchString = "No such string";
    public const string IncorrectUsedFound = "Incorrect value of used_found";
    public const string IncorrectIsError = "Incorrect value of is_error";
    public const string IncorrectIsCustom = "Incorrect value of is_custom";
    public const string IncorrectSame = "Incorrect value of same";
    public const string LanguageNotFound = "Language not found";
    public const string SearchFailed = "Error: Error searching string";
    public const string NoLanguagesString = "2523";
    public const string ContentDuplicated = "duplicated";
    public const string ContentOk = "ok";
    public const string ContentSqlError = "SQL-error";
    public const string ReturnDb = "DB";
    public const string ReturnCsrf = "CSRF";
    public const string ReturnInvalid = "Invalid return";
    public const string ReturnInvalidStatus = "Invalid status";
    public const string ReturnInvalidLine = "Invalid line decision";
    public const string ReturnLineMissing = "Line not found";
    public const string ReturnDecideFirst = "Decide every line (Approve or Deny) before closing.";
    public const string GroupsDb = "DB connect error";
    public const string GroupsBadRequest = "Bad request";
    public const string GroupsRequired = "Name and warehouses required";
    public const string GroupsUnknown = "Unknown action";
    public const string ManufacturerForbidden = "forbidden";
    public const string ManufacturerBadRequest = "bad_request";
    public const string ToggleUnknown = "Unknown action";
    public const string ToggleInvalid = "Invalid entity id";
    public const string StorageNotFound = "Storage not found";
    public const string PriceListNotFound = "Price list not found";

    private static readonly (string Table, string Column, bool Json)[] LangUsageColumns =
    [
        ("config_groups", "caption", false),
        ("config_items", "caption", false),
        ("config_items", "hint", false),
        ("content", "value", false),
        ("content", "description", false),
        ("content", "title_tag", false),
        ("content", "description_tag", false),
        ("content", "keywords_tag", false),
        ("content", "author_tag", false),
        ("control_groups", "caption", false),
        ("control_items", "caption", false),
        ("groups", "value", false),
        ("groups", "description", false),
        ("menu", "caption", false),
        ("menu", "structure", true),
        ("metadata_handler_rules", "title_rule", true),
        ("modules", "prototype_name", false),
        ("modules", "caption", false),
        ("modules", "data", true),
        ("notifications_settings", "caption", false),
        ("notifications_settings", "description", false),
        ("notifications_settings", "event", false),
        ("notifications_settings", "email_subject", false),
        ("notifications_settings", "email_body", false),
        ("notifications_settings", "sms_body", false),
        ("notifications_settings", "default_email_subject", false),
        ("notifications_settings", "default_email_body", false),
        ("notifications_settings", "default_sms_body", false),
        ("notifications_settings", "vars", true),
        ("plugins", "caption", false),
        ("plugins", "description", false),
        ("plugins", "data_structure", true),
        ("plugins", "data_value", true),
        ("reg_fields", "caption", false),
        ("reg_variants", "caption", false),
        ("shop_accounting_codes", "name", false),
        ("shop_catalogue_categories", "value", false),
        ("shop_catalogue_categories", "title_tag", false),
        ("shop_catalogue_categories", "description_tag", false),
        ("shop_catalogue_categories", "keywords_tag", false),
        ("shop_catalogue_products", "caption", false),
        ("shop_catalogue_products", "title_tag", false),
        ("shop_catalogue_products", "description_tag", false),
        ("shop_catalogue_products", "keywords_tag", false),
        ("shop_categories_properties_map", "value", false),
        ("shop_currencies", "caption_short", false),
        ("shop_docpart_cars", "caption", false),
        ("shop_docpart_cars_catalogues", "caption", false),
        ("shop_docpart_prices_cols_types", "caption", false),
        ("shop_docpart_prices_load_modes", "name", false),
        ("shop_docpart_search_tabs", "caption", false),
        ("shop_geo", "value", false),
        ("shop_kkt_devices", "name", false),
        ("shop_kkt_interfaces_types", "description", false),
        ("shop_line_lists", "caption", false),
        ("shop_line_lists_items", "value", false),
        ("shop_main_page_groups", "caption", false),
        ("shop_obtaining_modes", "caption", false),
        ("shop_offices", "caption", false),
        ("shop_offices", "country", false),
        ("shop_offices", "region", false),
        ("shop_offices", "city", false),
        ("shop_offices", "address", false),
        ("shop_offices", "description", false),
        ("shop_offices", "timetable", false),
        ("shop_orders_items_statuses_ref", "name", false),
        ("shop_orders_statuses_ref", "name", false),
        ("shop_payment_systems", "name", false),
        ("shop_payment_systems", "description", false),
        ("shop_print_docs", "caption", false),
        ("shop_print_docs", "description", false),
        ("shop_products_stickers", "value", false),
        ("shop_products_stickers", "description", false),
        ("shop_products_text", "content", false),
        ("shop_properties_types", "caption", false),
        ("shop_properties_types", "info", false),
        ("shop_properties_values_text", "value", false),
        ("shop_sao_actions", "name", false),
        ("shop_sao_states", "name", false),
        ("shop_tree_lists", "caption", false),
        ("shop_tree_lists_items", "value", false)
    ];

    public static Task<object> SaveStringDescriptionAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        bool hasKey,
        string? strKey,
        bool hasValue,
        string? value,
        CancellationToken cancellationToken)
        => LangAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            async () =>
            {
                if (!hasKey || !hasValue)
                {
                    return new FlagBody(false, TooFewArguments);
                }

                try
                {
                    var count = await ErpDb.LongAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings` WHERE `str_key` = ?"),
                        cancellationToken,
                        strKey ?? string.Empty).ConfigureAwait(false);
                    if (count != 1)
                    {
                        return new FlagBody(false, StringNotFound);
                    }

                    if (string.IsNullOrEmpty(value))
                    {
                        return new FlagBody(false, EmptyValue);
                    }

                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("UPDATE `lang_text_strings` SET `description` = ? WHERE `str_key` = ?"),
                        cancellationToken,
                        value,
                        strKey).ConfigureAwait(false);
                    return new DescriptionSaved(true, strKey ?? string.Empty, "OK", HtmlCompat(value));
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, LangStringsMissing);
                }
            },
            cancellationToken);

    public static Task<object> SetUsedFoundAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        bool hasKey,
        string? strKey,
        bool hasValue,
        string? value,
        CancellationToken cancellationToken)
        => LangAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            async () =>
            {
                if (!hasKey || !hasValue)
                {
                    return new FlagBody(false, TooFewArguments);
                }

                var used = PhpInt(value);
                if (used is not 0 and not 1 and not 2)
                {
                    return new FlagBody(false, IncorrectUsedFound);
                }

                try
                {
                    var count = await ErpDb.LongAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings` WHERE `str_key` = ?"),
                        cancellationToken,
                        strKey ?? string.Empty).ConfigureAwait(false);
                    if (count != 1)
                    {
                        return new FlagBody(false, NoSuchString);
                    }

                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("UPDATE `lang_text_strings` SET `used_found` = ? WHERE `str_key` = ?"),
                        cancellationToken,
                        used,
                        strKey).ConfigureAwait(false);
                    return new UsedFoundSaved(true, string.Empty, strKey ?? string.Empty, used);
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new FlagBody(false, LangStringsMissing);
                }
            },
            cancellationToken);

    public static Task<object> DeleteUnusedStringsAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        CancellationToken cancellationToken)
        => LangAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            async () =>
            {
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional("DELETE FROM `lang_text_strings_translation` WHERE `str_key` IN (SELECT `str_key` FROM `lang_text_strings` WHERE `is_custom` = ? AND `used_found` = ?)"),
                        cancellationToken,
                        1,
                        2).ConfigureAwait(false);
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional("DELETE FROM `lang_text_strings` WHERE `is_custom` = ? AND `used_found` = ?"),
                        cancellationToken,
                        1,
                        2).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return new FlagBody(true, "OK");
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    var text = ex.ToString();
                    return new FlagBody(false, text.Contains("lang_text_strings_translation", StringComparison.OrdinalIgnoreCase)
                        ? LangTranslationsMissing
                        : LangStringsMissing);
                }
            },
            cancellationToken);

    public static Task<object> CreateStringAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        IReadOnlyDictionary<string, string> config,
        bool hasDescription,
        string? description,
        bool hasSame,
        string? same,
        bool hasError,
        string? isErrorRaw,
        bool hasCustom,
        string? isCustomRaw,
        bool hasUsed,
        string? usedRaw,
        CancellationToken cancellationToken)
        => LangAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            async () =>
            {
                if (!hasDescription || !hasSame || !hasError || !hasCustom || !hasUsed)
                {
                    return new FlagBody(false, TooFewArguments);
                }

                var isError = PhpInt(isErrorRaw);
                if (isError is not 0 and not 1)
                {
                    return new FlagBody(false, IncorrectIsError);
                }

                string? sameValue = same;
                try
                {
                    if (!string.Equals(same, "no", StringComparison.Ordinal))
                    {
                        var langs = await ErpDb.LongAsync(
                            connection,
                            null,
                            ErpDb.Positional("SELECT COUNT(*) FROM `lang_languages` WHERE `lang_code` = ?"),
                            cancellationToken,
                            same ?? string.Empty).ConfigureAwait(false);
                        if (langs != 1)
                        {
                            return new FlagBody(false, IncorrectSame);
                        }
                    }
                    else
                    {
                        sameValue = null;
                    }

                    var isCustom = PhpInt(isCustomRaw);
                    if (isCustom is not 0 and not 1)
                    {
                        return new FlagBody(false, IncorrectIsCustom);
                    }

                    var used = PhpInt(usedRaw);
                    if (used is not 0 and not 1 and not 2)
                    {
                        return new FlagBody(false, IncorrectUsedFound);
                    }

                    if (string.IsNullOrEmpty(description))
                    {
                        return new FlagBody(false, EmptyValue);
                    }

                    var strKey = NextStringKey(config);
                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("INSERT INTO `lang_text_strings` (`description`, `same`, `is_error`, `str_key`, `used_found`, `is_custom`) VALUES (?, ?, ?, ?, ?, ?)"),
                        cancellationToken,
                        description,
                        sameValue,
                        isError,
                        strKey,
                        used,
                        isCustom).ConfigureAwait(false);

                    var body = new JsonObject
                    {
                        ["str_key"] = strKey,
                        ["description"] = HtmlCompat(description),
                        ["current_lang_translation"] = string.Empty,
                        ["same"] = sameValue,
                        ["is_error"] = isError,
                        ["used_found"] = used,
                        ["is_custom"] = isCustom
                    };
                    var filled = false;
                    await using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT `lang_code` FROM `lang_languages`";
                        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            var code = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                            if (Regex.IsMatch(code, "^[A-Za-z0-9_-]{1,16}$"))
                            {
                                body["has_" + code] = 0;
                                filled = true;
                            }
                        }
                    }

                    if (!filled)
                    {
                        return new FlagBody(false, NoLanguagesString);
                    }

                    return new CreatedString(true, "OK", body);
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    var text = ex.ToString();
                    return new FlagBody(false, text.Contains("lang_languages", StringComparison.OrdinalIgnoreCase)
                        ? LanguagesMissing
                        : LangStringsMissing);
                }
            },
            cancellationToken);

    public static Task<object> SaveTranslationAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        bool restricted,
        bool hasKey,
        string? strKey,
        bool hasLang,
        string? langCode,
        bool hasValue,
        string? value,
        CancellationToken cancellationToken)
        => LangAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            async () =>
            {
                if (!hasKey || !hasLang || !hasValue)
                {
                    return new FlagBody(false, TooFewArguments);
                }

                try
                {
                    if (restricted)
                    {
                        var locked = await ErpDb.LongAsync(
                            connection,
                            null,
                            ErpDb.Positional("SELECT `restrict_edit` FROM `lang_languages` WHERE `lang_code` = ?"),
                            cancellationToken,
                            langCode ?? string.Empty).ConfigureAwait(false);
                        if (locked == 1)
                        {
                            return new FlagBody(false, LangRestricted);
                        }
                    }

                    var strings = await ErpDb.LongAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings` WHERE `str_key` = ?"),
                        cancellationToken,
                        strKey ?? string.Empty).ConfigureAwait(false);
                    if (strings != 1)
                    {
                        return new FlagBody(false, StringNotFound);
                    }

                    var langs = await ErpDb.LongAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT COUNT(*) FROM `lang_languages` WHERE `lang_code` = ?"),
                        cancellationToken,
                        langCode ?? string.Empty).ConfigureAwait(false);
                    if (langs != 1)
                    {
                        return new FlagBody(false, LanguageNotFound);
                    }

                    if (string.IsNullOrEmpty(value))
                    {
                        return new FlagBody(false, EmptyValue);
                    }

                    var existing = await ErpDb.LongAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT COUNT(*) FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = ?"),
                        cancellationToken,
                        strKey,
                        langCode).ConfigureAwait(false);
                    if (existing == 1)
                    {
                        await ErpDb.ExecuteAsync(
                            connection,
                            null,
                            ErpDb.Positional("UPDATE `lang_text_strings_translation` SET `value` = ? WHERE `str_key` = ? AND `lang_code` = ?"),
                            cancellationToken,
                            value,
                            strKey,
                            langCode).ConfigureAwait(false);
                    }
                    else
                    {
                        await ErpDb.ExecuteAsync(
                            connection,
                            null,
                            ErpDb.Positional("INSERT INTO `lang_text_strings_translation` (`str_key`, `lang_code`, `value`) VALUES (?, ?, ?)"),
                            cancellationToken,
                            strKey,
                            langCode,
                            value).ConfigureAwait(false);
                    }

                    return new TranslationSaved(true, strKey ?? string.Empty, HtmlCompat(langCode), HtmlCompat(value), "OK");
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    var text = ex.ToString();
                    if (text.Contains("lang_text_strings_translation", StringComparison.OrdinalIgnoreCase))
                    {
                        return new FlagBody(false, LangTranslationsMissing);
                    }

                    if (text.Contains("lang_languages", StringComparison.OrdinalIgnoreCase))
                    {
                        return new FlagBody(false, LanguagesMissing);
                    }

                    return new FlagBody(false, LangStringsMissing);
                }
            },
            cancellationToken);

    public static Task<object> SearchUsedFoundAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        CancellationToken cancellationToken)
        => LangAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            async () =>
            {
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional("UPDATE `lang_text_strings` SET `used_found` = ? WHERE ?"),
                        cancellationToken,
                        0,
                        1).ConfigureAwait(false);
                    var keys = new List<string>();
                    await using (var command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "SELECT `str_key` FROM `lang_text_strings`";
                        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            keys.Add(reader.IsDBNull(0) ? string.Empty : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty);
                        }
                    }

                    foreach (var key in keys)
                    {
                        var found = false;
                        foreach (var column in LangUsageColumns)
                        {
                            if (!SafeIdent(column.Table) || !SafeIdent(column.Column))
                            {
                                continue;
                            }

                            var sql = column.Json
                                ? "SELECT 1 FROM `" + column.Table + "` WHERE `" + column.Column + "` LIKE ? LIMIT 1"
                                : "SELECT 1 FROM `" + column.Table + "` WHERE `" + column.Column + "` = ? LIMIT 1";
                            var probe = column.Json ? "%\"" + key + "\"%" : key;
                            var hit = await ErpDb.LongAsync(connection, transaction, sql, cancellationToken, probe).ConfigureAwait(false);
                            if (hit == 1)
                            {
                                found = true;
                                break;
                            }
                        }

                        await ErpDb.ExecuteAsync(
                            connection,
                            transaction,
                            ErpDb.Positional("UPDATE `lang_text_strings` SET `used_found` = ? WHERE `str_key` = ?"),
                            cancellationToken,
                            found ? 1 : 2,
                            key).ConfigureAwait(false);
                    }

                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return new FlagBody(true, "OK");
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    var text = ex.ToString();
                    if (text.Contains("lang_text_strings", StringComparison.OrdinalIgnoreCase)
                        && text.Contains("used_found", StringComparison.OrdinalIgnoreCase)
                        && !text.Contains("Unknown column", StringComparison.OrdinalIgnoreCase) == false
                        && text.Contains("UPDATE", StringComparison.OrdinalIgnoreCase))
                    {
                        return new FlagBody(false, LangStringsMissing);
                    }

                    return new FlagBody(false, text.Contains("lang_text_strings", StringComparison.OrdinalIgnoreCase) && text.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase)
                        ? LangStringsMissing
                        : SearchFailed);
                }
            },
            cancellationToken);

    public static async Task<object> ContentAliasAsync(
        DbConnection connection,
        string? alias,
        string? contentId,
        string? parent,
        string? isFrontend,
        CancellationToken cancellationToken)
    {
        try
        {
            var count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `content` WHERE `alias` = ? AND `parent` = ? AND `is_frontend` = ? AND `id` != ?"),
                cancellationToken,
                alias ?? string.Empty,
                parent ?? string.Empty,
                isFrontend ?? string.Empty,
                contentId ?? string.Empty).ConfigureAwait(false);
            var code = count > 0 ? ContentDuplicated : ContentOk;
            return new AliasBody(true, code, code);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, ContentPagesMissing);
        }
        catch (DbException)
        {
            return new FlagBody(false, ContentSqlError);
        }
    }

    public static async Task<object> ManufacturersAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        string? requestObject,
        CancellationToken cancellationToken)
    {
        var gate = await AdminThenCsrfAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            new ManufacturerBody(false, ManufacturerForbidden, [], []),
            cancellationToken).ConfigureAwait(false);
        if (gate is not null)
        {
            return gate;
        }

        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                """
                CREATE TABLE IF NOT EXISTS `shop_docpart_manufacturers` (
                  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
                  `name` VARCHAR(255) NOT NULL DEFAULT '',
                  PRIMARY KEY (`id`),
                  UNIQUE KEY `uq_name` (`name`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
                """,
                cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                """
                CREATE TABLE IF NOT EXISTS `shop_docpart_manufacturers_synonyms` (
                  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
                  `manufacturer_id` INT UNSIGNED NOT NULL DEFAULT 0,
                  `synonym` VARCHAR(255) NOT NULL DEFAULT '',
                  PRIMARY KEY (`id`),
                  KEY `idx_mfr` (`manufacturer_id`),
                  KEY `idx_synonym` (`synonym`(191))
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
                """,
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new ManufacturerBody(false, ManufacturersMissing, [], []);
        }

        JsonElement request;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(requestObject) ? "null" : requestObject);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new ManufacturerBody(false, ManufacturerBadRequest, [], []);
            }

            request = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return new ManufacturerBody(false, ManufacturerBadRequest, [], []);
        }

        var action = request.TryGetProperty("action", out var actionNode) ? actionNode.GetString() ?? string.Empty : string.Empty;
        try
        {
            switch (action)
            {
                case "get_manufacturers":
                    return new ManufacturerBody(true, null, await ManufacturerRowsAsync(connection, cancellationToken).ConfigureAwait(false), []);
                case "get_synonyms":
                    return new ManufacturerBody(true, null, [], await SynonymRowsAsync(connection, RequestInt(request, "id"), cancellationToken).ConfigureAwait(false));
                case "add_manufacturer":
                    return await AddManufacturerAsync(connection, CleanManufacturer(JsonString(request, "name")), cancellationToken).ConfigureAwait(false);
                case "save_manufacturer":
                    return await SaveManufacturerAsync(connection, RequestInt(request, "id"), CleanManufacturer(JsonString(request, "name")), cancellationToken).ConfigureAwait(false);
                case "del_manufacturer":
                    return await DeleteManufacturerAsync(connection, RequestInt(request, "id"), cancellationToken).ConfigureAwait(false);
                case "add_synonym":
                    return await AddSynonymAsync(connection, RequestInt(request, "id"), CleanManufacturer(JsonString(request, "name")), cancellationToken).ConfigureAwait(false);
                case "save_synonym":
                    return await SaveSynonymAsync(connection, RequestInt(request, "id"), CleanManufacturer(JsonString(request, "name")), cancellationToken).ConfigureAwait(false);
                case "del_synonym":
                    return await DeleteSynonymAsync(connection, RequestInt(request, "id"), cancellationToken).ConfigureAwait(false);
                default:
                    return new ManufacturerBody(false, "unknown_action", [], []);
            }
        }
        catch (DbException)
        {
            return new ManufacturerBody(false, "query_failed", [], []);
        }
    }

    public static async Task<object> StorageGroupsAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        string? requestObject,
        CancellationToken cancellationToken)
        => await WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new FlagBody(false, "Forbidden"),
            async _ =>
            {
                JsonElement request;
                try
                {
                    using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(requestObject) ? "null" : WebUtility.UrlDecode(requestObject));
                    if (document.RootElement.ValueKind != JsonValueKind.Object)
                    {
                        using var raw = JsonDocument.Parse(string.IsNullOrWhiteSpace(requestObject) ? "null" : requestObject);
                        if (raw.RootElement.ValueKind != JsonValueKind.Object)
                        {
                            return new FlagBody(false, GroupsBadRequest);
                        }

                        request = raw.RootElement.Clone();
                    }
                    else
                    {
                        request = document.RootElement.Clone();
                    }
                }
                catch (JsonException)
                {
                    return new FlagBody(false, GroupsBadRequest);
                }

                var action = JsonString(request, "action");
                try
                {
                    if (string.Equals(action, "get_table", StringComparison.Ordinal))
                    {
                        return new RawHttp(await GroupTableAsync(connection, cancellationToken).ConfigureAwait(false), "text/html; charset=utf-8");
                    }

                    if (string.Equals(action, "get_storages", StringComparison.Ordinal))
                    {
                        return new StorageListBody(true, await AvailableStoragesAsync(connection, cancellationToken).ConfigureAwait(false));
                    }

                    if (string.Equals(action, "add_group", StringComparison.Ordinal))
                    {
                        var name = WebUtility.UrlDecode(JsonString(request, "name")).Trim();
                        var ids = JsonInts(request, "storages");
                        if (name.Length == 0 || ids.Count == 0)
                        {
                            return new FlagBody(false, GroupsRequired);
                        }

                        var order = await ErpDb.LongAsync(connection, null, "SELECT COALESCE(MAX(`order`), 0) FROM `shop_storages_groups`", cancellationToken).ConfigureAwait(false) + 1;
                        await ErpDb.ExecuteAsync(
                            connection,
                            null,
                            ErpDb.Positional("INSERT INTO `shop_storages_groups` (`name`, `storages`, `order`) VALUES (?, ?, ?)"),
                            cancellationToken,
                            name,
                            string.Join(',', ids),
                            order).ConfigureAwait(false);
                        return new FlagBody(true, string.Empty);
                    }

                    if (string.Equals(action, "del", StringComparison.Ordinal))
                    {
                        var id = RequestInt(request, "id");
                        if (id <= 0)
                        {
                            return new FlagBody(false, string.Empty);
                        }

                        await ErpDb.ExecuteAsync(
                            connection,
                            null,
                            ErpDb.Positional("DELETE FROM `shop_storages_groups` WHERE `id` = ?"),
                            cancellationToken,
                            id).ConfigureAwait(false);
                        return new FlagBody(true, string.Empty);
                    }

                    return new FlagBody(false, GroupsUnknown);
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    var text = ex.ToString();
                    return new FlagBody(false, text.Contains("shop_storages_groups", StringComparison.OrdinalIgnoreCase)
                        ? WarehouseGroupsMissing
                        : WarehousesMissing);
                }
            },
            cancellationToken);

    public static async Task<object> ReturnActionAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        string? action,
        int returnId,
        int statusId,
        int lineId,
        string? decide,
        CancellationToken cancellationToken)
    {
        var gate = await ReturnAdminAsync(connection, adminSession, adminUser, csrf, cancellationToken).ConfigureAwait(false);
        if (gate is not null)
        {
            return gate;
        }

        if (returnId < 1)
        {
            return new FlagBody(false, ReturnInvalid);
        }

        var adminId = ParseId(adminUser);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            if (string.Equals(action, "set_return_status", StringComparison.Ordinal))
            {
                if (statusId < 1)
                {
                    return new FlagBody(false, ReturnInvalidStatus);
                }

                var closed = await ReturnStatusIdAsync(connection, ["3798", "epc_ret_st_closed"], cancellationToken).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("UPDATE `shop_orders_returns` SET `status_id` = ?, `return_complete` = ? WHERE `id` = ?"),
                    cancellationToken,
                    statusId,
                    statusId == closed ? 1 : 0,
                    returnId).ConfigureAwait(false);
                return new FlagBody(true, string.Empty);
            }

            if (string.Equals(action, "decide_line", StringComparison.Ordinal))
            {
                if (lineId < 1 || decide is not ("0" or "1"))
                {
                    return new FlagBody(false, ReturnInvalidLine);
                }

                var line = await OneRowAsync(
                    connection,
                    "SELECT * FROM `shop_orders_returns_items` WHERE `id` = ? AND `return_id` = ? LIMIT 1",
                    cancellationToken,
                    lineId,
                    returnId).ConfigureAwait(false);
                if (line is null)
                {
                    return new FlagBody(false, ReturnLineMissing);
                }

                var itemId = line.TryGetValue("item_id", out var itemValue) && itemValue is not null ? Convert.ToInt32(itemValue, CultureInfo.InvariantCulture) : 0;
                var approved = string.Equals(decide, "1", StringComparison.Ordinal);
                var nextStatus = await StatusFlagIdAsync(connection, approved ? "complete_return" : "reject_return", cancellationToken).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("UPDATE `shop_orders_returns_items` SET `return_success` = ? WHERE `id` = ?"),
                    cancellationToken,
                    approved ? 1 : 0,
                    lineId).ConfigureAwait(false);
                if (itemId > 0 && nextStatus > 0)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("UPDATE `shop_orders_items` SET `status` = ? WHERE `id` = ?"),
                        cancellationToken,
                        nextStatus,
                        itemId).ConfigureAwait(false);
                    var orderId = await ErpDb.LongAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT `order_id` FROM `shop_orders_items` WHERE `id` = ? LIMIT 1"),
                        cancellationToken,
                        itemId).ConfigureAwait(false);
                    if (orderId > 0)
                    {
                        var text = approved
                            ? "Return line approved for item [" + itemId.ToString(CultureInfo.InvariantCulture) + "] on return #" + returnId.ToString(CultureInfo.InvariantCulture)
                            : "Return line denied for item [" + itemId.ToString(CultureInfo.InvariantCulture) + "] on return #" + returnId.ToString(CultureInfo.InvariantCulture);
                        await ErpDb.ExecuteAsync(
                            connection,
                            null,
                            ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`, `time`, `user_id`, `is_manager`, `text`, `is_robot`) VALUES (?, ?, ?, ?, ?, 0)"),
                            cancellationToken,
                            orderId,
                            now,
                            adminId,
                            1,
                            text).ConfigureAwait(false);
                    }
                }

                var open = await ReturnStatusIdAsync(connection, ["3806", "3796", "epc_ret_st_under_consideration", "epc_ret_st_created"], cancellationToken).ConfigureAwait(false);
                if (open > 0)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("UPDATE `shop_orders_returns` SET `status_id` = ?, `return_complete` = 0 WHERE `id` = ? AND (`return_complete` IS NULL OR `return_complete` = 0)"),
                        cancellationToken,
                        open,
                        returnId).ConfigureAwait(false);
                }

                return new FlagBody(true, string.Empty);
            }

            if (string.Equals(action, "finalize_return", StringComparison.Ordinal))
            {
                var pending = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `shop_orders_returns_items` WHERE `return_id` = ? AND (`return_success` IS NULL OR (`return_success` NOT IN (0, 1) AND `return_success` NOT IN ('0', '1')))"),
                    cancellationToken,
                    returnId).ConfigureAwait(false);
                if (pending > 0)
                {
                    return new FlagBody(false, ReturnDecideFirst);
                }

                var sum = await ErpDb.DecimalAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        """
                        SELECT IFNULL(SUM(oi.`price` * oi.`count_need`), 0)
                        FROM `shop_orders_returns_items` ri
                        INNER JOIN `shop_orders_items` oi ON oi.`id` = ri.`item_id`
                        WHERE ri.`return_id` = ? AND ri.`return_success` IN (1, '1')
                        """),
                    cancellationToken,
                    returnId).ConfigureAwait(false);
                var closed = await ReturnStatusIdAsync(connection, ["3798", "epc_ret_st_closed"], cancellationToken).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("UPDATE `shop_orders_returns` SET `status_id` = ?, `return_complete` = 1, `sum` = ? WHERE `id` = ?"),
                    cancellationToken,
                    closed,
                    sum,
                    returnId).ConfigureAwait(false);
                var orderId = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        """
                        SELECT oi.`order_id`
                        FROM `shop_orders_returns_items` ri
                        INNER JOIN `shop_orders_items` oi ON oi.`id` = ri.`item_id`
                        WHERE ri.`return_id` = ? AND oi.`order_id` > 0
                        ORDER BY oi.`order_id` ASC
                        LIMIT 1
                        """),
                    cancellationToken,
                    returnId).ConfigureAwait(false);
                if (orderId > 0)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`, `time`, `user_id`, `is_manager`, `text`, `is_robot`) VALUES (?, ?, ?, ?, ?, 0)"),
                        cancellationToken,
                        orderId,
                        now,
                        adminId,
                        1,
                        "Return #" + returnId.ToString(CultureInfo.InvariantCulture) + " closed. Approved sum: " + sum.ToString("0.00", CultureInfo.InvariantCulture)).ConfigureAwait(false);
                }

                return new ReturnClosed(true, sum);
            }

            return new FlagBody(false, GroupsUnknown);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            var text = ex.ToString();
            if (text.Contains("shop_orders_logs", StringComparison.OrdinalIgnoreCase))
            {
                return new FlagBody(false, OrderLogsMissing);
            }

            if (text.Contains("shop_orders_items_statuses_ref", StringComparison.OrdinalIgnoreCase))
            {
                return new FlagBody(false, "Order item statuses are not in this database.");
            }

            if (text.Contains("shop_orders_items", StringComparison.OrdinalIgnoreCase))
            {
                return new FlagBody(false, OrderItemsMissing);
            }

            if (text.Contains("shop_orders_returns_items", StringComparison.OrdinalIgnoreCase))
            {
                return new FlagBody(false, ReturnsMissing);
            }

            if (text.Contains("shop_orders_returns_statuses", StringComparison.OrdinalIgnoreCase))
            {
                return new FlagBody(false, ReturnStatusesMissing);
            }

            return new FlagBody(false, ReturnsHeaderMissing);
        }
    }

    public static async Task<object> StorageToggleAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        string? action,
        string? entityType,
        int entityId,
        bool enabled,
        CancellationToken cancellationToken)
        => await WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new LangAccessBody(false, LangDenied, LangDenied),
            async adminId =>
            {
                var gate = await PageAccessAsync(connection, adminId, "shop/prices", PricePagesMissing, cancellationToken).ConfigureAwait(false);
                if (gate is not null)
                {
                    return gate;
                }

                if (!string.Equals(action, "toggle", StringComparison.Ordinal))
                {
                    return new ToggleBody(false, ToggleUnknown, null, null);
                }

                if (entityId <= 0)
                {
                    return new ToggleBody(false, ToggleInvalid, null, null);
                }

                var price = string.Equals(entityType, "price_list", StringComparison.Ordinal);
                var disabled = enabled ? 0 : 1;
                try
                {
                    await EnsureToggleColumnAsync(connection, price ? "shop_docpart_prices" : "shop_storages", cancellationToken).ConfigureAwait(false);
                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        """
                        CREATE TABLE IF NOT EXISTS `epc_storefront_storage_toggle_audit` (
                          `id` INT(11) NOT NULL AUTO_INCREMENT,
                          `entity_type` VARCHAR(16) NOT NULL DEFAULT '',
                          `entity_id` INT(11) NOT NULL DEFAULT 0,
                          `entity_name` VARCHAR(255) NOT NULL DEFAULT '',
                          `storefront_disabled` TINYINT(1) NOT NULL DEFAULT 0,
                          `user_id` INT(11) NOT NULL DEFAULT 0,
                          `user_label` VARCHAR(128) NOT NULL DEFAULT '',
                          `created_at` DATETIME NOT NULL,
                          PRIMARY KEY (`id`)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8
                        """,
                        cancellationToken).ConfigureAwait(false);
                    var table = price ? "shop_docpart_prices" : "shop_storages";
                    var name = await ErpDb.StringAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT `name` FROM `" + table + "` WHERE `id` = ? LIMIT 1"),
                        cancellationToken,
                        entityId).ConfigureAwait(false) ?? string.Empty;
                    if (name.Length == 0)
                    {
                        return new ToggleBody(false, price ? PriceListNotFound : StorageNotFound, null, null);
                    }

                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("UPDATE `" + table + "` SET `storefront_temp_disabled` = ? WHERE `id` = ? LIMIT 1"),
                        cancellationToken,
                        disabled,
                        entityId).ConfigureAwait(false);
                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("INSERT INTO `epc_storefront_storage_toggle_audit` (`entity_type`, `entity_id`, `entity_name`, `storefront_disabled`, `user_id`, `user_label`, `created_at`) VALUES (?, ?, ?, ?, ?, ?, NOW())"),
                        cancellationToken,
                        price ? "price_list" : "storage",
                        entityId,
                        name,
                        disabled,
                        adminId,
                        "admin").ConfigureAwait(false);
                    return new ToggleBody(true, null, disabled, name);
                }
                catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
                {
                    return new ToggleBody(false, price ? PriceListsMissing : WarehousesMissing, null, null);
                }
            },
            cancellationToken);

    private static async Task EnsureToggleColumnAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        if (!SafeIdent(table))
        {
            return;
        }

        try
        {
            await ErpDb.LongAsync(connection, null, "SELECT `storefront_temp_disabled` FROM `" + table + "` LIMIT 1", cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            if (ex.ToString().Contains("doesn't exist", StringComparison.OrdinalIgnoreCase)
                && ex.ToString().Contains(table, StringComparison.OrdinalIgnoreCase)
                && !ex.ToString().Contains("Unknown column", StringComparison.OrdinalIgnoreCase))
            {
                throw;
            }

            try
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    "ALTER TABLE `" + table + "` ADD COLUMN `storefront_temp_disabled` TINYINT(1) NOT NULL DEFAULT 0",
                    cancellationToken).ConfigureAwait(false);
            }
            catch (DbException)
            {
                // PHP swallows the ALTER when the column or the AFTER anchor is already there.
            }
        }
    }

    private static async Task<object?> PageAccessAsync(DbConnection connection, int adminId, string url, string missing, CancellationToken cancellationToken)
    {
        try
        {
            var allowed = new List<int>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = ErpDb.Positional("SELECT `group_id` FROM `content_access` WHERE `content_id` = (SELECT `id` FROM `content` WHERE `url` = ? AND `is_frontend` = 0 LIMIT 1)");
                ErpDb.AddParameters(command, url);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    allowed.Add(reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture));
                }
            }

            if (allowed.Count == 0)
            {
                return new LangAccessBody(false, LangDenied, LangDenied);
            }

            var groups = new List<int>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = ErpDb.Positional("SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ?");
                ErpDb.AddParameters(command, adminId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    groups.Add(reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture));
                }
            }

            return groups.Any(allowed.Contains) ? null : new LangAccessBody(false, LangDenied, LangDenied);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, missing);
        }
    }

    private static async Task<object?> AdminThenCsrfAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        object notAdmin,
        CancellationToken cancellationToken)
    {
        try
        {
            var count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `sessions` WHERE `session` = ? AND `type` = 1 AND `user_id` = ?"),
                cancellationToken,
                adminSession ?? string.Empty,
                ParseId(adminUser)).ConfigureAwait(false);
            if (count == 0)
            {
                return notAdmin;
            }

            if (count != 1)
            {
                return new RawHttp(string.Empty, "text/html; charset=utf-8");
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, AdminSessionsMissing);
        }

        if (csrf is null)
        {
            return CsrfFailure("Error! CSRF 1");
        }

        if (csrf.Length == 0)
        {
            return CsrfFailure("Error! CSRF 3");
        }

        var stored = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT IFNULL(`csrf_guard_key`, '') FROM `sessions` WHERE `session` = ? AND `type` = 1 AND `user_id` = ? LIMIT 1"),
            cancellationToken,
            adminSession ?? string.Empty,
            ParseId(adminUser)).ConfigureAwait(false) ?? string.Empty;
        return string.Equals(stored, csrf, StringComparison.Ordinal) ? null : CsrfFailure("Error! CSRF 4");
    }

    private static async Task<object?> ReturnAdminAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        CancellationToken cancellationToken)
    {
        try
        {
            var count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `sessions` WHERE `session` = ? AND `type` = 1 AND `user_id` = ?"),
                cancellationToken,
                adminSession ?? string.Empty,
                ParseId(adminUser)).ConfigureAwait(false);
            if (count == 0)
            {
                return new FlagBody(false, "Forbidden");
            }

            if (count != 1)
            {
                return new RawHttp(string.Empty, "text/html; charset=utf-8");
            }

            var stored = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT IFNULL(`csrf_guard_key`, '') FROM `sessions` WHERE `session` = ? AND `type` = 1 AND `user_id` = ? LIMIT 1"),
                cancellationToken,
                adminSession ?? string.Empty,
                ParseId(adminUser)).ConfigureAwait(false) ?? string.Empty;
            if (stored.Length == 0 || !string.Equals(stored, csrf ?? string.Empty, StringComparison.Ordinal))
            {
                return new FlagBody(false, ReturnCsrf);
            }

            return null;
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, AdminSessionsMissing);
        }
    }

    private static async Task<int> ReturnStatusIdAsync(DbConnection connection, IReadOnlyList<string> captions, CancellationToken cancellationToken)
    {
        foreach (var caption in captions)
        {
            var id = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `shop_orders_returns_statuses` WHERE `caption` = ? LIMIT 1"),
                cancellationToken,
                caption).ConfigureAwait(false);
            if (id > 0)
            {
                return (int)id;
            }
        }

        return (int)await ErpDb.LongAsync(connection, null, "SELECT `id` FROM `shop_orders_returns_statuses` ORDER BY `id` ASC LIMIT 1", cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> StatusFlagIdAsync(DbConnection connection, string column, CancellationToken cancellationToken)
    {
        if (!SafeIdent(column))
        {
            return 0;
        }

        return (int)await ErpDb.LongAsync(
            connection,
            null,
            "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `" + column + "` = 1 ORDER BY `id` ASC LIMIT 1",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<ManufacturerRow>> ManufacturerRowsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<ManufacturerRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `id`, `name` FROM `shop_docpart_manufacturers` ORDER BY `name` ASC";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ManufacturerRow(reader.GetInt32(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1)));
        }

        return rows;
    }

    private static async Task<List<SynonymRow>> SynonymRowsAsync(DbConnection connection, int manufacturerId, CancellationToken cancellationToken)
    {
        var rows = new List<SynonymRow>();
        if (manufacturerId <= 0)
        {
            return rows;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `id`, `synonym`, `manufacturer_id` FROM `shop_docpart_manufacturers_synonyms` WHERE `manufacturer_id` = ? ORDER BY `synonym` ASC");
        ErpDb.AddParameters(command, manufacturerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new SynonymRow(reader.GetInt32(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1), reader.GetInt32(2)));
        }

        return rows;
    }

    private static async Task<ManufacturerBody> AddManufacturerAsync(DbConnection connection, string name, CancellationToken cancellationToken)
    {
        if (name.Length == 0)
        {
            return new ManufacturerBody(false, "empty_name", [], []);
        }

        var existing = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT `id` FROM `shop_docpart_manufacturers` WHERE `name` = ? LIMIT 1"), cancellationToken, name).ConfigureAwait(false);
        if (existing > 0)
        {
            return new ManufacturerBody(false, "duplicate", [], []);
        }

        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("INSERT INTO `shop_docpart_manufacturers` (`name`) VALUES (?)"), cancellationToken, name).ConfigureAwait(false);
        var id = (int)await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return new ManufacturerBody(true, null, [], [], id);
    }

    private static async Task<ManufacturerBody> SaveManufacturerAsync(DbConnection connection, int id, string name, CancellationToken cancellationToken)
    {
        if (id <= 0 || name.Length == 0)
        {
            return new ManufacturerBody(false, "bad_input", [], []);
        }

        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_docpart_manufacturers` WHERE `name` = ? AND `id` <> ? LIMIT 1"),
            cancellationToken,
            name,
            id).ConfigureAwait(false);
        if (existing > 0)
        {
            return new ManufacturerBody(false, "duplicate", [], []);
        }

        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `shop_docpart_manufacturers` SET `name` = ? WHERE `id` = ?"), cancellationToken, name, id).ConfigureAwait(false);
        return new ManufacturerBody(true, null, [], []);
    }

    private static async Task<ManufacturerBody> DeleteManufacturerAsync(DbConnection connection, int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return new ManufacturerBody(false, "bad_id", [], []);
        }

        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("DELETE FROM `shop_docpart_manufacturers_synonyms` WHERE `manufacturer_id` = ?"), cancellationToken, id).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("DELETE FROM `shop_docpart_manufacturers` WHERE `id` = ?"), cancellationToken, id).ConfigureAwait(false);
        return new ManufacturerBody(true, null, [], []);
    }

    private static async Task<ManufacturerBody> AddSynonymAsync(DbConnection connection, int manufacturerId, string name, CancellationToken cancellationToken)
    {
        if (manufacturerId <= 0 || name.Length == 0)
        {
            return new ManufacturerBody(false, "bad_input", [], []);
        }

        var manufacturer = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT `id` FROM `shop_docpart_manufacturers` WHERE `id` = ? LIMIT 1"), cancellationToken, manufacturerId).ConfigureAwait(false);
        if (manufacturer <= 0)
        {
            return new ManufacturerBody(false, "manufacturer_missing", [], []);
        }

        var nameHit = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT `id` FROM `shop_docpart_manufacturers` WHERE `name` = ? LIMIT 1"), cancellationToken, name).ConfigureAwait(false);
        if (nameHit > 0)
        {
            return new ManufacturerBody(false, "duplicate_manufacturer_name", [], []);
        }

        var synonymHit = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT `id` FROM `shop_docpart_manufacturers_synonyms` WHERE `synonym` = ? LIMIT 1"), cancellationToken, name).ConfigureAwait(false);
        if (synonymHit > 0)
        {
            return new ManufacturerBody(false, "duplicate_synonym", [], []);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `shop_docpart_manufacturers_synonyms` (`manufacturer_id`, `synonym`) VALUES (?, ?)"),
            cancellationToken,
            manufacturerId,
            name).ConfigureAwait(false);
        var id = (int)await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return new ManufacturerBody(true, null, [], [], id);
    }

    private static async Task<ManufacturerBody> SaveSynonymAsync(DbConnection connection, int id, string name, CancellationToken cancellationToken)
    {
        if (id <= 0 || name.Length == 0)
        {
            return new ManufacturerBody(false, "bad_input", [], []);
        }

        var nameHit = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT `id` FROM `shop_docpart_manufacturers` WHERE `name` = ? LIMIT 1"), cancellationToken, name).ConfigureAwait(false);
        if (nameHit > 0)
        {
            return new ManufacturerBody(false, "duplicate_manufacturer_name", [], []);
        }

        var synonymHit = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_docpart_manufacturers_synonyms` WHERE `synonym` = ? AND `id` <> ? LIMIT 1"),
            cancellationToken,
            name,
            id).ConfigureAwait(false);
        if (synonymHit > 0)
        {
            return new ManufacturerBody(false, "duplicate_synonym", [], []);
        }

        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `shop_docpart_manufacturers_synonyms` SET `synonym` = ? WHERE `id` = ?"), cancellationToken, name, id).ConfigureAwait(false);
        return new ManufacturerBody(true, null, [], []);
    }

    private static async Task<ManufacturerBody> DeleteSynonymAsync(DbConnection connection, int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return new ManufacturerBody(false, "bad_id", [], []);
        }

        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("DELETE FROM `shop_docpart_manufacturers_synonyms` WHERE `id` = ?"), cancellationToken, id).ConfigureAwait(false);
        return new ManufacturerBody(true, null, [], []);
    }

    private static async Task<string> GroupTableAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var rows = new List<(int Id, string Name, string Storages)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `id`, `name`, `storages` FROM `shop_storages_groups` ORDER BY `order`, `id`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add((reader.GetInt32(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1), reader.IsDBNull(2) ? string.Empty : reader.GetString(2)));
            }
        }

        var names = new Dictionary<int, string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `id`, `name` FROM `shop_storages`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                names[reader.GetInt32(0)] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            }
        }

        var html = new StringBuilder();
        html.Append("<div class=\"col-lg-12\"><div class=\"hpanel epc-sg-groups-panel\">");
        html.Append("<div class=\"panel-heading hbuilt\"><i class=\"fa fa-object-group\"></i> Warehouse groups</div><div class=\"panel-body\">");
        if (rows.Count == 0)
        {
            html.Append("<div class=\"alert alert-info\" style=\"margin:0\"><strong>No warehouse groups yet.</strong> Create a group on the right to batch async API warehouses during price search. Docpart price lists and own-warehouse (types 1 / 2 / 6) are handled automatically and do not need a group.</div>");
        }
        else
        {
            html.Append("<div class=\"table-responsive\"><table class=\"table table-striped\" style=\"margin:0\"><thead><tr><th style=\"width:70px\">ID</th><th>Name</th><th>Warehouses</th><th style=\"width:90px\"></th></tr></thead><tbody>");
            foreach (var row in rows)
            {
                var labels = new List<string>();
                foreach (var part in row.Storages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sid) || sid <= 0)
                    {
                        continue;
                    }

                    var label = names.TryGetValue(sid, out var stored) ? stored : "#" + sid.ToString(CultureInfo.InvariantCulture);
                    labels.Add(HtmlQuote(label) + " <span class=\"text-muted\">(" + sid.ToString(CultureInfo.InvariantCulture) + ")</span>");
                }

                html.Append("<tr><td>").Append(row.Id.ToString(CultureInfo.InvariantCulture)).Append("</td><td><strong>")
                    .Append(HtmlQuote(row.Name)).Append("</strong></td><td>")
                    .Append(labels.Count == 0 ? "<span class=\"text-muted\">—</span>" : string.Join(", ", labels))
                    .Append("</td><td class=\"text-right\"><a href=\"javascript:void(0);\" class=\"btn btn-xs btn-danger\" onclick=\"del(")
                    .Append(row.Id.ToString(CultureInfo.InvariantCulture)).Append(");\"><i class=\"fa fa-trash\"></i> Delete</a></td></tr>");
            }

            html.Append("</tbody></table></div>");
        }

        html.Append("</div></div></div>");
        return html.ToString();
    }

    private static async Task<List<StorageChoice>> AvailableStoragesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var assigned = new HashSet<int>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `storages` FROM `shop_storages_groups`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var raw = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0)
                    {
                        assigned.Add(id);
                    }
                }
            }
        }

        var rows = new List<StorageChoice>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT `id`, `name`, `interface_type` FROM `shop_storages` WHERE `interface_type` NOT IN (1, 2, 6) ORDER BY `name`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = reader.GetInt32(0);
                if (assigned.Contains(id))
                {
                    continue;
                }

                rows.Add(new StorageChoice(id, false, reader.IsDBNull(1) ? string.Empty : reader.GetString(1), false, reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture)));
            }
        }

        return rows;
    }

    private static string NextStringKey(IReadOnlyDictionary<string, string> config)
    {
        var domain = config.TryGetValue("domain_path", out var configured) ? configured : string.Empty;
        var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(domain))).ToLowerInvariant();
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) + "_1_" + hash;
    }

    public static int PhpInt(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        var text = raw.Trim();
        var sign = 1;
        var index = 0;
        if (text[0] == '-')
        {
            sign = -1;
            index = 1;
        }

        var value = 0;
        var any = false;
        for (; index < text.Length && text[index] is >= '0' and <= '9'; index++)
        {
            any = true;
            value = (value * 10) + (text[index] - '0');
        }

        return any ? sign * value : 0;
    }

    private static bool SafeIdent(string value)
        => Regex.IsMatch(value, "^[A-Za-z0-9_]+$");

    private static string CleanManufacturer(string? raw)
    {
        var name = WebUtility.HtmlDecode(WebUtility.UrlDecode(raw ?? string.Empty));
        name = Regex.Replace(name, @"[\u0000-\u001F\u007F]", string.Empty).Trim();
        return name.Length <= 255 ? name : name[..255].Trim();
    }

    private static string JsonString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static int RequestInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return 0;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetInt32(out var number) ? number : 0,
            JsonValueKind.String => PhpInt(value.GetString()),
            _ => 0
        };
    }

    private static List<int> JsonInts(JsonElement element, string name)
    {
        var ids = new List<int>();
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return ids;
        }

        foreach (var item in value.EnumerateArray())
        {
            var id = item.ValueKind switch
            {
                JsonValueKind.Number => item.TryGetInt32(out var number) ? number : 0,
                JsonValueKind.String => PhpInt(item.GetString()),
                _ => 0
            };
            if (id > 0 && !ids.Contains(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private static string HtmlQuote(string value)
        => HtmlCompat(value).Replace("'", "&#039;", StringComparison.Ordinal);

    public sealed record DescriptionSaved(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("str_key")] string StrKey,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("new_value")] string NewValue);

    public sealed record UsedFoundSaved(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("str_key")] string StrKey,
        [property: JsonPropertyName("used_found")] int UsedFound);

    public sealed record CreatedString(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("str")] JsonObject Str);

    public sealed record TranslationSaved(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("str_key")] string StrKey,
        [property: JsonPropertyName("lang_code")] string LangCode,
        [property: JsonPropertyName("value")] string Value,
        [property: JsonPropertyName("message")] string Message);

    public sealed record AliasBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("result_code")] string ResultCode);

    public sealed record ManufacturerBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("message")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Message,
        [property: JsonPropertyName("manufacturers")] IReadOnlyList<ManufacturerRow> Manufacturers,
        [property: JsonPropertyName("synonyms")] IReadOnlyList<SynonymRow> Synonyms,
        [property: JsonPropertyName("id")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        int? Id = null);

    public sealed record ManufacturerRow(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string Name);

    public sealed record SynonymRow(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("synonym")] string Synonym,
        [property: JsonPropertyName("manufacturer_id")] int ManufacturerId);

    public sealed record StorageListBody(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("storages_list")] IReadOnlyList<StorageChoice> Storages);

    public sealed record StorageChoice(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("checked")] bool Checked,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("selected")] bool Selected,
        [property: JsonPropertyName("interface_type")] int InterfaceType);

    public sealed record ReturnClosed(
        [property: JsonPropertyName("status")] bool Status,
        [property: JsonPropertyName("approved_sum")] decimal ApprovedSum);

    public sealed record ToggleBody(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("message")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Message,
        [property: JsonPropertyName("storefront_disabled")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        int? StorefrontDisabled,
        [property: JsonPropertyName("entity_name")]
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? EntityName);
}
