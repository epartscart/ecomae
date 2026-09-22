using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpStorageListRow(
    long Id,
    string Name,
    string ShortName,
    int Hidden,
    int InterfaceType,
    string InterfaceTypeName,
    int ProductType,
    string PriceList)
{
    /// <summary>PHP storages.php <c>$product_types</c>.</summary>
    public string ProductTypeName => ProductType switch { 1 => "Catalog", 2 => "Price list", _ => string.Empty };

    /// <summary>PHP storages.php interface icon: 1 package, 2 xls, 6 none, else api.</summary>
    public string Icon => InterfaceType switch { 1 => "fa fa-archive", 2 => "fa fa-file-excel-o", 6 => "", _ => "fa fa-plug" };
}

public sealed record CpStorageList(IReadOnlyList<CpStorageListRow> Rows, int Total, int Page, int PageSize)
{
    public int Pages => Math.Max(1, (Total + PageSize - 1) / Math.Max(1, PageSize));
}

public sealed record CpStorageOption(string Value, string Caption);

/// <summary>One <c>connection_options</c> schema entry of <c>shop_storages_interfaces_types</c>.</summary>
public sealed record CpStorageConnectionField(string Name, string Caption, string Type, IReadOnlyList<CpStorageOption> Options);

public sealed record CpStorageInterfaceType(
    int Id,
    string Name,
    int ProductType,
    string HandlerFolder,
    string Description,
    IReadOnlyList<CpStorageConnectionField> Fields);

public sealed record CpStorageUserOption(long UserId, string Caption);

public sealed record CpStorageGroupRow(long Id, string Name, IReadOnlyList<(long Id, string Name)> Storages);

/// <summary>PHP <c>logistics/groups/groups.php</c>: auto-handled warehouses (types 1/2/6), groupable API warehouses, existing groups.</summary>
public sealed record CpStorageGroupsPage(
    IReadOnlyList<(long Id, string Name, int InterfaceType)> AutoStorages,
    IReadOnlyList<(long Id, string Name)> Eligible,
    IReadOnlyList<CpStorageGroupRow> Groups);

public sealed record CpStorageEditor(
    long Id,
    string Name,
    string ShortName,
    int Currency,
    int InterfaceType,
    int Hidden,
    int BgLineColor,
    IReadOnlyList<long> Users,
    IReadOnlyDictionary<string, string> ConnectionOptions,
    IReadOnlyList<CpStorageOption> Currencies,
    IReadOnlyList<CpStorageInterfaceType> InterfaceTypes,
    IReadOnlyList<CpStorageUserOption> UserOptions);

/// <summary>Reads for PHP <c>logistics/storages.php</c> (list, bulk delete) and <c>storage.php</c> (editor lookups).</summary>
public interface ICpStorageEditorService
{
    Task<CpStorageList> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default);

    Task<CpStorageEditor?> OpenAsync(long storageId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(IReadOnlyList<long> storageIds, CancellationToken cancellationToken = default);

    Task<CpStorageInterfaceType?> InterfaceTypeAsync(int interfaceTypeId, CancellationToken cancellationToken = default);

    Task<CpStorageGroupsPage> GroupsAsync(CancellationToken cancellationToken = default);
}

