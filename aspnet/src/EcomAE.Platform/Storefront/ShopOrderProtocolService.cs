using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>Who asked for the status change: PHP <c>initiator</c> 1 (CP manager) or the robot (4 for orders, 2 for items).</summary>
public sealed record ShopProtocolActor(bool IsManager, int AdminId)
{
    public static ShopProtocolActor Robot { get; } = new(false, 0);

    public static ShopProtocolActor Manager(int adminId) => new(true, adminId);
}

/// <summary>The PHP JSON answer: <c>{"status":true}</c> or <c>{"status":false,"message":…,"code":…}</c>.</summary>
public sealed record ShopProtocolResult(bool Status, string? Message = null, int? Code = null)
{
    public static ShopProtocolResult Ok { get; } = new(true);

    public object ToJson()
    {
        if (Status)
        {
            return new Dictionary<string, object?> { ["status"] = true };
        }

        var body = new Dictionary<string, object?> { ["status"] = false, ["message"] = Message ?? string.Empty };
        if (Code is { } code)
        {
            body["code"] = code;
        }

        return body;
    }
}

/// <summary>
/// PHP content/shop/protocol/set_order_status.php and set_order_item_status.php: the order and line status engine.
/// Order status: the manager cancel/finish cascades onto the lines, the status write, the
/// <c>order_status_to_manager</c> / <c>order_status_to_customer</c> notifications, the WhatsApp tracking log line
/// and the order history. Line status: the optional return split, the refund to balance when cancelling paid
/// lines, catalogue stock moves (reserve / issue / return), the status write, the paid-flag recheck, the
/// <c>order_item_status_*</c> notifications, the history, and the manager-only automatic order status
/// (finished, cancelled, back in work).
/// </summary>
public interface IShopOrderProtocolService
{
    Task<ShopProtocolResult> SetOrderStatusAsync(
        DbConnection connection,
        IReadOnlyList<long> orderIds,
        long status,
        ShopProtocolActor actor,
        CancellationToken cancellationToken = default);

    Task<ShopProtocolResult> SetOrderItemStatusAsync(
        DbConnection connection,
        IReadOnlyList<long> itemIds,
        long status,
        ShopProtocolActor actor,
        int? returnSplitCount = null,
        CancellationToken cancellationToken = default);
}

public sealed class ShopOrderProtocolService : IShopOrderProtocolService
{
    public const string OrderStatusToManager = "order_status_to_manager";
    public const string OrderStatusToCustomer = "order_status_to_customer";
    public const string ItemStatusToManager = "order_item_status_to_manager";
    public const string ItemStatusToCustomer = "order_item_status_to_customer";

    private readonly IStorefrontNotifyDispatcher _notify;
    private readonly ICpPlatformMailer _mailer;

    public ShopOrderProtocolService(IStorefrontNotifyDispatcher notify, ICpPlatformMailer mailer)
    {
        _notify = notify;
        _mailer = mailer;
    }

    // ---- set_order_status.php ----

