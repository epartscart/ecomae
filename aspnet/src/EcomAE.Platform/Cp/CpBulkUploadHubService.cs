using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Storefront;

namespace EcomAE.Platform.Cp;

public sealed record CpBulkDashboard(int Total, int Unreviewed, int Today, int Storefront, int AvailableToday);

public sealed record CpBulkHistoryRow(
    long Id,
    long UserId,
    bool CreatedByAdmin,
    long GroupId,
    string FileName,
    string Priority,
    string Source,
    int Uploaded,
    int Available,
    int Cross,
    int Short,
    int Notfound,
    string ReviewedAt,
    long ReviewedBy,
    string Notes,
    long ShopQuoteId,
    long CrmQuoteId,
    int CartAdded,
    string CreatedAt,
    string CustomerLabel);

/// <summary>One decoded <c>result_json</c> row: the option PHP <c>epc_bulk_selected_option</c> would pick.</summary>
public sealed record CpBulkResultLine(
    int Index,
    string Brand,
    string Article,
    int Qty,
    string StatusLabel,
    bool Available,
    string MatchType,
    string Manufacturer,
    string ArticleShow,
    string Name,
    int Exist,
    decimal Price,
    int TimeToExe,
    JsonElement? ProductObject);

public sealed record CpBulkHubUploadDetail(CpBulkHistoryRow Row, IReadOnlyList<CpBulkResultLine> Lines, string Csv);

public sealed record CpBulkCustomer(long UserId, string Email, string Label, long GroupId);

public sealed record CpBulkPriceProfile(long GroupId, string Code, string Caption);

public sealed record CpBulkHistoryFilter(long UserId, string Source, bool Unreviewed, string Query);

public sealed record CpBulkActionResult(bool Succeeded, string Code, string Message, long Id, int Writes, string RedirectTo = "")
{
    public static CpBulkActionResult Fail(string code, string message) => new(false, code, message, 0, 0);
}

