using System.Globalization;
using System.Net;
using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>catalogue_editor.php</c> <c>save_tree</c> twin.
/// Drag-tree UX, category image upload, template blob-to-disk, and image unlink stay Classic.
/// </summary>
public interface ICpCatalogueEditorWriteService
{
    Task<ErpSimpleWriteResult> SaveTreeAsync(CpCatalogueEditorSaveRequest request, CancellationToken cancellationToken = default);
}

public sealed record CpCatalogueEditorSaveRequest(
    string? TreeJson = null,
    string? LangCode = null,
    string? DomainPath = null);

public sealed record CpCatalogueEditorProperty(
    long Id,
    bool JustCreated,
    long CategoryId,
    int PropertyTypeId,
    string Value,
    string ValueLangStrId,
    long ListId,
    int ForSimilar,
    int IsOption);

public sealed record CpCatalogueEditorCategory(
    long Id,
    string Alias,
    string Url,
    int Count,
    int Level,
    long Parent,
    string Value,
    string ValueLangStrId,
    string TitleTag,
    string TitleTagLangStrId,
    string DescriptionTag,
    string DescriptionTagLangStrId,
    string KeywordsTag,
    string KeywordsTagLangStrId,
    string RobotsTag,
    string ImportFormat,
    string ExportFormat,
    string Image,
    int PublishedFlag,
    int Order,
    IReadOnlyList<CpCatalogueEditorProperty> Properties);

public sealed class CpCatalogueEditorWriteService : ICpCatalogueEditorWriteService
{
    public const string LangDescription = "CATEGORIES TREE EDITING";

    private readonly IErpWriteConnectionFactory _connections;
    private int _createdStrings;

    public CpCatalogueEditorWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveTreeAsync(
        CpCatalogueEditorSaveRequest request,
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

        var lang = NormalizeLang(request.LangCode);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var category in parsed.Categories)
            {
                var value = await RequireTranslationAsync(
                    connection, transaction, category.ValueLangStrId, category.Value,
                    lang, request.DomainPath, cancellationToken).ConfigureAwait(false);
                var title = await RequireTranslationAsync(
                    connection, transaction, category.TitleTagLangStrId, category.TitleTag,
                    lang, request.DomainPath, cancellationToken).ConfigureAwait(false);
                var description = await RequireTranslationAsync(
                    connection, transaction, category.DescriptionTagLangStrId, category.DescriptionTag,
                    lang, request.DomainPath, cancellationToken).ConfigureAwait(false);
                var keywords = await RequireTranslationAsync(
                    connection, transaction, category.KeywordsTagLangStrId, category.KeywordsTag,
                    lang, request.DomainPath, cancellationToken).ConfigureAwait(false);

                var exists = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT COUNT(*) FROM `shop_catalogue_categories` WHERE `id` = ?"),
                    cancellationToken,
                    category.Id).ConfigureAwait(false) > 0;

