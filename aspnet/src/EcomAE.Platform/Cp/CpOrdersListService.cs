using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Cp;

/// <summary>PHP shop/order_process/orders.php filter (cookie orders_filter + orders_sort + orders_need_page), carried on the query string.</summary>
public sealed record CpOrdersFilter(
    string Tab,
    long TimeFrom,
    long TimeTo,
    long OrderId,
    IReadOnlyList<int> Status,
    IReadOnlyList<int> Paid,
    IReadOnlyList<int> PaidType,
    IReadOnlyList<int> Office,
    string Customer,
    long CustomerId,
    int Viewed,
    string Phone,
    string Article,
    string Sort,
    bool Desc,
    int Page)
{
    public static readonly string[] SortFields =
    [
        "id", "time", "last_modified", "count_items", "price_sum", "price_purchase", "profit", "paid",
        "paid_type", "status", "obtain_caption", "customer", "checks_count", "office_id",
    ];

    public bool HasSearch =>
        TimeFrom > 0 || TimeTo > 0 || OrderId > 0 || Paid.Count > 0 || PaidType.Count > 0 || Office.Count > 0
        || Customer.Length > 0 || CustomerId > 0 || Viewed >= 0 || Phone.Length > 0 || Article.Length > 0;
}

public sealed record CpOrdersRow(
    long Id,
    long Time,
    int Paid,
    int PaidType,
    int Status,
    string ObtainCaption,
    long UserId,
    string Customer,
    long OfficeId,
    int CountItems,
    decimal PriceSum,
    decimal PricePurchase,
    decimal Profit,
    int ViewedFlag,
    int CountNotViewedMsg,
    int ChecksCount,
    long LastModified);

public sealed record CpOrdersItemRow(long Id, long OrderId, string Brand, string Article, string Name, decimal Price, decimal CountNeed, int Status);

public sealed record CpOrderStatusRef(int Id, string Name, string Color, bool ForFinish, bool ForInverse, bool ForCreated);

public sealed record CpOrdersTotals(int OrdersCount, decimal PriceSumTotal, decimal ProfitSumTotal, decimal PurchaseSumTotal);

public sealed record CpOrdersList(
    IReadOnlyList<CpOrdersRow> Rows,
    int Total,
    int Page,
    int PageSize,
    CpOrdersTotals Totals,
    IReadOnlyList<CpOrderStatusRef> Statuses,
    IReadOnlyList<(int Id, string Name)> PaidTypes,
    IReadOnlyList<(long Id, string Caption)> Offices,
    IReadOnlyDictionary<long, IReadOnlyList<CpOrdersItemRow>> Items,
    IReadOnlyList<int> OpenStatusIds,
    IReadOnlyList<int> CompletedStatusIds);

public interface ICpOrdersListService
{
    Task<CpOrdersList> ListAsync(CpOrdersFilter filter, int managerId, int pageSize, CancellationToken cancellationToken = default);
}

