using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpOfficeListRow(long Id, string Caption, string City, string Address, string Phone);

public sealed record CpOfficeList(IReadOnlyList<CpOfficeListRow> Rows, int Total, int Page, int PageSize)
{
    public int Pages => Math.Max(1, (Total + PageSize - 1) / Math.Max(1, PageSize));
}

/// <summary>PHP <c>office.php</c> editor state: translated field values plus their lang string keys (hidden <c>*_lang_str_id</c>).</summary>
public sealed record CpOfficeEditor(
    long Id,
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyDictionary<string, string> LangStrIds,
    string Phone,
    string Email,
    string Coordinates,
    IReadOnlyList<long> Users,
    IReadOnlyList<CpStorageUserOption> UserOptions)
{
    public static readonly string[] TranslatedItems = ["caption", "country", "region", "city", "address", "description", "timetable"];

    public string Value(string item) => Values.TryGetValue(item, out var v) ? v : string.Empty;

    public string LangStrId(string item) => LangStrIds.TryGetValue(item, out var v) ? v : "0";
}

public sealed record CpOfficeGeoNode(long Id, long Parent, int Level, string Caption, bool Checked);

/// <summary>PHP <c>office_geo_nodes.php</c>: full <c>shop_geo</c> tree with the office's linked nodes checked.</summary>
public sealed record CpOfficeGeoPage(long OfficeId, string OfficeCaption, IReadOnlyList<CpOfficeGeoNode> Nodes);

public sealed record CpOfficeStorageRange(decimal MaxPoint, decimal Markup);

public sealed record CpOfficeStorageGroupMarkup(long GroupId, IReadOnlyList<CpOfficeStorageRange> Ranges);

public sealed record CpOfficeStorageLink(
    long StorageId,
    string Name,
    int ProductType,
    bool Checked,
    long TimeToShop,
    IReadOnlyList<CpOfficeStorageGroupMarkup> Groups);

/// <summary>PHP <c>office_storages_link.php</c>: every warehouse with per-user-group markup ranges from <c>shop_offices_storages_map</c>.</summary>
public sealed record CpOfficeStoragesPage(
    long OfficeId,
    string OfficeCaption,
    IReadOnlyList<(long Id, string Caption)> UserGroups,
    IReadOnlyList<CpOfficeStorageLink> Storages);

public interface ICpOfficeEditorService
{
    Task<CpOfficeList> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<CpOfficeEditor?> OpenAsync(long officeId, CancellationToken cancellationToken = default);

    Task<CpOfficeGeoPage?> GeoAsync(long officeId, CancellationToken cancellationToken = default);

    Task<CpOfficeStoragesPage?> StoragesAsync(long officeId, CancellationToken cancellationToken = default);
}

