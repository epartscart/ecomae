using System.Data.Common;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/shop/order_process/my_order.php</c>: the signed-in customer's order card, lines, garage links,
/// payments, VAT summary, print links and message client. The legacy AJAX URLs are retained because ASP.NET maps them.
/// </summary>
/// <remarks>
/// PHP echoes several database values directly. This renderer deliberately HTML-escapes those values.
/// </remarks>
public static partial class StorefrontMyOrder
{
    public const string PagePath = "/shop/orders/order";

    public sealed record Input(
        long UserId,
        string? OrderId,
        string LangHref,
        string CsrfKey,
        IReadOnlyDictionary<string, string> Config,
        string? CurrencyCookie,
        string? CountryCookie,
        string PickerHtml,
        string AlertsHtml,
        string? ObtainHtml = null);

    private sealed record Item(
        string Id, string Status, string Price, string Count, string Name, string Article, string Manufacturer,
        string Time, string Guaranteed);

    private sealed record Car(string Id, string Caption, string Vin, bool Linked);
    internal sealed record BalanceState(decimal Balance, string BalanceText, bool Available, decimal Limit, string LimitText);

    private static readonly Lazy<string> UnpaidTemplate = new(() => Template("MyOrderUnpaid.html"));
    private static readonly Lazy<string> UnpaidBalancePositiveTemplate = new(() => Template("MyOrderUnpaidBalancePositive.html"));
    private static readonly Lazy<string> UnpaidBalanceNegativeTemplate = new(() => Template("MyOrderUnpaidBalanceNegative.html"));
    private static readonly Lazy<string> PartialTemplate = new(() => Template("MyOrderPartial.html"));
    private static readonly Lazy<string> PartialBalancePositiveTemplate = new(() => Template("MyOrderPartialBalancePositive.html"));
    private static readonly Lazy<string> PartialUnavailablePositiveTemplate = new(() => Template("MyOrderPartialUnavailablePositive.html"));
    private static readonly Lazy<string> PartialBelowMinimumTemplate = new(() => Template("MyOrderPartialBelowMinimum.html"));
    private static readonly Lazy<string> PaidTemplate = new(() => Template("MyOrderPaid.html"));

    public static string PhpDate(long unix) => ErpDocumentControlRender.PhpDate("dd.MM.yyyy H:mm", unix);

    public sealed record PostInput(
        IReadOnlyDictionary<string, string> Form,
        string? Session,
        string? UserCookie,
        string? AdminSession,
        string? AdminUser,
        string? Referer,
        string LangHref,
        string AlertsHtml);

    public static bool IsActionPost(IReadOnlyDictionary<string, string> form)
        => form.TryGetValue("action", out var action) && !string.IsNullOrEmpty(action) && action != "0";

