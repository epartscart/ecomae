using System.Globalization;
using System.Net;
using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>main_page_products.php</c> save_action twin.
/// Drag-tree editor stays on the Classic twin.
/// </summary>
public interface ICpMainPageProductsWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(CpMainPageProductsSaveRequest request, CancellationToken cancellationToken = default);
}

public sealed record CpMainPageProductsSaveRequest(
    string? TreeJson = null,
    string? LangCode = null,
    string? DomainPath = null);

public sealed record CpMainPageProductSlot(long ProductId, int Order);

public sealed record CpMainPageProductGroup(
    string Value,
    string ValueLangStrId,
    int ShowCaption,
    int Active,
    IReadOnlyList<CpMainPageProductSlot> Products);

public sealed class CpMainPageProductsWriteService : ICpMainPageProductsWriteService
{
    public const string LangDescription = "MAIN PAGE PRODUCTS EDITING";

    private readonly IErpWriteConnectionFactory _connections;
    private int _createdStrings;

    public CpMainPageProductsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        CpMainPageProductsSaveRequest request,
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
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `shop_main_page_products`"),
                cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `shop_main_page_groups`"),
                cancellationToken).ConfigureAwait(false);

            var order = 1;
            long lastGroupId = 0;
            foreach (var group in parsed.Groups)
            {
                var captionKey = await RequireTranslationAsync(
                    connection,
                    transaction,
                    group.ValueLangStrId,
                    HtmlEncode(group.Value),
                    lang,
                    request.DomainPath,
                    LangDescription,
                    cancellationToken).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `shop_main_page_groups` (`caption`, `order`, `show_caption`, `active`) VALUES (?, ?, ?, ?)"),
                    cancellationToken,
                    captionKey,
                    order,
                    group.ShowCaption,
                    group.Active).ConfigureAwait(false);
                var groupId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                lastGroupId = groupId;
                foreach (var product in group.Products)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            "INSERT INTO `shop_main_page_products` (`product_id`, `order`, `group_id`) VALUES (?, ?, ?)"),
                        cancellationToken,
                        product.ProductId,
                        product.Order,
                        groupId).ConfigureAwait(false);
                }

                order++;
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Homepage slots saved.", lastGroupId);
        }
        catch (ErpWriteException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not save homepage slots.");
        }
    }

    public static (IReadOnlyList<CpMainPageProductGroup> Groups, string? Error) ParseTree(string? raw)
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

            var groups = new List<CpMainPageProductGroup>();
            foreach (var node in document.RootElement.EnumerateArray())
            {
                if (node.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var value = ReadString(node, "value");
                if (string.IsNullOrWhiteSpace(value))
                {
                    return ([], "Each homepage group needs a value.");
                }

                var products = new List<CpMainPageProductSlot>();
                if (node.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    var productOrder = 1;
                    foreach (var child in data.EnumerateArray())
                    {
                        if (child.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        var productId = ReadLong(child, "product_id");
                        if (productId <= 0)
                        {
                            return ([], "Each homepage product needs a positive product_id.");
                        }

                        products.Add(new CpMainPageProductSlot(productId, productOrder));
                        productOrder++;
                    }
                }

                groups.Add(new CpMainPageProductGroup(
                    value,
                    ReadString(node, "value_lang_str_id"),
                    ReadFlag(node, "show_caption") ? 1 : 0,
                    ReadFlag(node, "active") ? 1 : 0,
                    products));
                if (groups.Count > 80)
                {
                    return ([], "tree_json has too many groups.");
                }
            }

            return (groups, null);
        }
        catch (JsonException)
        {
            return ([], "tree_json is not valid JSON.");
        }
    }

    public static string HtmlEncode(string? raw)
        => WebUtility.HtmlEncode((raw ?? string.Empty).Trim());

    public static string NormalizeAction(string? raw)
    {
        var action = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return action switch
        {
            "save" or "save_action" or "save_tree" => "save",
            _ => action
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

        throw new ErpWriteException("Could not allocate a homepage-slot translation key.");
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
