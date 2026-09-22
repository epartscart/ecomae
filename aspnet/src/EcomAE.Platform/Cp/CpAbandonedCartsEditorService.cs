using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Filter state of <c>shop/order_process/carts.php</c> (PHP keeps it in the <c>carts_items_filter</c> cookie; here it is the query string).</summary>
public sealed record CpCartsFilter(long TimeFrom, long TimeTo, string Customer, long StorageId, string Sort, bool Desc, int Page)
{
    public static readonly string[] SortFields =
    [
        "id", "manufacturer", "article", "product_name", "price", "count_need", "price_sum", "price_purchase_sum", "profit", "time", "user_id",
    ];

    public bool Any => TimeFrom > 0 || TimeTo > 0 || Customer.Length > 0 || StorageId > 0;
}

/// <summary>One reservation row of <c>shop_carts_details</c> (product_type 1) or the synthetic t2 row (product_type 2).</summary>
public sealed record CpCartDetail(string StorageName, string StorageRecordId, decimal PricePurchase, decimal CountReserved, decimal PricePurchaseSum);

public sealed record CpCartRow(
    long Id,
    int ProductType,
    string Manufacturer,
    string Article,
    string ProductName,
    decimal Price,
    decimal CountNeed,
    decimal PriceSum,
    decimal PricePurchaseSum,
    decimal Profit,
    long Time,
    long UserId,
    string CustomerLabel,
    IReadOnlyList<CpCartDetail> Details);

