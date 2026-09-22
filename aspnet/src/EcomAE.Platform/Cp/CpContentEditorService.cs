using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpContentListRow(
    long Id,
    string Value,
    string TitleTag,
    string DescriptionTag,
    string ContentType,
    long Parent,
    int Level,
    string Url,
    bool System,
    bool Main,
    bool Published,
    long TimeCreated,
    long TimeEdited);

public sealed record CpContentFilter(string Id = "", string Content = "", string Meta = "")
{
    public bool IsEmpty => Id.Length == 0 && Content.Length == 0 && Meta.Length == 0;
}

/// <summary>
/// Rows of <c>content_manager.php</c>. <c>Total</c> is the number of pages in the mode; <c>PageCount</c> follows the PHP
/// rule (hierarchy paginates root pages only, direct list paginates every matching row).
/// </summary>
public sealed record CpContentList(
    IReadOnlyList<CpContentListRow> Rows,
    int Total,
    int PaginationTotal,
    int Page,
    int PageSize,
    bool IsFrontend,
    bool Hierarchy,
    string SortField,
    bool SortAsc,
    CpContentFilter Filter)
{
    public int PageCount => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(PaginationTotal / (double)PageSize));
}

public sealed record CpContentGroupOption(long Id, string Caption, int Level);

public sealed record CpContentParentOption(long Id, string Caption, string Url, int Level);

/// <summary>Editor state of <c>content_create_edit.php</c>: translated values plus their <c>*_lang_str_id</c> keys.</summary>
public sealed record CpContentEditor(
    long Id,
    bool IsFrontend,
    string Value,
    string ValueLangStrId,
    string Alias,
    string TitleTag,
    string TitleLangStrId,
    string DescriptionTag,
    string DescriptionTagLangStrId,
    string KeywordsTag,
    string KeywordsLangStrId,
    string RobotsTag,
    string AuthorTag,
    string AuthorLangStrId,
    string Description,
    string DescriptionLangStrId,
    string CssJs,
    string ContentType,
    string Content,
    string ContentLangStrId,
    long Parent,
    string ParentCaption,
    bool Main,
    bool Published,
    bool System,
    string Url,
    IReadOnlyList<long> GroupsAccess,
    IReadOnlyList<CpContentGroupOption> Groups,
    IReadOnlyList<CpContentParentOption> Parents);

/// <summary>Tree dump of <c>content_tree.php</c> (<c>data</c>-nested nodes with every column the tree save expects).</summary>
public sealed record CpContentTree(bool IsFrontend, string TreeJson, int Count);

public interface ICpContentEditorService
{
    Task<CpContentList> ListAsync(
        bool isFrontend,
        bool hierarchy,
        int page,
        int pageSize,
        string? sortField,
        bool sortAsc,
        CpContentFilter filter,
        CancellationToken cancellationToken = default);

    Task<CpContentEditor?> OpenAsync(long contentId, bool isFrontend, CancellationToken cancellationToken = default);

    Task<CpContentTree> TreeAsync(bool isFrontend, CancellationToken cancellationToken = default);
}

public sealed class CpContentEditorService : ICpContentEditorService
{
    private static readonly string[] SortFields = ["id", "value", "title_tag", "description_tag", "content_type", "parent", "level", "time_created", "time_edited"];

    private readonly IErpWriteConnectionFactory _connections;

    public CpContentEditorService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    private sealed record RawRow(
        long Id, string Value, string TitleTag, string DescriptionTag, string ContentType, long Parent, int Level, string Url,
        bool System, bool Main, bool Published, long TimeCreated, long TimeEdited, int Order);

