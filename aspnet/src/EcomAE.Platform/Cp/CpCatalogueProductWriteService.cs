using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>product.php</c> create/edit twin.
/// Image upload and manual line-list item create stay on the Classic twin.
/// </summary>
public interface ICpCatalogueProductWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(CpCatalogueProductSaveRequest request, CancellationToken cancellationToken = default);
}

public sealed record CpCatalogueProductSaveRequest(
    string? Action = null,
    long ProductId = 0,
    long CategoryId = 0,
    string? Caption = null,
    string? CaptionLangStrId = null,
    string? Alias = null,
    string? TitleTag = null,
    string? TitleTagLangStrId = null,
    string? DescriptionTag = null,
    string? DescriptionTagLangStrId = null,
    string? KeywordsTag = null,
    string? KeywordsTagLangStrId = null,
    string? RobotsTag = null,
    int PublishedFlag = 1,
    string? ProductText = null,
    string? ProductTextLangStrId = null,
    string? PropertiesJson = null,
    string? StickersJson = null,
    string? RelatedJson = null,
    string? LangCode = null,
    string? DomainPath = null);

public sealed record CpCatalogueProductProperty(
    long PropertyId,
    int PropertyTypeId,
    string Caption,
    string Value,
    string ValueLangStrId,
    IReadOnlyList<long> OptionIds);

public sealed record CpCatalogueProductSticker(
    long Id,
    bool IsNew,
    string Value,
    string ValueLangStrId,
    string ColorText,
    string ColorBackground,
    string Href,
    string ClassCss,
    string Description,
    string DescriptionLangStrId);

public sealed class CpCatalogueProductWriteService : ICpCatalogueProductWriteService
{
    public const string LangCreating = "PRODUCT CREATING";
    public const string LangEditing = "PRODUCT EDITING";

    private static readonly Regex ArticleClean = new("[^A-Za-z0-9А-Яа-яёЁ]", RegexOptions.Compiled);

    private readonly IErpWriteConnectionFactory _connections;
    private int _createdStrings;

