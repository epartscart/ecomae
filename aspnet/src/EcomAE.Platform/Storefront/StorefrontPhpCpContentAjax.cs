using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string ContentMissing = "Content is not in this database.";

    public static Task<object> ContentJsonListAsync(
        DbConnection connection,
        string? adminSession,
        string? adminUser,
        string? csrf,
        IReadOnlyDictionary<string, string> fields,
        int pageLimit,
        CancellationToken cancellationToken)
        => WithCpAdminAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            () => new FlagBody(false, "Forbidden"),
            (_, token) => ContentJsonBodyAsync(connection, fields, pageLimit, token),
            cancellationToken);

    private static async Task<object> ContentJsonBodyAsync(
        DbConnection connection,
        IReadOnlyDictionary<string, string> fields,
        int pageLimit,
        CancellationToken cancellationToken)
    {
        var frontend = OmsField(fields, "is_frontend");
        int maxLevel;
        long total;
        try
        {
            maxLevel = await ContentMaxLevelAsync(connection, frontend, cancellationToken).ConfigureAwait(false);
            total = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `content` WHERE `is_frontend` = ?"),
                cancellationToken,
                frontend).ConfigureAwait(false);
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, ContentMissing);
        }

        if (maxLevel < 1)
        {
            return ContentListPayload(new JsonArray(), maxLevel, 0, total, pageLimit);
        }

        var capped = Math.Min(maxLevel, 32);
        var page = 0;
        if (int.TryParse(OmsField(fields, "s_page"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
        {
            page = parsed;
        }

        var from = pageLimit > 0 ? page * pageLimit : 0;
        long pagination;
        var content = new JsonArray();
        try
        {
            pagination = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional(ContentCountSql(capped)),
                cancellationToken,
                frontend).ConfigureAwait(false);
            if (pageLimit > 0)
            {
                content = await ContentPageAsync(connection, capped, frontend, from, pageLimit, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (DbException ex) when (CpMissingSchema.IsMissing(ex))
        {
            return new FlagBody(false, ContentMissing);
        }

        return ContentListPayload(content, maxLevel, pagination, total, pageLimit);
    }

    private static JsonObject ContentListPayload(JsonArray content, int maxLevel, long pagination, long total, int pageLimit)
        => new()
        {
            ["status"] = true,
            ["message"] = string.Empty,
            ["content"] = content,
            ["max_level"] = maxLevel,
            ["count_total_for_pagination"] = pagination > int.MaxValue ? int.MaxValue : (int)pagination,
            ["count_total"] = total > int.MaxValue ? int.MaxValue : (int)total,
            ["list_page_limit"] = pageLimit,
        };

    private static async Task<int> ContentMaxLevelAsync(DbConnection connection, string frontend, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT MAX(`level`) FROM `content` WHERE `is_frontend` = ?");
        ErpDb.AddParameters(command, frontend);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (value is null || value is DBNull)
        {
            return 0;
        }

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static string ContentCountSql(int maxLevel)
        => "SELECT COUNT(`t1`.`id`) FROM `content` AS `t1` " + ContentJoins(maxLevel) + " WHERE `t1`.`parent` = 0 AND `t1`.`is_frontend` = ?";

    private static string ContentPageSql(int maxLevel, int from, int pageLimit)
        => "SELECT " + ContentFields(maxLevel) + " FROM `content` AS `t1` " + ContentJoins(maxLevel)
            + " WHERE `t1`.`parent` = 0 AND `t1`.`is_frontend` = ? ORDER BY `t1`.`id` LIMIT "
            + from.ToString(CultureInfo.InvariantCulture) + ", " + pageLimit.ToString(CultureInfo.InvariantCulture);

    private static string ContentJoins(int maxLevel)
    {
        var joins = new StringBuilder();
        for (var level = 2; level <= maxLevel; level++)
        {
            joins.Append(" LEFT JOIN `content` AS `t").Append(level).Append("` ON `t").Append(level)
                .Append("`.`parent` = `t").Append(level - 1).Append("`.`id` ");
        }

        return joins.ToString();
    }

    private static string ContentFields(int maxLevel)
    {
        var fields = new StringBuilder();
        for (var level = 1; level <= maxLevel; level++)
        {
            if (level > 1)
            {
                fields.Append(',');
            }

            var alias = level.ToString(CultureInfo.InvariantCulture);
            fields.Append("`t").Append(alias).Append("`.`id` AS `l").Append(alias).Append("_id`,");
            fields.Append("`t").Append(alias).Append("`.`value` AS `l").Append(alias).Append("_value`,");
            fields.Append("`t").Append(alias).Append("`.`level` AS `l").Append(alias).Append("_level`,");
            fields.Append("`t").Append(alias).Append("`.`parent` AS `l").Append(alias).Append("_parent`");
        }

        return fields.ToString();
    }

    private static async Task<JsonArray> ContentPageAsync(
        DbConnection connection,
        int maxLevel,
        string frontend,
        int from,
        int pageLimit,
        CancellationToken cancellationToken)
    {
        var rows = new List<Dictionary<string, object?>>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(ContentPageSql(maxLevel, from, pageLimit));
            ErpDb.AddParameters(command, frontend);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var cells = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    cells[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }

                rows.Add(cells);
            }
        }

        var content = new JsonArray();
        var shown = new HashSet<int>();
        var translate = CpOfficeEditorService.Translator(connection, cancellationToken);
        foreach (var cells in rows)
        {
            var record = new JsonObject();
            for (var level = 1; level <= maxLevel; level++)
            {
                var alias = level.ToString(CultureInfo.InvariantCulture);
                var id = ContentCell(cells, "l" + alias + "_id");
                record["l" + alias + "_id"] = ContentNode(id);
                record["l" + alias + "_value"] = id is null
                    ? JsonNode.Parse("null")
                    : await translate(ContentText(cells, "l" + alias + "_value")).ConfigureAwait(false);
                record["l" + alias + "_level"] = ContentNode(ContentCell(cells, "l" + alias + "_level"));
                record["l" + alias + "_parent"] = ContentNode(ContentCell(cells, "l" + alias + "_parent"));
            }

            for (var level = 1; level <= maxLevel; level++)
            {
                var idNode = record["l" + level.ToString(CultureInfo.InvariantCulture) + "_id"];
                if (idNode is not JsonValue idValue || !idValue.TryGetValue<int>(out var id))
                {
                    break;
                }

                if (shown.Add(id))
                {
                    content.Add(record.DeepClone());
                }
            }
        }

        return content;
    }

    private static object? ContentCell(Dictionary<string, object?> cells, string name)
        => cells.TryGetValue(name, out var value) ? value : null;

    private static string ContentText(Dictionary<string, object?> cells, string name)
        => ContentCell(cells, name) is null ? string.Empty : Convert.ToString(ContentCell(cells, name), CultureInfo.InvariantCulture) ?? string.Empty;

    private static JsonNode ContentNode(object? value)
        => value is null ? JsonNode.Parse("null")! : JsonValue.Create(Convert.ToInt32(value, CultureInfo.InvariantCulture))!;
}