    /// <summary>Signed-in <c>pay_on_place</c>, including PHP CSRF, customer ownership, log and robot status protocol.</summary>
    public static async Task<StorefrontPhpAjax.RawHttp> PostAsync(
        DbConnection connection,
        PostInput input,
        Func<string, Task<string>> t,
        Func<long, long, Task<bool>> setOrderStatus,
        long now,
        CancellationToken cancellationToken)
    {
        const string contentType = "text/html; charset=UTF-8";
        var form = input.Form;
        var csrf = await StorefrontPhpAjax.StopCsrfAsync(
            connection,
            form.GetValueOrDefault("csrf_guard_key"),
            input.Session,
            input.UserCookie,
            input.AdminSession,
            input.AdminUser,
            input.Referer,
            contentType,
            cancellationToken).ConfigureAwait(false);
        if (csrf is not null)
        {
            return new(input.AlertsHtml + csrf.Body, contentType);
        }

        var userId = await ErpUserAccess.UserIdAsync(
            connection,
            new ErpUserAccess.Cookies(input.Session, input.UserCookie, null, null),
            cancellationToken).ConfigureAwait(false);
        var error = await t("2122").ConfigureAwait(false) + ": <br/> " + await t("2304").ConfigureAwait(false) + ".";
        var success = string.Empty;
        var orderId = ShopPayForOrderService.PhpIntCast(form.GetValueOrDefault("order_id"));
        if (form.GetValueOrDefault("action") == "pay_on_place" && userId > 0)
        {
            var row = await RowsAsync(
                connection,
                "SELECT `office_id` FROM `shop_orders` WHERE `id` = ? AND `user_id` = ? AND `paid_type` = 0",
                [orderId, userId],
                cancellationToken).ConfigureAwait(false);
            if (row.Count > 0 && row[0][0] is { } office
                && await ScalarAsync(connection, "SELECT COUNT(*) FROM `shop_offices` WHERE `id` = ?", [office], cancellationToken).ConfigureAwait(false) > 0)
            {
                var paidType = await ScalarTextAsync(connection, "SELECT `name` FROM `shop_orders_paid_type` WHERE `active` = 1 AND `id` = 1", [], cancellationToken).ConfigureAwait(false) ?? string.Empty;
                try
                {
                    await ExecuteAsync(connection, "UPDATE `shop_orders` SET `paid_type` = 1 WHERE `id` = ?", [orderId], cancellationToken).ConfigureAwait(false);
                    success = await t("3630").ConfigureAwait(false);
                    error = string.Empty;
                    await ExecuteAsync(
                        connection,
                        "INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,?,?,?)",
                        [orderId, now, userId, 0, "Способ оплаты: <b>" + paidType + "</b>", 0],
                        cancellationToken).ConfigureAwait(false);
                    var status = await ScalarAsync(connection, "SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_paid` = 1 LIMIT 1", [], cancellationToken).ConfigureAwait(false);
                    if (status > 0 && !await setOrderStatus(orderId, status).ConfigureAwait(false))
                    {
                        error = await t("2385").ConfigureAwait(false) + ": <br/> " + await t("4662").ConfigureAwait(false) + ".";
                    }
                }
                catch (DbException)
                {
                    error = await t("2122").ConfigureAwait(false) + ": <br/> " + await t("3629").ConfigureAwait(false) + ".";
                }
            }
        }

        var location = input.LangHref + PagePath + "?order_id=" + orderId.ToString(CultureInfo.InvariantCulture);
        return new(
            input.AlertsHtml + "\t<script>\n\t\tlocation=\"" + location + "&error_message=" + error + "&success_message=" + success + "\";\n\t</script>\n\t",
            contentType);
    }