    public CpCatalogueProductWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        CpCatalogueProductSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var action = NormalizeAction(request.Action, request.ProductId);
        if (action is not ("create" or "edit"))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Action must be create or edit.");
        }

        if (action == "create" && request.CategoryId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Create needs a positive category_id.");
        }

        if (action == "edit" && request.ProductId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Edit needs a positive product_id.");
        }

        var properties = ParseProperties(request.PropertiesJson);
        if (properties.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", properties.Error);
        }

        var stickers = ParseStickers(request.StickersJson);
        if (stickers.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", stickers.Error);
        }

        var related = ParseRelated(request.RelatedJson);
        if (related.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", related.Error);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var lang = NormalizeLang(request.LangCode);
        var langDescription = action == "create" ? LangCreating : LangEditing;
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var caption = await RequireTranslationAsync(
                connection, transaction, request.CaptionLangStrId,
                HtmlEncode(SanitizePlain(request.Caption)),
                lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
            var title = await RequireTranslationAsync(
                connection, transaction, request.TitleTagLangStrId,
                HtmlEncode(SanitizePlain(request.TitleTag)),
                lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
            var description = await RequireTranslationAsync(
                connection, transaction, request.DescriptionTagLangStrId,
                HtmlEncode(SanitizePlain(request.DescriptionTag)),
                lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
            var keywords = await RequireTranslationAsync(
                connection, transaction, request.KeywordsTagLangStrId,
                HtmlEncode(SanitizePlain(request.KeywordsTag)),
                lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
            var alias = SanitizePlain(request.Alias);
            var robots = HtmlEncode(request.RobotsTag);

            long productId;
            long categoryId;
            if (action == "create")
            {
                categoryId = request.CategoryId;
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `shop_catalogue_products` (`category_id`, `caption`, `alias`, `title_tag`, `description_tag`, `keywords_tag`, `robots_tag`, `published_flag`) VALUES (?,?,?,?,?,?,?,?)"),
                    cancellationToken,
                    categoryId,
                    caption,
                    alias,
                    title,
                    description,
                    keywords,
                    robots,
                    request.PublishedFlag).ConfigureAwait(false);
                productId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                productId = request.ProductId;
                var found = await ErpDb.LongAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("SELECT COUNT(*) FROM `shop_catalogue_products` WHERE `id` = ?"),
                    cancellationToken,
                    productId).ConfigureAwait(false);
                if (found == 0)
                {
                    throw new ErpWriteException("Product not found.");
                }

                categoryId = request.CategoryId > 0
                    ? request.CategoryId
                    : await ErpDb.LongAsync(
                        connection,
                        transaction,
                        ErpDb.Positional("SELECT `category_id` FROM `shop_catalogue_products` WHERE `id` = ?"),
                        cancellationToken,
                        productId).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "UPDATE `shop_catalogue_products` SET `caption` = ?, `alias` = ?, `title_tag` = ?, `description_tag` = ?, `keywords_tag` = ?, `robots_tag` = ?, `published_flag` = ? WHERE `id` = ?"),
                    cancellationToken,
                    caption,
                    alias,
                    title,
                    description,
                    keywords,
                    robots,
                    request.PublishedFlag,
                    productId).ConfigureAwait(false);

                foreach (var table in new[]
                {
                    "shop_properties_values_int",
                    "shop_properties_values_float",
                    "shop_properties_values_text",
                    "shop_properties_values_bool",
                    "shop_properties_values_list",
                    "shop_properties_values_tree_list"
                })
                {
                    await TryExecuteAsync(
                        connection, transaction,
                        "DELETE FROM `" + table + "` WHERE `product_id` = ?",
                        cancellationToken, productId).ConfigureAwait(false);
                }
            }

            await WritePropertiesAsync(
                connection, transaction, productId, categoryId, properties.Properties,
                lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
            await WriteTextAsync(
                connection, transaction, productId, request.ProductText, request.ProductTextLangStrId,
                lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
            await WriteStickersAsync(
                connection, transaction, productId, action == "create", stickers.Stickers,
                lang, request.DomainPath, langDescription, cancellationToken).ConfigureAwait(false);
            await WriteRelatedAsync(connection, transaction, productId, related.Ids, cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok(action == "create" ? "Product created." : "Product saved.", productId);
        }
        catch (ErpWriteException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not save product.");
        }
    }

    public static string NormalizeAction(string? raw, long productId = 0)
    {
        var action = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return action switch
        {
            "create" => "create",
            "edit" or "update" => "edit",
            "save" or "save_action" => productId > 0 ? "edit" : "create",
            _ => action
        };
    }

    public static string HtmlEncode(string? raw)
        => WebUtility.HtmlEncode((raw ?? string.Empty).Trim());

    public static string SanitizePlain(string? raw)
    {
        var value = (raw ?? string.Empty).Replace("'", "", StringComparison.Ordinal)
            .Replace("\"", "", StringComparison.Ordinal)
            .Replace("`", "", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal)
            .Replace("\t", "", StringComparison.Ordinal);
        return value.Trim();
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

    public static (IReadOnlyList<CpCatalogueProductProperty> Properties, string? Error) ParseProperties(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return ([], null);
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ([], "properties_objects must be a JSON array.");
            }

            var properties = new List<CpCatalogueProductProperty>();
            foreach (var node in document.RootElement.EnumerateArray())
            {
                if (node.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var type = (int)ReadLong(node, "property_type_id", "propertyTypeId");
                if (type is < 1 or > 6)
                {
                    return ([], "Each property_type_id must be 1-6.");
                }

                var propertyId = ReadLong(node, "property_id", "propertyId");
                if (propertyId <= 0)
                {
                    return ([], "Each property needs a positive property_id.");
                }

                var options = new List<long>();
                var value = string.Empty;
                if (node.TryGetProperty("value", out var valueNode) && valueNode.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in valueNode.EnumerateArray())
                    {
                        var id = item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var n)
                            ? n
                            : long.TryParse(item.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                                ? parsed
                                : 0;
                        if (id > 0)
                        {
                            options.Add(id);
                        }
                    }
                }
                else
                {
                    value = ReadString(node, "value");
                }

                properties.Add(new CpCatalogueProductProperty(
                    propertyId,
                    type,
                    ReadString(node, "caption"),
                    value,
                    ReadString(node, "value_lang_str_id", "valueLangStrId"),
                    options));
                if (properties.Count > 80)
                {
                    return ([], "properties_objects has too many entries.");
                }
            }

            return (properties, null);
        }
        catch (JsonException)
        {
            return ([], "properties_objects is not valid JSON.");
        }
    }

    public static (IReadOnlyList<CpCatalogueProductSticker> Stickers, string? Error) ParseStickers(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return ([], null);
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ([], "product_stickers must be a JSON array.");
            }

            var stickers = new List<CpCatalogueProductSticker>();
            foreach (var node in document.RootElement.EnumerateArray())
            {
                if (node.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                stickers.Add(new CpCatalogueProductSticker(
                    ReadLong(node, "id"),
                    ReadFlag(node, "is_new", "isNew"),
                    ReadString(node, "value"),
                    ReadString(node, "value_lang_str_id", "valueLangStrId"),
                    ReadString(node, "color_text", "colorText"),
                    ReadString(node, "color_background", "colorBackground"),
                    ReadString(node, "href"),
                    ReadString(node, "class_css", "classCss"),
                    ReadString(node, "description"),
                    ReadString(node, "description_lang_str_id", "descriptionLangStrId")));
                if (stickers.Count > 40)
                {
                    return ([], "product_stickers has too many entries.");
                }
            }

            return (stickers, null);
        }
        catch (JsonException)
        {
            return ([], "product_stickers is not valid JSON.");
        }
    }

    public static (IReadOnlyList<long> Ids, string? Error) ParseRelated(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return ([], null);
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return ([], "product_related must be a JSON array.");
            }

            var ids = new List<long>();
            foreach (var node in document.RootElement.EnumerateArray())
            {
                var id = node.ValueKind == JsonValueKind.Object
                    ? ReadLong(node, "product_id", "productId")
                    : node.ValueKind == JsonValueKind.Number && node.TryGetInt64(out var n) ? n : 0;
                if (id > 0)
                {
                    ids.Add(id);
                }

                if (ids.Count > 80)
                {
                    return ([], "product_related has too many entries.");
                }
            }

            return (ids, null);
        }
        catch (JsonException)
        {
            return ([], "product_related is not valid JSON.");
        }
    }

    private async Task WritePropertiesAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        long productId,
        long categoryId,
        IReadOnlyList<CpCatalogueProductProperty> properties,
        string lang,
        string? domainPath,
        string langDescription,
        CancellationToken cancellationToken)
    {
        foreach (var property in properties)
        {
            switch (property.PropertyTypeId)
            {
                case 1:
                case 2:
                case 4:
                    await InsertPropertyAsync(
                        connection, transaction, TableFor(property.PropertyTypeId),
                        productId, property.PropertyId, categoryId, property.Value, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case 3:
                    var text = property.Value;
                    if (IsArticleCaption(property.Caption))
                    {
                        text = ArticleClean.Replace(text, string.Empty).ToUpperInvariant();
                    }

                    var textKey = await RequireTranslationAsync(
                        connection, transaction, property.ValueLangStrId, HtmlEncode(text),
                        lang, domainPath, langDescription, cancellationToken).ConfigureAwait(false);
                    await InsertPropertyAsync(
                        connection, transaction, "shop_properties_values_text",
                        productId, property.PropertyId, categoryId, textKey, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case 5:
                case 6:
                    var table = property.PropertyTypeId == 5
                        ? "shop_properties_values_list"
                        : "shop_properties_values_tree_list";
                    foreach (var optionId in property.OptionIds)
                    {
                        await InsertPropertyAsync(
                            connection, transaction, table,
                            productId, property.PropertyId, categoryId, optionId.ToString(CultureInfo.InvariantCulture),
                            cancellationToken).ConfigureAwait(false);
                    }

                    break;
            }
        }
    }

    private async Task WriteTextAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        long productId,
        string? productText,
        string? langStrId,
        string lang,
        string? domainPath,
        string langDescription,
        CancellationToken cancellationToken)
    {
        var content = await RequireTranslationAsync(
            connection, transaction, langStrId, StripPhp(productText ?? string.Empty),
            lang, domainPath, langDescription, cancellationToken).ConfigureAwait(false);
        var found = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_products_text` WHERE `product_id` = ?"),
            cancellationToken,
            productId).ConfigureAwait(false);
        if (found > 0)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `shop_products_text` SET `content` = ? WHERE `product_id` = ?"),
                cancellationToken,
                content,
                productId).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `shop_products_text` (`product_id`, `content`) VALUES (?,?)"),
                cancellationToken,
                productId,
                content).ConfigureAwait(false);
        }
    }

    private async Task WriteStickersAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        long productId,
        bool creating,
        IReadOnlyList<CpCatalogueProductSticker> stickers,
        string lang,
        string? domainPath,
        string langDescription,
        CancellationToken cancellationToken)
    {
        if (!creating)
        {
            var keep = stickers.Where(s => !s.IsNew && s.Id > 0).Select(s => s.Id).ToArray();
            if (keep.Length == 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("DELETE FROM `shop_products_stickers` WHERE `product_id` = ?"),
                    cancellationToken,
                    productId).ConfigureAwait(false);
            }
            else
            {
                var placeholders = string.Join(",", keep.Select(_ => "?"));
                var args = new object?[keep.Length + 1];
                args[0] = productId;
                for (var i = 0; i < keep.Length; i++)
                {
                    args[i + 1] = keep[i];
                }

                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("DELETE FROM `shop_products_stickers` WHERE `product_id` = ? AND `id` NOT IN (" + placeholders + ")"),
                    cancellationToken,
                    args).ConfigureAwait(false);
            }
        }

        var order = 1;
        foreach (var sticker in stickers)
        {
            var insertNew = creating || sticker.IsNew || sticker.Id <= 0;
            var value = await RequireTranslationAsync(
                connection, transaction, insertNew ? null : sticker.ValueLangStrId, HtmlEncode(sticker.Value),
                lang, domainPath, langDescription, cancellationToken).ConfigureAwait(false);
            var description = await RequireTranslationAsync(
                connection, transaction, insertNew ? null : sticker.DescriptionLangStrId, HtmlEncode(sticker.Description),
                lang, domainPath, langDescription, cancellationToken).ConfigureAwait(false);
            if (insertNew)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `shop_products_stickers` (`product_id`, `value`, `color_text`, `color_background`, `href`, `class_css`, `description`, `order`) VALUES (?,?,?,?,?,?,?,?)"),
                    cancellationToken,
                    productId,
                    value,
                    HtmlEncode(sticker.ColorText),
                    HtmlEncode(sticker.ColorBackground),
                    HtmlEncode(sticker.Href),
                    HtmlEncode(sticker.ClassCss),
                    description,
                    order).ConfigureAwait(false);
            }
            else
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "UPDATE `shop_products_stickers` SET `value` = ?, `color_text`=?, `color_background`=?, `href`=?, `class_css`=?, `description`=?, `order`=? WHERE `id` = ?"),
                    cancellationToken,
                    value,
                    HtmlEncode(sticker.ColorText),
                    HtmlEncode(sticker.ColorBackground),
                    HtmlEncode(sticker.Href),
                    HtmlEncode(sticker.ClassCss),
                    description,
                    order,
                    sticker.Id).ConfigureAwait(false);
            }

            order++;
        }
    }

    private static async Task WriteRelatedAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        long productId,
        IReadOnlyList<long> relatedIds,
        CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("DELETE FROM `shop_related_products` WHERE `product_id` = ?"),
            cancellationToken,
            productId).ConfigureAwait(false);
        var order = 1;
        foreach (var relatedId in relatedIds)
        {
            if (relatedId == productId)
            {
                continue;
            }

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("INSERT INTO `shop_related_products` (`product_id`, `product_id_related`, `order`) VALUES (?,?,?)"),
                cancellationToken,
                productId,
                relatedId,
                order).ConfigureAwait(false);
            order++;
        }
    }

    private static async Task InsertPropertyAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string table,
        long productId,
        long propertyId,
        long categoryId,
        string? value,
        CancellationToken cancellationToken)
        => await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("INSERT INTO `" + table + "` (`product_id`, `property_id`, `category_id`, `value`) VALUES (?, ?, ?, ?)"),
            cancellationToken,
            productId,
            propertyId,
            categoryId,
            value).ConfigureAwait(false);

    private static string TableFor(int type)
        => type switch
        {
            1 => "shop_properties_values_int",
            2 => "shop_properties_values_float",
            4 => "shop_properties_values_bool",
            _ => "shop_properties_values_text"
        };

    private static bool IsArticleCaption(string? caption)
    {
        var value = (caption ?? string.Empty).Trim().ToLowerInvariant();
        return value is "article" or "артикул";
    }

    private static async Task TryExecuteAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params object?[] parameters)
    {
        try
        {
            await ErpDb.ExecuteAsync(connection, transaction, ErpDb.Positional(sql), cancellationToken, parameters)
                .ConfigureAwait(false);
        }
        catch (System.Data.Common.DbException)
        {
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

        throw new ErpWriteException("Could not allocate a product translation key.");
    }

    private static string NormalizeLang(string? langCode)
    {
        var lang = (langCode ?? string.Empty).Trim().ToLowerInvariant();
        return lang.Length is < 2 or > 16 ? "en" : lang;
    }

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