public sealed class CpStorageEditorService : ICpStorageEditorService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpStorageEditorService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<CpStorageList> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        pageSize = pageSize < 1 ? 30 : pageSize;
        page = page < 1 ? 1 : page;
        if (!_connections.IsConfigured)
        {
            return new([], 0, page, pageSize);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var total = (int)await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `shop_storages`", cancellationToken).ConfigureAwait(false);
            var rows = new List<CpStorageListRow>();
            var optionsByRow = new Dictionary<long, string>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT s.`id`, IFNULL(s.`name`,''), IFNULL(s.`short_name`,''), IFNULL(s.`hidden`,0), IFNULL(s.`interface_type`,0), IFNULL(s.`connection_options`,''), " +
                    "IFNULL((SELECT t.`name` FROM `shop_storages_interfaces_types` t WHERE t.`id` = s.`interface_type`),''), " +
                    "IFNULL((SELECT t.`product_type` FROM `shop_storages_interfaces_types` t WHERE t.`id` = s.`interface_type`),0) " +
                    "FROM `shop_storages` s ORDER BY s.`id` LIMIT " + ((page - 1) * pageSize).ToString(CultureInfo.InvariantCulture) + ", " + pageSize.ToString(CultureInfo.InvariantCulture);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var id = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                    optionsByRow[id] = reader.GetString(5);
                    rows.Add(new CpStorageListRow(id, reader.GetString(1), reader.GetString(2),
                        Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
                        Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture), reader.GetString(6),
                        Convert.ToInt32(reader.GetValue(7), CultureInfo.InvariantCulture), string.Empty));
                }
            }

            var prices = new Dictionary<long, string>();
            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT `id`, IFNULL(`name`,'') FROM `shop_docpart_prices`";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    prices[Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture)] = reader.GetString(1);
                }
            }
            catch (DbException)
            {
            }

            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].InterfaceType != 2 || !optionsByRow.TryGetValue(rows[i].Id, out var optJson))
                {
                    continue;
                }

                var priceId = ParseOptions(optJson).GetValueOrDefault("price_id", "");
                if (long.TryParse(priceId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid) && prices.TryGetValue(pid, out var pname))
                {
                    rows[i] = rows[i] with { PriceList = priceId + " - " + pname };
                }
            }

            return new(rows, total, page, pageSize);
        }
        catch (DbException)
        {
            return new([], 0, page, pageSize);
        }
    }

    public async Task<CpStorageEditor?> OpenAsync(long storageId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return null;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var name = string.Empty;
        var shortName = string.Empty;
        var currency = 0;
        var interfaceType = 1;
        if (storageId <= 0)
        {
            try
            {
                var shopCurrency = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `value` FROM `config_items` WHERE `name` = ? LIMIT 1"), cancellationToken, "shop_currency").ConfigureAwait(false);
                int.TryParse(shopCurrency, NumberStyles.Integer, CultureInfo.InvariantCulture, out currency);
            }
            catch (DbException)
            {
            }
        }

        var hidden = 0;
        var bgLineColor = 0;
        var usersJson = "[]";
        var optionsJson = "{}";
        if (storageId > 0)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = ErpDb.Positional(
                "SELECT IFNULL(`name`,''), IFNULL(`short_name`,''), IFNULL(`currency`,0), IFNULL(`interface_type`,1), IFNULL(`hidden`,0), IFNULL(`bg_line_color`,0), IFNULL(`users`,''), IFNULL(`connection_options`,'') " +
                "FROM `shop_storages` WHERE `id` = ? LIMIT 1");
            ErpDb.AddParameters(cmd, storageId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            name = reader.GetString(0);
            shortName = reader.GetString(1);
            currency = Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture);
            interfaceType = Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture);
            hidden = Convert.ToInt32(reader.GetValue(4), CultureInfo.InvariantCulture);
            bgLineColor = Convert.ToInt32(reader.GetValue(5), CultureInfo.InvariantCulture);
            usersJson = reader.GetString(6);
            optionsJson = reader.GetString(7);
        }

        var currencies = await CurrenciesAsync(connection, cancellationToken).ConfigureAwait(false);
        var types = await InterfaceTypesAsync(connection, cancellationToken).ConfigureAwait(false);
        var userOptions = await BackendUsersAsync(connection, cancellationToken).ConfigureAwait(false);
        return new CpStorageEditor(storageId, name, shortName, currency, interfaceType, hidden, bgLineColor,
            ParseIdList(usersJson), ParseOptions(optionsJson), currencies, types, userOptions);
    }

    public async Task<CpStorageInterfaceType?> InterfaceTypeAsync(int interfaceTypeId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured || interfaceTypeId <= 0)
        {
            return null;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var types = await InterfaceTypesAsync(connection, cancellationToken).ConfigureAwait(false);
        return types.FirstOrDefault(t => t.Id == interfaceTypeId);
    }

    public async Task<CpStorageGroupsPage> GroupsAsync(CancellationToken cancellationToken = default)
    {
        var all = new List<(long Id, string Name, int InterfaceType)>();
        var groups = new List<CpStorageGroupRow>();
        if (!_connections.IsConfigured)
        {
            return new([], [], []);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`name`,''), IFNULL(`interface_type`,0) FROM `shop_storages` ORDER BY `id`";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    all.Add((Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture), reader.GetString(1), Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture)));
                }
            }

            var names = all.ToDictionary(x => x.Id, x => x.Name);
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`name`,''), IFNULL(`storages`,'') FROM `shop_storages_groups` ORDER BY `order`, `id`";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var members = ParseIdList(reader.GetString(2))
                        .Select(sid => (sid, names.TryGetValue(sid, out var n) ? n : "#" + sid.ToString(CultureInfo.InvariantCulture)))
                        .ToList();
                    groups.Add(new CpStorageGroupRow(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture), reader.GetString(1), members));
                }
            }
        }
        catch (DbException)
        {
        }

        return new(
            all.Where(x => x.InterfaceType is 1 or 2 or 6).ToList(),
            all.Where(x => x.InterfaceType is not (1 or 2 or 6)).Select(x => (x.Id, x.Name)).ToList(),
            groups);
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(IReadOnlyList<long> storageIds, CancellationToken cancellationToken = default)
    {
        var ids = storageIds.Where(i => i > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select at least one warehouse to delete.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var marks = string.Join(",", Enumerable.Repeat("?", ids.Length));
        var args = ids.Cast<object?>().ToArray();
        var writes = await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional("DELETE FROM `shop_storages` WHERE `id` IN (" + marks + ")"), cancellationToken, args).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional("DELETE FROM `shop_offices_storages_map` WHERE `storage_id` IN (" + marks + ")"), cancellationToken, args).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ErpSimpleWriteResult(true, "ok", writes.ToString(CultureInfo.InvariantCulture) + " warehouse(s) deleted.", writes, 0);
    }

    /// <summary>PHP <c>storage.php</c> save_action(): the posted connection option controls become the stored JSON object; checkboxes are 1/0.</summary>
    public static string ConnectionOptionsFromForm(IFormCollection form, CpStorageInterfaceType? type)
    {
        var dict = new Dictionary<string, object>(StringComparer.Ordinal);
        if (type is not null)
        {
            foreach (var f in type.Fields)
            {
                if (f.Type == "hidden")
                {
                    continue;
                }

                var key = "co_" + f.Name;
                if (f.Type == "checkbox")
                {
                    var v = form[key].ToString();
                    dict[f.Name] = v is "1" or "on" or "true" ? 1 : 0;
                }
                else
                {
                    dict[f.Name] = form[key].ToString();
                }
            }
        }

        return JsonSerializer.Serialize(dict);
    }

    internal static IReadOnlyList<long> ParseIdList(string json)
    {
        var list = new List<long>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return list;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in doc.RootElement.EnumerateArray())
                {
                    if (e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out var n))
                    {
                        list.Add(n);
                    }
                    else if (e.ValueKind == JsonValueKind.String && long.TryParse(e.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s))
                    {
                        list.Add(s);
                    }
                }
            }
        }
        catch (JsonException)
        {
            foreach (var part in json.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                {
                    list.Add(n);
                }
            }
        }

        return list;
    }

    internal static IReadOnlyDictionary<string, string> ParseOptions(string json)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return dict;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    dict[p.Name] = p.Value.ValueKind switch
                    {
                        JsonValueKind.String => p.Value.GetString() ?? string.Empty,
                        JsonValueKind.Null => string.Empty,
                        JsonValueKind.True => "1",
                        JsonValueKind.False => "0",
                        _ => p.Value.ToString(),
                    };
                }
            }
        }
        catch (JsonException)
        {
        }

        return dict;
    }

    private static async Task<List<CpStorageOption>> CurrenciesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var list = new List<CpStorageOption>();
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT `iso_code`, IFNULL(`iso_name`,'') FROM `shop_currencies` WHERE `available` = 1 ORDER BY `order`";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add(new CpStorageOption(reader.GetValue(0).ToString() ?? "", reader.GetString(1)));
            }
        }
        catch (DbException)
        {
        }

        return list;
    }

    private static async Task<List<CpStorageInterfaceType>> InterfaceTypesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var raw = new List<(int Id, string Name, int ProductType, string Handler, string Description, string Schema)>();
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "SELECT `id`, REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(IFNULL(`name`,''), 'Веб-сервис', 'API'), 'Веб сервис', 'API'), '(API)', ''), '(Web-Сервис)',''), 'Форум-Авто (forum-auto.ru)', 'API Форум-Авто (forum-auto.ru)'), " +
                "IFNULL(`product_type`,0), IFNULL(`handler_folder`,''), IFNULL(`description`,''), IFNULL(`connection_options`,'') " +
                "FROM `shop_storages_interfaces_types` WHERE `control_available` = 1 ORDER BY 2";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                raw.Add((Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture), reader.GetString(1).Trim(),
                    Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture), reader.GetString(3), reader.GetString(4), reader.GetString(5)));
            }
        }
        catch (DbException)
        {
        }

        var list = new List<CpStorageInterfaceType>();
        foreach (var t in raw)
        {
            var fields = new List<CpStorageConnectionField>();
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(t.Schema) ? "[]" : t.Schema);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var e in doc.RootElement.EnumerateArray())
                    {
                        var fname = Str(e, "name");
                        if (fname.Length == 0) continue;
                        var ftype = Str(e, "type");
                        var options = new List<CpStorageOption>();
                        if (ftype == "select" && e.TryGetProperty("options", out var opts))
                        {
                            if (string.Equals(Str(e, "options_way"), "sql", StringComparison.OrdinalIgnoreCase) && opts.ValueKind == JsonValueKind.String)
                            {
                                options = await OptionsFromSqlAsync(connection, opts.GetString() ?? "", cancellationToken).ConfigureAwait(false);
                            }
                            else if (opts.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var o in opts.EnumerateArray())
                                {
                                    options.Add(new CpStorageOption(Str(o, "value"), Str(o, "caption")));
                                }
                            }
                        }

                        fields.Add(new CpStorageConnectionField(fname, Str(e, "caption"), ftype.Length == 0 ? "text" : ftype, options));
                    }
                }
            }
            catch (JsonException)
            {
            }

            list.Add(new CpStorageInterfaceType(t.Id, t.Name, t.ProductType, t.Handler, t.Description, fields));
        }

        return list;
    }

    /// <summary>Interface schema SQL is operator config the PHP page runs verbatim; only SELECTs are honoured.</summary>
    private static async Task<List<CpStorageOption>> OptionsFromSqlAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        var list = new List<CpStorageOption>();
        if (!sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return list;
        }

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = sql.TrimEnd().TrimEnd(';');
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var v = Ordinal(reader, "value");
            var c = Ordinal(reader, "caption");
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var value = v >= 0 && !reader.IsDBNull(v) ? reader.GetValue(v).ToString() ?? "" : "";
                var caption = c >= 0 && !reader.IsDBNull(c) ? reader.GetValue(c).ToString() ?? "" : value;
                list.Add(new CpStorageOption(value, caption));
            }
        }
        catch (DbException)
        {
        }

        return list;
    }

    /// <summary>storage.php users_selector: users bound to the backend root group or any group nested under it.</summary>
    private static async Task<List<CpStorageUserOption>> BackendUsersAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var list = new List<CpStorageUserOption>();
        try
        {
            var groups = new List<long>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "SELECT `id`, IFNULL(`parent`,0) FROM `groups`";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                var all = new List<(long Id, long Parent)>();
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    all.Add((Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture), Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture)));
                }

                var root = await ErpDb.LongAsync(connection, null, "SELECT IFNULL(MIN(`id`),0) FROM `groups` WHERE `for_backend` = 1", cancellationToken).ConfigureAwait(false);
                if (root > 0)
                {
                    var queue = new Queue<long>();
                    queue.Enqueue(root);
                    while (queue.Count > 0)
                    {
                        var g = queue.Dequeue();
                        if (groups.Contains(g)) continue;
                        groups.Add(g);
                        foreach (var child in all.Where(x => x.Parent == g)) queue.Enqueue(child.Id);
                    }
                }
            }

            if (groups.Count == 0)
            {
                return list;
            }

            await using var users = connection.CreateCommand();
            users.CommandText = ErpDb.Positional(
                "SELECT DISTINCT u.`user_id`, IFNULL(u.`email`,''), IFNULL(u.`phone`,''), " +
                "IFNULL((SELECT p.`data_value` FROM `users_profiles` p WHERE p.`user_id` = u.`user_id` AND p.`data_key` = 'surname' LIMIT 1),''), " +
                "IFNULL((SELECT p.`data_value` FROM `users_profiles` p WHERE p.`user_id` = u.`user_id` AND p.`data_key` = 'name' LIMIT 1),'') " +
                "FROM `users` u WHERE u.`user_id` IN (SELECT b.`user_id` FROM `users_groups_bind` b WHERE b.`group_id` IN (" + string.Join(",", Enumerable.Repeat("?", groups.Count)) + ")) ORDER BY u.`user_id`");
            ErpDb.AddParameters(users, groups.Cast<object?>().ToArray());
            await using var ur = await users.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await ur.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = Convert.ToInt64(ur.GetValue(0), CultureInfo.InvariantCulture);
                var email = ur.GetString(1);
                var phone = ur.GetString(2);
                var contact = email.Length > 0 ? "E-mail: " + email : phone.Length > 0 ? "Phone: " + phone : string.Empty;
                list.Add(new CpStorageUserOption(id, "(" + id.ToString(CultureInfo.InvariantCulture) + ") " + (ur.GetString(3) + " " + ur.GetString(4)).Trim() + " " + contact));
            }
        }
        catch (DbException)
        {
        }

        return list;
    }

    private static int Ordinal(DbDataReader reader, string name)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), name, StringComparison.OrdinalIgnoreCase)) return i;
        }

        return -1;
    }

    private static string Str(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var p)) return string.Empty;
        return p.ValueKind == JsonValueKind.String ? p.GetString() ?? string.Empty : p.ToString();
    }
}