    public static async Task<string> RenderAsync(
        DbConnection connection,
        Input input,
        Func<string, Task<string>> t,
        Func<long, string> date,
        CancellationToken cancellationToken)
    {
        if (input.UserId <= 0)
        {
            return string.Empty;
        }

        var orderId = ShopPayForOrderService.PhpIntCast(input.OrderId);
        var orderRows = await RowsAsync(
            connection,
            "SELECT `id`,`time`,`office_id`,`status`,`paid`,`paid_type`,`how_get`,`how_get_json` FROM `shop_orders` WHERE `id` = ? AND `user_id` = ?",
            [orderId, input.UserId],
            cancellationToken).ConfigureAwait(false);
        if (orderRows.Count == 0)
        {
            return input.AlertsHtml + await t("4525").ConfigureAwait(false);
        }

        var order = orderRows[0];
        var officeExists = await ScalarAsync(connection, "SELECT COUNT(*) FROM `shop_offices` WHERE `id` = ?", [order[2]], cancellationToken).ConfigureAwait(false) > 0;
        if (!officeExists)
        {
            return input.AlertsHtml + await t("4525").ConfigureAwait(false);
        }

        // PHP performs this write only after both customer ownership and office validity have succeeded.
        await ExecuteAsync(
            connection,
            "UPDATE `shop_orders_messages` SET `read` = 1 WHERE `order_id` = ? AND `is_customer` = 0",
            [orderId],
            cancellationToken).ConfigureAwait(false);

        var itemStatusRows = await RowsAsync(connection, "SELECT `id`,`name`,`color`,`count_flag` FROM `shop_orders_items_statuses_ref` ORDER BY `order` ASC", [], cancellationToken).ConfigureAwait(false);
        var itemStatuses = itemStatusRows.ToDictionary(r => r[0] ?? string.Empty, r => (Name: r[1] ?? string.Empty, Color: r[2] ?? string.Empty, Count: r[3] is not null && !StorefrontMyOrders.PhpValue.NumericEquals(r[3]!, 0)), StringComparer.Ordinal);
        var statusName = await ScalarTextAsync(connection, "SELECT `name` FROM `shop_orders_statuses_ref` WHERE `id` = ?", [order[3]], cancellationToken).ConfigureAwait(false);
        var paidTypeName = await ScalarTextAsync(connection, "SELECT `name` FROM `shop_orders_paid_type` WHERE `active` = 1 AND `id` = ?", [order[5]], cancellationToken).ConfigureAwait(false);
        var onPlaceName = await ScalarTextAsync(connection, "SELECT `name` FROM `shop_orders_paid_type` WHERE `active` = 1 AND `id` = 1", [], cancellationToken).ConfigureAwait(false) ?? string.Empty;

        var items = (await RowsAsync(
            connection,
            "SELECT `id`,`status`,`price`,`count_need`,`t2_name`,`t2_article`,`t2_manufacturer`,`t2_time_to_exe`,`t2_time_to_exe_guaranteed` FROM `shop_orders_items` WHERE `order_id` = ?",
            [orderId],
            cancellationToken).ConfigureAwait(false))
            .Select(r => new Item(r[0] ?? "", r[1] ?? "", r[2] ?? "", r[3] ?? "", r[4] ?? "", r[5] ?? "", r[6] ?? "", r[7] ?? "", r[8] ?? ""))
            .ToList();

        var links = (await RowsAsync(connection, "SELECT `garage_id` FROM `shop_docpart_garage_orders` WHERE `order_id` = ?", [orderId], cancellationToken).ConfigureAwait(false))
            .Select(r => r[0] ?? string.Empty).ToHashSet(StringComparer.Ordinal);
        var cars = (await RowsAsync(connection, "SELECT `id`,`caption`,`vin` FROM `shop_docpart_garage` WHERE `user_id` = ?", [input.UserId], cancellationToken).ConfigureAwait(false))
            .Select(r => new Car(r[0] ?? "", r[1] ?? "", r[2] ?? "", links.Contains(r[0] ?? "")))
            .OrderByDescending(c => c.Linked)
            .ToList();
        var printDocs = await PrintDocsAsync(connection, orderId, input.CsrfKey, t, cancellationToken).ConfigureAwait(false);

        var config = input.Config;
        var records = await EpcCurrency.RecordsAsync(connection, Config(config, "shop_currency"), cancellationToken).ConfigureAwait(false);
        var selectedIso = await EpcCurrency.SelectedIsoAsync(connection, records, Config(config, "shop_currency"), input.UserId, input.CurrencyCookie, input.CountryCookie, cancellationToken).ConfigureAwait(false);
        string Money(decimal value) => EpcCurrency.FormatAmount((double)value, records, selectedIso, Config(config, "currency_show_mode") ?? "sign_before");

        decimal total = 0;
        decimal count = 0;
        foreach (var item in items)
        {
            if (itemStatuses.TryGetValue(item.Status, out var status) && status.Count)
            {
                var qty = Decimal(item.Count);
                total += Decimal(item.Price) * qty;
                count += qty;
            }
        }

        const string issue = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `order_id` = ?),0)";
        const string income = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1 AND `order_id` = ?),0)";
        var paidSum = Decimal(await ScalarTextAsync(connection, "SELECT CAST((" + issue + "-" + income + ") AS DECIMAL(20,2))", [orderId, orderId], cancellationToken).ConfigureAwait(false));
        var left = total - paidSum;
        var partial = ShopPayForOrderService.PhpTruthy(Config(config, "partial_payment"));
        var percent = Decimal(Config(config, "partial_payment_min_percent"));
        var minPay = total * (percent / 100m);
        var balance = await BalanceAsync(connection, input.UserId, ShopPayForOrderService.PhpIntCast(order[2]), left, total, partial, percent, config, cancellationToken).ConfigureAwait(false);

        var customerVat = await ErpDashboardReadService.CustomerContextAsync(connection, input.UserId, cancellationToken).ConfigureAwait(false);
        var tenantVat = await ErpDashboardReadService.LoadTenantVatAsync(connection, cancellationToken).ConfigureAwait(false);
        var inclusive = ErpDashboardReadService.DisplayMode(customerVat.VatType) == "inclusive";
        decimal subtotal = 0, vat = 0, gross = 0, vatRate = 0;
        foreach (var item in items.Where(i => itemStatuses.TryGetValue(i.Status, out var s) && s.Count))
        {
            var line = ErpDashboardReadService.LineAmounts(Decimal(item.Price), Decimal(item.Count), tenantVat.RatePercent, inclusive, tenantVat.SalesEnabled);
            subtotal += line.LineNet;
            vat += line.VatAmount;
            gross += line.Gross;
            if (line.VatAmount > 0)
            {
                vatRate = tenantVat.RatePercent;
            }
        }

        var paid = ShopPayForOrderService.PhpIntCast(order[4]);
        var html = paid == 1
            ? PaidTemplate.Value
            : partial
                ? left <= minPay
                    ? PartialBelowMinimumTemplate.Value
                    : balance.Available
                        ? balance.Balance < 0 ? PartialTemplate.Value : PartialBalancePositiveTemplate.Value
                        : balance.Balance < 0 ? PartialTemplate.Value : PartialUnavailablePositiveTemplate.Value
                : balance.Available
                    ? balance.Balance < 0 ? UnpaidBalanceNegativeTemplate.Value : UnpaidBalancePositiveTemplate.Value
                    : UnpaidTemplate.Value;
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ALERTS"] = input.AlertsHtml,
            ["ORDER_ID"] = orderId.ToString(CultureInfo.InvariantCulture),
            ["USER_ID"] = input.UserId.ToString(CultureInfo.InvariantCulture),
            ["CSRF_HTML"] = H(input.CsrfKey),
            ["CSRF_JS"] = JsSafe(input.CsrfKey),
            ["LANG_JS"] = JsSafe(input.LangHref),
            ["DATE"] = date(ShopPayForOrderService.PhpIntCast(order[1])),
            ["STATUS_NAME"] = "{{T:" + statusName + "}}",
            ["PAID_TYPE_NAME"] = string.IsNullOrEmpty(paidTypeName) ? string.Empty : "{{T:" + paidTypeName + "}}",
            ["ON_PLACE_NAME"] = "{{T:" + onPlaceName + "}}",
            ["CARS"] = await CarsAsync(cars, input, t).ConfigureAwait(false),
            ["ITEMS"] = await ItemsAsync(items, itemStatuses, Money, t).ConfigureAwait(false),
            ["ELEMENTS"] = Elements(items),
            ["COUNT_TOTAL"] = StorefrontPhpAjax.PhpFloatString((double)count),
            ["TOTAL_MONEY"] = Money(total),
            ["PAID_MONEY"] = Money(paidSum),
            ["LEFT_MONEY"] = Money(left),
            ["LEFT"] = Number(left),
            ["BALANCE"] = balance.BalanceText,
            ["BALANCE_LIMIT"] = balance.LimitText,
            ["MIN_PAY"] = StorefrontPhpAjax.PhpFloatString((double)minPay),
            ["PERCENT"] = Config(config, "partial_payment_min_percent") ?? string.Empty,
            ["PICKER"] = input.PickerHtml,
            ["VAT_LABEL"] = VatLabel(customerVat.VatType),
            ["RETURN_BUTTON"] = ShopPayForOrderService.PhpNumber(Config(config, "return_available")) == 1m
                ? "\t\t        <button onclick=\"confirm_return();\" type=\"button\" class=\"btn btn-xs btn-ar btn-default\"><i class=\"fa fa-reply\"></i> {{T:4527}}</button>\n"
                : string.Empty,
            ["PRINT_DOCS"] = printDocs,
            ["OBTAIN_INFO"] = input.ObtainHtml
                ?? "<div class=\"alert alert-info\" style=\"margin:0;\">Delivery / pickup details are not set for this order yet. Please contact the seller if you need them updated.</div>",
        };
        html = PatchVatRows(html, Money(subtotal), Money(vat), Money(gross), vatRate, inclusive);
        foreach (var (key, value) in replacements)
        {
            html = html.Replace("{{" + key + "}}", value, StringComparison.Ordinal);
        }

        return await TranslateAsync(html, t).ConfigureAwait(false);
    }

    internal static async Task<BalanceState> BalanceAsync(
        DbConnection connection,
        long userId,
        long officeId,
        decimal paidLeft,
        decimal priceTotal,
        bool partial,
        decimal partialPercent,
        IReadOnlyDictionary<string, string> config,
        CancellationToken cancellationToken)
    {
        var officeSql = config.ContainsKey("wholesaler") ? " AND `office_id` = ?" : string.Empty;
        var sql = "SELECT CAST(("
            + "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `user_id` = ? AND `income`=1 AND `active` = 1" + officeSql + "),0)"
            + "-"
            + "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `user_id` = ? AND `income`=0 AND `active` = 1" + officeSql + "),0)"
            + ") AS DECIMAL(20,2)) AS `balance` FROM `shop_users_accounting` WHERE `user_id` = ? LIMIT 1";
        object?[] args = config.ContainsKey("wholesaler")
            ? [userId, officeId, userId, officeId, userId]
            : [userId, userId, userId];
        var rows = await RowsAsync(connection, sql, args, cancellationToken).ConfigureAwait(false);
        var raw = rows.Count > 0 ? rows[0][0] : null;
        var balance = Decimal(raw);
        var balanceText = string.IsNullOrEmpty(raw) ? "0" : raw;

        var minimumForBalance = partial ? priceTotal * (partialPercent / 100m) : paidLeft;
        var available = false;
        var limit = 0m;
        var limitText = "0";
        var overdraft = ShopPayForOrderService.PhpTruthy(Config(config, "client_overdraft"));
        var overdraftValue = ShopPayForOrderService.PhpIntCast(Config(config, "client_overdraft_value"));
        if (balance >= minimumForBalance)
        {
            available = true;
            if (!partial || balance >= paidLeft)
            {
                limit = paidLeft;
                limitText = Number(paidLeft);
            }
            else if (overdraft)
            {
                if (overdraftValue == 0)
                {
                    limit = paidLeft;
                    limitText = Number(paidLeft);
                }
                else
                {
                    limit = balance + overdraftValue;
                    limitText = StorefrontPhpAjax.PhpFloatString((double)limit);
                }
            }
            else
            {
                limit = balance;
                limitText = balanceText;
            }
        }
        else if (overdraft)
        {
            if (overdraftValue == 0)
            {
                available = true;
                limit = paidLeft;
                limitText = Number(paidLeft);
            }
            else if (minimumForBalance - balance <= overdraftValue)
            {
                available = true;
                limit = balance + overdraftValue;
                limitText = StorefrontPhpAjax.PhpFloatString((double)limit);
            }
        }

        if (limit == 0)
        {
            available = false;
            limitText = "0";
        }

        return new(balance, balanceText, available, limit, limitText);
    }

    private static async Task<string> CarsAsync(IReadOnlyList<Car> cars, Input input, Func<string, Task<string>> t)
    {
        if (cars.Count == 0)
        {
            return "<tr> <td>" + await t("5622").ConfigureAwait(false) + "</td> <td><a class=\"btn btn-xs btn-ar btn-primary pull-right\" href=\"/garazh/avtomobil\"><i class=\"fa fa-car\" aria-hidden=\"true\"></i> " + await t("2267").ConfigureAwait(false) + "</a></td> </tr>\n\t\t\t";
        }

        var sb = new StringBuilder();
        foreach (var car in cars)
        {
            var color = car.Linked ? "#66bf05" : "#f2f2f2";
            var extra = car.Linked ? " " : string.Empty;
            sb.Append("<tr class=\"car_tr_").Append(H(car.Id)).Append("\"> <td style=\"padding-top: 5px; padding-bottom: 6px; vertical-align: middle;\"><a").Append(extra)
                .Append(" class=\"btn btn-xs\" style=\"color:").Append(color).Append("; font-size: 14px; border: 1px solid #dddddd;\" onclick=\"check_car(this, ").Append(JsNumber(car.Id))
                .Append(");\"><i class=\"fa fa-check\" aria-hidden=\"true\"></i></a></td> <td style=\"line-height:1.1em; width:100%; font-size: 12px; padding-top: 1px; padding-bottom: 1px; vertical-align: middle;\">")
                .Append(H(car.Caption)).Append("<a style=\"margin-top: 2px; color:#555;\" class=\"btn btn-xs btn-default pull-right\" onclick=\"delete_car(this, ").Append(JsNumber(car.Id))
                .Append(");\" title=\"").Append(await t("2224").ConfigureAwait(false)).Append("\"><i style=\"margin-right:0px;\" class=\"fa fa-trash-o\" aria-hidden=\"true\"></i></a>   <a style=\"margin-top: 2px; color:#555; margin-right:5px;\" class=\"btn btn-xs btn-default pull-right\" href=\"")
                .Append(H(input.LangHref)).Append("/garazh/avtomobil?car_id=").Append(H(car.Id)).Append("\" title=\"").Append(await t("2270").ConfigureAwait(false))
                .Append("\"><i style=\"margin-right:0px;\" class=\"fa fa-pencil-square-o\" aria-hidden=\"true\"></i></a>   <a style=\"margin-top: 2px; color:#555; margin-right:5px;\" class=\"btn btn-xs btn-default pull-right\" onclick=\"active_car(this, ")
                .Append(JsNumber(car.Id)).Append(");\" title=\"").Append(await t("5621").ConfigureAwait(false)).Append("\"><i style=\"margin-right:0px;\" class=\"fa fa-check-square-o\" aria-hidden=\"true\"></i></a> <br/> <small>")
                .Append(H(car.Vin)).Append("</small></td> </tr>");
        }

        return sb.ToString();
    }

    private static async Task<string> ItemsAsync(
        IReadOnlyList<Item> items,
        IReadOnlyDictionary<string, (string Name, string Color, bool Count)> statuses,
        Func<decimal, string> money,
        Func<string, Task<string>> t)
    {
        var sb = new StringBuilder();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            statuses.TryGetValue(item.Status, out var status);
            var time = item.Time;
            if (Decimal(item.Time) < Decimal(item.Guaranteed))
            {
                time += " - " + item.Guaranteed;
            }

            sb.Append("\t\t\t<tr style=\"background:").Append(H(status.Color)).Append("\">\n")
                .Append("\t\t\t\t<td class=\"\" style=\"vertical-align: middle;\">\n")
                .Append("\t\t\t\t\t<input type=\"checkbox\" onchange=\"on_one_check_changed('checked_").Append(H(item.Id)).Append("');\" id=\"checked_").Append(H(item.Id)).Append("\" name=\"checked_").Append(H(item.Id)).Append("\"/>\n")
                .Append("\t\t\t\t</td>\n")
                .Append("\t\t\t\t<td style=\"vertical-align: middle;\">").Append(H(item.Id)).Append("</td>\n")
                .Append("\t\t\t\t<td style=\"vertical-align: middle; white-space: nowrap;\">").Append(H(item.Manufacturer)).Append("</td>\n")
                .Append("\t\t\t\t<td style=\"vertical-align: middle; white-space: nowrap;\">").Append(H(item.Article)).Append("</td>\n")
                .Append("\t\t\t\t<td style=\"vertical-align: middle; width: 100%; min-width: 200px; max-width: 800px; word-wrap: break-word;\">").Append(H(item.Name)).Append("</td>\n")
                .Append("\t\t\t\t<td style=\"vertical-align: middle; white-space: nowrap;\">").Append(money(Decimal(item.Price))).Append("</td>\n")
                .Append("\t\t\t\t<td style=\"vertical-align: middle; white-space: nowrap; text-align:center;\">").Append(H(item.Count)).Append("</td>\n")
                .Append("\t\t\t\t<td style=\"vertical-align: middle; white-space: nowrap;\">").Append(money(Decimal(item.Price) * Decimal(item.Count))).Append("</td>\n")
                .Append("\t\t\t\t<td style=\"vertical-align: middle; white-space: nowrap;\">").Append(H(time)).Append(' ').Append(await t("5315").ConfigureAwait(false)).Append("</td>\n")
                .Append("\t\t\t\t<td style=\"vertical-align: middle;\">").Append(await t(status.Name).ConfigureAwait(false)).Append("</td>\n")
                .Append("\t\t\t</tr>\n")
                .Append(index + 1 < items.Count ? "\t\t\t\t\t\t\n" : "\t\t\t");
        }

        return sb.ToString();
    }

    private static async Task<string> PrintDocsAsync(
        DbConnection connection,
        long orderId,
        string csrf,
        Func<string, Task<string>> t,
        CancellationToken cancellationToken)
    {
        var buttons = new List<string>();
        foreach (var row in await RowsAsync(connection, "SELECT `name`,`caption`,`parameters_values` FROM `shop_print_docs` WHERE `control_available` = 1 ORDER BY `id` ASC", [], cancellationToken).ConfigureAwait(false))
        {
            var name = row[0] ?? string.Empty;
            if (name is not ("sales_receipt" or "invoice_for_payment") || !Visible(row[2]))
            {
                continue;
            }

            var label = name == "sales_receipt"
                ? "Sales receipt"
                : name == "invoice_for_payment"
                    ? "Tax Invoice (UAE e-Invoice)"
                    : await t(row[1] ?? string.Empty).ConfigureAwait(false);
            buttons.Add("<a class=\"btn btn-ar btn-primary\" href=\"/content/shop/print_docs/service/print.php?doc_name=" + H(name)
                + "&order_id=" + orderId.ToString(CultureInfo.InvariantCulture) + "&csrf_guard_key=" + H(csrf)
                + "\" target=\"_blank\"><i class=\"fa fa-print\"></i> " + H(label) + "</a>");
        }

        return buttons.Count == 0
            ? string.Empty
            : "\t\t\t<div style=\"margin-top:50px;\">\n\t\t\t<p class=\"lead\">{{T:4547}}</p>\n\t\t\t"
                + string.Join(" ", buttons) + "\t\t</div>\n\t\t\t\n";
    }

    private static bool Visible(string? json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json ?? string.Empty);
            return doc.RootElement.TryGetProperty("button_visible_for_customer", out var value)
                && ShopPayForOrderService.PhpIntCast(value.ToString()) == 1;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Elements(IEnumerable<Item> items)
    {
        var sb = new StringBuilder("var elements_array = new Array();\nvar elements_id_array = new Array();\n");
        foreach (var item in items)
        {
            sb.Append("elements_array[elements_array.length] = \"checked_").Append(JsText(item.Id)).Append("\";\n")
                .Append("elements_id_array[elements_id_array.length] = ").Append(JsNumber(item.Id)).Append(";\n");
        }

        return sb.ToString();
    }

    private static string PatchVatRows(string html, string subtotal, string vat, string total, decimal rate, bool inclusive)
    {
        const string old = "<tr><td>Subtotal (excl. VAT)</td><td class=\"text-right\">{{TOTAL_MONEY}}</td></tr>\n\t\t\t\t\t"
            + "<tr><th>Total (incl. VAT)</th><th class=\"text-right\">{{TOTAL_MONEY}}</th></tr>";
        var rows = "<tr><td>Subtotal (excl. VAT)</td><td class=\"text-right\">" + subtotal + "</td></tr>\n\t\t\t\t\t";
        if (rate > 0 && vat != total)
        {
            rows += "<tr><td>VAT " + rate.ToString("0.00", CultureInfo.InvariantCulture) + "%</td><td class=\"text-right\">" + vat + "</td></tr>\n\t\t\t\t\t";
        }

        rows += "<tr><th>Total " + (inclusive ? "(incl. VAT)" : "(amount due)") + "</th><th class=\"text-right\">" + total + "</th></tr>";
        return html.Replace(old, rows, StringComparison.Ordinal);
    }

    private static async Task<string> TranslateAsync(string html, Func<string, Task<string>> t)
    {
        var output = new StringBuilder(html.Length);
        var at = 0;
        foreach (Match match in TranslationToken().Matches(html))
        {
            output.Append(html, at, match.Index - at);
            var translated = await t(match.Groups[1].Value).ConfigureAwait(false);
            var openScript = html.LastIndexOf("<script", match.Index, StringComparison.OrdinalIgnoreCase);
            var closeScript = html.LastIndexOf("</script", match.Index, StringComparison.OrdinalIgnoreCase);
            output.Append(openScript > closeScript ? JsSafe(translated) : translated);
            at = match.Index + match.Length;
        }

        return output.Append(html, at, html.Length - at).ToString();
    }

    private static string VatLabel(string type) => type switch
    {
        "local_b2b" => "UAE business (B2B) — prices excl. VAT",
        "gcc" => "GCC buyer — export / zero-rated",
        "export" => "Export — zero-rated",
        "tax_exempt" => "Tax-exempt certificate",
        _ => "UAE retail (B2C) — prices incl. VAT",
    };

    private static string Template(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EcomAE.Platform.Storefront.Templates." + name)
            ?? throw new InvalidOperationException("Missing storefront order template " + name);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string? Config(IReadOnlyDictionary<string, string> config, string key) => config.TryGetValue(key, out var value) ? value : null;
    private static decimal Decimal(string? value) => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : 0;
    private static string Number(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    private static string H(string? value) => StorefrontSupplierLpoNotifier.H(value ?? string.Empty);
    private static string JsText(string? value) => (value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
    internal static string JsSafe(string? value)
    {
        var json = JsonSerializer.Serialize(value ?? string.Empty);
        return json[1..^1];
    }
    private static string JsNumber(string? value) => ShopPayForOrderService.PhpIntCast(value).ToString(CultureInfo.InvariantCulture);

    private static async Task<long> ScalarAsync(DbConnection connection, string sql, object?[] args, CancellationToken cancellationToken)
        => ShopPayForOrderService.PhpIntCast(await ScalarTextAsync(connection, sql, args, cancellationToken).ConfigureAwait(false));

    private static async Task<string?> ScalarTextAsync(DbConnection connection, string sql, object?[] args, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql, object?[] args, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<string?[]>> RowsAsync(DbConnection connection, string sql, object?[] args, CancellationToken cancellationToken)
    {
        var rows = new List<string?[]>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new string?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++)
            {
                row[i] = reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
            }
            rows.Add(row);
        }
        return rows;
    }

    [GeneratedRegex(@"\{\{T:([^{}\r\n]+)\}\}")]
    private static partial Regex TranslationToken();
}
