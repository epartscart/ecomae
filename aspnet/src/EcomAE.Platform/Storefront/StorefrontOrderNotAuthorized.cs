using System.Data.Common;
using System.Globalization;
using System.Text;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/shop/order_process/my_order_not_authorized.php</c> (<c>/shop/orders/zakaz-bez-registracii</c>), the
/// GET page: the action alerts, then for <c>?order_id=</c> the guest order (<c>user_id = 0</c>) summary, payment state
/// and the pay / pay-on-place blocks, then the order lookup form. A missing order prints PHP's redirect script and stops.
/// The order data comes from PHP <c>orders_background.php</c> (statuses, line statuses left out of totals, offices).
/// </summary>
/// <remarks>
/// Intended deviation: PHP echoes the raw <c>order_id</c> query value (MySQL matches <c>'10abc'</c> to order 10), which
/// is a reflected XSS. The page escapes it in HTML and prints the matched order id in the script.
/// </remarks>
public static class StorefrontOrderNotAuthorized
{
    public const string PagePath = "/shop/orders/zakaz-bez-registracii";

    public static readonly int[] StringIds =
    [
        4525, 4551, 3244, 2242, 4418, 4645, 3507, 2081, 4528, 4529, 3516, 4379, 4530, 3513, 3514, 3515, 4552, 4533, 4534,
        3524, 4535, 4664, 3526, 4539, 4540, 4541, 4343, 4344, 4345, 4553, 4554, 4555,
    ];

    /// <param name="OrderId">PHP <c>$_GET['order_id']</c>, null when not set.</param>
    /// <param name="CsrfKey">The visitor session's <c>csrf_guard_key</c> (PHP <c>DP_User::getUserSession()</c>), empty when none.</param>
    /// <param name="PickerHtml">The output of <c>epc_payment_method_picker.php</c>.</param>
    /// <param name="AlertsHtml">The output of <c>actions_alert.php</c>.</param>
    public sealed record Input(
        string? OrderId,
        string LangHref,
        string CsrfKey,
        IReadOnlyDictionary<string, string> Config,
        string PickerHtml,
        string AlertsHtml);

    /// <summary>PHP <c>date("d.m.Y", $t)." ".date("G:i", $t)</c> in the server time zone.</summary>
    public static string PhpDate(long unix) => ErpDocumentControlRender.PhpDate("dd.MM.yyyy H:mm", unix);

    public static async Task<string> RenderAsync(
        DbConnection connection,
        Input input,
        Func<string, Task<string>> t,
        Func<long, string> date,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder(input.AlertsHtml);
        if (input.OrderId is not null)
        {
            if (!await AppendOrderAsync(sb, connection, input, t, date, cancellationToken).ConfigureAwait(false))
            {
                return sb.ToString();
            }
        }

        sb.Append("<div class=\"panel panel-primary\">\n\t<div class=\"panel-heading\">").Append(await t("4553").ConfigureAwait(false))
            .Append("</div>\n\t<div class=\"panel-body\">\n\t\t<form method=\"GET\">\n\t\t\t<input type=\"hidden\" name=\"csrf_guard_key\" value=\"").Append(input.CsrfKey)
            .Append("\" />\n\t\t\t\n\t\t\t<div class=\"input-group\">\n\t\t\t\t<input value=\"\" type=\"text\" class=\"form-control\" placeholder=\"").Append(await t("4554").ConfigureAwait(false))
            .Append("\" name=\"order_id\" />\n\t\t\t\t<span class=\"input-group-btn\">\n\t\t\t\t\t<button class=\"btn btn-ar btn-primary\" type=\"submit\">").Append(await t("4555").ConfigureAwait(false))
            .Append("</button>\n\t\t\t\t</span>\n\t\t\t</div>\n\n\t\t</form>\n\t</div>\n</div>");
        return sb.ToString();
    }