public interface ICpBulkUploadHubService
{
    Task<CpBulkDashboard> DashboardAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CpBulkHistoryRow>> ListHistoryAsync(CpBulkHistoryFilter filter, int limit, CancellationToken cancellationToken = default);
    Task<CpBulkHubUploadDetail?> GetUploadAsync(long uploadId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CpBulkCustomer>> SearchCustomersAsync(string query, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CpBulkPriceProfile>> PriceProfilesAsync(CancellationToken cancellationToken = default);
    Task<CpBulkActionResult> ProcessAsync(long customerId, long groupId, string priority, Stream file, string fileName, CancellationToken cancellationToken = default);
    Task<CpBulkActionResult> AddToCartAsync(long uploadId, long customerId, IReadOnlyList<int>? indexes, long adminId, CancellationToken cancellationToken = default);
    Task<CpBulkActionResult> CreateShopQuoteAsync(long uploadId, long customerId, IReadOnlyList<int>? indexes, long adminId, CancellationToken cancellationToken = default);
    Task<CpBulkActionResult> CreateCrmQuoteAsync(long uploadId, long customerId, IReadOnlyList<int>? indexes, long adminId, CancellationToken cancellationToken = default);
}

/// <summary>
/// PHP <c>cp/content/shop/bulk_upload/bulk_upload_hub.php</c> + <c>ajax_bulk_cp.php</c> twin
/// (helpers in <c>content/shop/bulk_upload/epc_bulk_helpers.php</c>): dashboard, history inbox,
/// customer search, CP-side processing (source <c>cp</c>), add-to-cart, shop quote and ERP CRM quote.
/// </summary>
public sealed class CpBulkUploadHubService : ICpBulkUploadHubService
{
    public const int HistoryLimit = 50;
    public const int CustomerLimit = 20;

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IStorefrontBulkUploadCheckService _checker;

    public CpBulkUploadHubService(IErpWriteConnectionFactory connections, IStorefrontBulkUploadCheckService checker)
    {
        _connections = connections;
        _checker = checker;
    }

    public async Task<CpBulkDashboard> DashboardAsync(CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new(0, 0, 0, 0, 0);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var total = await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_bulk_upload_history`", cancellationToken).ConfigureAwait(false);
            var unreviewed = await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_bulk_upload_history` WHERE `cp_reviewed_at` IS NULL", cancellationToken).ConfigureAwait(false);
            var today = await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_bulk_upload_history` WHERE DATE(`created_at`) = CURDATE()", cancellationToken).ConfigureAwait(false);
            var storefront = await ErpDb.LongAsync(connection, null, "SELECT COUNT(*) FROM `epc_bulk_upload_history` WHERE `source` = 'storefront'", cancellationToken).ConfigureAwait(false);
            var available = await ErpDb.LongAsync(connection, null, "SELECT IFNULL(SUM(`available_count`),0) FROM `epc_bulk_upload_history` WHERE DATE(`created_at`) = CURDATE()", cancellationToken).ConfigureAwait(false);
            return new((int)total, (int)unreviewed, (int)today, (int)storefront, (int)available);
        }
        catch (DbException)
        {
            return new(0, 0, 0, 0, 0);
        }
    }

    public static (string Where, object?[] Args) BuildHistoryWhere(CpBulkHistoryFilter filter)
    {
        var where = new List<string> { "1=1" };
        var args = new List<object?>();
        if (filter.UserId > 0)
        {
            where.Add("`user_id` = ?");
            args.Add(filter.UserId);
        }

        if (!string.IsNullOrEmpty(filter.Source))
        {
            where.Add("`source` = ?");
            args.Add(filter.Source);
        }

        if (filter.Unreviewed)
        {
            where.Add("`cp_reviewed_at` IS NULL");
        }

        if (!string.IsNullOrEmpty(filter.Query))
        {
            where.Add("(`file_name` LIKE ? OR CAST(`user_id` AS CHAR) LIKE ?)");
            var like = "%" + filter.Query + "%";
            args.Add(like);
            args.Add(like);
        }

        return (string.Join(" AND ", where), args.ToArray());
    }

    private const string HistoryColumns =
        "`id`, `user_id`, `created_by_admin`, `group_id`, IFNULL(`file_name`,''), IFNULL(`priority`,'price'), IFNULL(`source`,'storefront'), " +
        "`uploaded_count`, `available_count`, `cross_count`, `short_count`, `notfound_count`, " +
        "IFNULL(DATE_FORMAT(`cp_reviewed_at`,'%Y-%m-%d %H:%i'),''), `cp_reviewed_by`, IFNULL(`cp_notes`,''), `shop_quote_id`, `crm_quote_id`, `cart_added_count`, " +
        "IFNULL(DATE_FORMAT(`created_at`,'%Y-%m-%d %H:%i'),'')";

    public async Task<IReadOnlyList<CpBulkHistoryRow>> ListHistoryAsync(CpBulkHistoryFilter filter, int limit, CancellationToken cancellationToken = default)
    {
        var rows = new List<CpBulkHistoryRow>();
        if (!_connections.IsConfigured)
        {
            return rows;
        }

        limit = Math.Clamp(limit, 1, 100);
        var (where, args) = BuildHistoryWhere(filter);
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var c = connection.CreateCommand();
            c.CommandText = ErpDb.Positional(
                "SELECT " + HistoryColumns + " FROM `epc_bulk_upload_history` WHERE " + where +
                " ORDER BY `id` DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture));
            ErpDb.AddParameters(c, args);
            var raw = new List<CpBulkHistoryRow>();
            await using (var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    raw.Add(ReadHistory(r, string.Empty));
                }
            }

            foreach (var row in raw)
            {
                rows.Add(row with { CustomerLabel = await CustomerLabelAsync(connection, row.UserId, cancellationToken).ConfigureAwait(false) });
            }

            return rows;
        }
        catch (DbException)
        {
            return rows;
        }
    }

    public async Task<CpBulkHubUploadDetail?> GetUploadAsync(long uploadId, CancellationToken cancellationToken = default)
    {
        if (uploadId <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            return await LoadUploadAsync(connection, uploadId, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return null;
        }
    }

    private static async Task<CpBulkHubUploadDetail?> LoadUploadAsync(DbConnection connection, long uploadId, CancellationToken cancellationToken)
    {
        await using var c = connection.CreateCommand();
        c.CommandText = ErpDb.Positional(
            "SELECT " + HistoryColumns + ", IFNULL(`result_json`,''), IFNULL(`csv_result`,'') FROM `epc_bulk_upload_history` WHERE `id` = ? LIMIT 1");
        ErpDb.AddParameters(c, uploadId);
        CpBulkHistoryRow? row = null;
        var json = string.Empty;
        var csv = string.Empty;
        await using (var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                row = ReadHistory(r, string.Empty);
                json = r.GetString(19);
                csv = r.GetString(20);
            }
        }

        if (row is null)
        {
            return null;
        }

        row = row with { CustomerLabel = await CustomerLabelAsync(connection, row.UserId, cancellationToken).ConfigureAwait(false) };
        return new CpBulkHubUploadDetail(row, ParseLines(json), csv);
    }

    private static CpBulkHistoryRow ReadHistory(DbDataReader r, string label) => new(
        r.GetInt64(0),
        r.GetInt64(1),
        Convert.ToInt32(r.GetValue(2), CultureInfo.InvariantCulture) != 0,
        r.GetInt64(3),
        r.GetString(4),
        r.GetString(5),
        r.GetString(6),
        Convert.ToInt32(r.GetValue(7), CultureInfo.InvariantCulture),
        Convert.ToInt32(r.GetValue(8), CultureInfo.InvariantCulture),
        Convert.ToInt32(r.GetValue(9), CultureInfo.InvariantCulture),
        Convert.ToInt32(r.GetValue(10), CultureInfo.InvariantCulture),
        Convert.ToInt32(r.GetValue(11), CultureInfo.InvariantCulture),
        r.GetString(12),
        r.GetInt64(13),
        r.GetString(14),
        r.GetInt64(15),
        r.GetInt64(16),
        Convert.ToInt32(r.GetValue(17), CultureInfo.InvariantCulture),
        r.GetString(18),
        label);

    /// <summary>Decode <c>result_json</c> (PHP or ASP.NET writer) into display lines; tolerant of malformed JSON.</summary>
    public static IReadOnlyList<CpBulkResultLine> ParseLines(string json)
    {
        var lines = new List<CpBulkResultLine>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return lines;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return lines;
            }

            var index = 0;
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                if (row.ValueKind == JsonValueKind.Object)
                {
                    lines.Add(ToLine(index, row));
                }

                index++;
            }
        }
        catch (JsonException)
        {
        }

        return lines;
    }

    private static CpBulkResultLine ToLine(int index, JsonElement row)
    {
        var input = Prop(row, "input");
        var option = SelectedOption(row);
        var available = Bool(row, "available");
        return new CpBulkResultLine(
            index,
            Str(input, "brand"),
            Str(input, "article"),
            Int(input, "qty"),
            Str(row, "status_label"),
            available,
            option is null ? string.Empty : Str(option.Value, "match_type"),
            option is null ? string.Empty : Str(option.Value, "manufacturer"),
            option is null ? string.Empty : Str(option.Value, "article_show"),
            option is null ? string.Empty : Str(option.Value, "name"),
            option is null ? 0 : Int(option.Value, "exist"),
            option is null ? 0m : Dec(option.Value, "price"),
            option is null ? 0 : Int(option.Value, "time_to_exe"),
            option is null ? null : PropOrNull(option.Value, "product_object")?.Clone());
    }

    /// <summary>PHP <c>epc_bulk_selected_option</c>: selected exact, selected cross, exact, cross.</summary>
    public static JsonElement? SelectedOption(JsonElement row)
    {
        var exact = PropOrNull(row, "exact");
        var cross = PropOrNull(row, "cross");
        if (exact is { ValueKind: JsonValueKind.Object } e && Bool(e, "selected"))
        {
            return e;
        }

        if (cross is { ValueKind: JsonValueKind.Object } c && Bool(c, "selected"))
        {
            return c;
        }

        if (exact is { ValueKind: JsonValueKind.Object } e2)
        {
            return e2;
        }

        if (cross is { ValueKind: JsonValueKind.Object } c2)
        {
            return c2;
        }

        return null;
    }

    /// <summary>PHP <c>epc_bulk_collect_product_objects</c>.</summary>
    public static IReadOnlyList<JsonElement> CollectProductObjects(string json, IReadOnlyList<int>? indexes)
    {
        var out_ = new List<JsonElement>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return out_;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return out_;
            }

            var i = 0;
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var idx = i++;
                if (indexes is not null && !indexes.Contains(idx))
                {
                    continue;
                }

                if (row.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var opt = SelectedOption(row);
                if (opt is null)
                {
                    continue;
                }

                var po = PropOrNull(opt.Value, "product_object");
                if (po is { ValueKind: JsonValueKind.Object } p && p.EnumerateObject().Any())
                {
                    out_.Add(p.Clone());
                }
            }
        }
        catch (JsonException)
        {
        }

        return out_;
    }

    private static JsonElement Prop(JsonElement el, string name)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) ? v : default;

    private static JsonElement? PropOrNull(JsonElement el, string name)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v : null;

    public static string Str(JsonElement el, string name)
    {
        var v = Prop(el, name);
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? string.Empty,
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True => "1",
            JsonValueKind.False => "0",
            _ => string.Empty,
        };
    }

    public static int Int(JsonElement el, string name)
    {
        var v = Prop(el, name);
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n))
        {
            return n;
        }

        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d))
        {
            return (int)d;
        }

        return v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : 0;
    }

    public static decimal Dec(JsonElement el, string name)
    {
        var v = Prop(el, name);
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d))
        {
            return d;
        }

        return v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var s) ? s : 0m;
    }

    private static bool Bool(JsonElement el, string name)
    {
        var v = Prop(el, name);
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => v.TryGetDecimal(out var d) && d != 0,
            JsonValueKind.String => v.GetString() is { Length: > 0 } s && s != "0",
            _ => false,
        };
    }

    /// <summary>PHP <c>epc_bulk_customer_label</c>.</summary>
    public static async Task<string> CustomerLabelAsync(DbConnection connection, long userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return "Unassigned / admin preview";
        }

        var email = string.Empty;
        var phone = string.Empty;
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = ErpDb.Positional("SELECT IFNULL(`email`,''), IFNULL(`phone`,'') FROM `users` WHERE `user_id` = ? LIMIT 1");
            ErpDb.AddParameters(c, userId);
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                email = r.GetString(0);
                phone = r.GetString(1);
            }
        }

        var name = string.Empty;
        var company = string.Empty;
        try
        {
            await using var p = connection.CreateCommand();
            p.CommandText = ErpDb.Positional(
                "SELECT `data_key`, IFNULL(`data_value`,'') FROM `users_profiles` WHERE `user_id` = ? AND `data_key` IN ('name','surname','patronymic','company','company_name')");
            ErpDb.AddParameters(p, userId);
            var parts = new List<string>();
            await using var r = await p.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var key = r.GetString(0);
                var val = r.GetString(1).Trim();
                if (val.Length == 0)
                {
                    continue;
                }

                if (key is "company" or "company_name")
                {
                    company = val;
                }
                else
                {
                    parts.Add(val);
                }
            }

            name = string.Join(' ', parts).Trim();
            if (name.Length == 0 && company.Length > 0)
            {
                name = company;
            }
        }
        catch (DbException)
        {
            name = string.Empty;
        }

        var bits = new List<string>();
        if (name.Length > 0)
        {
            bits.Add(name);
        }

        if (email.Length > 0)
        {
            bits.Add(email);
        }
        else if (phone.Length > 0)
        {
            bits.Add(phone);
        }

        bits.Add("#" + userId.ToString(CultureInfo.InvariantCulture));
        return string.Join(" · ", bits);
    }

    public async Task<IReadOnlyList<CpBulkCustomer>> SearchCustomersAsync(string query, int limit, CancellationToken cancellationToken = default)
    {
        var found = new List<CpBulkCustomer>();
        if (!_connections.IsConfigured)
        {
            return found;
        }

        query = (query ?? string.Empty).Trim();
        limit = Math.Clamp(limit, 1, 50);
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var hits = new List<(long Id, string Email)>();
            await using (var c = connection.CreateCommand())
            {
                if (query.Length == 0)
                {
                    c.CommandText = "SELECT u.`user_id`, IFNULL(u.`email`,'') FROM `users` u WHERE u.`user_id` > 0 ORDER BY u.`user_id` DESC LIMIT " + limit.ToString(CultureInfo.InvariantCulture);
                }
                else if (query.All(char.IsAsciiDigit))
                {
                    c.CommandText = ErpDb.Positional("SELECT u.`user_id`, IFNULL(u.`email`,'') FROM `users` u WHERE u.`user_id` = ? LIMIT 1");
                    ErpDb.AddParameters(c, long.Parse(query, CultureInfo.InvariantCulture));
                }
                else
                {
                    c.CommandText = ErpDb.Positional(
                        "SELECT DISTINCT u.`user_id`, IFNULL(u.`email`,'') FROM `users` u LEFT JOIN `users_profiles` p ON p.`user_id` = u.`user_id` " +
                        "WHERE u.`user_id` > 0 AND (u.`email` LIKE ? OR u.`phone` LIKE ? OR p.`data_value` LIKE ?) ORDER BY u.`user_id` DESC LIMIT " +
                        limit.ToString(CultureInfo.InvariantCulture));
                    var like = "%" + query + "%";
                    ErpDb.AddParameters(c, like, like, like);
                }

                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    hits.Add((r.GetInt64(0), r.GetString(1)));
                }
            }

            foreach (var (id, email) in hits)
            {
                found.Add(new CpBulkCustomer(
                    id,
                    email,
                    await CustomerLabelAsync(connection, id, cancellationToken).ConfigureAwait(false),
                    await CustomerGroupIdAsync(connection, id, cancellationToken).ConfigureAwait(false)));
            }

            return found;
        }
        catch (DbException)
        {
            return found;
        }
    }

    /// <summary>PHP <c>epc_bulk_customer_group_id</c> fallback: latest <c>users_groups_bind</c> row.</summary>
    public static async Task<long> CustomerGroupIdAsync(DbConnection connection, long userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return 0;
        }

        try
        {
            return await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT IFNULL(`group_id`,0) FROM `users_groups_bind` WHERE `user_id` = ? ORDER BY `record_id` DESC LIMIT 1"),
                cancellationToken, userId).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return 0;
        }
    }

    public async Task<IReadOnlyList<CpBulkPriceProfile>> PriceProfilesAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<CpBulkPriceProfile>();
        if (!_connections.IsConfigured)
        {
            return list;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var c = connection.CreateCommand();
            c.CommandText = "SELECT pp.`group_id`, IFNULL(pp.`code`,''), IFNULL(g.`value`,'') FROM `epc_price_profiles` pp INNER JOIN `groups` g ON g.`id` = pp.`group_id` ORDER BY pp.`id` ASC";
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var code = r.GetString(1);
                var value = r.GetString(2);
                list.Add(new CpBulkPriceProfile(r.GetInt64(0), code, value.Length > 0 ? value : code));
            }

            return list;
        }
        catch (DbException)
        {
            return list;
        }
    }

    public async Task<CpBulkActionResult> ProcessAsync(long customerId, long groupId, string priority, Stream file, string fileName, CancellationToken cancellationToken = default)
    {
        if (customerId <= 0)
        {
            return CpBulkActionResult.Fail("customer", "Select a customer first");
        }

        if (!_connections.IsConfigured)
        {
            return CpBulkActionResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (groupId <= 0)
            {
                groupId = await CustomerGroupIdAsync(connection, customerId, cancellationToken).ConfigureAwait(false);
            }

            if (groupId <= 0)
            {
                return CpBulkActionResult.Fail("group", "Customer has no price group — pick a price profile");
            }

            var safePriority = StorefrontBulkUploadHistoryWriteService.NormalizePriority(priority);
            var processed = await _checker.ProcessAsync(file, fileName, safePriority, cancellationToken).ConfigureAwait(false);
            if (!processed.Status || processed.Rows.Count == 0)
            {
                return CpBulkActionResult.Fail("rows", processed.Message ?? "No valid rows found");
            }

            var summary = processed.Summary;
            var writes = await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    "INSERT INTO `epc_bulk_upload_history` (`user_id`, `created_by_admin`, `group_id`, `file_name`, `priority`, `source`, " +
                    "`uploaded_count`, `available_count`, `cross_count`, `short_count`, `notfound_count`, `result_json`, `csv_result`, `created_at`, `updated_at`) " +
                    "VALUES (?, 1, ?, ?, ?, 'cp', ?, ?, ?, ?, ?, ?, ?, NOW(), NOW())"),
                cancellationToken,
                customerId,
                groupId,
                StorefrontBulkUploadHistoryWriteService.NormalizeFileName(fileName),
                safePriority,
                summary.Uploaded,
                summary.Available,
                summary.Cross,
                summary.Short,
                summary.Notfound,
                StorefrontBulkUploadHistoryWriteService.SerializeRows(processed.Rows),
                processed.Csv).ConfigureAwait(false);
            if (writes <= 0)
            {
                return CpBulkActionResult.Fail("db", "History row was not inserted.");
            }

            var uploadId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return new CpBulkActionResult(
                true, "ok",
                "Processed " + summary.Uploaded.ToString(CultureInfo.InvariantCulture) + " lines · " + summary.Available.ToString(CultureInfo.InvariantCulture) + " available",
                uploadId, writes);
        }
        catch (DbException ex)
        {
            return CpBulkActionResult.Fail("db", ex.Message);
        }
    }

    private async Task<(DbConnection Connection, CpBulkHubUploadDetail Upload, string Json, IReadOnlyList<JsonElement> Products, CpBulkActionResult? Error)> PrepareAsync(
        long uploadId, IReadOnlyList<int>? indexes, CancellationToken cancellationToken)
    {
        var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var json = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `result_json` FROM `epc_bulk_upload_history` WHERE `id` = ? LIMIT 1"), cancellationToken, uploadId).ConfigureAwait(false);
        var upload = await LoadUploadAsync(connection, uploadId, cancellationToken).ConfigureAwait(false);
        if (upload is null)
        {
            return (connection, null!, string.Empty, Array.Empty<JsonElement>(), CpBulkActionResult.Fail("not_found", "Upload not found"));
        }

        var products = CollectProductObjects(json ?? string.Empty, indexes);
        if (products.Count == 0)
        {
            return (connection, upload, json ?? string.Empty, products, CpBulkActionResult.Fail("empty", "No available lines selected"));
        }

        return (connection, upload, json ?? string.Empty, products, null);
    }

    private static async Task MarkReviewedAsync(DbConnection connection, DbTransaction tx, long uploadId, long adminId, string notes, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection, tx,
            ErpDb.Positional("UPDATE `epc_bulk_upload_history` SET `cp_reviewed_at` = NOW(), `cp_reviewed_by` = ?, `cp_notes` = ?, `updated_at` = NOW() WHERE `id` = ? LIMIT 1"),
            cancellationToken, adminId, CpBulkUploadWriteService.NormalizeNotes(notes), uploadId).ConfigureAwait(false);
    }

    public async Task<CpBulkActionResult> AddToCartAsync(long uploadId, long customerId, IReadOnlyList<int>? indexes, long adminId, CancellationToken cancellationToken = default)
    {
        if (uploadId <= 0)
        {
            return CpBulkActionResult.Fail("invalid", "Upload id required");
        }

        if (!_connections.IsConfigured)
        {
            return CpBulkActionResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            var (connection, upload, _, products, error) = await PrepareAsync(uploadId, indexes, cancellationToken).ConfigureAwait(false);
            await using (connection)
            {
                if (error is not null)
                {
                    return error;
                }

                var userId = customerId > 0 ? customerId : upload.Row.UserId;
                if (userId <= 0)
                {
                    return CpBulkActionResult.Fail("customer", "Select a customer before adding to cart.");
                }

                await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                var added = 0;
                var skipped = 0;
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                foreach (var po in products)
                {
                    var price = Dec(po, "price");
                    var article = Str(po, "article");
                    if (article.Length == 0 || price <= 0)
                    {
                        skipped++;
                        continue;
                    }

                    var manufacturer = Str(po, "manufacturer");
                    var officeId = Int(po, "office_id");
                    var storageId = Int(po, "storage_id");
                    var dup = await ErpDb.LongAsync(
                        connection, tx,
                        ErpDb.Positional(
                            "SELECT COUNT(*) FROM `shop_carts` WHERE `user_id` = ? AND `product_type` = 2 AND `t2_manufacturer` = ? AND `t2_article` = ? " +
                            "AND `t2_office_id` = ? AND `t2_storage_id` = ? AND CAST(`price` AS DECIMAL(12,2)) = CAST(? AS DECIMAL(12,2))"),
                        cancellationToken, userId, manufacturer, article, officeId, storageId, price).ConfigureAwait(false);
                    if (dup > 0)
                    {
                        skipped++;
                        continue;
                    }

                    var tte = Int(po, "time_to_exe");
                    var articleShow = Str(po, "article_show");
                    await ErpDb.ExecuteAsync(
                        connection, tx,
                        ErpDb.Positional(
                            "INSERT INTO `shop_carts` (`product_type`, `price`, `count_need`, `time`, `user_id`, `session_id`, " +
                            "`t2_manufacturer`, `t2_article`, `t2_article_show`, `t2_name`, `t2_exist`, `t2_time_to_exe`, `t2_time_to_exe_guaranteed`, `t2_storage`, `t2_min_order`, " +
                            "`t2_probability`, `t2_markup`, `t2_price_purchase`, `t2_office_id`, `t2_storage_id`, `t2_product_json`, `t2_json_params`) " +
                            "VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
                        cancellationToken,
                        2, price, Math.Max(1, Int(po, "count_need")), now, userId, 0,
                        manufacturer, article, articleShow.Length > 0 ? articleShow : article, Str(po, "name"), Int(po, "exist"),
                        tte, po.TryGetProperty("time_to_exe_guaranteed", out _) ? Int(po, "time_to_exe_guaranteed") : tte, Str(po, "storage"), Math.Max(1, Int(po, "min_order")),
                        po.TryGetProperty("probability", out _) ? Int(po, "probability") : 100, Int(po, "markup"), Dec(po, "price_purchase"), officeId, storageId,
                        po.GetRawText(), Str(po, "json_params")).ConfigureAwait(false);
                    added++;
                }

                await ErpDb.ExecuteAsync(
                    connection, tx,
                    ErpDb.Positional("UPDATE `epc_bulk_upload_history` SET `cart_added_count` = `cart_added_count` + ?, `updated_at` = NOW() WHERE `id` = ?"),
                    cancellationToken, added, uploadId).ConfigureAwait(false);
                await MarkReviewedAsync(connection, tx, uploadId, adminId, "Added " + added.ToString(CultureInfo.InvariantCulture) + " lines to customer cart", cancellationToken).ConfigureAwait(false);
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                var message = "Added " + added.ToString(CultureInfo.InvariantCulture) + " to cart" + (skipped > 0 ? " (" + skipped.ToString(CultureInfo.InvariantCulture) + " skipped)" : string.Empty);
                return new CpBulkActionResult(true, "ok", message, uploadId, added + 2);
            }
        }
        catch (DbException ex)
        {
            return CpBulkActionResult.Fail("db", ex.Message);
        }
    }

    public async Task<CpBulkActionResult> CreateShopQuoteAsync(long uploadId, long customerId, IReadOnlyList<int>? indexes, long adminId, CancellationToken cancellationToken = default)
    {
        if (uploadId <= 0)
        {
            return CpBulkActionResult.Fail("invalid", "Upload id required");
        }

        if (!_connections.IsConfigured)
        {
            return CpBulkActionResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            var (connection, upload, _, products, error) = await PrepareAsync(uploadId, indexes, cancellationToken).ConfigureAwait(false);
            await using (connection)
            {
                if (error is not null)
                {
                    return error;
                }

                var userId = customerId > 0 ? customerId : upload.Row.UserId;
                if (userId <= 0)
                {
                    return CpBulkActionResult.Fail("customer", "Select a customer before creating a quote.");
                }

                await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                await ErpDb.ExecuteAsync(
                    connection, tx,
                    ErpDb.Positional(
                        "INSERT INTO `shop_quote_requests` (`user_id`, `session_id`, `status`, `time_created`, `time_updated`, `time_submitted`, `admin_note`) VALUES (?, 0, 'quoted', ?, ?, ?, ?)"),
                    cancellationToken, userId, now, now, now, "From bulk upload #" + uploadId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                var quoteId = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
                var lines = 0;
                foreach (var po in products)
                {
                    var show = Str(po, "article_show");
                    var note = (Str(po, "manufacturer") + " " + (show.Length > 0 ? show : Str(po, "article"))).Trim();
                    await ErpDb.ExecuteAsync(
                        connection, tx,
                        ErpDb.Positional(
                            "INSERT INTO `shop_quote_items` (`quote_id`, `product_type`, `product_object_json`, `count_need`, `quoted_price`, `quoted_time_to_exe`, `line_admin_note`) VALUES (?, 2, ?, ?, ?, ?, ?)"),
                        cancellationToken, quoteId, po.GetRawText(), Math.Max(1, Int(po, "count_need")), Dec(po, "price"), Int(po, "time_to_exe"),
                        CpCrmQuoteWriteService.Clip(note, 512)).ConfigureAwait(false);
                    lines++;
                }

                await ErpDb.ExecuteAsync(
                    connection, tx,
                    ErpDb.Positional("UPDATE `epc_bulk_upload_history` SET `shop_quote_id` = ?, `updated_at` = NOW() WHERE `id` = ?"),
                    cancellationToken, quoteId, uploadId).ConfigureAwait(false);
                await MarkReviewedAsync(connection, tx, uploadId, adminId, "Shop quote #" + quoteId.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new CpBulkActionResult(
                    true, "ok",
                    "Shop quote #" + quoteId.ToString(CultureInfo.InvariantCulture) + " created (" + lines.ToString(CultureInfo.InvariantCulture) + " lines)",
                    quoteId, lines + 3,
                    "/cp/quote-requests-app?quote_id=" + quoteId.ToString(CultureInfo.InvariantCulture));
            }
        }
        catch (DbException ex)
        {
            return CpBulkActionResult.Fail("db", ex.Message);
        }
    }

    public async Task<CpBulkActionResult> CreateCrmQuoteAsync(long uploadId, long customerId, IReadOnlyList<int>? indexes, long adminId, CancellationToken cancellationToken = default)
    {
        if (uploadId <= 0)
        {
            return CpBulkActionResult.Fail("invalid", "Upload id required");
        }

        if (!_connections.IsConfigured)
        {
            return CpBulkActionResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            var (connection, upload, _, products, error) = await PrepareAsync(uploadId, indexes, cancellationToken).ConfigureAwait(false);
            await using (connection)
            {
                if (error is not null)
                {
                    return error;
                }

                var userId = customerId > 0 ? customerId : upload.Row.UserId;
                if (userId <= 0)
                {
                    return CpBulkActionResult.Fail("customer", "Select a customer before creating an ERP quote.");
                }

                await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var count = await ErpDb.LongAsync(connection, tx, "SELECT COUNT(*) FROM `epc_crm_quotes`", cancellationToken).ConfigureAwait(false);
                var number = CpCrmQuoteWriteService.NextQuoteNumber(count + 1, DateTimeOffset.UtcNow);
                await ErpDb.ExecuteAsync(
                    connection, tx,
                    ErpDb.Positional(
                        "INSERT INTO `epc_crm_quotes` (`opportunity_id`, `lead_id`, `customer_user_id`, `quote_number`, `status`, `subtotal`, `notes`, `time_created`, `time_updated`) VALUES (0, 0, ?, ?, 'draft', 0, ?, ?, ?)"),
                    cancellationToken, userId, CpCrmQuoteWriteService.Clip(number, 32), "From CP bulk upload #" + uploadId.ToString(CultureInfo.InvariantCulture), now, now).ConfigureAwait(false);
                var quoteId = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
                var sort = 0;
                var lines = 0;
                foreach (var po in products)
                {
                    var show = Str(po, "article_show");
                    var desc = (Str(po, "manufacturer") + " " + (show.Length > 0 ? show : Str(po, "article")) + " — " + Str(po, "name")).Trim();
                    if (desc.Length == 0)
                    {
                        desc = "Spare part";
                    }

                    await ErpDb.ExecuteAsync(
                        connection, tx,
                        ErpDb.Positional("INSERT INTO `epc_crm_quote_lines` (`quote_id`, `description`, `qty`, `unit_price`, `sort_order`) VALUES (?, ?, ?, ?, ?)"),
                        cancellationToken, quoteId, CpCrmQuoteWriteService.Clip(desc, 512), Math.Max(0.001m, Dec(po, "count_need") == 0 ? 1m : Dec(po, "count_need")), Math.Max(0m, Dec(po, "price")), sort++).ConfigureAwait(false);
                    lines++;
                }

                await ErpDb.ExecuteAsync(
                    connection, tx,
                    ErpDb.Positional("UPDATE `epc_crm_quotes` SET `subtotal` = (SELECT IFNULL(SUM(`qty` * `unit_price`), 0) FROM `epc_crm_quote_lines` WHERE `quote_id` = ?), `time_updated` = ? WHERE `id` = ?"),
                    cancellationToken, quoteId, now, quoteId).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection, tx,
                    ErpDb.Positional("UPDATE `epc_bulk_upload_history` SET `crm_quote_id` = ?, `updated_at` = NOW() WHERE `id` = ?"),
                    cancellationToken, quoteId, uploadId).ConfigureAwait(false);
                await MarkReviewedAsync(connection, tx, uploadId, adminId, "ERP CRM quote #" + quoteId.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new CpBulkActionResult(
                    true, "ok",
                    "ERP quote #" + quoteId.ToString(CultureInfo.InvariantCulture) + " created (" + lines.ToString(CultureInfo.InvariantCulture) + " lines)",
                    quoteId, lines + 4,
                    "/erp/sales-quotations-app?quote_id=" + quoteId.ToString(CultureInfo.InvariantCulture));
            }
        }
        catch (DbException ex)
        {
            return CpBulkActionResult.Fail("db", ex.Message);
        }
    }
}
