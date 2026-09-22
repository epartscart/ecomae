using System.Data.Common;
using System.Globalization;
using System.Text.Json.Nodes;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>One row of PHP <c>user_manager.php</c>: users + reg variant + group captions + profile columns + balance.</summary>
public sealed record CpUserListRow(
    long UserId,
    int RegVariant,
    string RegVariantCaption,
    string Email,
    bool EmailConfirmed,
    string Phone,
    bool PhoneConfirmed,
    bool Unlocked,
    long TimeRegistered,
    long TimeLastVisit,
    bool AdminCreated,
    decimal Balance,
    IReadOnlyList<string> Groups,
    IReadOnlyList<string> ProfileValues);

/// <summary>PHP <c>reg_fields</c> row (main_flag = 0 additional fields).</summary>
public sealed record CpRegField(
    long Id,
    string Name,
    string Caption,
    string ShowFor,
    string RequiredFor,
    int MaxLen,
    string Regexp,
    string WidgetType,
    string WidgetOptions,
    bool ToFilter,
    bool ToUsersTable);

public sealed record CpRegVariant(long Id, string Caption);

/// <summary>PHP <c>groups</c> row for the user editor tree.</summary>
public sealed record CpUserGroupNode(
    long Id,
    string Value,
    long Parent,
    int Level,
    bool Unblocked,
    bool ForGuests,
    bool ForRegistrated,
    bool ForBackend);

/// <summary>PHP <c>users_filter</c> cookie shape; reg-field filters keyed by <c>reg_fields.name</c>.</summary>
public sealed record CpUserFilter(
    string UserId = "",
    long GroupId = -1,
    string Email = "",
    string Phone = "",
    int Unlocked = -1,
    IReadOnlyDictionary<string, string>? Fields = null)
{
    public bool IsEmpty =>
        UserId.Length == 0 && GroupId < 0 && Email.Length == 0 && Phone.Length == 0 && Unlocked < 0
        && (Fields is null || Fields.Values.All(v => v.Length == 0));
}