                if (exists)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            "UPDATE `shop_catalogue_categories` SET `alias`=?, `url`=?, `count`=?, `level`=?, `value`=?, `parent`=?, `title_tag`=?, `description_tag`=?, `keywords_tag`=?, `robots_tag`=?, `import_format`=?, `export_format`=?, `order` = ?, `published_flag` = ?, `image` = ? WHERE `id`=?"),
                        cancellationToken,
                        category.Alias,
                        category.Url,
                        category.Count,
                        category.Level,
                        value,
                        category.Parent,
                        title,
                        description,
                        keywords,
                        category.RobotsTag,
                        category.ImportFormat,
                        category.ExportFormat,
                        category.Order,
                        category.PublishedFlag,
                        category.Image,
                        category.Id).ConfigureAwait(false);

                    var existingProperties = await LoadPropertyIdsAsync(
                        connection, transaction, category.Id, cancellationToken).ConfigureAwait(false);
                    var keepProperties = category.Properties
                        .Where(p => !p.JustCreated && p.Id > 0)
                        .Select(p => p.Id)
                        .ToHashSet();
                    foreach (var propertyId in existingProperties)
                    {
                        if (!keepProperties.Contains(propertyId))
                        {
                            await ErpDb.ExecuteAsync(
                                connection,
                                transaction,
                                ErpDb.Positional("DELETE FROM `shop_categories_properties_map` WHERE `id` = ?"),
                                cancellationToken,
                                propertyId).ConfigureAwait(false);
                        }
                    }
                }
                else
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            "INSERT INTO `shop_catalogue_categories` (`id`, `alias`, `url`,`count`, `level`, `value`, `parent`, `title_tag`, `description_tag`, `keywords_tag`, `robots_tag`, `import_format`, `export_format`, `order`, `published_flag`, `image`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
                        cancellationToken,
                        category.Id,
                        category.Alias,
                        category.Url,
                        category.Count,
                        category.Level,
                        value,
                        category.Parent,
                        title,
                        description,
                        keywords,
                        category.RobotsTag,
                        category.ImportFormat,
                        category.ExportFormat,
                        category.Order,
                        category.PublishedFlag,
                        category.Image).ConfigureAwait(false);
                }

                var propertyOrder = 1;
                foreach (var property in category.Properties)
                {
                    var propertyValue = await RequireTranslationAsync(
                        connection, transaction, property.JustCreated ? null : property.ValueLangStrId,
                        HtmlEncode(property.Value),
                        lang, request.DomainPath, cancellationToken).ConfigureAwait(false);
                    var propertyCategoryId = property.CategoryId > 0 ? property.CategoryId : category.Id;
                    if (property.JustCreated)
                    {
                        await ErpDb.ExecuteAsync(
                            connection,
                            transaction,
                            ErpDb.Positional(
                                "INSERT INTO `shop_categories_properties_map` (`category_id`, `property_type_id`, `value`, `list_id`, `order`, `for_similar`, `is_option`) VALUES (?, ?, ?, ?, ?, ?, ?)"),
                            cancellationToken,
                            propertyCategoryId,
                            property.PropertyTypeId,
                            propertyValue,
                            property.ListId,
                            propertyOrder,
                            property.ForSimilar,
                            property.IsOption).ConfigureAwait(false);
                    }
                    else if (property.Id > 0)
                    {
                        await ErpDb.ExecuteAsync(
                            connection,
                            transaction,
                            ErpDb.Positional(
                                "UPDATE `shop_categories_properties_map` SET `value` = ?, `list_id` = ?, `order` = ?, `for_similar` = ?, `is_option` = ?  WHERE `id` = ?"),
                            cancellationToken,
                            propertyValue,
                            property.ListId,
                            propertyOrder,
                            property.ForSimilar,
                            property.IsOption,
                            property.Id).ConfigureAwait(false);
                    }

                    propertyOrder++;
                }
            }

            var keep = parsed.Categories.Select(c => c.Id).ToHashSet();
            var existing = await LoadCategoryIdsAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            var deleted = existing.Where(id => !keep.Contains(id)).ToArray();
            foreach (var id in deleted)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("DELETE FROM `shop_catalogue_categories` WHERE `id` = ?"),
                    cancellationToken,
                    id).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("DELETE FROM `shop_categories_properties_map` WHERE `category_id` = ?"),
                    cancellationToken,
                    id).ConfigureAwait(false);
            }

            if (deleted.Length > 0)
            {
                await DeleteProductsInCategoriesAsync(connection, transaction, deleted, cancellationToken)
                    .ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Catalogue tree saved.", parsed.Categories[0].Id);
        }
        catch (ErpWriteException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not save catalogue tree.");
        }
    }

    public static (IReadOnlyList<CpCatalogueEditorCategory> Categories, string? Error) ParseTree(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return ([], "tree_json is required.");
        }

        if (text.Length > 400_000)
        {
            return ([], "tree_json is too large.");
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var categories = new List<CpCatalogueEditorCategory>();
            var error = Walk(document.RootElement, categories);
            if (error is not null)
            {
                return ([], error);
            }

            return categories.Count == 0
                ? ([], "At least one catalogue category is required.")
                : categories.Count > 500
                    ? ([], "tree_json has too many categories.")
                    : (categories, null);
        }
        catch (JsonException)
        {
            return ([], "tree_json is not valid JSON.");
        }
    }

    public static string HtmlEncode(string? raw)
        => WebUtility.HtmlEncode(Sanitize(raw));

    public static string NormalizeAction(string? raw)
    {
        var action = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return action switch
        {
            "save" or "save_tree" or "save_action" => "save",
            _ => action
        };
    }

    private static string? Walk(JsonElement element, List<CpCatalogueEditorCategory> categories)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var error = Walk(item, categories);
                if (error is not null)
                {
                    return error;
                }
            }

            return null;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var id = ReadLong(element, "id");
        if (id <= 0)
        {
            return "Each catalogue category needs a positive id.";
        }

        var data = GetProperty(element, "data");
        var childCount = data.ValueKind == JsonValueKind.Array
            ? data.GetArrayLength()
            : (int)ReadLong(element, "$count", "count");
        var value = Sanitize(ReadString(element, "value"));
        if (value.Length == 0)
        {
            value = "Category #" + id.ToString(CultureInfo.InvariantCulture);
        }

        var properties = ParseProperties(element, id);
        if (properties.Error is not null)
        {
            return properties.Error;
        }

        categories.Add(new CpCatalogueEditorCategory(
            id,
            HtmlEncode(ReadString(element, "alias")),
            HtmlEncode(ReadString(element, "url")),
            childCount,
            (int)Math.Max(1, ReadLong(element, "$level", "level")),
            ReadLong(element, "$parent", "parent"),
            HtmlEncode(value),
            ReadString(element, "value_lang_str_id", "valueLangStrId"),
            HtmlEncode(ReadString(element, "title_tag", "titleTag")),
            ReadString(element, "title_tag_lang_str_id", "titleTagLangStrId"),
            HtmlEncode(ReadString(element, "description_tag", "descriptionTag")),
            ReadString(element, "description_tag_lang_str_id", "descriptionTagLangStrId"),
            HtmlEncode(ReadString(element, "keywords_tag", "keywordsTag")),
            ReadString(element, "keywords_tag_lang_str_id", "keywordsTagLangStrId"),
            HtmlEncode(ReadString(element, "robots_tag", "robotsTag")),
            Sanitize(ReadString(element, "import_format", "importFormat")),
            Sanitize(ReadString(element, "export_format", "exportFormat")),
            Sanitize(ReadString(element, "image")),
            PublishedFlag(element),
            categories.Count + 1,
            properties.Properties));

        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in data.EnumerateArray())
            {
                var error = Walk(child, categories);
                if (error is not null)
                {
                    return error;
                }
            }
        }

        return null;
    }

    private static (IReadOnlyList<CpCatalogueEditorProperty> Properties, string? Error) ParseProperties(
        JsonElement node,
        long categoryId)
    {
        if (!node.TryGetProperty("properties", out var properties) || properties.ValueKind == JsonValueKind.Null)
        {
            return ([], null);
        }

        if (properties.ValueKind != JsonValueKind.Array)
        {
            return ([], "Category properties must be a JSON array.");
        }

        var parsed = new List<CpCatalogueEditorProperty>();
        foreach (var property in properties.EnumerateArray())
        {
            if (property.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            parsed.Add(new CpCatalogueEditorProperty(
                ReadLong(property, "id"),
                ReadFlag(property, "just_created", "justCreated"),
                ReadLong(property, "category_id", "categoryId") is var propertyCategory && propertyCategory > 0
                    ? propertyCategory
                    : categoryId,
                (int)ReadLong(property, "property_type_id", "propertyTypeId"),
                ReadString(property, "value"),
                ReadString(property, "value_lang_str_id", "valueLangStrId"),
                ReadLong(property, "list_id", "listId"),
                ReadFlag(property, "for_similar", "forSimilar") ? 1 : 0,
                ReadFlag(property, "is_option", "isOption") ? 1 : 0));
            if (parsed.Count > 40)
            {
                return ([], "A category has too many properties.");
            }
        }

        return (parsed, null);
    }

    private async Task DeleteProductsInCategoriesAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        IReadOnlyList<long> categoryIds,
        CancellationToken cancellationToken)
    {
        List<long> productIds;
        try
        {
            productIds = await LoadProductIdsAsync(connection, transaction, categoryIds, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (System.Data.Common.DbException)
        {
            return;
        }

        if (productIds.Count == 0)
        {
            return;
        }

        string[] tables =
        [
            "shop_products_images",
            "shop_products_text",
            "shop_properties_values_int",
            "shop_properties_values_float",
            "shop_properties_values_text",
            "shop_properties_values_bool",
            "shop_properties_values_list",
            "shop_properties_values_tree_list",
            "shop_main_page_products",
            "shop_products_stickers",
            "shop_products_evaluations",
            "shop_storages_data"
        ];
        foreach (var table in tables)
        {
            await TryDeleteByProductIdAsync(connection, transaction, table, "product_id", productIds, cancellationToken)
                .ConfigureAwait(false);
        }

        await TryDeleteByProductIdAsync(
            connection, transaction, "shop_related_products", "product_id", productIds, cancellationToken)
            .ConfigureAwait(false);
        await TryDeleteByProductIdAsync(
            connection, transaction, "shop_related_products", "product_id_related", productIds, cancellationToken)
            .ConfigureAwait(false);
        await TryDeleteByProductIdAsync(
            connection, transaction, "shop_catalogue_products", "id", productIds, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task TryDeleteByProductIdAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string table,
        string column,
        IReadOnlyList<long> productIds,
        CancellationToken cancellationToken)
    {
        foreach (var id in productIds)
        {
            try
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("DELETE FROM `" + table + "` WHERE `" + column + "` = ?"),
                    cancellationToken,
                    id).ConfigureAwait(false);
            }
            catch (System.Data.Common.DbException)
            {
                return;
            }
        }
    }

    private static async Task<List<long>> LoadCategoryIdsAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT `id` FROM `shop_catalogue_categories`";
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
        long categoryId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional("SELECT `id` FROM `shop_categories_properties_map` WHERE `category_id` = ?");
        ErpDb.AddParameters(command, categoryId);
        var ids = new List<long>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ids.Add(reader.GetInt64(0));
        }

        return ids;
    }

    private static async Task<List<long>> LoadProductIdsAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        IReadOnlyList<long> categoryIds,
        CancellationToken cancellationToken)
    {
        var ids = new List<long>();
        foreach (var categoryId in categoryIds)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = ErpDb.Positional("SELECT `id` FROM `shop_catalogue_products` WHERE `category_id` = ?");
            ErpDb.AddParameters(command, categoryId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ids.Add(reader.GetInt64(0));
            }
        }

        return ids;
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
                LangDescription,
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

        throw new ErpWriteException("Could not allocate a catalogue-tree translation key.");
    }

    private static string NormalizeLang(string? langCode)
    {
        var lang = (langCode ?? string.Empty).Trim().ToLowerInvariant();
        return lang.Length is < 2 or > 16 ? "en" : lang;
    }

    private static string Sanitize(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        return value.Equals("null", StringComparison.OrdinalIgnoreCase) ? string.Empty : value;
    }

    private static JsonElement GetProperty(JsonElement node, string name)
        => node.TryGetProperty(name, out var value) ? value : default;

    private static long ReadLong(JsonElement node, params string[] names)
    {
        foreach (var name in names)
        {
            if (!node.TryGetProperty(name, out var value))
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
        }

        return 0;
    }

    private static string ReadString(JsonElement node, params string[] names)
    {
        foreach (var name in names)
        {
            if (!node.TryGetProperty(name, out var value))
            {
                continue;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
                _ => string.Empty
            };
        }

        return string.Empty;
    }

    private static int PublishedFlag(JsonElement node)
    {
        if (!node.TryGetProperty("published_flag", out _) && !node.TryGetProperty("publishedFlag", out _))
        {
            return 1;
        }

        return ReadFlag(node, "published_flag", "publishedFlag") ? 1 : 0;
    }

    private static bool ReadFlag(JsonElement node, params string[] names)
    {
        foreach (var name in names)
        {
            if (!node.TryGetProperty(name, out var value))
            {
                continue;
            }

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.Number => value.TryGetInt64(out var n) && n != 0,
                JsonValueKind.String => value.GetString() is "1" or "true" or "yes" or "on",
                _ => false
            };
        }

        return false;
    }
}