public sealed partial class CpOrdersListService : ICpOrdersListService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpOrdersListService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static CpOrdersFilter ReadFilter(HttpRequest request)
    {
        var q = request.Query;
        var tab = q["tab"].ToString();
        if (tab is not ("open" or "completed" or "all"))
        {
            tab = "open";
        }

        var sort = q["sort"].ToString();
        if (!CpOrdersFilter.SortFields.Contains(sort, StringComparer.Ordinal))
        {
            sort = "id";
        }

        return new CpOrdersFilter(
            tab,
            ReadLong(q["time_from"]),
            ReadLong(q["time_to"]),
            ReadLong(q["order_id_f"]),
            ReadInts(q["status"]),
            ReadInts(q["paid"]),
            ReadInts(q["paid_type"]),
            ReadInts(q["office"]),
            q["customer"].ToString().Trim(),
            ReadLong(q["customer_id"]),
            ReadInts(q["viewed"]) is [var v] ? v : -1,
            q["phone"].ToString().Trim(),
            q["article"].ToString().Trim(),
            sort,
            !string.Equals(q["dir"].ToString(), "asc", StringComparison.OrdinalIgnoreCase),
            (int)Math.Max(1, ReadLong(q["page"])));
    }

    public async Task<CpOrdersList> ListAsync(CpOrdersFilter filter, int managerId, int pageSize, CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 500);
        var statuses = new List<CpOrderStatusRef>();
        var paidTypes = new List<(int, string)>();
        var offices = new List<(long, string)>();
        var empty = new CpOrdersList([], 0, 1, pageSize, new(0, 0, 0, 0), statuses, paidTypes, offices,
            new Dictionary<long, IReadOnlyList<CpOrdersItemRow>>(), [], []);
        if (!_connections.IsConfigured)
        {
            return empty;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using (var c = connection.CreateCommand())
        {
            c.CommandText = "SELECT `id`, IFNULL(`name`,''), IFNULL(`color`,''), IFNULL(`for_finish`,0), IFNULL(`for_inverse`,0), IFNULL(`for_created`,0) FROM `shop_orders_statuses_ref` ORDER BY `order` ASC, `id` ASC";
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                statuses.Add(new(I(r, 0), r.GetString(1), r.GetString(2), I(r, 3) == 1, I(r, 4) == 1, I(r, 5) == 1));
            }
        }

        var openIds = statuses.Where(s => !s.ForFinish && !s.ForInverse).Select(s => s.Id).ToList();
        var completedIds = statuses.Where(s => s.ForFinish).Select(s => s.Id).ToList();

        await using (var c = connection.CreateCommand())
        {
            c.CommandText = "SELECT `id`, IFNULL(`name`,'') FROM `shop_orders_paid_type` WHERE `active` = 1 ORDER BY `order`";
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                paidTypes.Add((I(r, 0), r.GetString(1)));
            }
        }

        await using (var c = connection.CreateCommand())
        {
            c.CommandText = "SELECT `id`, IFNULL(`caption`,'') FROM `shop_offices` ORDER BY `id`";
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                offices.Add((L(r, 0), r.GetString(1)));
            }
        }

        var notCount = new List<int>();
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE IFNULL(`count_flag`,1) = 0";
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                notCount.Add(I(r, 0));
            }
        }

        var profileFields = new List<string>();
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = "SELECT `name` FROM `reg_fields` WHERE `to_users_table` = 1";
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var name = r.GetString(0);
                if (SafeName().IsMatch(name))
                {
                    profileFields.Add(name);
                }
            }
        }

        var customerSql = CustomerSql(profileFields);

        // Tab normalisation (epc_orders_ws_normalize_filter_for_tab): open → open statuses unless a status filter is set; completed → finished statuses.
        var statusIds = filter.Status.ToList();
        if (filter.Tab == "open" && statusIds.Count == 0)
        {
            statusIds = openIds;
        }
        else if (filter.Tab == "completed")
        {
            statusIds = completedIds;
        }

        var where = new List<string>();
        var args = new List<object>();
        if (filter.TimeFrom > 0) { where.Add("`time` >= ?"); args.Add(filter.TimeFrom); }
        if (filter.TimeTo > 0)
        {
            var to = filter.TimeTo;
            if (to % 86400 == 0)
            {
                to += 86399;
            }

            where.Add("`time` <= ?");
            args.Add(to);
        }

        if (filter.OrderId > 0) { where.Add("`id` = ?"); args.Add(filter.OrderId); }
        AddIn(where, args, "`status`", statusIds);
        AddIn(where, args, "`paid`", filter.Paid);
        if (filter.Customer.Length > 0) { where.Add(customerSql + " LIKE ?"); args.Add("%" + filter.Customer + "%"); }
        if (filter.CustomerId > 0) { where.Add("`user_id` = ?"); args.Add(filter.CustomerId); }
        if (filter.Viewed is 0 or 1)
        {
            where.Add("IFNULL((SELECT `viewed_flag` FROM `shop_orders_viewed` WHERE `order_id` = `shop_orders`.`id` AND `user_id` = ? LIMIT 1), 1) = ?");
            args.Add(managerId);
            args.Add(filter.Viewed);
        }

        AddIn(where, args, "`paid_type`", filter.PaidType);
        AddIn(where, args, "`office_id`", filter.Office);
        if (filter.Phone.Length > 0) { where.Add(customerSql + " LIKE ?"); args.Add("%" + filter.Phone + "%"); }
        if (filter.Article.Length > 0)
        {
            where.Add("`id` IN (SELECT DISTINCT `order_id` FROM `shop_orders_items` WHERE `t2_article` = ?)");
            args.Add(filter.Article);
        }

        var whereSql = where.Count == 0 ? "" : " WHERE " + string.Join(" AND ", where);

        var notCountAnd = string.Concat(notCount.Select(s => " AND `status` != " + s.ToString(CultureInfo.InvariantCulture)));
        var notCountBare = notCount.Count == 0 ? "1=1" : string.Join(" AND ", notCount.Select(s => "`status` != " + s.ToString(CultureInfo.InvariantCulture)));
        var sumSql = "CAST((SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id`" + notCountAnd + ") AS DECIMAL(20,2))";
        var purchaseSql = "((CAST(IFNULL((SELECT SUM(`price_purchase`*(`count_reserved`+`count_issued`)) FROM `shop_orders_items_details` WHERE `order_id` = `shop_orders`.`id` AND `order_item_id` IN (SELECT `id` FROM `shop_orders_items` WHERE " + notCountBare + ")), 0) AS DECIMAL(20,2)))"
            + " + (CAST(IFNULL((SELECT SUM(`t2_price_purchase`*`count_need`) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id` AND " + notCountBare + "), 0) AS DECIMAL(20,2))))";

        var total = (int)await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `shop_orders`" + whereSql), cancellationToken, args.ToArray()).ConfigureAwait(false);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        var page = Math.Clamp(filter.Page, 1, pages);

        CpOrdersTotals totals;
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = ErpDb.Positional("SELECT COUNT(*), IFNULL(SUM(" + sumSql + "),0), IFNULL(SUM(" + sumSql + " - " + purchaseSql + "),0), IFNULL(SUM(" + purchaseSql + "),0) FROM `shop_orders`" + whereSql);
            ErpDb.AddParameters(c, args.ToArray());
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            totals = await r.ReadAsync(cancellationToken).ConfigureAwait(false)
                ? new((int)L(r, 0), D(r, 1), D(r, 2), D(r, 3))
                : new(0, 0, 0, 0);
        }

        var rows = new List<CpOrdersRow>();
        await using (var c = connection.CreateCommand())
        {
            var orderBy = filter.Sort == "last_modified" ? "last_modified" : "`" + filter.Sort + "`";
            c.CommandText = ErpDb.Positional(
                "SELECT `shop_orders`.`id` AS `id`, `shop_orders`.`time` AS `time`, `shop_orders`.`paid` AS `paid`, IFNULL(`shop_orders`.`paid_type`,0) AS `paid_type`, `shop_orders`.`status` AS `status`, "
                + "IFNULL((SELECT `caption` FROM `shop_obtaining_modes` WHERE `id` = `shop_orders`.`how_get`),'') AS `obtain_caption`, `shop_orders`.`user_id` AS `user_id`, "
                + customerSql + " AS `customer`, IFNULL(`shop_orders`.`office_id`,0) AS `office_id`, "
                + "(SELECT COUNT(*) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id`) AS `count_items`, "
                + "IFNULL(" + sumSql + ",0) AS `price_sum`, " + purchaseSql + " AS `price_purchase`, IFNULL(" + sumSql + ",0) - " + purchaseSql + " AS `profit`, "
                + "IFNULL((SELECT `viewed_flag` FROM `shop_orders_viewed` WHERE `order_id` = `shop_orders`.`id` AND `user_id` = ? LIMIT 1), 1) AS `viewed_flag`, "
                + "(SELECT COUNT(*) FROM `shop_orders_messages` WHERE `order_id` = `shop_orders`.`id` AND `read` = 0 AND `is_customer` = 1) AS `count_not_viewed_msg`, "
                + "(SELECT COUNT(DISTINCT(`check_id`)) FROM `shop_kkt_checks_products` WHERE `id` IN (SELECT `check_product_id` FROM `shop_kkt_checks_products_to_orders_items_map` WHERE `order_item_id` IN (SELECT `id` FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id`))) AS `checks_count`, "
                + "GREATEST(IFNULL((SELECT MAX(`time`) FROM `shop_orders_logs` WHERE `order_id` = `shop_orders`.`id`),0), IFNULL((SELECT MAX(`time`) FROM `shop_orders_messages` WHERE `order_id` = `shop_orders`.`id`),0), `shop_orders`.`time`) AS `last_modified` "
                + "FROM `shop_orders`" + whereSql + " ORDER BY " + orderBy + (filter.Desc ? " DESC" : " ASC") + ", `id` DESC LIMIT ? OFFSET ?");
            var all = new List<object> { managerId };
            all.AddRange(args);
            all.Add(pageSize);
            all.Add((page - 1) * pageSize);
            ErpDb.AddParameters(c, all.ToArray());
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(L(r, 0), L(r, 1), I(r, 2), I(r, 3), I(r, 4), S(r, 5), L(r, 6), S(r, 7), L(r, 8), I(r, 9),
                    D(r, 10), D(r, 11), D(r, 12), I(r, 13), I(r, 14), I(r, 15), L(r, 16)));
            }
        }

        var items = new Dictionary<long, IReadOnlyList<CpOrdersItemRow>>();
        if (rows.Count > 0)
        {
            var lists = new Dictionary<long, List<CpOrdersItemRow>>();
            await using var c = connection.CreateCommand();
            c.CommandText = ErpDb.Positional("SELECT `id`, `order_id`, IFNULL(`t2_brand`,''), IFNULL(`t2_article`,''), IFNULL(`t2_name`,''), IFNULL(`price`,0), IFNULL(`count_need`,0), IFNULL(`status`,0) FROM `shop_orders_items` WHERE `order_id` IN ("
                + string.Join(",", rows.Select(_ => "?")) + ") ORDER BY `id`");
            ErpDb.AddParameters(c, rows.Select(x => (object)x.Id).ToArray());
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var row = new CpOrdersItemRow(L(r, 0), L(r, 1), S(r, 2), S(r, 3), S(r, 4), D(r, 5), D(r, 6), I(r, 7));
                if (!lists.TryGetValue(row.OrderId, out var list))
                {
                    lists[row.OrderId] = list = [];
                }

                list.Add(row);
            }

            foreach (var kv in lists)
            {
                items[kv.Key] = kv.Value;
            }
        }

        return new CpOrdersList(rows, total, page, pageSize, totals, statuses, paidTypes, offices, items, openIds, completedIds);
    }

    /// <summary>PHP $SQL_SELECT_CUSTOMER: guest orders show phone/e-mail typed at checkout; registered orders show ID, e-mail, phone and to_users_table profile fields.</summary>
    internal static string CustomerSql(IReadOnlyList<string> profileFields)
    {
        var pieces = string.Concat(profileFields.Select(f =>
            ", IF(IFNULL((SELECT `data_value` FROM `users_profiles` WHERE `data_key` = '" + f + "' AND `user_id` = `shop_orders`.`user_id` LIMIT 1), '') != '', CONCAT(', ', (SELECT `data_value` FROM `users_profiles` WHERE `data_key` = '" + f + "' AND `user_id` = `shop_orders`.`user_id` LIMIT 1)), '')"));
        return "IF(`shop_orders`.`user_id` = 0, "
            + "CONCAT('Guest (ID 0)', IF(`phone_not_auth` = '' OR `phone_not_auth` IS NULL, '', CONCAT(', Phone: ', `phone_not_auth`)), IF(`email_not_auth` = '' OR `email_not_auth` IS NULL, '', CONCAT(', E-mail: ', `email_not_auth`))), "
            + "CONCAT('ID ', `shop_orders`.`user_id`, ', E-mail: ', IFNULL((SELECT IF(`email` != '', `email`, 'n/a') FROM `users` WHERE `user_id` = `shop_orders`.`user_id` LIMIT 1), 'n/a'), ', Phone: ', IFNULL((SELECT IF(`phone` != '', `phone`, 'n/a') FROM `users` WHERE `user_id` = `shop_orders`.`user_id` LIMIT 1), 'n/a')"
            + pieces + "))";
    }

    private static void AddIn(List<string> where, List<object> args, string column, IReadOnlyList<int> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        where.Add(column + " IN (" + string.Join(",", values.Select(_ => "?")) + ")");
        args.AddRange(values.Cast<object>());
    }

    private static long ReadLong(Microsoft.Extensions.Primitives.StringValues v) =>
        long.TryParse(v.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : 0;

    private static IReadOnlyList<int> ReadInts(Microsoft.Extensions.Primitives.StringValues v)
    {
        var list = new List<int>();
        foreach (var piece in v.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(piece, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 0 && !list.Contains(n))
            {
                list.Add(n);
            }
        }

        return list;
    }

    private static int I(DbDataReader r, int i) => r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i), CultureInfo.InvariantCulture);
    private static long L(DbDataReader r, int i) => r.IsDBNull(i) ? 0 : Convert.ToInt64(r.GetValue(i), CultureInfo.InvariantCulture);
    private static decimal D(DbDataReader r, int i) => r.IsDBNull(i) ? 0m : Convert.ToDecimal(r.GetValue(i), CultureInfo.InvariantCulture);
    private static string S(DbDataReader r, int i) => r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i), CultureInfo.InvariantCulture) ?? "";

    [GeneratedRegex("^[A-Za-z0-9_]+$")]
    private static partial Regex SafeName();
}