public sealed class CpOfficeEditorService : ICpOfficeEditorService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpOfficeEditorService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpOfficeList> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var rows = new List<CpOfficeListRow>();
        var total = 0;
        if (!_connections.IsConfigured)
        {
            return new(rows, 0, page, pageSize);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            total = (int)await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `shop_offices`", cancellationToken).ConfigureAwait(false);
            var translate = Translator(connection, cancellationToken);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional(
                "SELECT `id`, IFNULL(`caption`,''), IFNULL(`city`,''), IFNULL(`address`,''), IFNULL(`phone`,'') FROM `shop_offices` ORDER BY `id` LIMIT ? OFFSET ?");
            ErpDb.AddParameters(cmd, pageSize, (page - 1) * pageSize);
            var raw = new List<(long Id, string Caption, string City, string Address, string Phone)>();
            await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    raw.Add((Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)));
                }
            }

            foreach (var r in raw)
            {
                rows.Add(new CpOfficeListRow(
                    r.Id,
                    await translate(r.Caption).ConfigureAwait(false),
                    await translate(r.City).ConfigureAwait(false),
                    await translate(r.Address).ConfigureAwait(false),
                    r.Phone));
            }
        }
        catch (DbException)
        {
        }

        return new(rows, total, page, pageSize);
    }

    public async Task<CpOfficeEditor?> OpenAsync(long officeId, CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in CpOfficeEditor.TranslatedItems)
        {
            values[item] = string.Empty;
            keys[item] = "0";
        }

        var phone = string.Empty;
        var email = string.Empty;
        var coordinates = string.Empty;
        var users = new List<long>();
        var userOptions = new List<CpStorageUserOption>();
        if (!_connections.IsConfigured)
        {
            return officeId > 0 ? null : new CpOfficeEditor(0, values, keys, phone, email, coordinates, users, userOptions);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (officeId > 0)
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = ErpDb.Positional(
                    "SELECT IFNULL(`caption`,''), IFNULL(`country`,''), IFNULL(`region`,''), IFNULL(`city`,''), IFNULL(`address`,''), IFNULL(`description`,''), IFNULL(`timetable`,''), " +
                    "IFNULL(`phone`,''), IFNULL(`email`,''), IFNULL(`coordinates`,''), IFNULL(`users`,'') FROM `shop_offices` WHERE `id` = ? LIMIT 1");
                ErpDb.AddParameters(cmd, officeId);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                for (var i = 0; i < CpOfficeEditor.TranslatedItems.Length; i++)
                {
                    keys[CpOfficeEditor.TranslatedItems[i]] = reader.GetString(i);
                }

                phone = reader.GetString(7);
                email = reader.GetString(8);
                coordinates = reader.GetString(9);
                users = CpStorageEditorService.ParseIdList(reader.GetString(10)).ToList();
                await reader.CloseAsync().ConfigureAwait(false);

                var translate = Translator(connection, cancellationToken);
                foreach (var item in CpOfficeEditor.TranslatedItems)
                {
                    values[item] = await translate(keys[item]).ConfigureAwait(false);
                }
            }

            userOptions = await CpStorageEditorService.BackendUsersAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            if (officeId > 0)
            {
                return null;
            }
        }

        return new CpOfficeEditor(officeId, values, keys, phone, email, coordinates, users, userOptions);
    }

    public async Task<CpOfficeGeoPage?> GeoAsync(long officeId, CancellationToken cancellationToken = default)
    {
        if (officeId <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var captionKey = await OfficeCaptionKeyAsync(connection, officeId, cancellationToken).ConfigureAwait(false);
            if (captionKey is null)
            {
                return null;
            }

            var translate = Translator(connection, cancellationToken);
            var checkedIds = new HashSet<long>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional("SELECT `geo_id` FROM `shop_offices_geo_map` WHERE `office_id` = ?");
                ErpDb.AddParameters(cmd, officeId);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    checkedIds.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
                }
            }

            var raw = new List<(long Id, long Parent, int Level, string Value)>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`parent`,0), IFNULL(`level`,0), IFNULL(`value`,'') FROM `shop_geo` ORDER BY `parent`, `order`, `id`";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    raw.Add((
                        Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture),
                        reader.GetString(3)));
                }
            }

            // Depth-first order so the flat list renders as an indented tree.
            var byParent = raw.GroupBy(x => x.Parent).ToDictionary(g => g.Key, g => g.ToList());
            var nodes = new List<CpOfficeGeoNode>();
            async Task WalkAsync(long parent, int depth)
            {
                if (!byParent.TryGetValue(parent, out var children) || depth > 32)
                {
                    return;
                }

                foreach (var c in children)
                {
                    nodes.Add(new CpOfficeGeoNode(c.Id, c.Parent, depth, await translate(c.Value).ConfigureAwait(false), checkedIds.Contains(c.Id)));
                    await WalkAsync(c.Id, depth + 1).ConfigureAwait(false);
                }
            }

            await WalkAsync(0, 0).ConfigureAwait(false);
            foreach (var orphan in raw.Where(x => x.Parent != 0 && raw.All(y => y.Id != x.Parent)))
            {
                nodes.Add(new CpOfficeGeoNode(orphan.Id, orphan.Parent, 0, await translate(orphan.Value).ConfigureAwait(false), checkedIds.Contains(orphan.Id)));
                await WalkAsync(orphan.Id, 1).ConfigureAwait(false);
            }

            return new CpOfficeGeoPage(officeId, await translate(captionKey).ConfigureAwait(false), nodes);
        }
        catch (DbException)
        {
            return null;
        }
    }

    public async Task<CpOfficeStoragesPage?> StoragesAsync(long officeId, CancellationToken cancellationToken = default)
    {
        if (officeId <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var captionKey = await OfficeCaptionKeyAsync(connection, officeId, cancellationToken).ConfigureAwait(false);
            if (captionKey is null)
            {
                return null;
            }

            var translate = Translator(connection, cancellationToken);
            var groups = new List<(long Id, string Caption)>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`value`,'') FROM `groups` ORDER BY `id`";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                var raw = new List<(long, string)>();
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    raw.Add((Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture), reader.GetString(1)));
                }

                await reader.CloseAsync().ConfigureAwait(false);
                foreach (var (id, name) in raw)
                {
                    var caption = await translate(name).ConfigureAwait(false);
                    groups.Add((id, caption.Length > 0 ? caption : "#" + id.ToString(CultureInfo.InvariantCulture)));
                }
            }

            var storages = new List<(long Id, string Name, int ProductType)>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT s.`id`, IFNULL(s.`name`,''), IFNULL(t.`product_type`,0) FROM `shop_storages` s " +
                    "INNER JOIN `shop_storages_interfaces_types` t ON s.`interface_type` = t.`id` ORDER BY s.`id`";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    storages.Add((Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture), reader.GetString(1), Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture)));
                }
            }

            var map = new List<(long StorageId, long GroupId, decimal MaxPoint, decimal Markup, long Time)>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(
                    "SELECT `storage_id`, `group_id`, `max_point`, `markup`, IFNULL(`additional_time`,0) FROM `shop_offices_storages_map` WHERE `office_id` = ? ORDER BY `storage_id`, `group_id`, `max_point`");
                ErpDb.AddParameters(cmd, officeId);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    map.Add((
                        Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader.GetValue(3), CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader.GetValue(4), CultureInfo.InvariantCulture)));
                }
            }

            var links = new List<CpOfficeStorageLink>();
            foreach (var s in storages)
            {
                var rows = map.Where(m => m.StorageId == s.Id).ToList();
                var groupMarkups = new List<CpOfficeStorageGroupMarkup>();
                foreach (var g in groups)
                {
                    var ranges = rows.Where(r => r.GroupId == g.Id)
                        .Select(r => new CpOfficeStorageRange(r.MaxPoint >= 2147483647m ? -1m : r.MaxPoint, r.Markup))
                        .OrderBy(r => r.MaxPoint < 0 ? decimal.MaxValue : r.MaxPoint)
                        .ToList();
                    if (ranges.Count == 0 || ranges[^1].MaxPoint != -1m)
                    {
                        ranges.Add(new CpOfficeStorageRange(-1m, 0m));
                    }

                    groupMarkups.Add(new CpOfficeStorageGroupMarkup(g.Id, ranges));
                }

                links.Add(new CpOfficeStorageLink(s.Id, s.Name, s.ProductType, rows.Count > 0, rows.Count > 0 ? rows[0].Time : 0, groupMarkups));
            }

            return new CpOfficeStoragesPage(officeId, await translate(captionKey).ConfigureAwait(false), groups, links);
        }
        catch (DbException)
        {
            return null;
        }
    }

    /// <summary>Serialises the storages-link page into the <c>storages_list</c> JSON accepted by <see cref="ICpStorageWriteService.SaveMembershipAsync"/>.</summary>
    public static string StoragesListJson(CpOfficeStoragesPage page)
    {
        return JsonSerializer.Serialize(page.Storages.Select(s => new
        {
            id = s.StorageId,
            name = s.Name,
            product_type = s.ProductType,
            @checked = s.Checked,
            time_to_shop = s.TimeToShop,
            groups = s.Groups.Select(g => new
            {
                id = g.GroupId,
                prices_ranges = g.Ranges.Select(r => new { max_point = r.MaxPoint, markup = r.Markup })
            })
        }));
    }

    private static async Task<string?> OfficeCaptionKeyAsync(DbConnection connection, long officeId, CancellationToken cancellationToken)
    {
        return await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT IFNULL(`caption`,'') FROM `shop_offices` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            officeId).ConfigureAwait(false);
    }

    /// <summary>PHP <c>translate_str_by_id()</c>: numeric keys resolve through <c>lang_text_strings_translation</c>; anything else is literal.</summary>
    internal static Func<string, Task<string>> Translator(DbConnection connection, CancellationToken cancellationToken)
    {
        var cache = new Dictionary<string, string>(StringComparer.Ordinal);
        return async key =>
        {
            key = (key ?? string.Empty).Trim();
            if (key.Length == 0 || !key.All(char.IsDigit))
            {
                return key;
            }

            if (!cache.TryGetValue(key, out var text))
            {
                text = await ErpDb.StringAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `value` FROM `lang_text_strings_translation` WHERE `str_key` = ? ORDER BY `lang_code` = 'en' DESC LIMIT 1"),
                    cancellationToken,
                    key).ConfigureAwait(false) ?? key;
                cache[key] = text;
            }

            return text;
        };
    }
}
