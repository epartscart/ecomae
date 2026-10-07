using System.Data.Common;
using System.Globalization;
using System.Text.Json.Serialization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public static async Task<object> TreeBrunchAsync(
        DbConnection connection,
        int? treeListId,
        int? parentId,
        int? int1,
        int? int2,
        int? int3,
        CancellationToken cancellationToken)
    {
        try
        {
            var rows = await TreeRowsAsync(connection, treeListId, parentId, cancellationToken).ConfigureAwait(false);
            var data = new List<TreeBrunchItem>(rows.Count);
            foreach (var row in rows)
            {
                data.Add(new TreeBrunchItem(row.Id, await TranslateTreeAsync(connection, row.Value, cancellationToken).ConfigureAwait(false), row.Count, []));
            }

            return new TreeBrunchBody(data, int1, int2, int3);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return TreeListsMissing;
        }
    }

    public static async Task<object> TreeAsyncAsync(
        DbConnection connection,
        string? treeListId,
        string? parentId,
        CancellationToken cancellationToken)
    {
        try
        {
            int? tree = int.TryParse(treeListId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var treeValue) ? treeValue : null;
            int? parent = int.TryParse(parentId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parentValue) ? parentValue : null;
            if (treeListId is not null && tree is null)
            {
                tree = 0;
            }

            if (parentId is not null && parent is null)
            {
                parent = 0;
            }

            var rows = await TreeRowsAsync(connection, tree, parent, cancellationToken).ConfigureAwait(false);
            var data = new List<TreeAsyncItem>(rows.Count);
            foreach (var row in rows)
            {
                var kids = int.TryParse(row.Count, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count > 0
                    ? row.Count
                    : null;
                data.Add(new TreeAsyncItem(row.Id, await TranslateTreeAsync(connection, row.Value, cancellationToken).ConfigureAwait(false), kids));
            }

            return new TreeAsyncBody(parentId, data);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return TreeListsMissing;
        }
    }

    private static async Task<List<TreeRow>> TreeRowsAsync(
        DbConnection connection,
        int? treeListId,
        int? parentId,
        CancellationToken cancellationToken)
    {
        var rows = new List<TreeRow>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `id`, `value`, `count` FROM `shop_tree_lists_items` WHERE `tree_list_id` = ? AND `parent` = ? ORDER BY `order`");
        ErpDb.AddParameters(command, treeListId, parentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new TreeRow(
                reader.IsDBNull(0) ? string.Empty : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty,
                reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                reader.IsDBNull(2) ? "0" : Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? "0"));
        }

        return rows;
    }

    private static async Task<string?> TranslateTreeAsync(DbConnection connection, string value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        try
        {
            var translated = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? AND `lang_code` = 'en' LIMIT 1"),
                cancellationToken,
                value).ConfigureAwait(false);
            return string.IsNullOrEmpty(translated) ? value : translated;
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return value;
        }
    }

    public sealed record TreeBrunchBody(
        [property: JsonPropertyName("data")] IReadOnlyList<TreeBrunchItem> Data,
        [property: JsonPropertyName("int_1")] int? Int1,
        [property: JsonPropertyName("int_2")] int? Int2,
        [property: JsonPropertyName("int_3")] int? Int3);

    public sealed record TreeBrunchItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("value")] string? Value,
        [property: JsonPropertyName("webix_kids")] string WebixKids,
        [property: JsonPropertyName("data")] object[] Data);

    public sealed record TreeAsyncBody(
        [property: JsonPropertyName("parent")] string? Parent,
        [property: JsonPropertyName("data")] IReadOnlyList<TreeAsyncItem> Data);

    public sealed record TreeAsyncItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("value")] string? Value,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [property: JsonPropertyName("webix_kids")] string? WebixKids);

    private sealed record TreeRow(string Id, string Value, string Count);
}
