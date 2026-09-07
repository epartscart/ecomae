using System.Globalization;
using System.Net;
using System.Text.Json;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>geo_tree.php</c> save_tree twin.</summary>
public interface ICpGeoTreeWriteService
{
    Task<ErpSimpleWriteResult> SaveTreeAsync(
        string? treeJson,
        string? langCode,
        string? domainPath,
        CancellationToken cancellationToken = default);
}

public sealed class CpGeoTreeWriteService : ICpGeoTreeWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private int _createdStrings;

    public CpGeoTreeWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveTreeAsync(
        string? treeJson,
        string? langCode,
        string? domainPath,
        CancellationToken cancellationToken = default)
    {
        var parsed = ParseTree(treeJson);
        if (parsed.Error is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", parsed.Error);
        }

        if (parsed.Nodes.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "At least one geo node is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var lang = NormalizeLang(langCode);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var node in parsed.Nodes)
            {
                var valueKey = await RequireTranslationAsync(
                    connection,
                    transaction,
                    node.ValueLangStrId,
                    node.Value,
                    lang,
                    domainPath,
                    cancellationToken).ConfigureAwait(false);
                if (node.FromServer)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            """
                            UPDATE `shop_geo`
                            SET `count` = ?, `level` = ?, `value` = ?, `parent` = ?, `order` = ?
                            WHERE `id` = ?
                            """),
                        cancellationToken,
                        node.Count,
                        node.Level,
                        valueKey,
                        node.Parent,
                        node.Order,
                        node.Id).ConfigureAwait(false);
                }
                else
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            """
                            INSERT INTO `shop_geo` (`id`, `count`, `level`, `value`, `parent`, `order`)
                            VALUES (?, ?, ?, ?, ?, ?)
                            """),
                        cancellationToken,
                        node.Id,
                        node.Count,
                        node.Level,
                        valueKey,
                        node.Parent,
                        node.Order).ConfigureAwait(false);
                }
            }

            var keep = parsed.Nodes.Select(n => n.Id).Distinct().ToArray();
            var placeholders = string.Join(",", keep.Select(_ => "?"));
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `shop_geo` WHERE `id` NOT IN (" + placeholders + ")"),
                cancellationToken,
                keep.Cast<object?>().ToArray()).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ErpWriteException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", ex.Message);
        }
        catch (System.Data.Common.DbException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("invalid", "Could not save the geo tree.");
        }

        return new ErpSimpleWriteResult(true, "ok", "Geo tree saved.", parsed.Nodes[0].Id, parsed.Nodes.Count);
    }

    /// <summary>PHP geo_tree.php Webix hierarchy or linear node list.</summary>
    public static (IReadOnlyList<GeoTreeNode> Nodes, string? Error) ParseTree(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return ([], "tree_json is required.");
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            var nodes = new List<GeoTreeNode>();
            Walk(doc.RootElement, nodes);
            return nodes.Count == 0
                ? ([], "At least one geo node is required.")
                : nodes.Count > 500
                    ? ([], "Too many geo nodes.")
                    : (nodes, null);
        }
        catch (JsonException)
        {
            return ([], "tree_json is not valid JSON.");
        }
    }

    public readonly record struct GeoTreeNode(
        long Id,
        int Count,
        int Level,
        string Value,
        string ValueLangStrId,
        long Parent,
        int Order,
        bool FromServer);

    private static void Walk(JsonElement element, List<GeoTreeNode> nodes)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Walk(item, nodes);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var id = ReadLong(element, "id");
        if (id <= 0)
        {
            return;
        }

        var data = GetProperty(element, "data");
        var childCount = data.ValueKind == JsonValueKind.Array
            ? data.GetArrayLength()
            : (int)ReadLong(element, "$count", "count");
        var order = nodes.Count + 1;
        nodes.Add(new GeoTreeNode(
            id,
            childCount,
            (int)Math.Max(1, ReadLong(element, "$level", "level")),
            WebUtility.HtmlEncode(ReadString(element, "value").Trim()),
            ReadString(element, "value_lang_str_id", "valueLangStrId"),
            ReadLong(element, "$parent", "parent"),
            order,
            ReadLong(element, "from_server", "fromServer") == 1));

        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in data.EnumerateArray())
            {
                Walk(child, nodes);
            }
        }
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
                "GEO TREE EDITING",
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

        throw new ErpWriteException("Could not allocate a geo translation key.");
    }

    private static string NormalizeLang(string? langCode)
    {
        var lang = (langCode ?? string.Empty).Trim().ToLowerInvariant();
        return lang.Length is < 2 or > 16 ? "en" : lang;
    }

    private static JsonElement GetProperty(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (item.TryGetProperty(name, out var prop))
            {
                return prop;
            }
        }

        return default;
    }

    private static long ReadLong(JsonElement item, params string[] names)
    {
        var prop = GetProperty(item, names);
        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var n))
        {
            return n;
        }

        return prop.ValueKind == JsonValueKind.String
            && long.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static string ReadString(JsonElement item, params string[] names)
    {
        var prop = GetProperty(item, names);
        return prop.ValueKind == JsonValueKind.String
            ? prop.GetString() ?? string.Empty
            : prop.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False
                ? prop.ToString()
                : string.Empty;
    }
}
