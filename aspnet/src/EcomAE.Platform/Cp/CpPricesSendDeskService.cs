using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Typed reads for the PHP <c>prices_send.php</c> page (recipients, groups, offices, storages, catalogue tree, brands).</summary>
public interface ICpPricesSendDeskService
{
    Task<CpPricesSendDesk> LoadAsync(CpPricesSendUserFilter filter, CpPricesSendUserSort sort, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CpPricesSendBrand>> ListBrandsAsync(int limit, CancellationToken cancellationToken = default);
}

public sealed class CpPricesSendDeskService : ICpPricesSendDeskService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpPricesSendDeskService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    /// <summary>PHP user list SQL: WHERE clause + bound values from the cookie filter (exact matches only).</summary>
    public static (string Where, object?[] Args) BuildUserWhere(CpPricesSendUserFilter filter)
    {
        var parts = new List<string>();
        var args = new List<object?>();
        if (filter.UserId.Length > 0)
        {
            parts.Add("`users`.`user_id` = ?");
            args.Add(filter.UserId);
        }

        if (filter.GroupId != -1)
        {
            parts.Add("`users_groups_bind`.`group_id` = ?");
            args.Add(filter.GroupId);
        }

        if (filter.Email.Length > 0)
        {
            parts.Add("`users`.`email` = ?");
            args.Add(System.Net.WebUtility.HtmlEncode(filter.Email));
        }

        if (filter.Cellphone.Length > 0)
        {
            parts.Add("IF((SELECT COUNT(`users_profiles`.`user_id`) FROM `users_profiles` WHERE `users_profiles`.`data_key` = 'cellphone' AND `users_profiles`.`data_value` = ? AND `users_profiles`.`user_id` = `users`.`user_id`) = 1, 1, 0) = 1");
            args.Add(filter.Cellphone);
        }

        if (filter.Surname.Length > 0)
        {
            parts.Add("IF((SELECT COUNT(`users_profiles`.`user_id`) FROM `users_profiles` WHERE `users_profiles`.`data_key` = 'surname' AND `users_profiles`.`data_value` = ? AND `users_profiles`.`user_id` = `users`.`user_id`) = 1, 1, 0) = 1");
            args.Add(filter.Surname);
        }

        return (parts.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", parts), args.ToArray());
    }

    public static string UsersSql(CpPricesSendUserFilter filter, CpPricesSendUserSort sort)
    {
        var (where, _) = BuildUserWhere(filter);
        var field = Array.IndexOf(CpPricesSendUserSort.Fields, sort.Field) >= 0 ? sort.Field : "user_id";
        return "SELECT DISTINCT `users`.`user_id` AS `user_id`, IFNULL(`users`.`email`,'') AS `email`, IFNULL(`users`.`email_confirmed`,0) AS `email_confirmed`, "
               + "TRIM(CONCAT(IFNULL((SELECT `data_value` FROM `users_profiles` WHERE `data_key` = 'surname' AND `user_id` = `users`.`user_id` LIMIT 1),''), ' ', "
               + "IFNULL((SELECT `data_value` FROM `users_profiles` WHERE `data_key` = 'name' AND `user_id` = `users`.`user_id` LIMIT 1),''), ' ', "
               + "IFNULL((SELECT `data_value` FROM `users_profiles` WHERE `data_key` = 'patronymic' AND `user_id` = `users`.`user_id` LIMIT 1),''))) AS `fio` "
               + "FROM `users` INNER JOIN `users_profiles` ON `users`.`user_id` = `users_profiles`.`user_id` "
               + "INNER JOIN `users_groups_bind` ON `users_groups_bind`.`user_id` = `users`.`user_id`"
               + where
               + " ORDER BY `" + field + "` " + (sort.Ascending ? "ASC" : "DESC") + " LIMIT 2000";
    }

    public async Task<CpPricesSendDesk> LoadAsync(CpPricesSendUserFilter filter, CpPricesSendUserSort sort, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return CpPricesSendDesk.Unavailable("No database configured — price lists cannot be built.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var translate = CpOfficeEditorService.Translator(connection, cancellationToken);

            var groups = new List<CpPricesSendGroup>();
            var groupNames = new Dictionary<int, string>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`value`,'') FROM `groups` ORDER BY `id`";
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var id = Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture);
                    var name = r.GetString(1);
                    groups.Add(new CpPricesSendGroup(id, name));
                }
            }

            for (var i = 0; i < groups.Count; i++)
            {
                var name = await translate(groups[i].Name).ConfigureAwait(false);
                groups[i] = groups[i] with { Name = name };
                groupNames[groups[i].Id] = name;
            }

            var userRows = new List<(long Id, string Email, bool Confirmed, string Fio)>();
            var (_, args) = BuildUserWhere(filter);
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(UsersSql(filter, sort));
                ErpDb.AddParameters(cmd, args);
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    userRows.Add((
                        Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                        r.GetString(1),
                        Convert.ToInt32(r.GetValue(2), CultureInfo.InvariantCulture) != 0,
                        r.GetString(3)));
                }
            }

            var bind = new Dictionary<long, List<string>>();
            if (userRows.Count > 0)
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT DISTINCT `user_id`, `group_id` FROM `users_groups_bind` WHERE `user_id` IN ("
                                  + string.Join(",", userRows.Select(u => u.Id.ToString(CultureInfo.InvariantCulture))) + ") ORDER BY `user_id`, `group_id`";
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var uid = Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture);
                    var gid = Convert.ToInt32(r.GetValue(1), CultureInfo.InvariantCulture);
                    if (!bind.TryGetValue(uid, out var list))
                    {
                        list = [];
                        bind[uid] = list;
                    }

                    list.Add(groupNames.TryGetValue(gid, out var gn) ? gn : "#" + gid.ToString(CultureInfo.InvariantCulture));
                }
            }

            var users = userRows
                .Select(u => new CpPricesSendUserRow(u.Id, u.Email, u.Confirmed, u.Fio, bind.TryGetValue(u.Id, out var g) ? g : []))
                .ToArray();

            var offices = new List<CpPricesSendOffice>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`caption`,'') FROM `shop_offices` ORDER BY `id`";
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    offices.Add(new CpPricesSendOffice(Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture), r.GetString(1)));
                }
            }

            for (var i = 0; i < offices.Count; i++)
            {
                offices[i] = offices[i] with { Caption = await translate(offices[i].Caption).ConfigureAwait(false) };
            }

            var linked = new HashSet<int>();
            if (offices.Count > 0)
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = ErpDb.Positional("SELECT DISTINCT `storage_id` FROM `shop_offices_storages_map` WHERE `office_id` = ?");
                ErpDb.AddParameters(cmd, offices[0].Id);
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    linked.Add(Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture));
                }
            }

            var storages = new List<CpPricesSendStorage>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT s.`id`, IFNULL(s.`name`,''), IFNULL(s.`interface_type`,0), "
                                  + "IFNULL((SELECT `name` FROM `shop_storages_interfaces_types` WHERE `id` = s.`interface_type`),'') "
                                  + "FROM `shop_storages` s WHERE s.`interface_type` = 1 OR s.`interface_type` = 2 ORDER BY s.`id`";
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var id = Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture);
                    storages.Add(new CpPricesSendStorage(id, r.GetString(1), Convert.ToInt32(r.GetValue(2), CultureInfo.InvariantCulture), r.GetString(3), linked.Contains(id)));
                }
            }

            var catalogueStorages = storages.Where(s => s.InterfaceType == 1).ToArray();

            var flat = new List<(long Id, long Parent, string Value)>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`parent`,0), IFNULL(`value`,''), IFNULL(`alias`,'') FROM `shop_catalogue_categories` ORDER BY `level`, `order`, `id`";
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var value = r.GetString(2);
                    var alias = r.GetString(3);
                    flat.Add((
                        Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture),
                        Convert.ToInt64(r.GetValue(1), CultureInfo.InvariantCulture),
                        alias.Length > 0 ? alias : value));
                }
            }

            for (var i = 0; i < flat.Count; i++)
            {
                flat[i] = (flat[i].Id, flat[i].Parent, await translate(flat[i].Value).ConfigureAwait(false));
            }

            var labels = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, fallback) in CpPricesSendLabels.Defaults)
            {
                var text = await translate(key).ConfigureAwait(false);
                labels[key] = text == key ? fallback : text;
            }

            return new CpPricesSendDesk(true, string.Empty, filter, sort, groups, users, offices, storages, catalogueStorages, BuildTree(flat), labels);
        }
        catch (DbException ex)
        {
            return CpPricesSendDesk.Unavailable("Database unavailable: " + ex.Message);
        }
    }

    public static IReadOnlyList<CpPricesSendCategoryNode> BuildTree(IReadOnlyList<(long Id, long Parent, string Value)> flat)
    {
        var ids = flat.Select(f => f.Id).ToHashSet();
        var byParent = flat.GroupBy(f => ids.Contains(f.Parent) && f.Parent != f.Id ? f.Parent : 0).ToDictionary(g => g.Key, g => g.ToList());
        var visited = new HashSet<long>();

        List<CpPricesSendCategoryNode> Build(long parent)
        {
            if (!byParent.TryGetValue(parent, out var children))
            {
                return [];
            }

            var list = new List<CpPricesSendCategoryNode>();
            foreach (var c in children)
            {
                if (!visited.Add(c.Id))
                {
                    continue;
                }

                list.Add(new CpPricesSendCategoryNode(c.Id, c.Parent, c.Value, Build(c.Id)));
            }

            return list;
        }

        return Build(0);
    }

    public async Task<IReadOnlyList<CpPricesSendBrand>> ListBrandsAsync(int limit, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit <= 0 ? 30 : limit, 1, 50);
        if (!_connections.IsConfigured)
        {
            return [];
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional("SELECT `manufacturer` AS `brand`, COUNT(*) AS `cnt` FROM `shop_docpart_prices_data` WHERE `manufacturer` <> '' GROUP BY `manufacturer` ORDER BY `cnt` DESC LIMIT ?");
            ErpDb.AddParameters(cmd, limit);
            var rows = new List<CpPricesSendBrand>();
            await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new CpPricesSendBrand(r.GetString(0), Convert.ToInt32(r.GetValue(1), CultureInfo.InvariantCulture)));
            }

            return rows;
        }
        catch (DbException)
        {
            return [];
        }
    }
}