    public async Task<CpContentList> ListAsync(
        bool isFrontend,
        bool hierarchy,
        int page,
        int pageSize,
        string? sortField,
        bool sortAsc,
        CpContentFilter filter,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var sort = SortFields.Contains(sortField ?? string.Empty, StringComparer.Ordinal) ? sortField! : "id";
        var rows = new List<CpContentListRow>();
        var total = 0;
        var paginationTotal = 0;
        if (!_connections.IsConfigured)
        {
            return new(rows, 0, 0, page, pageSize, isFrontend, hierarchy, sort, sortAsc, filter);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var frontend = isFrontend ? 1 : 0;
            var translate = CpOfficeEditorService.Translator(connection, cancellationToken);
            total = (int)await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT COUNT(*) FROM `content` WHERE `is_frontend` = ?"),
                cancellationToken, frontend).ConfigureAwait(false);

            if (hierarchy)
            {
                var all = await ReadRowsAsync(connection, "SELECT " + Columns + " FROM `content` WHERE `is_frontend` = ? ORDER BY `order`, `id`", cancellationToken, frontend).ConfigureAwait(false);
                var byParent = all.GroupBy(r => r.Parent).ToDictionary(g => g.Key, g => g.OrderBy(r => r.Order).ThenBy(r => r.Id).ToList());
                var roots = byParent.TryGetValue(0, out var r0) ? r0 : [];
                paginationTotal = roots.Count;
                var flat = new List<RawRow>();
                foreach (var root in roots.Skip((page - 1) * pageSize).Take(pageSize))
                {
                    Flatten(root, byParent, flat, 0);
                }

                foreach (var raw in flat)
                {
                    rows.Add(await ToRowAsync(raw, translate).ConfigureAwait(false));
                }
            }
            else
            {
                var where = new System.Text.StringBuilder(" WHERE `is_frontend` = ?");
                var args = new List<object> { frontend };
                if (filter.Id.Length > 0 && long.TryParse(filter.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idFilter))
                {
                    where.Append(" AND `id` = ?");
                    args.Add(idFilter);
                }

                if (filter.Content.Length > 0)
                {
                    where.Append(" AND `content` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` LIKE ?)");
                    args.Add("%" + filter.Content + "%");
                }

                if (filter.Meta.Length > 0)
                {
                    where.Append(" AND (`title_tag` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` LIKE ?)"
                        + " OR `description_tag` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` LIKE ?)"
                        + " OR `keywords_tag` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` LIKE ?)"
                        + " OR `value` IN (SELECT `str_key` FROM `lang_text_strings_translation` WHERE `value` LIKE ?))");
                    var like = "%" + filter.Meta + "%";
                    args.Add(like);
                    args.Add(like);
                    args.Add(like);
                    args.Add(like);
                }

                paginationTotal = (int)await ErpDb.LongAsync(
                    connection, null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `content`" + where),
                    cancellationToken, args.ToArray()).ConfigureAwait(false);

                var orderExpr = sort is "value" or "title_tag" or "description_tag"
                    ? "(SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = `content`.`" + sort + "` ORDER BY `lang_code` = 'en' DESC LIMIT 1)"
                    : "`" + sort + "`";
                var sql = "SELECT " + Columns + " FROM `content`" + where + " ORDER BY " + orderExpr + (sortAsc ? " ASC" : " DESC") + ", `id` LIMIT ? OFFSET ?";
                args.Add(pageSize);
                args.Add((page - 1) * pageSize);
                var raws = await ReadRowsAsync(connection, sql, cancellationToken, args.ToArray()).ConfigureAwait(false);
                foreach (var raw in raws)
                {
                    rows.Add(await ToRowAsync(raw, translate).ConfigureAwait(false));
                }
            }
        }
        catch (DbException)
        {
        }