    public async Task<ShopProtocolResult> SetOrderStatusAsync(
        DbConnection connection,
        IReadOnlyList<long> orderIds,
        long status,
        ShopProtocolActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var orders = (orderIds ?? []).ToList();
        if (orders.Count == 0)
        {
            return new ShopProtocolResult(false, "SQL error", 701);
        }

        var translator = new StorefrontPhpTranslator(connection);
        var ordersData = new Dictionary<long, OrderData>();
        foreach (var orderId in orders)
        {
            ordersData[orderId] = await OrderDataAsync(connection, orderId, cancellationToken).ConfigureAwait(false);
        }

        if (actor.IsManager)
        {
            var orderFinish = await IdsAsync(connection, "SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_finish` = 1", cancellationToken).ConfigureAwait(false);
            var orderInverse = await IdsAsync(connection, "SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_inverse` = 1", cancellationToken).ConfigureAwait(false);
            var itemInverse = await IdsAsync(connection, "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `count_flag` = 0 ORDER BY `order` ASC", cancellationToken).ConfigureAwait(false);
            var itemFinish = await IdsAsync(connection, "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_finish` = 1 ORDER BY `order` ASC", cancellationToken).ConfigureAwait(false);

            if (orderInverse.Contains(status))
            {
                var items = await OrderItemsAsync(connection, orders, cancellationToken).ConfigureAwait(false);
                var cancel = items.Where(i => !itemInverse.Contains(i.Status)).Select(i => i.Id).ToList();
                if (cancel.Count > 0 && itemInverse.Count > 0)
                {
                    var cascade = await SetOrderItemStatusAsync(connection, cancel, itemInverse[0], ShopProtocolActor.Robot, null, cancellationToken).ConfigureAwait(false);
                    if (!cascade.Status)
                    {
                        return cascade;
                    }
                }
            }

            if (orderFinish.Contains(status))
            {
                foreach (var orderId in orders)
                {
                    if (ordersData[orderId].Paid != 1)
                    {
                        return new ShopProtocolResult(false, await translator.TextAsync(5296, cancellationToken).ConfigureAwait(false), 101);
                    }
                }

                var items = await OrderItemsAsync(connection, orders, cancellationToken).ConfigureAwait(false);
                var issue = items.Where(i => !itemInverse.Contains(i.Status) && !itemFinish.Contains(i.Status)).Select(i => i.Id).ToList();
                if (issue.Count > 0 && itemFinish.Count > 0)
                {
                    var cascade = await SetOrderItemStatusAsync(connection, issue, itemFinish[0], ShopProtocolActor.Robot, null, cancellationToken).ConfigureAwait(false);
                    if (!cascade.Status)
                    {
                        return cascade;
                    }
                }
            }
        }

        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_orders` SET `status`=? WHERE " + string.Join(" OR ", orders.Select(_ => "`id`=?"))),
                cancellationToken,
                [status, .. orders.Cast<object?>()]).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return new ShopProtocolResult(false, "SQL error", 701);
        }

        var statusRef = await StatusRowAsync(connection, "shop_orders_statuses_ref", status, cancellationToken).ConfigureAwait(false);
        var statusName = await translator.TextAsync(statusRef.GetValueOrDefault("name"), cancellationToken).ConfigureAwait(false);
        var config = _mailer.ReadConfig();
        foreach (var (orderId, data) in ordersData)
        {
            var texts = await OrderTextsAsync(connection, translator, orderId, config, cancellationToken).ConfigureAwait(false);
            await NotifyAsync(connection, OrderStatusToManager, OrderStatusToCustomer, orderId, null, statusName, statusRef, data, texts, cancellationToken)
                .ConfigureAwait(false);
            await LogWhatsappTrackingAsync(connection, config, orderId, statusName, data, cancellationToken).ConfigureAwait(false);
        }

        var logText = await translator.TextAsync(4568, cancellationToken).ConfigureAwait(false) + " <b>" + statusName + "</b>";
        foreach (var orderId in orders)
        {
            await LogAsync(connection, orderId, actor, logText, cancellationToken).ConfigureAwait(false);
        }

        return ShopProtocolResult.Ok;
    }

    // ---- set_order_item_status.php ----

    public async Task<ShopProtocolResult> SetOrderItemStatusAsync(
        DbConnection connection,
        IReadOnlyList<long> itemIds,
        long status,
        ShopProtocolActor actor,
        int? returnSplitCount = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var items = (itemIds ?? []).ToList();
        var translator = new StorefrontPhpTranslator(connection);

        if (returnSplitCount is { } splitCount)
        {
            var split = await SplitForReturnAsync(connection, translator, items, splitCount, actor, cancellationToken).ConfigureAwait(false);
            if (split.Error is not null)
            {
                return new ShopProtocolResult(false, await translator.TextAsync(2122, cancellationToken).ConfigureAwait(false) + ". " + split.Error);
            }

            items = split.Items;
        }

        if (items.Count == 0)
        {
            return new ShopProtocolResult(false, "SQL error", 701);
        }

        var inList = string.Join(",", items.Select(_ => "?"));
        var orders = new List<long>();
        foreach (var row in await RowsAsync(connection, "SELECT DISTINCT(`order_id`), `id` FROM `shop_orders_items` WHERE `id` IN (" + inList + ")", items.Cast<object?>().ToArray(), cancellationToken).ConfigureAwait(false))
        {
            orders.Add(PhpLong(row.GetValueOrDefault("order_id")));
        }

        var itemStatuses = await StatusRowsAsync(connection, "shop_orders_items_statuses_ref", cancellationToken).ConfigureAwait(false);
        var notCount = itemStatuses.Where(s => PhpLong(s.Value.GetValueOrDefault("count_flag")) == 0).Select(s => s.Key).ToList();

        if (notCount.Contains(status) && orders.Count > 0)
        {
            var paidOrders = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `shop_orders` WHERE `paid` != ? AND `id` IN (" + string.Join(",", orders.Select(_ => "?")) + ")"),
                cancellationToken,
                [0, .. orders.Cast<object?>()]).ConfigureAwait(false);
            if (paidOrders > 0)
            {
                var refund = await RefundCancelledLinesAsync(connection, translator, orders, items, notCount, cancellationToken).ConfigureAwait(false);
                if (refund is not null)
                {
                    return refund;
                }
            }
        }

        var ordersData = new Dictionary<long, OrderData>();
        foreach (var orderId in orders)
        {
            if (ordersData.ContainsKey(orderId))
            {
                continue;
            }

            var data = await OrderDataAsync(connection, orderId, cancellationToken).ConfigureAwait(false);
            var lines = await RowsAsync(
                connection,
                "SELECT `id` FROM `shop_orders_items` WHERE `id` IN (" + inList + ") AND `order_id` = " + orderId.ToString(CultureInfo.InvariantCulture) + " ORDER BY `id`",
                items.Cast<object?>().ToArray(),
                cancellationToken).ConfigureAwait(false);
            ordersData[orderId] = data with { ItemIds = string.Join(", ", lines.Select(l => l.GetValueOrDefault("id") ?? string.Empty)) };
        }

        await MoveCatalogueStockAsync(connection, items, status, cancellationToken).ConfigureAwait(false);

        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_orders_items` SET `status` = ? WHERE `id` IN (" + inList + ")"),
                cancellationToken,
                [status, .. items.Cast<object?>()]).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return new ShopProtocolResult(false, "SQL error", 701);
        }

        var notCountSql = NotCountSql(notCount);
        foreach (var orderId in ordersData.Keys)
        {
            var sums = await OrderSumsAsync(connection, orderId, notCountSql, cancellationToken).ConfigureAwait(false);
            if (sums is null)
            {
                return new ShopProtocolResult(false, "Forbidden");
            }

            if (sums.Paid == 1 && sums.PaidLeft > 0 && sums.PaidSum >= 0)
            {
                await SetPaidAsync(connection, orderId, sums.PaidSum == 0 ? 0 : 2, cancellationToken).ConfigureAwait(false);
            }
        }

        var statusRef = itemStatuses.GetValueOrDefault(status) ?? new Dictionary<string, string>();
        var statusName = await translator.TextAsync(statusRef.GetValueOrDefault("name"), cancellationToken).ConfigureAwait(false);
        var config = _mailer.ReadConfig();
        foreach (var (orderId, data) in ordersData)
        {
            var texts = await OrderTextsAsync(connection, translator, orderId, config, cancellationToken).ConfigureAwait(false);
            await NotifyAsync(connection, ItemStatusToManager, ItemStatusToCustomer, orderId, data.ItemIds, statusName, statusRef, data, texts, cancellationToken)
                .ConfigureAwait(false);
        }

        var before = await translator.TextAsync(4569, cancellationToken).ConfigureAwait(false);
        var after = await translator.TextAsync(4570, cancellationToken).ConfigureAwait(false);
        foreach (var itemId in items)
        {
            var orderId = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT `order_id` FROM `shop_orders_items` WHERE `id`= ?"), cancellationToken, itemId)
                .ConfigureAwait(false);
            await LogAsync(
                connection,
                orderId,
                actor,
                before + " " + itemId.ToString(CultureInfo.InvariantCulture) + " " + after + " <b>" + statusName + "</b>",
                cancellationToken).ConfigureAwait(false);
        }

        if (actor.IsManager)
        {
            await AutoOrderStatusAsync(connection, ordersData, cancellationToken).ConfigureAwait(false);
        }

        return ShopProtocolResult.Ok;
    }

    /// <summary>
    /// PHP <c>retun=1</c>: one line only; a partial count is split off into a new line (catalogue details cloned with
    /// the split quantity) and the status then applies to the new line. Runs in one transaction.
    /// </summary>
    private static async Task<(List<long> Items, string? Error)> SplitForReturnAsync(
        DbConnection connection,
        StorefrontPhpTranslator translator,
        List<long> items,
        int count,
        ShopProtocolActor actor,
        CancellationToken cancellationToken)
    {
        // Commands outside the open transaction are rejected by the driver, so the strings are read up front.
        var strings = new Dictionary<int, string>();
        foreach (var key in new[] { 5630, 5631, 5636, 5637, 5638, 5639, 5640 })
        {
            strings[key] = await translator.TextAsync(key, cancellationToken).ConfigureAwait(false);
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (items.Count > 1)
            {
                throw new ProtocolException(strings[5630]);
            }

            var itemId = items.Count > 0 ? items[0] : 0;
            var item = (await RowsAsync(connection, "SELECT * FROM `shop_orders_items` WHERE `id` = ?", [itemId], cancellationToken, transaction).ConfigureAwait(false))
                .FirstOrDefault() ?? new Dictionary<string, string>();
            var orderId = PhpLong(item.GetValueOrDefault("order_id"));
            var countNeed = (int)PhpLong(item.GetValueOrDefault("count_need"));
            if (countNeed < count || count <= 0)
            {
                throw new ProtocolException(strings[5631]);
            }

            if (countNeed > count)
            {
                var columns = (await ColumnsAsync(connection, "shop_orders_items", transaction, cancellationToken).ConfigureAwait(false))
                    .Where(c => !string.Equals(c, "id", StringComparison.OrdinalIgnoreCase))
                    .Select(c => "`" + c + "`")
                    .ToList();
                var list = string.Join(", ", columns);
                var copy = connection.CreateCommand();
                await using (copy)
                {
                    copy.Transaction = transaction;
                    copy.CommandText = ErpDb.Positional("INSERT INTO `shop_orders_items` (" + list + ") SELECT " + list + " FROM `shop_orders_items` WHERE `id` = ?");
                    ErpDb.AddParameters(copy, itemId);
                    if (await copy.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
                    {
                        throw new ProtocolException(strings[5640]);
                    }
                }

                var newItemId = await ErpDb.LongAsync(connection, transaction, "SELECT LAST_INSERT_ID()", cancellationToken).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(connection, transaction, ErpDb.Positional("UPDATE `shop_orders_items` SET `count_need` = ? WHERE `id` = ?"), cancellationToken, count, newItemId)
                    .ConfigureAwait(false);

                var productType = PhpLong(item.GetValueOrDefault("product_type"));
                if (productType == 1)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        "INSERT INTO `shop_orders_items_details` (`id`, `order_id`, `order_item_id`, `office_id`, `storage_id`, `storage_record_id`, `count_reserved`, `count_issued`, `count_canceled`, `price_purchase`) "
                        + "SELECT NULL, `order_id`, " + newItemId.ToString(CultureInfo.InvariantCulture) + ", `office_id`, `storage_id`, `storage_record_id`, "
                        + count.ToString(CultureInfo.InvariantCulture) + ", `count_issued`, `count_canceled`, `price_purchase` FROM `shop_orders_items_details` WHERE `order_item_id` = "
                        + itemId.ToString(CultureInfo.InvariantCulture),
                        cancellationToken).ConfigureAwait(false);
                }

                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`) VALUES (?, ?, ?, ?, ?)"),
                    cancellationToken,
                    orderId,
                    now,
                    actor.AdminId,
                    1,
                    "Дублированна позиция ID " + newItemId.ToString(CultureInfo.InvariantCulture) + " от позиции ID " + itemId.ToString(CultureInfo.InvariantCulture)
                        + " с указанием количества " + count.ToString(CultureInfo.InvariantCulture) + " шт.").ConfigureAwait(false);

                var remaining = countNeed - count;
                await ErpDb.ExecuteAsync(connection, transaction, ErpDb.Positional("UPDATE `shop_orders_items` SET `count_need` = ? WHERE `id` = ?"), cancellationToken, remaining, itemId)
                    .ConfigureAwait(false);
                if (productType == 1)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional("UPDATE `shop_orders_items_details` SET `count_reserved` = ? WHERE `order_item_id` = ?"),
                        cancellationToken,
                        remaining,
                        itemId).ConfigureAwait(false);
                }

                var text = strings[5636] + " " + newItemId.ToString(CultureInfo.InvariantCulture) + " "
                    + strings[5637] + "  ID " + itemId.ToString(CultureInfo.InvariantCulture) + ". "
                    + strings[5638] + " " + countNeed.ToString(CultureInfo.InvariantCulture) + " "
                    + strings[5639] + " " + remaining.ToString(CultureInfo.InvariantCulture);
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`) VALUES (?, ?, ?, ?, ?)"),
                    cancellationToken,
                    orderId,
                    now,
                    actor.AdminId,
                    1,
                    text).ConfigureAwait(false);
                items = [newItemId];
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return (items, null);
        }
        catch (ProtocolException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return (items, ex.Message);
        }
        catch (DbException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return (items, ex.Message);
        }
    }

    /// <summary>
    /// PHP paid-order guard for a "not counted" (cancelling) line status: when the order total after the cancelled
    /// lines drops below what was paid, the overpaid part goes back to the customer balance (and straight out again
    /// for guests), the order becomes unpaid, then the paid flag is recomputed. Runs once per listed line's order,
    /// like PHP.
    /// </summary>
    private static async Task<ShopProtocolResult?> RefundCancelledLinesAsync(
        DbConnection connection,
        StorefrontPhpTranslator translator,
        List<long> orders,
        List<long> items,
        IReadOnlyList<long> notCount,
        CancellationToken cancellationToken)
    {
        var notCountSql = NotCountSql(notCount);
        var offices = await IdsAsync(connection, "SELECT `id` FROM `shop_offices`", cancellationToken).ConfigureAwait(false);
        var itemsIn = string.Join(",", items.Select(_ => "?"));
        foreach (var orderId in orders)
        {
            var order = await OrderSumsAsync(connection, orderId, notCountSql, cancellationToken).ConfigureAwait(false);
            if (order is null || !offices.Contains(order.OfficeId))
            {
                return new ShopProtocolResult(false, await translator.TextAsync(3504, cancellationToken).ConfigureAwait(false));
            }

            var cancelledRaw = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT SUM(`price`*`count_need`) AS `price_otmena` FROM `shop_orders_items` WHERE `id` IN(" + itemsIn + ") AND `order_id` = ?"),
                cancellationToken,
                [.. items.Cast<object?>(), orderId]).ConfigureAwait(false) ?? string.Empty;
            var cancelled = PhpDecimal(cancelledRaw);

            if (order.PriceSum - cancelled < order.PaidSum)
            {
                var amount = cancelled;
                var amountText = cancelledRaw;
                if (cancelled > order.PaidSum)
                {
                    amount = order.PaidSum;
                    amountText = order.PaidSumRaw;
                    var over = order.PaidSum - (order.PriceSum - cancelled);
                    if (over > 0)
                    {
                        amount = over;
                        amountText = PhpFloat(over);
                    }
                }

                var direct = order.UserId == 0;
                try
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        null,
                        ErpDb.Positional("INSERT INTO `shop_users_accounting` (`user_id`, `time`, `income`, `amount`, `operation_code`, `active`, `order_id`, `office_id`) "
                            + "VALUES (?,?,?,?, (SELECT `id` FROM `shop_accounting_codes` WHERE `key` = ? LIMIT 1) ,?,?, (SELECT `office_id` FROM `shop_orders` WHERE `id` = ? LIMIT 1) )"),
                        cancellationToken,
                        order.UserId,
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        1,
                        amount,
                        "5_refund_from_order_to_balance",
                        1,
                        orderId,
                        orderId).ConfigureAwait(false);
                    await SetPaidAsync(connection, orderId, 0, cancellationToken).ConfigureAwait(false);
                    if (direct)
                    {
                        await ErpDb.ExecuteAsync(
                            connection,
                            null,
                            ErpDb.Positional("INSERT INTO `shop_users_accounting` (`user_id`, `time`, `income`, `amount`, `operation_code`, `active`, `order_id`, `office_id`) "
                                + "VALUES (?,?,?,?, (SELECT `id` FROM `shop_accounting_codes` WHERE `key` = ? LIMIT 1) ,?,?, (SELECT `office_id` FROM `shop_orders` WHERE `id` = ? LIMIT 1) )"),
                            cancellationToken,
                            order.UserId,
                            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            0,
                            amount,
                            "6_refund_from_balance",
                            1,
                            0,
                            orderId).ConfigureAwait(false);
                    }

                    var log = await translator.TextAsync(3492, cancellationToken).ConfigureAwait(false) + " <b>" + amountText + "</b> ("
                        + await translator.TextAsync(direct ? 3493 : 4642, cancellationToken).ConfigureAwait(false) + ")";
                    await LogAsync(connection, orderId, ShopProtocolActor.Robot, log, cancellationToken).ConfigureAwait(false);
                }
                catch (DbException)
                {
                    return new ShopProtocolResult(false, await translator.TextAsync(3488, cancellationToken).ConfigureAwait(false));
                }
            }

            var again = await OrderSumsAsync(connection, orderId, notCountSql, cancellationToken).ConfigureAwait(false);
            if (again is null)
            {
                continue;
            }

            if (again.PriceSum - cancelled - again.PaidSum == 0 && again.PaidSum > 0)
            {
                await SetPaidAsync(connection, orderId, 1, cancellationToken).ConfigureAwait(false);
            }
            else if (again.PaidSum > 0)
            {
                await SetPaidAsync(connection, orderId, 2, cancellationToken).ConfigureAwait(false);
            }
        }

        return null;
    }

    /// <summary>PHP stock moves for catalogue lines (product type 1) between exist / reserved / issued, driven by the new status flags.</summary>
    private static async Task MoveCatalogueStockAsync(DbConnection connection, IReadOnlyList<long> items, long status, CancellationToken cancellationToken)
    {
        var flags = (await RowsAsync(connection, "SELECT `count_flag`, `issue_flag` FROM `shop_orders_items_statuses_ref` WHERE `id` = ?", [status], cancellationToken).ConfigureAwait(false))
            .FirstOrDefault();
        var countFlag = PhpLong(flags?.GetValueOrDefault("count_flag")) != 0;
        var issueFlag = PhpLong(flags?.GetValueOrDefault("issue_flag")) != 0;

        foreach (var itemId in items)
        {
            var row = (await RowsAsync(
                connection,
                "SELECT `product_type`, (SELECT `count_flag` FROM `shop_orders_items_statuses_ref` WHERE `id` = `shop_orders_items`.`status`) AS `count_flag_current`, "
                + "(SELECT SUM(`count_issued`) FROM `shop_orders_items_details` WHERE `order_item_id` = `shop_orders_items`.`id`) AS `previously_issued` FROM `shop_orders_items` WHERE `id` = ?",
                [itemId],
                cancellationToken).ConfigureAwait(false)).FirstOrDefault();
            if (row is null || PhpLong(row.GetValueOrDefault("product_type")) != 1)
            {
                continue;
            }

            var previouslyCanceled = PhpLong(row.GetValueOrDefault("count_flag_current")) != 1;
            var previouslyIssued = PhpDecimal(row.GetValueOrDefault("previously_issued")) != 0;
            var details = await IdsAsync(connection, "SELECT `id` FROM `shop_orders_items_details` WHERE `order_item_id` = " + itemId.ToString(CultureInfo.InvariantCulture), cancellationToken)
                .ConfigureAwait(false);

            if (issueFlag)
            {
                if (!previouslyCanceled && !previouslyIssued)
                {
                    await MoveAsync(connection, details, "`issued` = `issued` + {reserved}, `reserved` = `reserved` - {reserved}", "count_issued", "count_reserved", cancellationToken).ConfigureAwait(false);
                }
                else if (previouslyCanceled)
                {
                    await MoveAsync(connection, details, "`exist` = `exist` - {canceled}, `issued` = `issued` + {canceled}", "count_issued", "count_canceled", cancellationToken).ConfigureAwait(false);
                }
            }
            else if (!countFlag)
            {
                if (!previouslyCanceled && !previouslyIssued)
                {
                    await MoveAsync(connection, details, "`exist` = `exist` + {reserved}, `reserved` = `reserved` - {reserved}", "count_canceled", "count_reserved", cancellationToken).ConfigureAwait(false);
                }
                else if (!previouslyCanceled)
                {
                    await MoveAsync(connection, details, "`exist` = `exist` + {issued}, `issued` = `issued` - {issued}", "count_canceled", "count_issued", cancellationToken).ConfigureAwait(false);
                }
            }
            else if (previouslyCanceled)
            {
                await MoveAsync(connection, details, "`exist` = `exist` - {canceled}, `reserved` = `reserved` + {canceled}", "count_reserved", "count_canceled", cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// One PHP stock move: first every warehouse row (<c>shop_storages_data</c>) for the detail records, then every
    /// detail record's <paramref name="target"/> takes the <paramref name="source"/> quantity and the source becomes 0.
    /// </summary>
    private static async Task MoveAsync(DbConnection connection, IReadOnlyList<long> details, string storageSet, string target, string source, CancellationToken cancellationToken)
    {
        foreach (var detail in details)
        {
            var id = detail.ToString(CultureInfo.InvariantCulture);
            var sql = storageSet
                .Replace("{reserved}", "(SELECT `count_reserved` FROM `shop_orders_items_details` WHERE `id`=" + id + ")", StringComparison.Ordinal)
                .Replace("{canceled}", "(SELECT `count_canceled` FROM `shop_orders_items_details` WHERE `id`=" + id + ")", StringComparison.Ordinal)
                .Replace("{issued}", "(SELECT `count_issued` FROM `shop_orders_items_details` WHERE `id`=" + id + ")", StringComparison.Ordinal);
            await TryAsync(connection, "UPDATE `shop_storages_data` SET " + sql + " WHERE `id` = (SELECT `storage_record_id` FROM `shop_orders_items_details` WHERE `id`=" + id + ")", cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var detail in details)
        {
            var id = detail.ToString(CultureInfo.InvariantCulture);
            await TryAsync(connection, "UPDATE `shop_orders_items_details` SET `" + target + "` = `" + source + "` WHERE `id` = " + id, cancellationToken).ConfigureAwait(false);
            await TryAsync(connection, "UPDATE `shop_orders_items_details` SET `" + source + "` = 0 WHERE `id` = " + id, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// PHP manager-only follow-up after a line change, using the order status read before the change: all lines issued or
    /// cancelled (at least one issued, order fully paid) → finished; all cancelled → cancelled; a finished or cancelled
    /// order with a line back in work → the first <c>for_paid</c> status.
    /// </summary>
    private async Task AutoOrderStatusAsync(DbConnection connection, Dictionary<long, OrderData> ordersData, CancellationToken cancellationToken)
    {
        foreach (var (orderId, data) in ordersData)
        {
            var finish = await IdsAsync(connection, "SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_finish` = 1", cancellationToken).ConfigureAwait(false);
            if (finish.Count > 0 && !finish.Contains(data.Status))
            {
                var counts = await CountsAsync(connection, orderId, cancellationToken).ConfigureAwait(false);
                if (counts.Finish > 0 && counts.All == counts.Finish + counts.Inverse && data.Paid == 1)
                {
                    await SetOrderStatusAsync(connection, [orderId], finish[0], ShopProtocolActor.Robot, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        foreach (var (orderId, data) in ordersData)
        {
            var inverse = await IdsAsync(connection, "SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_inverse` = 1", cancellationToken).ConfigureAwait(false);
            if (inverse.Count > 0 && !inverse.Contains(data.Status))
            {
                var counts = await CountsAsync(connection, orderId, cancellationToken).ConfigureAwait(false);
                if (counts.All > 0 && counts.All == counts.Inverse)
                {
                    await SetOrderStatusAsync(connection, [orderId], inverse[0], ShopProtocolActor.Robot, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        foreach (var (orderId, data) in ordersData)
        {
            var finish = await IdsAsync(connection, "SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_finish` = 1", cancellationToken).ConfigureAwait(false);
            var inverse = await IdsAsync(connection, "SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_inverse` = 1", cancellationToken).ConfigureAwait(false);
            var paid = await IdsAsync(connection, "SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_paid` = 1", cancellationToken).ConfigureAwait(false);
            if (paid.Count > 0 && !paid.Contains(data.Status) && (finish.Contains(data.Status) || inverse.Contains(data.Status)))
            {
                var counts = await CountsAsync(connection, orderId, cancellationToken).ConfigureAwait(false);
                if (counts.All > 0 && counts.All != counts.Finish + counts.Inverse)
                {
                    await SetOrderStatusAsync(connection, [orderId], paid[0], ShopProtocolActor.Robot, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private static async Task<(long All, long Finish, long Inverse)> CountsAsync(DbConnection connection, long orderId, CancellationToken cancellationToken)
    {
        var row = (await RowsAsync(
            connection,
            "SELECT COUNT(*) AS `all`, "
            + "(SELECT COUNT(*) FROM `shop_orders_items` WHERE `order_id` = ? AND `status` IN(SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_finish` = 1)) AS `finish`, "
            + "(SELECT COUNT(*) FROM `shop_orders_items` WHERE `order_id` = ? AND `status` IN(SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `count_flag` = 0)) AS `inverse` "
            + "FROM `shop_orders_items` WHERE `order_id` = ?",
            [orderId, orderId, orderId],
            cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        return (PhpLong(row?.GetValueOrDefault("all")), PhpLong(row?.GetValueOrDefault("finish")), PhpLong(row?.GetValueOrDefault("inverse")));
    }

    // ---- notifications ----

    private async Task NotifyAsync(
        DbConnection connection,
        string managerNotification,
        string customerNotification,
        long orderId,
        string? itemIds,
        string statusName,
        IReadOnlyDictionary<string, string> statusRef,
        OrderData data,
        (string Staff, string Customer) texts,
        CancellationToken cancellationToken)
    {
        var managers = (await StorefrontOrderNotificationService.OfficeManagerIdsAsync(connection, (int)data.OfficeId, cancellationToken).ConfigureAwait(false))
            .Select(StorefrontNotifyPerson.User)
            .ToList();
        await _notify.SendAsync(connection, managerNotification, Vars(orderId, itemIds, statusName, texts.Staff), managers, cancellationToken, statusRef).ConfigureAwait(false);

        StorefrontNotifyPerson customer = data.CustomerId > 0
            ? StorefrontNotifyPerson.User((int)data.CustomerId)
            : StorefrontNotifyPerson.Direct(data.GuestEmail, data.GuestPhone);
        await _notify.SendAsync(connection, customerNotification, Vars(orderId, itemIds, statusName, texts.Customer), [customer], cancellationToken, statusRef).ConfigureAwait(false);
    }

    private static Dictionary<string, string> Vars(long orderId, string? itemIds, string statusName, string orderText)
    {
        var vars = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["order_id"] = orderId.ToString(CultureInfo.InvariantCulture),
            ["status_name"] = statusName,
            ["order_text"] = orderText,
        };
        if (itemIds is not null)
        {
            vars["item_id"] = itemIds;
        }

        return vars;
    }

    /// <summary>get_order_info_html_for_manager.php (the staff layout) and get_order_info_html_for_user.php.</summary>
    private static async Task<(string Staff, string Customer)> OrderTextsAsync(
        DbConnection connection,
        StorefrontPhpTranslator translator,
        long orderId,
        IReadOnlyDictionary<string, string> config,
        CancellationToken cancellationToken)
    {
        try
        {
            var data = await StorefrontOrderNotificationService.OrderEmailData.LoadAsync(connection, orderId, cancellationToken).ConfigureAwait(false);
            return (
                await StorefrontOrderNotificationService.BuildStaffHtmlAsync(connection, translator, data, config, cancellationToken).ConfigureAwait(false),
                await StorefrontOrderNotificationService.BuildCustomerHtmlAsync(connection, translator, data, config, cancellationToken).ConfigureAwait(false));
        }
        catch (DbException)
        {
            return (string.Empty, string.Empty);
        }
    }

    /// <summary>PHP <c>epc_wa_notify_order_status_change()</c>: logs the wa.me tracking link for the operator; nothing is sent.</summary>
    private static async Task LogWhatsappTrackingAsync(
        DbConnection connection,
        IReadOnlyDictionary<string, string> config,
        long orderId,
        string statusName,
        OrderData data,
        CancellationToken cancellationToken)
    {
        if (orderId <= 0 || statusName.Length == 0)
        {
            return;
        }

        try
        {
            var items = await RowsAsync(
                connection,
                "SELECT `id`, `t2_manufacturer`, `t2_article`, `t2_article_show`, `t2_name`, `count_need`, `price`, `t2_storage_id` FROM `shop_orders_items` WHERE `order_id` = ? ORDER BY `id` ASC LIMIT 40",
                [orderId],
                cancellationToken).ConfigureAwait(false);
            var site = await StorefrontWhatsappNotifier.SiteNameAsync(connection, config, cancellationToken).ConfigureAwait(false);
            var message = WhatsappStatusMessage(site, StorefrontWhatsappNotifier.Cfg(config, "domain_path"), orderId, statusName, items);

            var phone = string.Empty;
            if (PhpNonEmpty(data.GuestPhone))
            {
                phone = StorefrontWhatsappNotifier.Digits(data.GuestPhone);
            }
            else if (data.CustomerId > 0)
            {
                phone = StorefrontWhatsappNotifier.Digits(await ErpDb.StringAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `phone` FROM `users` WHERE `user_id` = ? LIMIT 1"),
                    cancellationToken,
                    data.CustomerId).ConfigureAwait(false) ?? string.Empty);
            }

            var target = phone.Length > 0 ? phone : SalesDigits(config);
            var href = target.Length > 0 ? "https://wa.me/" + target + "?text=" + Uri.EscapeDataString(message) : string.Empty;
            await LogAsync(
                connection,
                orderId,
                ShopProtocolActor.Robot,
                "WhatsApp tracking template ready: " + statusName + (href.Length > 0 ? " — " + href : string.Empty),
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    /// <summary>PHP <c>epc_wa_order_status_message()</c>.</summary>
    public static string WhatsappStatusMessage(string site, string domainPath, long orderId, string statusName, IReadOnlyList<IReadOnlyDictionary<string, string>> items)
    {
        var domain = domainPath.TrimEnd('/');
        var lines = WhatsappLines(items, 8);
        var id = orderId.ToString(CultureInfo.InvariantCulture);
        var en = "Hello from " + site + ".\n\nOrder #" + id + " update: " + statusName + ".";
        if (lines.Length > 0)
        {
            en += "\n\nItems:\n" + lines;
        }

        en += "\n\nTrack your order: " + domain + "/shop/orders";
        var ar = "تحديث طلب #" + id + " من " + site + ": " + statusName + ".";
        if (lines.Length > 0)
        {
            ar += "\n\n" + lines;
        }

        ar += "\n\n" + domain + "/shop/orders";
        return StorefrontWhatsappNotifier.Bilingual(en, ar);
    }

    /// <summary>PHP <c>epc_wa_order_lines_text()</c>.</summary>
    public static string WhatsappLines(IReadOnlyList<IReadOnlyDictionary<string, string>> items, int max)
    {
        var lines = new List<string>();
        foreach (var item in items)
        {
            if (lines.Count >= max)
            {
                lines.Add("…");
                break;
            }

            var brand = item.GetValueOrDefault("t2_manufacturer")?.Trim() ?? string.Empty;
            var article = (item.TryGetValue("t2_article_show", out var show) && show is not null ? show : item.GetValueOrDefault("t2_article") ?? string.Empty).Trim();
            var name = item.GetValueOrDefault("t2_name")?.Trim() ?? string.Empty;
            var qty = PhpLong(item.GetValueOrDefault("count_need"));
            var line = brand + " " + article;
            if (name.Length > 0)
            {
                line += " — " + name;
            }

            if (qty > 0)
            {
                line += " ×" + qty.ToString(CultureInfo.InvariantCulture);
            }

            lines.Add(line.Trim());
        }

        return string.Join("\n", lines);
    }

    /// <summary>PHP <c>epc_wa_sales_digits()</c>: <c>epc_whatsapp_number</c>, else <c>epc_contact_phone</c>, else the platform sales line.</summary>
    private static string SalesDigits(IReadOnlyDictionary<string, string> config)
    {
        foreach (var key in new[] { "epc_whatsapp_number", "epc_contact_phone" })
        {
            if (config.TryGetValue(key, out var raw) && PhpNonEmpty(raw))
            {
                var digits = StorefrontWhatsappNotifier.Digits(raw);
                return digits.Length > 0 ? digits : "971567607011";
            }
        }

        return "971567607011";
    }

    // ---- data ----

    private sealed record OrderData(long CustomerId, long Status, long Paid, long OfficeId, string GuestEmail, string GuestPhone, string ItemIds = "");

    private sealed record OrderSums(long UserId, long OfficeId, long Paid, decimal PriceSum, decimal PaidSum, string PaidSumRaw, decimal PaidLeft);

    private static async Task<OrderData> OrderDataAsync(DbConnection connection, long orderId, CancellationToken cancellationToken)
    {
        var row = (await RowsAsync(connection, "SELECT * FROM `shop_orders` WHERE `id` = ?", [orderId], cancellationToken).ConfigureAwait(false)).FirstOrDefault()
            ?? new Dictionary<string, string>();
        return new OrderData(
            PhpLong(row.GetValueOrDefault("user_id")),
            PhpLong(row.GetValueOrDefault("status")),
            PhpLong(row.GetValueOrDefault("paid")),
            PhpLong(row.GetValueOrDefault("office_id")),
            row.GetValueOrDefault("email_not_auth") ?? string.Empty,
            row.GetValueOrDefault("phone_not_auth") ?? string.Empty);
    }

    /// <summary>PHP order totals: counted line sum, payments (<c>income=0</c> minus <c>income=1</c> on the order) and the remainder.</summary>
    private static async Task<OrderSums?> OrderSumsAsync(DbConnection connection, long orderId, string notCountSql, CancellationToken cancellationToken)
    {
        const string Income = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1 AND `order_id` = ?), 0)";
        const string Issue = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `order_id` = ?),0)";
        var row = (await RowsAsync(
            connection,
            "SELECT `user_id`, `office_id`, `paid`, CAST( (SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id`= `shop_orders`.`id` " + notCountSql + " ) AS DECIMAL(10,2)) AS `price_sum`, "
            + "CAST( (" + Issue + " - " + Income + ") AS DECIMAL(10,2) ) AS `paid_sum`, "
            + "CAST( ( (SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id`= `shop_orders`.`id` " + notCountSql + " ) - (" + Issue + " - " + Income + ") ) AS DECIMAL(10,2) ) AS `paid_left` "
            + "FROM `shop_orders` WHERE `id` = ?",
            [orderId, orderId, orderId, orderId, orderId],
            cancellationToken).ConfigureAwait(false)).FirstOrDefault();
        if (row is null)
        {
            return null;
        }

        var paidSumRaw = row.GetValueOrDefault("paid_sum") ?? string.Empty;
        return new OrderSums(
            PhpLong(row.GetValueOrDefault("user_id")),
            PhpLong(row.GetValueOrDefault("office_id")),
            PhpLong(row.GetValueOrDefault("paid")),
            PhpDecimal(row.GetValueOrDefault("price_sum")),
            PhpDecimal(paidSumRaw),
            paidSumRaw,
            PhpDecimal(row.GetValueOrDefault("paid_left")));
    }

    private static string NotCountSql(IEnumerable<long> notCount)
        => string.Concat(notCount.Select(id => " AND `status` != " + id.ToString(CultureInfo.InvariantCulture)));

    private static async Task<List<(long Id, long Status)>> OrderItemsAsync(DbConnection connection, IReadOnlyList<long> orders, CancellationToken cancellationToken)
    {
        var rows = await RowsAsync(
            connection,
            "SELECT `id`, `status` FROM `shop_orders_items` WHERE `order_id` IN(" + string.Join(",", orders.Select(o => o.ToString(CultureInfo.InvariantCulture))) + ")",
            [],
            cancellationToken).ConfigureAwait(false);
        return rows.Select(r => (PhpLong(r.GetValueOrDefault("id")), PhpLong(r.GetValueOrDefault("status")))).ToList();
    }

    private static async Task<IReadOnlyDictionary<string, string>> StatusRowAsync(DbConnection connection, string table, long id, CancellationToken cancellationToken)
        => (await StatusRowsAsync(connection, table, cancellationToken).ConfigureAwait(false)).GetValueOrDefault(id) ?? new Dictionary<string, string>();

    private static async Task<Dictionary<long, IReadOnlyDictionary<string, string>>> StatusRowsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var map = new Dictionary<long, IReadOnlyDictionary<string, string>>();
        foreach (var row in await RowsAsync(connection, "SELECT * FROM `" + table + "` ORDER BY `order` ASC", [], cancellationToken).ConfigureAwait(false))
        {
            map[PhpLong(row.GetValueOrDefault("id"))] = row;
        }

        return map;
    }

    private static Task SetPaidAsync(DbConnection connection, long orderId, int paid, CancellationToken cancellationToken)
        => ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `shop_orders` SET `paid`=? WHERE `id` = ?"), cancellationToken, paid, orderId);

    /// <summary>PHP history row: the manager writes as himself; the robot writes user 0 with <c>is_robot</c> = 1.</summary>
    private static async Task LogAsync(DbConnection connection, long orderId, ShopProtocolActor actor, string text, CancellationToken cancellationToken)
    {
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`, `is_robot`) VALUES (?, ?, ?, ?, ?, ?)"),
                cancellationToken,
                orderId,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                actor.IsManager ? actor.AdminId : 0,
                actor.IsManager ? 1 : 0,
                text,
                actor.IsManager ? 0 : 1).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    private static async Task TryAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        try
        {
            await ErpDb.ExecuteAsync(connection, null, sql, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    private static async Task<List<long>> IdsAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        var ids = new List<long>();
        try
        {
            foreach (var row in await RowsAsync(connection, sql, [], cancellationToken).ConfigureAwait(false))
            {
                ids.Add(PhpLong(row.GetValueOrDefault("id")));
            }
        }
        catch (DbException)
        {
        }

        return ids;
    }

    private static async Task<List<string>> ColumnsAsync(DbConnection connection, string table, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var columns = new List<string>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SHOW COLUMNS FROM `" + table + "`";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return columns;
    }

    private static async Task<List<IReadOnlyDictionary<string, string>>> RowsAsync(
        DbConnection connection,
        string sql,
        object?[] args,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null)
    {
        var rows = new List<IReadOnlyDictionary<string, string>>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null! : PhpString(reader.GetValue(i));
            }

            rows.Add(row);
        }

        return rows;
    }

    private static string PhpString(object value) => value switch
    {
        bool b => b ? "1" : "0",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    private static bool PhpNonEmpty(string? value) => !string.IsNullOrEmpty(value) && value != "0";

    private static long PhpLong(string? raw)
    {
        raw = (raw ?? string.Empty).Trim();
        var end = 0;
        while (end < raw.Length && (char.IsAsciiDigit(raw[end]) || (end == 0 && raw[end] is '-' or '+')))
        {
            end++;
        }

        return long.TryParse(raw[..end], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static decimal PhpDecimal(string? raw)
        => decimal.TryParse((raw ?? string.Empty).Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0m;

    /// <summary>PHP float echo (precision 14): <c>20.0</c> prints <c>20</c>, <c>12.5</c> prints <c>12.5</c>.</summary>
    public static string PhpFloat(decimal value)
        => ((double)value).ToString("G14", CultureInfo.InvariantCulture);

    private sealed class ProtocolException(string message) : Exception(message);
}