    /// <summary>The order part; false when the order is missing and PHP has printed the redirect and exited.</summary>
    private static async Task<bool> AppendOrderAsync(
        StringBuilder sb,
        DbConnection connection,
        Input input,
        Func<string, Task<string>> t,
        Func<long, string> date,
        CancellationToken cancellationToken)
    {
        var statuses = await MapAsync(connection, "SELECT `id`, `name` FROM `shop_orders_statuses_ref` ORDER BY `order` ASC", cancellationToken).ConfigureAwait(false);
        var notCount = new List<long>();
        foreach (var row in await RowsAsync(connection, "SELECT `id`, `count_flag` FROM `shop_orders_items_statuses_ref` ORDER BY `order` ASC", [], cancellationToken).ConfigureAwait(false))
        {
            if (row[1] is null || PhpNumber(row[1]) == 0)
            {
                notCount.Add(UmapiImageProxy.PhpIntCast(row[0]));
            }
        }

        var offices = await MapAsync(connection, "SELECT `id`, `caption` FROM `shop_offices`", cancellationToken).ConfigureAwait(false);

        var where = string.Concat(notCount.Select(id => " AND `status` != " + id.ToString(CultureInfo.InvariantCulture)));
        const string income = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 1 AND `order_id` = ?), 0)";
        const string issue = "IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active` = 1 AND `income` = 0 AND `order_id` = ?),0)";
        var sql = "SELECT `id`, `time`, `office_id`, `status`, `paid`, `paid_type`, (SELECT `caption` FROM `shop_obtaining_modes` WHERE `id` = `shop_orders`.`how_get`) AS `obtain_caption`, CAST( ("
            + issue + " - " + income + ") AS DECIMAL(20,2) ) AS `paid_sum`, CAST( ( (SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id`= `shop_orders`.`id` "
            + where + " ) - (" + issue + " - " + income + ") ) AS DECIMAL(20,2) )  AS `paid_left`, CAST( (SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id`= `shop_orders`.`id` "
            + where + " ) AS DECIMAL(20,2)) AS `price_sum` FROM `shop_orders` WHERE `id` = ? AND `user_id` = ?;";
        var raw = input.OrderId!;
        var found = await RowsAsync(connection, sql, [raw, raw, raw, raw, raw, 0], cancellationToken).ConfigureAwait(false);
        if (found.Count == 0)
        {
            sb.Append("\t\t<script>\n\t\tlocation = '").Append(input.LangHref).Append(PagePath).Append("?info_message=")
                .Append(OAuthStart.PhpUrlEncode(await t("4525").ConfigureAwait(false)))
                .Append("';\n\t\t</script>\n\t\t");
            return false;
        }

        var paidTypes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in await RowsAsync(connection, "SELECT `id`, `name` FROM `shop_orders_paid_type` WHERE `active` = 1 ORDER BY `order`;", [], cancellationToken).ConfigureAwait(false))
        {
            paidTypes[row[0] ?? string.Empty] = await t(row[1] ?? string.Empty).ConfigureAwait(false);
        }

        var order = found[0];
        var rowId = UmapiImageProxy.PhpIntCast(order[0]).ToString(CultureInfo.InvariantCulture);
        var time = order[1] is null ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : UmapiImageProxy.PhpIntCast(order[1]);
        var officeCaption = order[2] is not null && offices.TryGetValue(order[2]!, out var caption) ? caption ?? string.Empty : string.Empty;
        var status = order[3] is not null && statuses.TryGetValue(order[3]!, out var statusName) ? statusName ?? string.Empty : string.Empty;
        var paid = order[4] is null ? 0 : PhpNumber(order[4]);
        var paidType = order[5];
        var obtainCaption = order[6] ?? string.Empty;
        var paidSum = order[7] ?? string.Empty;
        var paidLeft = order[8];
        var priceSum = order[9];
        var shownId = StorefrontSupplierLpoNotifier.H(raw);

        sb.Append("    \n    <p>").Append(await t("4551").ConfigureAwait(false))
            .Append("</p>\n    \n    <table class=\"table\">\n        <tr> <td>").Append(await t("3244").ConfigureAwait(false)).Append("</td> <td>").Append(shownId)
            .Append("</td> <tr>\n        <tr> <td>").Append(await t("2242").ConfigureAwait(false)).Append("</td> <td>").Append(date(time))
            .Append("</td> <tr>\n        <tr> <td>").Append(await t("4418").ConfigureAwait(false)).Append("</td> <td>").Append(await t(officeCaption).ConfigureAwait(false))
            .Append("</td> <tr>\n\t\t<tr> <td>").Append(await t("4645").ConfigureAwait(false)).Append("</td> <td>")
            .Append(paidType is not null && paidTypes.TryGetValue(paidType, out var paidTypeName) && paidTypeName.Length > 0 && paidTypeName != "0" ? paidTypeName : string.Empty)
            .Append("</td> </tr>\n        <tr> <td>").Append(await t("3507").ConfigureAwait(false)).Append("</td> <td>").Append(await t(obtainCaption).ConfigureAwait(false))
            .Append("</td> <tr>\n        <tr> <td>").Append(await t("2081").ConfigureAwait(false)).Append("</td> <td>").Append(await t(status).ConfigureAwait(false))
            .Append("</td> <tr>\n    </table>\n    \n\t\n\n\t\n\t<div id=\"\">\n\t\t<div class=\"panel panel-primary\">\n\t\t\t<div class=\"panel-body\">\n\t\t\t\t<div style=\"overflow: hidden; overflow-x: auto;\">\n\t\t\t\t\t<p class=\"lead\">")
            .Append(await t("4528").ConfigureAwait(false))
            .Append("</p>\n\t\t\t\t\n\t\t\t\t\t<table class=\"table\">\n\t\t\t\t\t\t<tr>\n\t\t\t\t\t\t\t<td>").Append(await t("4529").ConfigureAwait(false))
            .Append(":</td>\n\t\t\t\t\t\t\t<td>").Append(await t("3516").ConfigureAwait(false))
            .Append(":</td>\n\t\t\t\t\t\t\t<td>").Append(await t("4379").ConfigureAwait(false))
            .Append(":</td>\n\t\t\t\t\t\t\t<td>").Append(await t("4530").ConfigureAwait(false))
            .Append(":</td>\n\t\t\t\t\t\t</tr>\n\t\t\t\t\t\t<tr>\n\t\t\t\t\t\t\t<td>\n\t\t\t\t\t\t\t\t<strong>\n\t\t\t\t\t\t\t\t\t");
        var badge = paid switch
        {
            0 => ("#e74c3c", "3513"),
            1 => ("#62cb31", "3514"),
            2 => ("#3498db", "3515"),
            _ => (null, null),
        };
        if (badge.Item1 is not null)
        {
            sb.Append("<div style=\"color:#FFF;background-color:").Append(badge.Item1).Append(";border-radius:3px;padding:6px 12px;font-weight:normal;\">")
                .Append(await t(badge.Item2!).ConfigureAwait(false)).Append("</div>");
        }

        sb.Append("\t\t\t\t\t\t\t\t</strong>\n\t\t\t\t\t\t\t</td>\n\t\t\t\t\t\t\t<td>").Append(priceSum ?? string.Empty)
            .Append("</td>\n\t\t\t\t\t\t\t<td>").Append(paidSum)
            .Append("</td>\n\t\t\t\t\t\t\t<td>").Append(paidLeft ?? string.Empty)
            .Append("</td>\n\t\t\t\t\t\t</tr>\n\t\t\t\t\t</table>\n\t\t\t\t</div>\n\t\t\t\t\n\t\t\t\t\n\t\t\t\t\n\t\t\t\t\n\t\t\t\t");
        if (paid != 1)
        {
            await AppendPaymentAsync(sb, input, t, rowId, shownId, paid, paidType, paidTypes, paidLeft, priceSum).ConfigureAwait(false);
        }

        sb.Append("\t\t\t</div>\n\t\t</div>\n\t</div>\n\t");
        return true;
    }

    private static async Task AppendPaymentAsync(
        StringBuilder sb,
        Input input,
        Func<string, Task<string>> t,
        string rowId,
        string shownId,
        double paid,
        string? paidType,
        IReadOnlyDictionary<string, string> paidTypes,
        string? paidLeft,
        string? priceSum)
    {
        var left = paidLeft ?? string.Empty;
        var partial = Truthy(Config(input, "partial_payment"));
        var percent = Config(input, "partial_payment_min_percent") ?? string.Empty;
        var minPay = string.Empty;
        sb.Append("\t\t\t\t\t<div class=\"form-horizontal\">\n\t\t\t\t\t\t").Append(input.PickerHtml).Append("\t\t\t\t\t\t");
        if (partial)
        {
            var min = StorefrontPhpAjax.PhpFloatCast(priceSum) * (StorefrontPhpAjax.PhpFloatCast(percent) / 100);
            minPay = StorefrontPhpAjax.PhpFloatString(min);
            sb.Append("\t\t\t\t\t\t\t\n\t\t\t\t\t\t\t<div class=\"form-group col-md-12\">\n\t\t\t\t\t\t\t");
            if (paidLeft is null || StorefrontPhpAjax.PhpFloatCast(paidLeft) <= min)
            {
                sb.Append("\t\t\t\t\t\t\t\t<input type=\"hidden\" value=\"").Append(left).Append("\" id=\"pay_value\" />\n\t\t\t\t\t\t\t\t<p>")
                    .Append(await t("4552").ConfigureAwait(false)).Append(' ').Append(left)
                    .Append("</p>\n\t\t\t\t\t\t\t\t<button onclick=\"add_payment_to_order();\" type=\"button\" class=\"btn btn-ar btn-primary\">")
                    .Append(await t("4533").ConfigureAwait(false)).Append("</button>\n\t\t\t\t\t\t\t\t");
            }
            else
            {
                sb.Append("\t\t\t\t\t\t\t\t<label>").Append(await t("4534").ConfigureAwait(false))
                    .Append(":</label>\n\t\t\t\t\t\t\t\t<div class=\"header-search-box\">\n\t\t\t\t\t\t\t\t\t<div class=\"input-group\">\n\t\t\t\t\t\t\t\t\t\t<input style=\"padding-left:7px;!important;\" type=\"number\" class=\"form-control\" placeholder=\"")
                    .Append(await t("3524").ConfigureAwait(false)).Append("\" value=\"").Append(left)
                    .Append("\" id=\"pay_value\" />\n\t\t\t\t\t\t\t\t\t\t<span class=\"input-group-btn\">\n\t\t\t\t\t\t\t\t\t\t\t<button onclick=\"add_payment_to_order();\" type=\"button\" class=\"btn btn-ar btn-primary\">")
                    .Append(await t("4533").ConfigureAwait(false))
                    .Append("</button>\n\t\t\t\t\t\t\t\t\t\t</span>\n\t\t\t\t\t\t\t\t\t</div>\n\t\t\t\t\t\t\t\t</div>\n\t\t\t\t\t\t\t\t");
            }

            sb.Append("\t\t\t\t\t\t\t</div>\n\t\t\t\t\t\t\t");
        }
        else
        {
            sb.Append("\t\t\t\t\t\t\t<input type=\"hidden\" value=\"").Append(left).Append("\" id=\"pay_value\" />\n\t\t\t\t\t\t\t")
                .Append("\t\t\t\t\t\t\t<a class=\"btn btn-ar btn-primary\" href=\"javascript:void(0);\" onclick=\"add_payment_to_order();\">")
                .Append(await t("4535").ConfigureAwait(false)).Append("</a>\n\t\t\t\t\t\t\t");
        }

        sb.Append("\t\t\t\t\t\t\n\t\t\t\t\t\t");
        if (PhpNumber(Config(input, "order_pay_on_place")) == 1 && paid == 0 && (paidType is null || PhpNumber(paidType) == 0))
        {
            sb.Append("\t\t\t\t\t\t\t<a class=\"btn btn-ar btn-default\" href=\"javascript:void(0);\" onclick=\"pay_on_place();\">")
                .Append(paidTypes.TryGetValue("1", out var onPlace) ? onPlace : string.Empty)
                .Append("</a>\n\t\t\t\t\t\t\t<form id=\"pay_form\" name=\"pay_form\" method=\"POST\" style=\"display:none;\">\n\t\t\t\t\t\t\t\t<input type=\"hidden\" name=\"action\" id=\"action\" value=\"pay_on_place\" />\n\t\t\t\t\t\t\t\t<input type=\"hidden\" name=\"order_id\" value=\"")
                .Append(shownId)
                .Append("\" />\n\t\t\t\t\t\t\t\t<input type=\"hidden\" name=\"csrf_guard_key\" value=\"").Append(input.CsrfKey)
                .Append("\" />\n\t\t\t\t\t\t\t</form>\n\t\t\t\t\t\t\t<script>\n\t\t\t\t\t\t\tfunction pay_on_place()\n\t\t\t\t\t\t\t{\n\t\t\t\t\t\t\t\tif( !confirm('")
                .Append(await t("4664").ConfigureAwait(false))
                .Append("') )\n\t\t\t\t\t\t\t\t{\n\t\t\t\t\t\t\t\t\treturn;\n\t\t\t\t\t\t\t\t}\n\t\t\t\t\t\t\t\tdocument.forms[\"pay_form\"].submit();\n\t\t\t\t\t\t\t}\n\t\t\t\t\t\t\t</script>\n\t\t\t\t\t\t");
        }

        sb.Append("\t\t\t\t\t\t\n\t\t\t\t\t</div>\n\t\t\t\t\t\n\t\t\t\t\t\n\t\t\t\t\t\n\t\t\t\t\t<script>\n\t\t\t\t\t//Обработка кнопки оплаты\n\t\t\t\t\tfunction add_payment_to_order()\n\t\t\t\t\t{\n\t\t\t\t\t\t//Сумма из поля ввода\n\t\t\t\t\t\tvar pay_value = document.getElementById('pay_value').value;\n\t\t\t\t\t\tpay_value = parseFloat(pay_value).toFixed(2);\n\t\t\t\t\t\t\n\t\t\t\t\t\t//Локальные проверки:\n\t\t\t\t\t\t\n\t\t\t\t\t\t//1. Должна быть указана сумма\n\t\t\t\t\t\tif( pay_value == '' || pay_value == 'NaN' )\n\t\t\t\t\t\t{\n\t\t\t\t\t\t\talert('")
            .Append(await t("3526").ConfigureAwait(false))
            .Append("');\n\t\t\t\t\t\t\treturn;\n\t\t\t\t\t\t}\n\t\t\t\t\t\t//2. Сумма не должна превышать остаток долга клиента по заказу, не должна быть отрицательной, не должна быть равна 0\n\t\t\t\t\t\tif( pay_value > ")
            .Append(left)
            .Append(" || pay_value <= 0 )\n\t\t\t\t\t\t{\n\t\t\t\t\t\t\talert('")
            .Append(await t("4539").ConfigureAwait(false))
            .Append("');\n\t\t\t\t\t\t\treturn;\n\t\t\t\t\t\t}\n\t\t\t\t\t\t\n\t\t\t\t\t\t\n\t\t\t\t\t\t");
        if (partial)
        {
            sb.Append("\t\t\t\t\t\t\t//Если желаемый платеж меньше оставшегося долга по заказу\n\t\t\t\t\t\t\tif( pay_value < ").Append(left)
                .Append(" )\n\t\t\t\t\t\t\t{\n\t\t\t\t\t\t\t\t//Проверяем, чтобы он был не менее минимально-допустимого платежа\n\t\t\t\t\t\t\t\tif( pay_value < ").Append(minPay)
                .Append(" )\n\t\t\t\t\t\t\t\t{\n\t\t\t\t\t\t\t\t\talert('").Append(await t("4540").ConfigureAwait(false)).Append(' ').Append(percent).Append("% ")
                .Append(await t("4541").ConfigureAwait(false)).Append(": ").Append(minPay)
                .Append("');\n\t\t\t\t\t\t\t\t\treturn;\n\t\t\t\t\t\t\t\t}\n\t\t\t\t\t\t\t}\n\t\t\t\t\t\t\t");
        }

        sb.Append("\t\t\t\t\t\t\n\n\t\t\t\t\t\tvar request_object = new Object;\n\t\t\t\t\t\trequest_object.order_id = ").Append(rowId)
            .Append(";\n\t\t\t\t\t\trequest_object.amount = pay_value;\n\t\t\t\t\t\tif (typeof window.epcSelectedPayHandler === 'function') {\n\t\t\t\t\t\t\trequest_object.pay_handler = window.epcSelectedPayHandler();\n\t\t\t\t\t\t}\n\t\t\t\t\t\t\n\t\t\t\t\t\tjQuery.ajax({\n\t\t\t\t\t\t\ttype: \"POST\",\n\t\t\t\t\t\t\tasync: false, //Запрос синхронный\n\t\t\t\t\t\t\turl: \"/content/shop/finance/ajax_create_operation.php\",\n\t\t\t\t\t\t\tdataType: \"text\",//Тип возвращаемого значения\n\t\t\t\t\t\t\tdata: \"request_object=\"+encodeURI(JSON.stringify(request_object))+\"&csrf_guard_key=")
            .Append(input.CsrfKey)
            .Append("\",\n\t\t\t\t\t\t\tsuccess: function(answer)\n\t\t\t\t\t\t\t{\n\t\t\t\t\t\t\t\tconsole.log(answer);\n\t\t\t\t\n\t\t\t\t\t\t\t\tvar answer_ob = JSON.parse(answer);\n\t\t\t\t\t\t\t\t\n\t\t\t\t\t\t\t\tif( typeof answer_ob.result == 'undefined' )\n\t\t\t\t\t\t\t\t{\n\t\t\t\t\t\t\t\t\talert(\"")
            .Append(await t("4343").ConfigureAwait(false))
            .Append("\");\n\t\t\t\t\t\t\t\t}\n\t\t\t\t\t\t\t\telse\n\t\t\t\t\t\t\t\t{\n\t\t\t\t\t\t\t\t\tif(answer_ob.result == true)\n\t\t\t\t\t\t\t\t\t{\t\t\t\t\t\n\t\t\t\t\t\t\t\t\t\tif( answer_ob.pay_system == 0 )\n\t\t\t\t\t\t\t\t\t\t{\n\t\t\t\t\t\t\t\t\t\t\talert(\"")
            .Append(await t("4344").ConfigureAwait(false))
            .Append("\");\n\t\t\t\t\t\t\t\t\t\t\treturn;\n\t\t\t\t\t\t\t\t\t\t}\n\t\t\t\t\t\t\t\t\t\telse\n\t\t\t\t\t\t\t\t\t\t{\n\t\t\t\t\t\t\t\t\t\t\tlocation = \"/content/shop/finance/payment_systems/\"+answer_ob.pay_system+\"/go_to_pay.php?operation=\"+answer_ob.operation+\"&csrf_guard_key=")
            .Append(input.CsrfKey)
            .Append("\";\n\t\t\t\t\t\t\t\t\t\t}\n\t\t\t\t\t\t\t\t\t}\n\t\t\t\t\t\t\t\t\telse\n\t\t\t\t\t\t\t\t\t{\n\t\t\t\t\t\t\t\t\t\talert(\"")
            .Append(await t("4345").ConfigureAwait(false))
            .Append("\");\n\t\t\t\t\t\t\t\t\t}\n\t\t\t\t\t\t\t\t}\n\t\t\t\t\t\t\t}\n\t\t\t\t\t\t});\n\t\t\t\t\t}\n\t\t\t\t\t</script>\n\t\t\t\t\t");
    }

    /// <param name="Form">The posted fields, last value per key (PHP <c>$_POST</c>).</param>
    /// <param name="AlertsHtml">The output of <c>actions_alert.php</c>, which PHP prints before it handles the post.</param>
    public sealed record PostInput(
        IReadOnlyDictionary<string, string> Form,
        string? Session,
        string? UserCookie,
        string? AdminSession,
        string? AdminUser,
        string? Referer,
        string LangHref,
        string AlertsHtml);

    /// <summary>A post PHP handles: a non-empty <c>action</c>.</summary>
    public static bool IsActionPost(IReadOnlyDictionary<string, string> form)
        => form.TryGetValue("action", out var action) && Truthy(action);

    /// <summary>
    /// The POST half of the page: <c>stop_csrf.php</c> (its JSON on failure), then action <c>pay_on_place</c> for a visitor
    /// (<c>DP_User::getUserId()</c> 0) on a guest order with no payment type whose office exists: <c>paid_type = 1</c>, the
    /// order log line, and the robot status protocol to the <c>for_paid</c> status through <paramref name="setOrderStatus"/>.
    /// The answer is the alerts plus PHP's script back to the order with <c>error_message</c> / <c>success_message</c>.
    /// </summary>
    public static async Task<StorefrontPhpAjax.RawHttp> PostAsync(
        DbConnection connection,
        PostInput input,
        Func<string, Task<string>> t,
        Func<long, long, Task<bool>> setOrderStatus,
        long now,
        CancellationToken cancellationToken)
    {
        const string html = "text/html; charset=UTF-8";
        var form = input.Form;
        var csrf = await StorefrontPhpAjax.StopCsrfAsync(
            connection,
            form.TryGetValue("csrf_guard_key", out var key) ? key : null,
            input.Session,
            input.UserCookie,
            input.AdminSession,
            input.AdminUser,
            input.Referer,
            html,
            cancellationToken).ConfigureAwait(false);
        if (csrf is not null)
        {
            return new StorefrontPhpAjax.RawHttp(input.AlertsHtml + csrf.Body, html);
        }

        var userId = await ErpUserAccess.UserIdAsync(connection, new ErpUserAccess.Cookies(input.Session, input.UserCookie, null, null), cancellationToken).ConfigureAwait(false);
        var error = await t("2122").ConfigureAwait(false) + ": <br/> " + await t("2304").ConfigureAwait(false) + ".";
        var success = string.Empty;
        var orderIdText = string.Empty;
        if (form.GetValueOrDefault("action") == "pay_on_place")
        {
            var orderId = UmapiImageProxy.PhpIntCast(form.GetValueOrDefault("order_id"));
            orderIdText = orderId.ToString(CultureInfo.InvariantCulture);
            var paidTypes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var row in await RowsAsync(connection, "SELECT `id`, `name` FROM `shop_orders_paid_type` WHERE `active` = 1 ORDER BY `order`;", [], cancellationToken).ConfigureAwait(false))
            {
                paidTypes[row[0] ?? string.Empty] = await t(row[1] ?? string.Empty).ConfigureAwait(false);
            }

            if (userId == 0)
            {
                var order = await RowsAsync(connection, "SELECT `office_id` FROM `shop_orders` WHERE `id` = ? AND `user_id` = ? AND `paid_type` = 0;", [orderId, 0], cancellationToken).ConfigureAwait(false);
                var offices = await MapAsync(connection, "SELECT `id`, `caption` FROM `shop_offices`", cancellationToken).ConfigureAwait(false);
                if (order.Count > 0 && order[0][0] is { } officeId && offices.ContainsKey(officeId))
                {
                    var updated = true;
                    try
                    {
                        await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `shop_orders` SET `paid_type` = 1 WHERE `id` = ?;"), cancellationToken, orderId).ConfigureAwait(false);
                    }
                    catch (DbException)
                    {
                        updated = false;
                    }

                    if (!updated)
                    {
                        error = await t("2122").ConfigureAwait(false) + ": <br/> " + await t("3629").ConfigureAwait(false) + ".";
                    }
                    else
                    {
                        success = await t("3630").ConfigureAwait(false);
                        error = string.Empty;
                        await ErpDb.ExecuteAsync(
                            connection,
                            null,
                            ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`, `is_robot`) VALUES (?, ?, ?, ?, ?, ?);"),
                            cancellationToken,
                            orderId, now, userId, 0, await t("4645").ConfigureAwait(false) + ": <b>" + paidTypes.GetValueOrDefault("1", string.Empty) + "</b>", 0).ConfigureAwait(false);
                        var forPaid = await RowsAsync(connection, "SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_paid` = 1;", [], cancellationToken).ConfigureAwait(false);
                        if (forPaid.Count > 0 && !await setOrderStatus(orderId, UmapiImageProxy.PhpIntCast(forPaid[0][0])).ConfigureAwait(false))
                        {
                            error = await t("2385").ConfigureAwait(false) + " <br/> " + await t("4662").ConfigureAwait(false) + ".";
                        }
                    }
                }
            }
        }

        var location = input.LangHref + PagePath + "?order_id=" + orderIdText;
        return new StorefrontPhpAjax.RawHttp(
            input.AlertsHtml + "\t<script>\n\t\tlocation=\"" + location + "&error_message=" + error + "&success_message=" + success + "\";\n\t</script>\n\t",
            html);
    }

    private static string? Config(Input input, string key) => input.Config.TryGetValue(key, out var value) ? value : null;

    /// <summary>PHP truthiness of a config value: not null, not <c>''</c> and not <c>'0'</c>.</summary>
    private static bool Truthy(string? value) => !string.IsNullOrEmpty(value) && value != "0";

    /// <summary>The number PHP 8 compares a numeric string as in <c>==</c>; NaN for a non-numeric string.</summary>
    private static double PhpNumber(string? value)
    {
        if (value is null)
        {
            return 0;
        }

        return double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN;
    }

    private static async Task<Dictionary<string, string?>> MapAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var row in await RowsAsync(connection, sql, [], cancellationToken).ConfigureAwait(false))
        {
            map[row[0] ?? string.Empty] = row[1];
        }

        return map;
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
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[i] = reader.IsDBNull(i)
                    ? null
                    : reader.GetValue(i) is bool flag ? (flag ? "1" : "0") : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
            }

            rows.Add(row);
        }

        return rows;
    }
}
