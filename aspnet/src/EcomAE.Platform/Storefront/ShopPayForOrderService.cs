using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>The <c>DP_Config</c> fields PHP <c>protocol/pay_for_order.php</c> reads.</summary>
public sealed record ShopPayForOrderConfig(
    bool Wholesaler,
    string? PartialPayment,
    string? PartialPaymentMinPercent,
    string? ClientOverdraft,
    string? ClientOverdraftValue,
    string DomainPath,
    string BackendDir)
{
    public static ShopPayForOrderConfig From(IReadOnlyDictionary<string, string> config)
    {
        string? Get(string key) => config.TryGetValue(key, out var value) ? value : null;
        return new ShopPayForOrderConfig(
            config.ContainsKey("wholesaler"),
            Get("partial_payment"),
            Get("partial_payment_min_percent"),
            Get("client_overdraft"),
            Get("client_overdraft_value"),
            Get("domain_path") ?? string.Empty,
            Get("backend_dir") ?? string.Empty);
    }
}

/// <summary>
/// One call of PHP <c>protocol/pay_for_order.php</c>. <c>Initiator</c> is 1 (CP manager), 2 (customer paying from
/// the balance) or 3 (payment system). <c>OrderId</c>, <c>PaySum</c> and <c>DirectPay</c> are the raw query strings.
/// <c>AdminId</c> is <c>DP_User::getAdminId()</c> when <c>isAdmin()</c>, <c>CustomerId</c> is <c>DP_User::getUserId()</c>.
/// </summary>
public sealed record ShopPayForOrderRequest(
    int Initiator,
    string OrderId,
    string PaySum,
    string DirectPay,
    int AdminId,
    int CustomerId,
    bool TechKeyOk,
    ShopPayForOrderConfig Config);

/// <summary>The PHP answer <c>{"status":…,"message":…}</c>.</summary>
public sealed record ShopPayForOrderResult(bool Status, string Message)
{
    public object ToJson() => new Dictionary<string, object?> { ["status"] = Status, ["message"] = Message };
}

/// <summary>
/// PHP <c>content/shop/protocol/pay_for_order.php</c>: binds a payment to an order. In one transaction: the order debt
/// checks, the initiator checks (manager office access, customer partial payment and overdraft, payment-system tech key),
/// the <c>2_income_for_direct_pay</c> income for a manager direct payment, the <c>1_pay_for_order</c> expense with its
/// UAE advance VAT row, the paid flag and the order log. After the commit: the paid type with its log line, the
/// <c>order_pay_to_manager</c> / <c>order_pay_to_customer</c> notifications, and the robot status change to the
/// <c>for_paid</c> status once the order is fully paid.
/// </summary>
public interface IShopPayForOrderService
{
    Task<ShopPayForOrderResult> PayAsync(DbConnection connection, ShopPayForOrderRequest request, CancellationToken cancellationToken = default);
}

public sealed class ShopPayForOrderService : IShopPayForOrderService
{
    public const int InitiatorManager = 1;
    public const int InitiatorCustomer = 2;
    public const int InitiatorPaymentSystem = 3;

    private const string Forbidden = "Forbidden";

    private readonly IStorefrontNotifyDispatcher? _notify;
    private readonly IShopOrderProtocolService? _protocol;
    private readonly IErpAdvanceVatService? _advanceVat;

    public ShopPayForOrderService(
        IStorefrontNotifyDispatcher? notify = null,
        IShopOrderProtocolService? protocol = null,
        IErpAdvanceVatService? advanceVat = null)
    {
        _notify = notify;
        _protocol = protocol;
        _advanceVat = advanceVat;
    }

    private sealed class PayException(string message) : Exception(message);

    private sealed record Applied(
        long OrderId,
        long UserId,
        long OfficeId,
        string PaySum,
        string? OrderSum,
        decimal OrderSumValue,
        decimal PaidLeftNew,
        int NewPaid,
        string NewPaidText,
        int LogUserId,
        int LogIsManager,
        int LogIsRobot,
        string EmailNotAuth,
        string PhoneNotAuth);