public sealed record CpUserList(
    IReadOnlyList<CpUserListRow> Rows,
    int Total,
    int Page,
    int PageSize,
    string SortField,
    bool SortAsc,
    CpUserFilter Filter,
    IReadOnlyList<CpRegField> FilterFields,
    IReadOnlyList<CpRegField> TableColumns,
    IReadOnlyList<CpUserGroupNode> Groups)
{
    public int PageCount => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

/// <summary>Editor state of PHP <c>user.php</c>.</summary>
public sealed record CpUserEditor(
    long UserId,
    int RegVariant,
    string Email,
    bool EmailConfirmed,
    string Phone,
    bool PhoneConfirmed,
    bool Unlocked,
    decimal Balance,
    string Comment,
    IReadOnlyDictionary<string, string> Profile,
    IReadOnlyList<long> GroupIds,
    IReadOnlyList<CpRegField> Fields,
    IReadOnlyList<CpRegVariant> Variants,
    IReadOnlyList<CpUserGroupNode> Groups)
{
    /// <summary>Webix tree JSON for the PHP group tree (id/value/data nesting with flags).</summary>
    public string GroupsTreeJson()
    {
        JsonArray Build(long parent)
        {
            var arr = new JsonArray();
            foreach (var g in Groups.Where(g => g.Parent == parent).OrderBy(g => g.Id))
            {
                arr.Add(new JsonObject
                {
                    ["id"] = g.Id,
                    ["value"] = g.Value,
                    ["unblocked"] = g.Unblocked ? 1 : 0,
                    ["for_guests"] = g.ForGuests,
                    ["for_registrated"] = g.ForRegistrated,
                    ["for_backend"] = g.ForBackend,
                    ["open"] = true,
                    ["data"] = Build(g.Id)
                });
            }

            return arr;
        }

        return Build(0).ToJsonString();
    }
}

public interface ICpUserEditorService
{
    Task<CpUserList> ListAsync(int page, int pageSize, string? sortField, bool sortAsc, CpUserFilter filter, CancellationToken cancellationToken = default);

    /// <summary><paramref name="userId"/> 0 returns the create-mode editor (fields, variants, groups only).</summary>
    Task<CpUserEditor?> OpenAsync(long userId, CancellationToken cancellationToken = default);
}

public sealed class CpUserEditorService : ICpUserEditorService
{
    private static readonly string[] SortFields = ["user_id", "reg_variant", "email", "phone", "time_registered", "time_last_visit", "admin_created", "balance", "unlocked"];

    private const string BalanceSql =
        "(IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `user_id` = `users`.`user_id` AND `income`=1 AND `active` = 1), 0)" +
        " - IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `user_id` = `users`.`user_id` AND `income`=0 AND `active` = 1), 0))";

    private readonly IErpWriteConnectionFactory _connections;

    public CpUserEditorService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpUserList> ListAsync(int page, int pageSize, string? sortField, bool sortAsc, CpUserFilter filter, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var rows = new List<CpUserListRow>();
        var total = 0;
        var filterFields = new List<CpRegField>();
        var columns = new List<CpRegField>();
        var groups = new List<CpUserGroupNode>();
        var sort = "user_id";
        if (!_connections.IsConfigured)
        {
            return new CpUserList(rows, 0, page, pageSize, sort, sortAsc, filter, filterFields, columns, groups);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var translate = CpOfficeEditorService.Translator(connection, cancellationToken);
            var allFields = await RegFieldsAsync(connection, translate, cancellationToken).ConfigureAwait(false);
            filterFields.AddRange(allFields.Where(f => f.ToFilter));
            columns.AddRange(allFields.Where(f => f.ToUsersTable));
            groups.AddRange(await GroupsAsync(connection, translate, cancellationToken).ConfigureAwait(false));
            var groupCaption = groups.ToDictionary(g => g.Id, g => g.Value);
            var variants = (await VariantsAsync(connection, translate, cancellationToken).ConfigureAwait(false)).ToDictionary(v => v.Id, v => v.Caption);

            var wanted = sortField ?? string.Empty;
            if (SortFields.Contains(wanted, StringComparer.Ordinal) || columns.Any(c => c.Name == wanted))
            {
                sort = wanted;
            }

            var where = new List<string>();
            var args = new List<object?>();
            if (filter.UserId.Length > 0 && long.TryParse(filter.UserId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fid))
            {
                where.Add("`users`.`user_id` = ?");
                args.Add(fid);
            }

            if (filter.GroupId >= 0)
            {
                where.Add("`users_groups_bind`.`group_id` = ?");
                args.Add(filter.GroupId);
            }

            if (filter.Email.Length > 0)
            {
                where.Add("`users`.`email` = ?");
                args.Add(filter.Email);
            }

            if (filter.Phone.Length > 0)
            {
                where.Add("`users`.`phone` LIKE ?");
                args.Add(filter.Phone + "%");
            }

            if (filter.Unlocked is 0 or 1)
            {
                where.Add("`users`.`unlocked` = ?");
                args.Add(filter.Unlocked);
            }

            foreach (var f in filterFields)
            {
                if (filter.Fields is not null && filter.Fields.TryGetValue(f.Name, out var v) && v.Length > 0)
                {
                    where.Add("EXISTS (SELECT 1 FROM `users_profiles` WHERE `users_profiles`.`data_key` = ? AND `users_profiles`.`data_value` LIKE ? AND `users_profiles`.`user_id` = `users`.`user_id`)");
                    args.Add(f.Name);
                    args.Add("%" + v + "%");
                }
            }

            var whereSql = where.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", where);
            var profileCols = string.Concat(columns.Select(c =>
                ", (SELECT `data_value` FROM `users_profiles` WHERE `data_key` = '" + c.Name.Replace("'", "''", StringComparison.Ordinal) + "' AND `user_id` = `users`.`user_id` LIMIT 1) AS `" + c.Name.Replace("`", "``", StringComparison.Ordinal) + "`"));
            var from = " FROM `users` LEFT JOIN `users_groups_bind` ON `users_groups_bind`.`user_id` = `users`.`user_id`" + whereSql;

            total = (int)await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(DISTINCT `users`.`user_id`)" + from), cancellationToken, args.ToArray()).ConfigureAwait(false);

            var orderCol = "`" + sort.Replace("`", "``", StringComparison.Ordinal) + "`";
            var sql =
                "SELECT `users`.`user_id`, `users`.`reg_variant`, IFNULL(`users`.`email`,''), `users`.`email_confirmed`, IFNULL(`users`.`phone`,''), `users`.`phone_confirmed`, `users`.`unlocked`, " +
                "IFNULL(`users`.`time_registered`,0), IFNULL(`users`.`time_last_visit`,0), IFNULL(`users`.`admin_created`,0), " + BalanceSql + " AS `balance`" + profileCols +
                from + " GROUP BY `users`.`user_id` ORDER BY " + orderCol + (sortAsc ? " ASC" : " DESC") + ", `users`.`user_id` ASC LIMIT ? OFFSET ?";
            args.Add(pageSize);
            args.Add((page - 1) * pageSize);

            var raw = new List<(long Id, int Variant, string Email, bool EmailOk, string Phone, bool PhoneOk, bool Unlocked, long Reg, long Visit, bool Admin, decimal Balance, List<string> Profile)>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(sql);
                ErpDb.AddParameters(cmd, args.ToArray());
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var profile = new List<string>();
                    for (var i = 0; i < columns.Count; i++)
                    {
                        profile.Add(reader.IsDBNull(11 + i) ? string.Empty : Convert.ToString(reader.GetValue(11 + i), CultureInfo.InvariantCulture) ?? string.Empty);
                    }

                    raw.Add((L(reader, 0), (int)L(reader, 1), reader.GetString(2), L(reader, 3) > 0, reader.GetString(4), L(reader, 5) > 0, L(reader, 6) > 0,
                        L(reader, 7), L(reader, 8), L(reader, 9) > 0, reader.IsDBNull(10) ? 0m : Convert.ToDecimal(reader.GetValue(10), CultureInfo.InvariantCulture), profile));
                }
            }

            foreach (var r in raw)
            {
                var userGroups = new List<string>();
                await using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = ErpDb.Positional("SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ? ORDER BY `group_id`");
                    ErpDb.AddParameters(cmd, r.Id);
                    await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var gid = L(reader, 0);
                        userGroups.Add(groupCaption.TryGetValue(gid, out var cap) ? cap : gid.ToString(CultureInfo.InvariantCulture));
                    }
                }

                rows.Add(new CpUserListRow(
                    r.Id, r.Variant, variants.TryGetValue(r.Variant, out var vc) ? vc : r.Variant.ToString(CultureInfo.InvariantCulture),
                    r.Email, r.EmailOk, r.Phone, r.PhoneOk, r.Unlocked, r.Reg, r.Visit, r.Admin, r.Balance, userGroups, r.Profile));
            }
        }
        catch (DbException)
        {
            rows.Clear();
            total = 0;
        }

        return new CpUserList(rows, total, page, pageSize, sort, sortAsc, filter, filterFields, columns, groups);
    }

    public async Task<CpUserEditor?> OpenAsync(long userId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return userId > 0 ? null : Empty(0, [], [], []);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var translate = CpOfficeEditorService.Translator(connection, cancellationToken);
            var fields = await RegFieldsAsync(connection, translate, cancellationToken).ConfigureAwait(false);
            var variants = await VariantsAsync(connection, translate, cancellationToken).ConfigureAwait(false);
            var groups = await GroupsAsync(connection, translate, cancellationToken).ConfigureAwait(false);
            if (userId <= 0)
            {
                return Empty(0, fields, variants, groups);
            }

            int variant;
            string email;
            bool emailOk;
            string phone;
            bool phoneOk;
            bool unlocked;
            string comment;
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(
                    "SELECT `reg_variant`, IFNULL(`email`,''), `email_confirmed`, IFNULL(`phone`,''), `phone_confirmed`, `unlocked`, IFNULL(`comment`,'') FROM `users` WHERE `user_id` = ? LIMIT 1");
                ErpDb.AddParameters(cmd, userId);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                variant = (int)L(reader, 0);
                email = reader.GetString(1);
                emailOk = L(reader, 2) > 0;
                phone = reader.GetString(3);
                phoneOk = L(reader, 4) > 0;
                unlocked = L(reader, 5) > 0;
                comment = reader.GetString(6);
            }

            var balance = await ErpDb.DecimalAsync(connection, null, ErpDb.Positional("SELECT " + BalanceSql + " FROM `users` WHERE `user_id` = ?"), cancellationToken, userId).ConfigureAwait(false);
            var profile = new Dictionary<string, string>(StringComparer.Ordinal);
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional("SELECT `data_key`, IFNULL(`data_value`,'') FROM `users_profiles` WHERE `user_id` = ?");
                ErpDb.AddParameters(cmd, userId);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    profile[reader.GetString(0)] = reader.GetString(1);
                }
            }

            var groupIds = new List<long>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional("SELECT `group_id` FROM `users_groups_bind` WHERE `user_id` = ? ORDER BY `group_id`");
                ErpDb.AddParameters(cmd, userId);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    groupIds.Add(L(reader, 0));
                }
            }

            return new CpUserEditor(userId, variant, email, emailOk, phone, phoneOk, unlocked, balance, comment, profile, groupIds, fields, variants, groups);
        }
        catch (DbException)
        {
            return userId > 0 ? null : Empty(0, [], [], []);
        }
    }

    private static CpUserEditor Empty(long id, IReadOnlyList<CpRegField> fields, IReadOnlyList<CpRegVariant> variants, IReadOnlyList<CpUserGroupNode> groups)
        => new(id, variants.Count > 0 ? (int)variants[0].Id : 1, string.Empty, false, string.Empty, false, true, 0m, string.Empty,
            new Dictionary<string, string>(StringComparer.Ordinal), [], fields, variants, groups);

    private static long L(DbDataReader reader, int i) => reader.IsDBNull(i) ? 0 : Convert.ToInt64(reader.GetValue(i), CultureInfo.InvariantCulture);

    private static string S(DbDataReader reader, int i) => reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;

    internal static async Task<List<CpRegField>> RegFieldsAsync(DbConnection connection, Func<string, Task<string>> translate, CancellationToken cancellationToken)
    {
        var raw = new List<CpRegField>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT `id`, IFNULL(`name`,''), IFNULL(`caption`,''), IFNULL(`show_for`,'[]'), IFNULL(`required_for`,'[]'), IFNULL(`maxlen`,0), IFNULL(`regexp`,''), IFNULL(`widget_type`,'text'), IFNULL(`widget_options`,'{}'), IFNULL(`to_filter`,0), IFNULL(`to_users_table`,0) FROM `reg_fields` WHERE `main_flag` = 0 ORDER BY `order` ASC";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                raw.Add(new CpRegField(L(reader, 0), S(reader, 1), S(reader, 2), S(reader, 3), S(reader, 4), (int)L(reader, 5), S(reader, 6), S(reader, 7), S(reader, 8), L(reader, 9) > 0, L(reader, 10) > 0));
            }
        }

        var list = new List<CpRegField>(raw.Count);
        foreach (var f in raw)
        {
            list.Add(f with { Caption = await translate(f.Caption).ConfigureAwait(false) });
        }

        return list;
    }

    internal static async Task<List<CpRegVariant>> VariantsAsync(DbConnection connection, Func<string, Task<string>> translate, CancellationToken cancellationToken)
    {
        var raw = new List<(long Id, string Caption)>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT `id`, IFNULL(`caption`,'') FROM `reg_variants` ORDER BY `order` ASC, `id` ASC";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                raw.Add((L(reader, 0), S(reader, 1)));
            }
        }

        var list = new List<CpRegVariant>(raw.Count);
        foreach (var v in raw)
        {
            list.Add(new CpRegVariant(v.Id, await translate(v.Caption).ConfigureAwait(false)));
        }

        return list;
    }

    internal static async Task<List<CpUserGroupNode>> GroupsAsync(DbConnection connection, Func<string, Task<string>> translate, CancellationToken cancellationToken)
    {
        var raw = new List<CpUserGroupNode>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT `id`, IFNULL(`value`,''), IFNULL(`parent`,0), IFNULL(`level`,0), IFNULL(`unblocked`,1), IFNULL(`for_guests`,0), IFNULL(`for_registrated`,0), IFNULL(`for_backend`,0) FROM `groups` ORDER BY `id`";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                raw.Add(new CpUserGroupNode(L(reader, 0), S(reader, 1), L(reader, 2), (int)L(reader, 3), L(reader, 4) > 0, L(reader, 5) > 0, L(reader, 6) > 0, L(reader, 7) > 0));
            }
        }

        var list = new List<CpUserGroupNode>(raw.Count);
        foreach (var g in raw)
        {
            list.Add(g with { Value = await translate(g.Value).ConfigureAwait(false) });
        }

        return list;
    }
}