public sealed record CpCartsList(IReadOnlyList<CpCartRow> Rows, int Total, int Page, int PageSize, IReadOnlyList<(long Id, string Name)> Storages)
{
    public int PageCount => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public interface ICpAbandonedCartsEditorService
{
    Task<CpCartsList> ListAsync(CpCartsFilter filter, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>Read side of the CP carts twin (<c>shop/order_process/carts.php</c>): all customer basket lines with margin, storage filter, sorting, paging and per-line reservation details.</summary>
public sealed class CpAbandonedCartsEditorService : ICpAbandonedCartsEditorService
{
    private const string PriceSum = "`price`*`count_need`";
    private const string PricePurchaseSum = "IFNULL((SELECT SUM(`d`.`price_purchase`*`d`.`count_reserved`) FROM `shop_carts_details` `d` WHERE `d`.`cart_record_id` = `shop_carts`.`id`), CAST(IFNULL(`t2_price_purchase`,0)*`count_need` AS DECIMAL(12,2)))";

    private readonly IErpWriteConnectionFactory _connections;

    public CpAbandonedCartsEditorService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static CpCartsFilter ReadFilter(HttpRequest request)
    {
        var q = request.Query;
        var sort = q["sort"].ToString();
        if (!CpCartsFilter.SortFields.Contains(sort, StringComparer.Ordinal))
        {
            sort = "id";
        }

        return new CpCartsFilter(
            ReadLong(q["time_from"]),
            ReadLong(q["time_to"]),
            q["customer"].ToString().Trim(),
            ReadLong(q["storage_id"]),
            sort,
            !string.Equals(q["dir"].ToString(), "asc", StringComparison.OrdinalIgnoreCase),
            (int)Math.Max(1, ReadLong(q["page"])));
    }

    public async Task<CpCartsList> ListAsync(CpCartsFilter filter, int pageSize, CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 500);
        var storages = new List<(long Id, string Name)>();
        if (!_connections.IsConfigured)
        {
            return new([], 0, 1, pageSize, storages);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using (var sc = connection.CreateCommand())
            {
                sc.CommandText = "SELECT `id`, IFNULL(`name`,'') FROM `shop_storages` ORDER BY `id`";
                await using var sr = await sc.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await sr.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    storages.Add((Convert.ToInt64(sr.GetValue(0), CultureInfo.InvariantCulture), sr.GetString(1)));
                }
            }

            var storageName = storages.ToDictionary(s => s.Id, s => s.Name);
            var where = new List<string>();
            var args = new List<object>();
            if (filter.TimeFrom > 0) { where.Add("`time` > ?"); args.Add(filter.TimeFrom); }
            if (filter.TimeTo > 0) { where.Add("`time` < ?"); args.Add(filter.TimeTo); }
            if (filter.Customer.Length > 0)
            {
                if (long.TryParse(filter.Customer, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid))
                {
                    where.Add("`user_id` = ?");
                    args.Add(uid);
                }
                else
                {
                    where.Add("`user_id` IN (SELECT `user_id` FROM `users` WHERE `email` LIKE ? OR `username` LIKE ? OR `phone` LIKE ?)");
                    var like = "%" + filter.Customer + "%";
                    args.Add(like); args.Add(like); args.Add(like);
                }
            }

            if (filter.StorageId > 0)
            {
                where.Add("((`product_type` = 1 AND EXISTS (SELECT 1 FROM `shop_carts_details` `sd` WHERE `sd`.`cart_record_id` = `shop_carts`.`id` AND `sd`.`storage_id` = ?)) OR (`product_type` = 2 AND IFNULL(`t2_storage_id`,0) = ?))");
                args.Add(filter.StorageId); args.Add(filter.StorageId);
            }

            var whereSql = where.Count == 0 ? "" : " WHERE " + string.Join(" AND ", where);
            var total = (int)await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `shop_carts`" + whereSql), cancellationToken, args.ToArray()).ConfigureAwait(false);
            var pageCount = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            var page = Math.Min(filter.Page, pageCount);

            var orderBy = filter.Sort switch
            {
                "manufacturer" => "IFNULL(`t2_manufacturer`,'')",
                "article" => "IFNULL(`t2_article`,'')",
                "product_name" => "IFNULL(`t2_name`,'')",
                "price_sum" => "`price_sum`",
                "price_purchase_sum" => "`price_purchase_sum`",
                "profit" => "`profit`",
                _ => "`" + filter.Sort + "`",
            };

            var rows = new List<CpCartRow>();
            var pending = new List<(long Id, int Type, long StorageId, decimal PricePurchase, decimal Count)>();
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(
                    "SELECT `id`, IFNULL(`product_type`,0), IFNULL(`t2_manufacturer`,''), IFNULL(`t2_article_show`, IFNULL(`t2_article`,'')), IFNULL(`t2_name`,''), IFNULL(`price`,0), IFNULL(`count_need`,0), "
                    + "CAST(" + PriceSum + " AS DECIMAL(12,2)) AS `price_sum`, " + PricePurchaseSum + " AS `price_purchase_sum`, "
                    + "CAST(" + PriceSum + " - " + PricePurchaseSum + " AS DECIMAL(12,2)) AS `profit`, IFNULL(`time`,0), IFNULL(`user_id`,0), IFNULL(`t2_storage_id`,0), IFNULL(`t2_price_purchase`,0) "
                    + "FROM `shop_carts`" + whereSql + " ORDER BY " + orderBy + (filter.Desc ? " DESC" : " ASC") + ", `id` DESC LIMIT ? OFFSET ?");
                var all = new List<object>(args) { pageSize, (page - 1) * pageSize };
                ErpDb.AddParameters(cmd, all.ToArray());
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var id = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                    var type = Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture);
                    var count = Convert.ToDecimal(reader.GetValue(6), CultureInfo.InvariantCulture);
                    pending.Add((id, type, Convert.ToInt64(reader.GetValue(12), CultureInfo.InvariantCulture), Convert.ToDecimal(reader.GetValue(13), CultureInfo.InvariantCulture), count));
                    rows.Add(new CpCartRow(
                        id,
                        type,
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetString(4),
                        Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture),
                        count,
                        Convert.ToDecimal(reader.GetValue(7), CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader.GetValue(8), CultureInfo.InvariantCulture),
                        Convert.ToDecimal(reader.GetValue(9), CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader.GetValue(10), CultureInfo.InvariantCulture),
                        Convert.ToInt64(reader.GetValue(11), CultureInfo.InvariantCulture),
                        "",
                        []));
                }
            }

            var details = new Dictionary<long, List<CpCartDetail>>();
            var type1Ids = pending.Where(p => p.Type == 1).Select(p => p.Id).ToList();
            if (type1Ids.Count > 0)
            {
                await using var dc = connection.CreateCommand();
                dc.CommandText = ErpDb.Positional(
                    "SELECT `cart_record_id`, IFNULL(`storage_id`,0), IFNULL(`storage_record_id`,0), IFNULL(`price_purchase`,0), IFNULL(`count_reserved`,0) FROM `shop_carts_details` WHERE `cart_record_id` IN ("
                    + string.Join(",", type1Ids.Select(_ => "?")) + ") ORDER BY `id`");
                ErpDb.AddParameters(dc, type1Ids.Cast<object>().ToArray());
                await using var dr = await dc.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await dr.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var cartId = Convert.ToInt64(dr.GetValue(0), CultureInfo.InvariantCulture);
                    var sid = Convert.ToInt64(dr.GetValue(1), CultureInfo.InvariantCulture);
                    var pp = Convert.ToDecimal(dr.GetValue(3), CultureInfo.InvariantCulture);
                    var cr = Convert.ToDecimal(dr.GetValue(4), CultureInfo.InvariantCulture);
                    if (!details.TryGetValue(cartId, out var list))
                    {
                        list = [];
                        details[cartId] = list;
                    }

                    list.Add(new CpCartDetail(storageName.TryGetValue(sid, out var n) ? n : sid.ToString(CultureInfo.InvariantCulture), Convert.ToInt64(dr.GetValue(2), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture), pp, cr, pp * cr));
                }
            }

            var customers = new Dictionary<long, string>();
            var userIds = rows.Select(r => r.UserId).Where(u => u > 0).Distinct().ToList();
            if (userIds.Count > 0)
            {
                await using var uc = connection.CreateCommand();
                uc.CommandText = ErpDb.Positional("SELECT `user_id`, IFNULL(`email`,''), IFNULL(`username`,''), IFNULL(`phone`,'') FROM `users` WHERE `user_id` IN (" + string.Join(",", userIds.Select(_ => "?")) + ")");
                ErpDb.AddParameters(uc, userIds.Cast<object>().ToArray());
                await using var ur = await uc.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await ur.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var label = new[] { ur.GetString(2), ur.GetString(1), ur.GetString(3) }.FirstOrDefault(s => s.Trim().Length > 0) ?? "";
                    customers[Convert.ToInt64(ur.GetValue(0), CultureInfo.InvariantCulture)] = label.Trim();
                }
            }

            for (var i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var p = pending[i];
                IReadOnlyList<CpCartDetail> d;
                if (p.Type == 1 && details.TryGetValue(r.Id, out var list))
                {
                    d = list;
                }
                else
                {
                    d = [new CpCartDetail(storageName.TryGetValue(p.StorageId, out var n) ? n : (p.StorageId > 0 ? p.StorageId.ToString(CultureInfo.InvariantCulture) : "—"), "-", p.PricePurchase, p.Count, p.PricePurchase * p.Count)];
                }

                rows[i] = r with { Details = d, CustomerLabel = customers.TryGetValue(r.UserId, out var cl) ? cl : "" };
            }

            return new(rows, total, page, pageSize, storages);
        }
        catch (DbException)
        {
            return new([], 0, 1, pageSize, storages);
        }
    }

    private static long ReadLong(string? value)
        => long.TryParse((value ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : 0;
}