        return new(rows, total, paginationTotal, page, pageSize, isFrontend, hierarchy, sort, sortAsc, filter);
    }

    private const string Columns =
        "`id`, IFNULL(`value`,''), IFNULL(`title_tag`,''), IFNULL(`description_tag`,''), IFNULL(`content_type`,''), IFNULL(`parent`,0), IFNULL(`level`,0), IFNULL(`url`,''), "
        + "IFNULL(`system_flag`,0), IFNULL(`main_flag`,0), IFNULL(`published_flag`,1), IFNULL(`time_created`,0), IFNULL(`time_edited`,0), IFNULL(`order`,0)";

    private static void Flatten(RawRow node, Dictionary<long, List<RawRow>> byParent, List<RawRow> into, int depth)
    {
        if (depth > 32)
        {
            return;
        }

        into.Add(node);
        if (byParent.TryGetValue(node.Id, out var children))
        {
            foreach (var child in children)
            {
                Flatten(child, byParent, into, depth + 1);
            }
        }
    }

    private static async Task<CpContentListRow> ToRowAsync(RawRow r, Func<string, Task<string>> translate)
        => new(
            r.Id,
            await translate(r.Value).ConfigureAwait(false),
            await translate(r.TitleTag).ConfigureAwait(false),
            await translate(r.DescriptionTag).ConfigureAwait(false),
            r.ContentType,
            r.Parent,
            r.Level,
            r.Url,
            r.System,
            r.Main,
            r.Published,
            r.TimeCreated,
            r.TimeEdited);

    private static async Task<List<RawRow>> ReadRowsAsync(DbConnection connection, string sql, CancellationToken cancellationToken, params object[] args)
    {
        var list = new List<RawRow>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(cmd, args);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(new RawRow(
                L(reader, 0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), L(reader, 5), (int)L(reader, 6), reader.GetString(7),
                L(reader, 8) > 0, L(reader, 9) > 0, L(reader, 10) > 0, L(reader, 11), L(reader, 12), (int)L(reader, 13)));
        }

        return list;
    }

    private static long L(DbDataReader reader, int i) => reader.IsDBNull(i) ? 0 : Convert.ToInt64(reader.GetValue(i), CultureInfo.InvariantCulture);

    public async Task<CpContentEditor?> OpenAsync(long contentId, bool isFrontend, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var translate = CpOfficeEditorService.Translator(connection, cancellationToken);
            var value = ""; var alias = ""; var title = ""; var descTag = ""; var keywords = ""; var robots = ""; var author = "";
            var description = ""; var cssJs = ""; var contentType = "text"; var content = ""; var url = "";
            long parent = 0; var main = false; var published = true; var system = false;
            var groupsAccess = new List<long>();
            if (contentId > 0)
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = ErpDb.Positional(
                    "SELECT IFNULL(`value`,''), IFNULL(`alias`,''), IFNULL(`title_tag`,''), IFNULL(`description_tag`,''), IFNULL(`keywords_tag`,''), IFNULL(`robots_tag`,''), IFNULL(`author_tag`,''), "
                    + "IFNULL(`description`,''), IFNULL(`css_js`,''), IFNULL(`content_type`,'text'), IFNULL(`content`,''), IFNULL(`parent`,0), IFNULL(`main_flag`,0), IFNULL(`published_flag`,1), "
                    + "IFNULL(`system_flag`,0), IFNULL(`is_frontend`,1), IFNULL(`url`,'') FROM `content` WHERE `id` = ? LIMIT 1");
                ErpDb.AddParameters(cmd, contentId);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                value = reader.GetString(0); alias = reader.GetString(1); title = reader.GetString(2); descTag = reader.GetString(3);
                keywords = reader.GetString(4); robots = reader.GetString(5); author = reader.GetString(6); description = reader.GetString(7);
                cssJs = reader.GetString(8); contentType = reader.GetString(9); content = reader.GetString(10); parent = L(reader, 11);
                main = L(reader, 12) > 0; published = L(reader, 13) > 0; system = L(reader, 14) > 0; isFrontend = L(reader, 15) > 0; url = reader.GetString(16);
            }

            if (contentId > 0)
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = ErpDb.Positional("SELECT `group_id` FROM `content_access` WHERE `content_id` = ?");
                ErpDb.AddParameters(cmd, contentId);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    groupsAccess.Add(L(reader, 0));
                }
            }

            var groups = new List<CpContentGroupOption>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`value`,''), IFNULL(`level`,1) FROM `groups` ORDER BY `level`, `id`";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    groups.Add(new CpContentGroupOption(L(reader, 0), await translate(reader.GetString(1)).ConfigureAwait(false), (int)L(reader, 2)));
                }
            }

            var parents = new List<CpContentParentOption>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional("SELECT `id`, IFNULL(`value`,''), IFNULL(`url`,''), IFNULL(`level`,1) FROM `content` WHERE `is_frontend` = ? AND `id` <> ? ORDER BY `url`, `id` LIMIT 5000");
                ErpDb.AddParameters(cmd, isFrontend ? 1 : 0, contentId);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    parents.Add(new CpContentParentOption(L(reader, 0), await translate(reader.GetString(1)).ConfigureAwait(false), reader.GetString(2), (int)L(reader, 3)));
                }
            }

            var parentCaption = parent > 0 ? parents.FirstOrDefault(p => p.Id == parent)?.Caption ?? parent.ToString(CultureInfo.InvariantCulture) : "Root";
            return new CpContentEditor(
                contentId, isFrontend,
                await translate(value).ConfigureAwait(false), Key(value),
                alias,
                await translate(title).ConfigureAwait(false), Key(title),
                await translate(descTag).ConfigureAwait(false), Key(descTag),
                await translate(keywords).ConfigureAwait(false), Key(keywords),
                robots,
                await translate(author).ConfigureAwait(false), Key(author),
                await translate(description).ConfigureAwait(false), Key(description),
                cssJs,
                contentType,
                contentType == "php" ? content : await translate(content).ConfigureAwait(false), contentType == "php" ? "0" : Key(content),
                parent, parentCaption, main, published, system, url, groupsAccess, groups, parents);
        }
        catch (DbException)
        {
            return null;
        }
    }

    private static string Key(string raw)
    {
        raw = raw.Trim();
        return raw.Length > 0 && raw.All(char.IsDigit) ? raw : "0";
    }

    public async Task<CpContentTree> TreeAsync(bool isFrontend, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new(isFrontend, "[]", 0);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var translate = CpOfficeEditorService.Translator(connection, cancellationToken);
            var nodes = new List<JsonObject>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(
                    "SELECT `id`, IFNULL(`count`,0), IFNULL(`url`,''), IFNULL(`level`,1), IFNULL(`alias`,''), IFNULL(`value`,''), IFNULL(`parent`,0), IFNULL(`description`,''), "
                    + "IFNULL(`main_flag`,0), IFNULL(`title_tag`,''), IFNULL(`description_tag`,''), IFNULL(`keywords_tag`,''), IFNULL(`robots_tag`,''), IFNULL(`published_flag`,1), "
                    + "IFNULL(`open`,0), IFNULL(`css_js`,''), IFNULL(`system_flag`,0), IFNULL(`order`,0), IFNULL(`content_type`,'text') "
                    + "FROM `content` WHERE `is_frontend` = ? ORDER BY `order`, `id`");
                ErpDb.AddParameters(cmd, isFrontend ? 1 : 0);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var value = reader.GetString(5);
                    var description = reader.GetString(7);
                    var title = reader.GetString(9);
                    var descTag = reader.GetString(10);
                    var keywords = reader.GetString(11);
                    nodes.Add(new JsonObject
                    {
                        ["id"] = L(reader, 0),
                        ["$count"] = L(reader, 1),
                        ["url"] = reader.GetString(2),
                        ["$level"] = L(reader, 3),
                        ["alias"] = reader.GetString(4),
                        ["value"] = await translate(value).ConfigureAwait(false),
                        ["value_lang_str_id"] = Key(value),
                        ["$parent"] = L(reader, 6),
                        ["description"] = await translate(description).ConfigureAwait(false),
                        ["description_lang_str_id"] = Key(description),
                        ["main_flag"] = L(reader, 8),
                        ["title_tag"] = await translate(title).ConfigureAwait(false),
                        ["title_tag_lang_str_id"] = Key(title),
                        ["description_tag"] = await translate(descTag).ConfigureAwait(false),
                        ["description_tag_lang_str_id"] = Key(descTag),
                        ["keywords_tag"] = await translate(keywords).ConfigureAwait(false),
                        ["keywords_tag_lang_str_id"] = Key(keywords),
                        ["robots_tag"] = reader.GetString(12),
                        ["published_flag"] = L(reader, 13),
                        ["open"] = L(reader, 14) > 0,
                        ["css_js"] = reader.GetString(15),
                        ["system_flag"] = L(reader, 16),
                        ["order"] = L(reader, 17),
                        ["content_type"] = reader.GetString(18),
                        ["groups_access"] = new JsonArray(),
                        ["data"] = new JsonArray(),
                    });
                }
            }

            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `content_id`, `group_id` FROM `content_access`";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                var byId = nodes.ToDictionary(n => n["id"]!.GetValue<long>());
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (byId.TryGetValue(L(reader, 0), out var node) && node["groups_access"] is JsonArray ga)
                    {
                        ga.Add(L(reader, 1));
                    }
                }
            }

            var lookup = nodes.ToDictionary(n => n["id"]!.GetValue<long>());
            var roots = new JsonArray();
            foreach (var node in nodes)
            {
                var parent = node["$parent"]!.GetValue<long>();
                if (parent > 0 && lookup.TryGetValue(parent, out var parentNode) && parentNode != node && parentNode["data"] is JsonArray children)
                {
                    children.Add(node);
                }
                else
                {
                    roots.Add(node);
                }
            }

            return new(isFrontend, roots.ToJsonString(), nodes.Count);
        }
        catch (DbException)
        {
            return new(isFrontend, "[]", 0);
        }
    }
}