    public async Task<ShopPayForOrderResult> PayAsync(DbConnection connection, ShopPayForOrderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(request);
        var translator = new StorefrontPhpTranslator(connection);

        if (_advanceVat is not null)
        {
            await _advanceVat.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        }

        var texts = new Dictionary<int, string>();
        foreach (var key in new[] { 1316, 4366, 4529, 3584, 3515, 4565, 3488, 3489, 3490, 3494 })
        {
            texts[key] = await translator.TextAsync(key, cancellationToken).ConfigureAwait(false);
        }

        Applied applied;
        await using (var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                applied = await ApplyAsync(connection, tx, texts, request, cancellationToken).ConfigureAwait(false);
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is PayException or DbException)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return new ShopPayForOrderResult(false, ex.Message);
            }
        }

        await AfterCommitAsync(connection, translator, request, applied, cancellationToken).ConfigureAwait(false);
        return new ShopPayForOrderResult(true, string.Empty);
    }

    private async Task<Applied> ApplyAsync(
        DbConnection connection,
        DbTransaction tx,
        IReadOnlyDictionary<int, string> texts,
        ShopPayForOrderRequest request,
        CancellationToken cancellationToken)
    {
        var paySum = PhpNumber(request.PaySum) ?? throw new PayException(Forbidden);
        if (paySum <= 0m)
        {
            throw new PayException(Forbidden);
        }

        var notCounted = new List<long>();
        await using (var statuses = connection.CreateCommand())
        {
            statuses.Transaction = tx;
            statuses.CommandText = "SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `count_flag` = 0 ORDER BY `order` ASC";
            await using var reader = await statuses.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                notCounted.Add(Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture));
            }
        }

        var whereNotCounted = string.Concat(notCounted.Select(id => " AND `status` != " + id.ToString(CultureInfo.InvariantCulture)));
        const string income = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1 AND `order_id` = ?), 0)";
        const string issue = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `order_id` = ?),0)";
        var officeSql = request.Config.Wholesaler ? " AND `office_id` = (SELECT `office_id` FROM `shop_orders` WHERE `id` = ?) " : string.Empty;
        var incomeUser = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1 AND `user_id` = `shop_orders`.`user_id` " + officeSql + " ), 0)";
        var issueUser = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `user_id` = `shop_orders`.`user_id` " + officeSql + " ),0)";
        var orderSql = "SELECT *, CAST( (" + issue + " - " + income + ") AS DECIMAL(8,2) ) AS `paid_sum`, CAST( (" + incomeUser + " - " + issueUser
            + ") AS DECIMAL(8,2) ) AS `customer_balance`, CAST( ( (SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id`= `shop_orders`.`id` "
            + whereNotCounted + " ) - (" + issue + " - " + income + ") ) AS DECIMAL(8,2) )  AS `paid_left` FROM `shop_orders` WHERE `id` = ?;";
        var orderId = request.OrderId;
        var orderArgs = Enumerable.Repeat<object?>(orderId, request.Config.Wholesaler ? 7 : 5).ToArray();
        var order = await RowAsync(connection, tx, ErpDb.Positional(orderSql), cancellationToken, orderArgs).ConfigureAwait(false)
            ?? throw new PayException(Forbidden);

        var sumSql = "SELECT CAST( SUM(`price`*`count_need`) AS DECIMAL(8,2) ) AS `order_sum` FROM `shop_orders_items` WHERE `order_id` = ? "
            + string.Concat(notCounted.Select(_ => " AND `status` != ?")) + ";";
        var orderSum = await ErpDb.StringAsync(
            connection,
            tx,
            ErpDb.Positional(sumSql),
            cancellationToken,
            [orderId, .. notCounted.Cast<object?>()]).ConfigureAwait(false);
        var orderSumValue = PhpNumber(orderSum) ?? 0m;

        if (PhpNumber(order["paid"]) == 1m)
        {
            throw new PayException(Forbidden);
        }

        var paidLeft = PhpNumber(order["paid_left"]);
        if (paidLeft is null || paySum > paidLeft.Value)
        {
            throw new PayException(Forbidden);
        }

        var orderUser = (long)(PhpNumber(order["user_id"]) ?? 0m);
        var officeId = (long)(PhpNumber(order["office_id"]) ?? 0m);
        var directPayTruthy = PhpTruthy(request.DirectPay);
        if (request.Initiator == InitiatorManager)
        {
            if (request.AdminId <= 0)
            {
                throw new PayException(Forbidden);
            }

            var access = await ErpDb.LongAsync(
                connection,
                tx,
                ErpDb.Positional("SELECT COUNT(*) FROM `shop_offices` WHERE `id` = ? AND `users` LIKE ?;"),
                cancellationToken,
                order["office_id"],
                "%" + request.AdminId.ToString(CultureInfo.InvariantCulture) + "%").ConfigureAwait(false);
            if (access == 0)
            {
                throw new PayException(Forbidden);
            }

            if (StorefrontPhpAjax.PhpLooseEquals(request.DirectPay, 0) && orderUser == 0)
            {
                throw new PayException(Forbidden);
            }
        }
        else if (request.Initiator == InitiatorCustomer)
        {
            if (request.CustomerId == 0 || orderUser != request.CustomerId)
            {
                throw new PayException(Forbidden);
            }

            if (paySum < paidLeft.Value)
            {
                if (!PhpTruthy(request.Config.PartialPayment))
                {
                    throw new PayException(Forbidden);
                }

                var minPercent = PhpNumber(request.Config.PartialPaymentMinPercent) ?? 0m;
                if (paySum < orderSumValue * (minPercent / 100m))
                {
                    throw new PayException(Forbidden);
                }
            }

            var balance = PhpNumber(order["customer_balance"]) ?? 0m;
            if (paySum > balance)
            {
                if (!PhpTruthy(request.Config.ClientOverdraft))
                {
                    throw new PayException(Forbidden);
                }

                var overdraft = PhpIntCast(request.Config.ClientOverdraftValue);
                if (paySum - balance > overdraft && overdraft > 0)
                {
                    throw new PayException(Forbidden);
                }
            }
        }
        else if (request.Initiator == InitiatorPaymentSystem)
        {
            if (!request.TechKeyOk)
            {
                throw new PayException(Forbidden);
            }
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var dbOrderId = order["id"];
        if (request.Initiator == InitiatorManager && directPayTruthy)
        {
            await WriteAsync(
                connection,
                tx,
                "INSERT INTO `shop_users_accounting` (`user_id`, `time`, `income`, `amount`, `operation_code`, `active`, `pay_orders`, `office_id`) VALUES (?,?,?,?, (SELECT `id` FROM `shop_accounting_codes` WHERE `key` = ? LIMIT 1) ,?,?,  (SELECT `office_id` FROM `shop_orders` WHERE `id` = ? LIMIT 1) );",
                3488,
                texts,
                cancellationToken,
                order["user_id"], now, 1, request.PaySum, "2_income_for_direct_pay", 1, dbOrderId, dbOrderId).ConfigureAwait(false);
        }

        await WriteAsync(
            connection,
            tx,
            "INSERT INTO `shop_users_accounting` (`user_id`, `time`, `income`, `amount`, `operation_code`, `active`, `order_id`, `office_id`) VALUES (?,?,?,?, (SELECT `id` FROM `shop_accounting_codes` WHERE `key` = ? LIMIT 1) ,?,?, (SELECT `office_id` FROM `shop_orders` WHERE `id` = ? LIMIT 1) );",
            3490,
            texts,
            cancellationToken,
            order["user_id"], now, 0, request.PaySum, "1_pay_for_order", 1, dbOrderId, dbOrderId).ConfigureAwait(false);
        var ledgerId = await ErpDb.LastInsertIdAsync(connection, tx, cancellationToken).ConfigureAwait(false);
        var numericOrderId = (long)(PhpNumber(dbOrderId) ?? 0m);
        if (ledgerId > 0 && _advanceVat is not null)
        {
            await _advanceVat.RecordOrderPaymentAdvanceAsync(connection, tx, ledgerId, numericOrderId, paySum, now, cancellationToken).ConfigureAwait(false);
        }

        var paidLeftNew = paidLeft.Value - paySum;
        int newPaid;
        string newPaidText;
        if (paidLeftNew == 0m)
        {
            newPaid = 1;
            newPaidText = texts[3584];
        }
        else if (paidLeftNew > 0m)
        {
            newPaid = 2;
            newPaidText = texts[3515];
        }
        else
        {
            throw new PayException(texts[4565]);
        }

        await WriteAsync(connection, tx, "UPDATE `shop_orders` SET `paid`=? WHERE `id` = ?;", 3489, texts, cancellationToken, newPaid, orderId).ConfigureAwait(false);

        var (logUser, logManager, logRobot) = request.Initiator switch
        {
            InitiatorManager => (request.AdminId, 1, 0),
            InitiatorCustomer => (request.CustomerId, 0, 0),
            _ => (0, 0, 1),
        };
        var logText = texts[1316] + ". "
            + texts[4366] + " <b>" + request.PaySum + "</b><br/>"
            + texts[4529] + ": <b>" + newPaidText + "</b>";
        await WriteAsync(
            connection,
            tx,
            "INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`, `is_robot`) VALUES (?, ?, ?, ?, ?, ?);",
            3494,
            texts,
            cancellationToken,
            dbOrderId, now, logUser, logManager, logText, logRobot).ConfigureAwait(false);

        return new Applied(
            numericOrderId,
            orderUser,
            officeId,
            request.PaySum,
            orderSum,
            orderSumValue,
            paidLeftNew,
            newPaid,
            newPaidText,
            logUser,
            logManager,
            logRobot,
            order.TryGetValue("email_not_auth", out var email) ? email ?? string.Empty : string.Empty,
            order.TryGetValue("phone_not_auth", out var phone) ? phone ?? string.Empty : string.Empty);
    }

    private static async Task WriteAsync(
        DbConnection connection,
        DbTransaction tx,
        string sql,
        int failureText,
        IReadOnlyDictionary<int, string> texts,
        CancellationToken cancellationToken,
        params object?[] args)
    {
        try
        {
            await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional(sql), cancellationToken, args).ConfigureAwait(false);
        }
        catch (DbException)
        {
            throw new PayException(texts[failureText]);
        }
    }

    private async Task AfterCommitAsync(
        DbConnection connection,
        StorefrontPhpTranslator translator,
        ShopPayForOrderRequest request,
        Applied pay,
        CancellationToken cancellationToken)
    {
        try
        {
            var paidTypes = new Dictionary<long, string>();
            var raw = new List<(long Id, string Name)>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT `id`, `name` FROM `shop_orders_paid_type` WHERE `active` = 1 ORDER BY `order`;";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    raw.Add((Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture), reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty));
                }
            }

            foreach (var (id, name) in raw)
            {
                paidTypes[id] = await translator.TextAsync(name, cancellationToken).ConfigureAwait(false);
            }

            var paidType = request.Initiator == InitiatorManager && StorefrontPhpAjax.PhpLooseEquals(request.DirectPay, 1)
                ? 1
                : request.Initiator == InitiatorPaymentSystem ? 3 : 2;
            await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `shop_orders` SET `paid_type` = ? WHERE `id` = ?;"), cancellationToken, paidType, request.OrderId)
                .ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`, `is_robot`) VALUES (?, ?, ?, ?, ?, ?);"),
                cancellationToken,
                request.OrderId,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                pay.LogUserId,
                pay.LogIsManager,
                await translator.TextAsync(4645, cancellationToken).ConfigureAwait(false) + ": <b>" + (paidTypes.TryGetValue(paidType, out var caption) ? caption : string.Empty) + "</b>",
                pay.LogIsRobot).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }

        if (_notify is not null)
        {
            try
            {
                await NotifyAsync(connection, translator, request, pay, cancellationToken).ConfigureAwait(false);
            }
            catch (DbException)
            {
            }
        }

        if (pay.NewPaid == 1 && _protocol is not null)
        {
            try
            {
                var forPaid = await ErpDb.LongAsync(
                    connection,
                    null,
                    "SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_paid` = 1;",
                    cancellationToken).ConfigureAwait(false);
                if (forPaid > 0)
                {
                    await _protocol.SetOrderStatusAsync(connection, [pay.OrderId], forPaid, ShopProtocolActor.Robot, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (DbException)
            {
            }
        }
    }

    private async Task NotifyAsync(
        DbConnection connection,
        StorefrontPhpTranslator translator,
        ShopPayForOrderRequest request,
        Applied pay,
        CancellationToken cancellationToken)
    {
        var config = request.Config;
        var color = "#799658";
        try
        {
            var raw = await ErpDb.StringAsync(connection, null, "SELECT `data_value` FROM `templates` WHERE `is_frontend` = 1 AND `current` = 1 LIMIT 1;", cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("main_color", out var main)
                    && main.ValueKind == JsonValueKind.String
                    && !string.IsNullOrEmpty(main.GetString())
                    && main.GetString() != "0")
                {
                    color = main.GetString()!;
                }
            }
        }
        catch (Exception ex) when (ex is DbException or JsonException)
        {
        }

        var linkText = await translator.TextAsync(4643, cancellationToken).ConfigureAwait(false);
        string Link(string href)
            => "<div style=\"margin-top:10px;\"><a style=\"background: " + color
                + "; color: #fff; text-decoration: none; padding: 7px 13px; font-size: 16px; border-radius: 5px; display: inline-block;\" target=\"_blank\" href=\""
                + href + "\">" + linkText + "</a></div>";

        var id = request.OrderId;
        Dictionary<string, string> Vars(string link) => new(StringComparer.Ordinal)
        {
            ["order_id"] = id,
            ["pay_value"] = pay.PaySum,
            ["paid"] = pay.NewPaidText,
            ["order_sum"] = pay.OrderSum ?? string.Empty,
            ["paid_sum"] = ShopOrderProtocolService.PhpFloat(pay.OrderSumValue - pay.PaidLeftNew),
            ["paid_left"] = ShopOrderProtocolService.PhpFloat(pay.PaidLeftNew),
            ["order_link"] = link,
        };

        var managers = new List<StorefrontNotifyPerson>();
        var users = await ErpDb.StringAsync(connection, null, ErpDb.Positional("SELECT `users` FROM `shop_offices` WHERE `id` = ?;"), cancellationToken, pay.OfficeId)
            .ConfigureAwait(false);
        try
        {
            if (!string.IsNullOrWhiteSpace(users))
            {
                using var doc = JsonDocument.Parse(users);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in doc.RootElement.EnumerateArray())
                    {
                        var raw = element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
                        _ = int.TryParse(new string((raw ?? string.Empty).Trim().TakeWhile(char.IsAsciiDigit).ToArray()), NumberStyles.Integer, CultureInfo.InvariantCulture, out var uid);
                        managers.Add(StorefrontNotifyPerson.User(uid));
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        await _notify!.SendAsync(connection, "order_pay_to_manager", Vars(Link(config.DomainPath + config.BackendDir + "/shop/orders/order?order_id=" + id)), managers, cancellationToken)
            .ConfigureAwait(false);

        StorefrontNotifyPerson customer = pay.UserId > 0
            ? StorefrontNotifyPerson.User((int)pay.UserId)
            : StorefrontNotifyPerson.Direct(pay.EmailNotAuth, pay.PhoneNotAuth);
        var customerLink = Link(config.DomainPath + (pay.UserId > 0 ? "shop/orders/order?order_id=" : "shop/orders/zakaz-bez-registracii?order_id=") + id);
        await _notify.SendAsync(connection, "order_pay_to_customer", Vars(customerLink), [customer], cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Dictionary<string, string?>?> RowAsync(
        DbConnection connection,
        DbTransaction tx,
        string sql,
        CancellationToken cancellationToken,
        params object?[] args)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        ErpDb.AddParameters(command, args);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var row = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : ErpDocumentControlRender.PdoString(reader, i);
        }

        return row;
    }

    /// <summary>A PHP numeric string (leading/trailing whitespace allowed) as a number; anything else is null.</summary>
    public static decimal? PhpNumber(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        return text.Length > 0 && decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    /// <summary>PHP truthiness of a config or query string: unset, <c>""</c> and <c>"0"</c> are false.</summary>
    public static bool PhpTruthy(string? raw) => !string.IsNullOrEmpty(raw) && raw != "0";

    /// <summary>PHP <c>(int)</c> of a string: the leading integer part, 0 when there is none.</summary>
    public static long PhpIntCast(string? raw)
    {
        var text = (raw ?? string.Empty).TrimStart();
        var end = 0;
        while (end < text.Length && (char.IsAsciiDigit(text[end]) || (end == 0 && text[end] is '-' or '+')))
        {
            end++;
        }

        return long.TryParse(text[..end], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }
}
