using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Text;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>PHP <c>content/shop/order_process/my_orders_items.php</c>.</summary>
/// <remarks>Legacy cookie and database values that PHP emits raw are intentionally HTML-escaped.</remarks>
public static class StorefrontMyOrdersItems
{
    public const string LoginFormPostfix = "my_orders_items";
    public const string FilterCookie = "my_orders_items_filter";
    public const string SortCookie = "my_orders_items_sort";
    private static readonly string[] SortFields =
        ["id", "product_name", "price", "count_need", "price_sum", "status", "t2_time_to_exe", "time", "order_id", "office_id", "article", "manufacturer"];

    public sealed record Input(
        long UserId,
        string LangHref,
        string ContentUrl,
        int PageLimit,
        string? Page,
        string? FilterCookieValue,
        string? SortCookieValue,
        string LoginFormHtml);

    private sealed record RefRow(string Id, string? Name, string? Color);

    public static string PhpDate(long unix)
        => ErpDocumentControlRender.PhpDate("dd.MM.yyyy", unix) + "<br><small>" + ErpDocumentControlRender.PhpDate("H:mm", unix) + "</small>";

    public static async Task<string> RenderAsync(
        DbConnection connection,
        Input input,
        Func<string, Task<string>> t,
        Func<long, string> date,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        if (input.UserId > 0)
        {
            await AppendCustomerAsync(sb, connection, input, t, date, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            sb.Append("\t<p>").Append(await t("4564").ConfigureAwait(false)).Append("</p>\n\t<div class=\"panel panel-primary\">\n\t")
                .Append(input.LoginFormHtml).Append("\t</div>\n");
        }

        sb.Append("\n\n\n\n<div id=\"users_agreement_div\" style=\"padding: 0px 15px; border: 1px solid #ddd; background: #f7f7f7; margin:20px 0px;\">\n\t<table>\n\t\t<tr>\n\t\t\t<td><i class=\"fa fa-info-circle\" aria-hidden=\"true\"></i></td>\n\t\t\t<td style=\"line-height: 1.2em; padding: 15px 5px;\">")
            .Append(await t("4560").ConfigureAwait(false)).Append(" <a class=\"text_a\" href=\"").Append(input.LangHref)
            .Append("/shop/orders/zakaz-bez-registracii\">").Append(await t("4561").ConfigureAwait(false))
            .Append("</a></td>\n\t\t</tr>\n\t</table>\n</div>\n\n\n\n\n");
        return sb.ToString().Replace("\n", "\r\n", StringComparison.Ordinal).Replace('\u001e', '\n');
    }

    private static async Task AppendCustomerAsync(
        StringBuilder sb,
        DbConnection connection,
        Input input,
        Func<string, Task<string>> t,
        Func<long, string> date,
        CancellationToken cancellationToken)
    {
        var orderStatuses = await RefsAsync(connection, "SELECT `id`,`name`,`color` FROM `shop_orders_statuses_ref` ORDER BY `order` ASC;", cancellationToken).ConfigureAwait(false);
        var itemStatuses = await RefsAsync(connection, "SELECT `id`,`name`,`color` FROM `shop_orders_items_statuses_ref` ORDER BY `order` ASC;", cancellationToken).ConfigureAwait(false);
        var offices = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var row in await RowsAsync(connection, "SELECT `id`,`caption` FROM `shop_offices`;", [], cancellationToken).ConfigureAwait(false))
        {
            offices[row[0] ?? string.Empty] = row[1];
        }

        var decoded = StorefrontMyOrders.PhpValue.JsonDecode(input.FilterCookieValue);
        var timeFrom = StorefrontMyOrders.PhpValue.Of("");
        var timeTo = StorefrontMyOrders.PhpValue.Of("");
        var orderId = StorefrontMyOrders.PhpValue.Of("");
        long orderStatus = 0;
        long paid = -1;
        long itemStatus = 0;
        if (decoded.Truthy())
        {
            timeFrom = decoded.Get("time_from");
            timeTo = decoded.Get("time_to");
            orderId = decoded.Get("order_id");
            orderStatus = decoded.Get("order_status").IntCast();
            paid = decoded.Get("paid").IntCast();
            itemStatus = decoded.Get("order_item_status").IntCast();
        }

        sb.Append("\n\t<div class=\"row\">\n");
        await AppendDateFilterAsync(sb, "time_from", "3237", timeFrom, t).ConfigureAwait(false);
        await AppendDateFilterAsync(sb, "time_to", "3238", timeTo, t).ConfigureAwait(false);
        sb.Append("\t\t\n\t\t<div class=\"col-md-2\">\n\t\t\t<div>\n                <label style=\"margin-bottom: 0;\" for=\"order_id\">")
            .Append(await t("3244").ConfigureAwait(false)).Append("</label>\n            </div>\n\t\t\t<div>\n\t\t\t\t<input ")
            .Append(orderId.IsEmptyString ? "" : "style=\"background:#b9fcab;\"").Append(" type=\"text\" id=\"order_id\" value=\"")
            .Append(H(orderId.Echo())).Append("\" class=\"form-control\" />\n\t\t\t</div>\n\t\t</div>\n\t\t\n\t\t\n\t\t\n\t\t\n\t\t<div class=\"col-md-2\">\n\t\t\t<div>\n                <label style=\"margin-bottom: 0;\" for=\"paid\">")
            .Append(await t("4350").ConfigureAwait(false)).Append("</label>\n            </div>\n\t\t\t<div>\n\t\t\t\t<select ")
            .Append(paid != -1 ? "style=\"background:#b9fcab;\"" : "").Append(" id=\"paid\" class=\"form-control\">\n\t\t\t\t\t<option value=\"-1\">")
            .Append(await t("2094").ConfigureAwait(false)).Append("</option>\n\t\t\t\t\t<option value=\"1\">")
            .Append(await t("3514").ConfigureAwait(false)).Append("</option>\n\t\t\t\t\t<option value=\"0\">")
            .Append(await t("3513").ConfigureAwait(false)).Append("</option>\n\t\t\t\t\t<option value=\"2\">")
            .Append(await t("3515").ConfigureAwait(false)).Append("</option>\n\t\t\t\t</select>\n\t\t\t\t<script>\n\t\t\t\t\tdocument.getElementById(\"paid\").value = ")
            .Append(paid).Append(";\n\t\t\t\t</script>\n\t\t\t</div>\n\t\t</div>\n\t\t\n\t\t\n\t\t\n\t\t\n\t\t\n\t\t<div class=\"col-md-2\">\n\t\t\t<div>\n                <label style=\"margin-bottom: 0;\" for=\"order_status\">")
            .Append(await t("3603").ConfigureAwait(false)).Append("</label>\n            </div>\n\t\t\t<div>\n\t\t\t\t<select ")
            .Append(orderStatus != 0 ? "style=\"background:#b9fcab;\"" : "").Append(" id=\"order_status\" class=\"form-control\">\n\t\t\t\t<option value=\"0\">")
            .Append(await t("2094").ConfigureAwait(false)).Append("</option>\n\t\t\t\t");
        foreach (var status in orderStatuses)
        {
            sb.Append("\t\t\t\t\t<option value=\"").Append(H(status.Id)).Append("\" ")
                .Append(StorefrontMyOrders.PhpValue.NumericEquals(status.Id, orderStatus) ? "selected=\"selected\"" : "")
                .Append(">").Append(H(await t(status.Name ?? "").ConfigureAwait(false))).Append("</option>\n\t\t\t\t\t");
        }

        sb.Append("\t\t\t\t</select>\n\t\t\t</div>\n\t\t</div>\n\t\t\n\t\t<div class=\"col-md-2\">\n\t\t\t<div>\n                <label style=\"margin-bottom: 0;\" for=\"order_item_status\">")
            .Append(await t("3604").ConfigureAwait(false)).Append("</label>\n            </div>\n\t\t\t<div>\n\t\t\t\t<select ")
            .Append(itemStatus != 0 ? "style=\"background:#b9fcab;\"" : "").Append(" id=\"order_item_status\" class=\"form-control\">\n\t\t\t\t<option value=\"0\">")
            .Append(await t("2094").ConfigureAwait(false)).Append("</option>\n\t\t\t\t");
        foreach (var status in itemStatuses)
        {
            sb.Append("\t\t\t\t\t<option value=\"").Append(H(status.Id)).Append("\" ")
                .Append(StorefrontMyOrders.PhpValue.NumericEquals(status.Id, itemStatus) ? "selected=\"selected\"" : "")
                .Append(">").Append(H(await t(status.Name ?? "").ConfigureAwait(false))).Append("</option>\n\t\t\t\t\t");
        }

        sb.Append("\t\t\t\t</select>\n\t\t\t</div>\n\t\t</div>\n\t</div>\n\t\n")
            .Append(FilterAndSortScript(input.LangHref,
                await t("2232").ConfigureAwait(false),
                await t("2555").ConfigureAwait(false),
                await t("4562").ConfigureAwait(false)));

        var sort = StorefrontMyOrders.PhpValue.JsonDecode(input.SortCookieValue);
        var sortField = sort.Get("field").Echo();
        if (!SortFields.Contains(sortField, StringComparer.Ordinal))
        {
            sortField = "id";
        }

        var sortDirection = sort.Get("asc_desc").LooseEqualsString("asc") ? "asc" : "desc";
        sb.Append("\n\t<div style=\"overflow: hidden; overflow-x: auto;\">\n\t<table class=\"table\">\n\t<tr>\n\t\t<th class=\"hidden\" style=\"vertical-align: middle; white-space: nowrap;\"><input type=\"checkbox\" id=\"check_uncheck_all\" name=\"check_uncheck_all\" onchange=\"on_check_uncheck_all();\"/></th>\n\t\t<th style=\"vertical-align: middle; white-space: nowrap;\"><a href=\"javascript:void(0);\" onclick=\"sortOrdersItems('id');\" id=\"id_sorter\">ID</a></th>\n\t\t<th style=\"vertical-align: middle; white-space: nowrap;\"><a href=\"javascript:void(0);\" onclick=\"sortOrdersItems('manufacturer');\" id=\"manufacturer_sorter\">")
            .Append(await t("2070").ConfigureAwait(false)).Append("</a></th>\n\t\t<th style=\"vertical-align: middle; white-space: nowrap;\"><a href=\"javascript:void(0);\" onclick=\"sortOrdersItems('article');\" id=\"article_sorter\">")
            .Append(await t("2071").ConfigureAwait(false)).Append("</a></th>\n\t\t<th style=\"vertical-align: middle; white-space: nowrap; min-width:200px;\"><a href=\"javascript:void(0);\" onclick=\"sortOrdersItems('product_name');\" id=\"product_name_sorter\">")
            .Append(await t("2102").ConfigureAwait(false)).Append("</a></th>\n\t\t<th style=\"vertical-align: middle; white-space: nowrap;\"><a href=\"javascript:void(0);\" onclick=\"sortOrdersItems('price');\" id=\"price_sorter\">")
            .Append(await t("2751").ConfigureAwait(false)).Append("</a></th>\n\t\t<th style=\"vertical-align: middle; white-space: nowrap; text-align:center;\"><a href=\"javascript:void(0);\" onclick=\"sortOrdersItems('count_need');\" id=\"count_need_sorter\">")
            .Append(await t("4526").ConfigureAwait(false)).Append("</a></th>\n\t\t<th style=\"vertical-align: middle; white-space: nowrap;\"><a href=\"javascript:void(0);\" onclick=\"sortOrdersItems('price_sum');\" id=\"price_sum_sorter\">")
            .Append(await t("3251").ConfigureAwait(false)).Append("</a></th>\n\t\t<th style=\"vertical-align: middle; white-space: nowrap;\"><a href=\"javascript:void(0);\" onclick=\"sortOrdersItems('status');\" id=\"status_sorter\">")
            .Append(await t("2081").ConfigureAwait(false)).Append("</a></th>\n\t\t<th style=\"vertical-align: middle; white-space: nowrap;\"><a href=\"javascript:void(0);\" onclick=\"sortOrdersItems('t2_time_to_exe');\" id=\"t2_time_to_exe_sorter\">")
            .Append(await t("3550").ConfigureAwait(false)).Append("</a></th>\n\t\t\n\t\t\n\t\t<th style=\"vertical-align: middle; white-space: nowrap;\"><a href=\"javascript:void(0);\" onclick=\"sortOrdersItems('time');\" id=\"time_sorter\">")
            .Append(await t("3250").ConfigureAwait(false)).Append("</a></th>\n\t\t<th style=\"vertical-align: middle; white-space: nowrap;\"><a href=\"javascript:void(0);\" onclick=\"sortOrdersItems('order_id');\" id=\"order_id_sorter\">")
            .Append(await t("3243").ConfigureAwait(false)).Append("</a></th>\n\t\t<th class=\"hidden\" style=\"vertical-align: middle; white-space: nowrap;\"><a href=\"javascript:void(0);\" onclick=\"sortOrdersItems('office_id');\" id=\"office_id_sorter\">")
            .Append(await t("3506").ConfigureAwait(false)).Append("</a></th>\n\t</tr>\n\t\n\t<script>\n\t\t\t\tdocument.getElementById(\"")
            .Append(sortField).Append("_sorter\").innerHTML += \"<img src=\\\"/content/files/images/sort_").Append(sortDirection)
            .Append(".png\\\" style=\\\"width:15px; vertical-align: initial;\\\" />\";\n\t</script>\n\t\n\t\n\t\n\t\t\t");

        var args = new List<object?> { input.UserId };
        var where = new StringBuilder(" WHERE `order_id` IN(SELECT `id` FROM `shop_orders` WHERE `shop_orders`.`user_id` = ?)");
        if (!string.IsNullOrEmpty(input.FilterCookieValue))
        {
            AddFilter(decoded, "time_from", " AND (SELECT `time` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`) > ?", where, args);
            AddFilter(decoded, "time_to", " AND (SELECT `time` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`) < ?", where, args);
            AddFilter(decoded, "order_id", " AND `order_id` = ?", where, args);
            AddFilter(decoded, "order_status", " AND (SELECT `status` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`) = ?", where, args, 0);
            AddFilter(decoded, "paid", " AND (SELECT `paid` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`) = ?", where, args, -1);
            AddFilter(decoded, "order_item_status", " AND `status` = ?", where, args, 0);
        }

        var page = input.Page is null ? 1 : UmapiImageProxy.PhpIntCast(input.Page);
        if (page == 0)
        {
            page = 1;
        }

        var offset = page * input.PageLimit - input.PageLimit;
        var sql = "SELECT SQL_CALC_FOUND_ROWS *, `price`*`count_need` AS `price_sum`, "
            + "(SELECT `office_id` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`) AS `office_id`, "
            + "(SELECT `time` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`) AS `time`, "
            + "(SELECT `status` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`) AS `order_status`, "
            + "(SELECT `paid` FROM `shop_orders` WHERE `id` = `shop_orders_items`.`order_id`) AS `paid`, "
            + "`t2_name` AS `product_name`, `t2_article` AS `article`, `t2_manufacturer` AS `manufacturer` "
            + "FROM `shop_orders_items` " + where + " ORDER BY `" + sortField + "` " + sortDirection + " LIMIT "
            + offset.ToString(CultureInfo.InvariantCulture) + ", " + input.PageLimit.ToString(CultureInfo.InvariantCulture);
        var items = await NamedRowsAsync(connection, sql, [.. args], cancellationToken).ConfigureAwait(false);
        var allRows = UmapiImageProxy.PhpIntCast((await RowsAsync(connection, "SELECT FOUND_ROWS();", [], cancellationToken).ConfigureAwait(false))[0][0]);
        var forJs = new StringBuilder("var elements_array = new Array();\u001evar elements_id_array = new Array();\u001e");
        foreach (var item in items)
        {
            var id = item.GetValueOrDefault("id") ?? "";
            var statusId = item.GetValueOrDefault("status") ?? "";
            var status = itemStatuses.FirstOrDefault(s => s.Id == statusId);
            var order = item.GetValueOrDefault("order_id") ?? "";
            var delivery = item.GetValueOrDefault("t2_time_to_exe") ?? "";
            var guaranteed = item.GetValueOrDefault("t2_time_to_exe_guaranteed") ?? "";
            if (StorefrontMyOrders.PhpValue.Of(delivery).IntCast() < StorefrontMyOrders.PhpValue.Of(guaranteed).IntCast())
            {
                delivery += " - " + guaranteed;
            }

            delivery += " дн.";
            if (StorefrontMyOrders.PhpValue.NumericEquals(item.GetValueOrDefault("product_type") ?? "", 1))
            {
                delivery = "";
            }

            var paidCaption = item.GetValueOrDefault("paid") switch
            {
                var p when p is not null && StorefrontMyOrders.PhpValue.NumericEquals(p, 0) => await t("3513").ConfigureAwait(false),
                var p when p is not null && StorefrontMyOrders.PhpValue.NumericEquals(p, 1) => await t("3514").ConfigureAwait(false),
                var p when p is not null && StorefrontMyOrders.PhpValue.NumericEquals(p, 2) => await t("3515").ConfigureAwait(false),
                _ => "",
            };
            forJs.Append("elements_array[elements_array.length] = \"checked_").Append(id).Append("\";\u001eelements_id_array[elements_id_array.length] = ").Append(id).Append(";\u001e");
            sb.Append("<tr style=\"background:").Append(H(status?.Color)).Append("\">\n\t\t\t<td class=\"hidden\" style=\"line-height: 1em; vertical-align: middle;\">\n\t\t\t\t<input type=\"checkbox\" onchange=\"on_one_check_changed('checked_")
                .Append(H(id)).Append("');\" id=\"checked_").Append(H(id)).Append("\" name=\"checked_").Append(H(id)).Append("\"/>\n\t\t\t</td>\n\t\t\t<td style=\"line-height: 1em; vertical-align: middle; white-space: nowrap;\">")
                .Append(H(id)).Append("</td>\n\t\t\t<td style=\"line-height: 1em; vertical-align: middle;\">").Append(H(item.GetValueOrDefault("manufacturer")))
                .Append("</td>\n\t\t\t<td style=\"line-height: 1em; vertical-align: middle;\">").Append(H(item.GetValueOrDefault("article")))
                .Append("</td>\n\t\t\t<td style=\"line-height: 1em; vertical-align: middle;\">").Append(H(item.GetValueOrDefault("product_name")))
                .Append("</td>\n\t\t\t<td style=\"line-height: 1em; vertical-align: middle; white-space: nowrap;\">").Append(NumberFormat(item.GetValueOrDefault("price")))
                .Append("</td>\n\t\t\t<td style=\"line-height: 1em; vertical-align: middle; white-space: nowrap; text-align:center;\">").Append(H(item.GetValueOrDefault("count_need")))
                .Append("</td>\n\t\t\t<td style=\"line-height: 1em; vertical-align: middle; white-space: nowrap;\">").Append(NumberFormat(item.GetValueOrDefault("price_sum")))
                .Append("</td>\n\t\t\t<td style=\"line-height: 1em; vertical-align: middle;\">").Append(H(await t(status?.Name ?? "").ConfigureAwait(false)))
                .Append("</td>\n\t\t\t<td style=\"line-height: 1em; vertical-align: middle; white-space: nowrap;\">").Append(H(delivery))
                .Append("</td>\n\t\t\t<td style=\"line-height: 1em; vertical-align: middle;\">").Append(date(UmapiImageProxy.PhpIntCast(item.GetValueOrDefault("time"))))
                .Append("</td>\n\t\t\t<td style=\"line-height: 1em; vertical-align: middle; white-space: nowrap;\">\n\t\t\t\t<a href=\"").Append(input.LangHref).Append("/shop/orders/order?order_id=").Append(H(order))
                .Append("\">\n\t\t\t\t\t<i class=\"fa fa-sign-in\" aria-hidden=\"true\"></i> ").Append(await t("3243").ConfigureAwait(false)).Append(' ').Append(H(order))
                .Append("\t\t\t\t\t<br>\n\t\t\t\t\t\t\t\t\t\t<span style=\"font-weight:bold;font-size:0.8em;\">").Append(paidCaption)
                .Append("</span>\n\t\t\t\t</a>\n\t\t\t</td>\n\t\t\t<td class=\"hidden\" style=\"line-height: 1em; vertical-align: middle;\">")
                .Append(await t("3506").ConfigureAwait(false)).Append(": ").Append(H(offices.GetValueOrDefault(item.GetValueOrDefault("office_id") ?? "")))
                .Append("</td>\n\t\t\t\n\t\t</tr>\n\t\t\t");
        }

        if (items.Count == 0)
        {
            sb.Length -= 2;
            sb.Append("<tr><td colspan=\"12\">").Append(await t("4563").ConfigureAwait(false)).Append("</td></tr>\t");
        }
        else
        {
            sb.Length--;
        }

        sb.Append("</table>\n\t</div>\n\t");
        if (input.PageLimit != 0 && Math.Ceiling(allRows / (double)input.PageLimit) > 1)
        {
            sb.Append("\n\t\t<div class=\"text-center\">\n\t\t<ul class=\"pagination\">\n\t\t")
                .Append(StorefrontMyOrders.Pagination(allRows, input.PageLimit, 2, page, "active", input.ContentUrl))
                .Append("\t\t</ul>\n\t</div>\n\t\t");
        }
        else
        {
            sb.Append("\n\t\t");
        }

        sb.Append("\n\t<script>\n\t// ----------------------------------------------------------------------------------------\n\t").Append(forJs)
            .Append("\t//Обработка переключения Выделить все/Снять все\n\tfunction on_check_uncheck_all()\n\t{\n\t\tvar state = document.getElementById(\"check_uncheck_all\").checked;\n\t\t\n\t\tfor(var i=0; i<elements_array.length;i++)\n\t\t{\n\t\t\tdocument.getElementById(elements_array[i]).checked = state;\n\t\t}\n\t}//~function on_check_uncheck_all()\n\t// ----------------------------------------------------------------------------------------\n\t//Обработка переключения одного чекбокса\n\tfunction on_one_check_changed(id)\n\t{\n\t\t//Если хотя бы один чекбокс снят - снимаем общий чекбокс\n\t\tfor(var i=0; i<elements_array.length;i++)\n\t\t{\n\t\t\tif(document.getElementById(elements_array[i]).checked == false)\n\t\t\t{\n\t\t\t\tdocument.getElementById(\"check_uncheck_all\").checked = false;\n\t\t\t\tbreak;\n\t\t\t}\n\t\t}\n\t}//~function on_one_check_changed(id)\n\t// ----------------------------------------------------------------------------------------\n\t//Получение массива id отмеченых элементов\n\tfunction getCheckedElements()\n\t{\n\t\tvar checked_ids = new Array();\n\t\t//По массиву чекбоксов\n\t\tfor(var i=0; i<elements_array.length;i++)\n\t\t{\n\t\t\tif(document.getElementById(elements_array[i]).checked == true)\n\t\t\t{\n\t\t\t\tchecked_ids.push(elements_id_array[i]);\n\t\t\t}\n\t\t}\n\t\t\n\t\treturn checked_ids;\n\t}\n\t// ----------------------------------------------------------------------------------------\n\t</script>\n");
    }

    private static async Task AppendDateFilterAsync(StringBuilder sb, string id, string label, StorefrontMyOrders.PhpValue value, Func<string, Task<string>> t)
    {
        sb.Append("\t\t<div class=\"col-md-2\">\n\t\t\t<div>\n                <label style=\"margin-bottom: 0;\" for=\"").Append(id).Append("_show\">")
            .Append(await t(label).ConfigureAwait(false)).Append("</label>\n            </div>\n\t\t\t<div style=\"position: relative; height: 36px;\">\n\t\t\t\t<input style=\"position:absolute; z-index:2; opacity:0;width:100%;\" type=\"text\"  id=\"")
            .Append(id).Append("\" value=\"").Append(H(value.Echo())).Append("\" />\n\t\t\t\t<input style=\" ")
            .Append(value.IsEmptyString ? "" : "background:#b9fcab;").Append(" position:absolute; z-index:1;width:100%;\" type=\"text\" id=\"").Append(id)
            .Append("_show\" class=\"form-control\" />\n\t\t\t\t<script>\n\t\t\t\t//Инициализируем datetimepicker\n\t\t\t\tjQuery(\"#").Append(id)
            .Append("\").datetimepicker({\n\t\t\t\t\tlang:\"ru\",\n\t\t\t\t\tcloseOnDateSelect:true,\n\t\t\t\t\tcloseOnTimeSelect:false,\n\t\t\t\t\tdayOfWeekStart:1,\n\t\t\t\t\tformat:'unixtime',\n\t\t\t\t\tonClose:function(current_time, input)//При закрытии datetimepicker - отображаем в поле индикации\n\t\t\t\t\t{\n\t\t\t\t\t\tvar time_string = \"\";\n\t\t\t\t\t\tvar date_ob = new Date(current_time);\n\t\t\t\t\t\ttime_string += date_ob.getDate()+\".\";\n\t\t\t\t\t\ttime_string += (date_ob.getMonth() + 1)+\".\";\n\t\t\t\t\t\ttime_string += date_ob.getFullYear()+\" \";\n\t\t\t\t\t\ttime_string += date_ob.getHours()+\":\"+date_ob.getMinutes();\n\t\t\t\t\t\tdocument.getElementById(\"")
            .Append(id).Append("_show\").value = time_string;//Показываем время в понятном виде\n\t\t\t\t\t}\n\t\t\t\t\t");
        if (!value.LooseEqualsEmptyString())
        {
            sb.Append("\t\t\t\t\t\t,\n\t\t\t\t\t\tonGenerate:function(current_time, input)//При закрытии datetimepicker - отображаем в поле индикации\n\t\t\t\t\t\t{\n\t\t\t\t\t\t\tvar time_string = \"\";\n\t\t\t\t\t\t\tvar date_ob = new Date(current_time);\n\t\t\t\t\t\t\ttime_string += date_ob.getDate()+\".\";\n\t\t\t\t\t\t\ttime_string += (date_ob.getMonth() + 1)+\".\";\n\t\t\t\t\t\t\ttime_string += date_ob.getFullYear()+\" \";\n\t\t\t\t\t\t\ttime_string += date_ob.getHours()+\":\"+date_ob.getMinutes();\n\t\t\t\t\t\t\tdocument.getElementById(\"")
                .Append(id).Append("_show\").value = time_string;//Показываем время в понятном виде\n\t\t\t\t\t\t}\n\t\t\t\t\t\t");
        }

        sb.Append("\t\t\t\t});\n\t\t\t\t</script>\n\t\t\t</div>\n\t\t</div>\n\t\t\n\t\t\n\t\t\n");
    }

    private static string FilterAndSortScript(string lang, string filter, string clear, string orders)
        => FilterAndSort.Replace("@@LANG@@", lang, StringComparison.Ordinal)
            .Replace("@@FILTER@@", filter, StringComparison.Ordinal)
            .Replace("@@CLEAR@@", clear, StringComparison.Ordinal)
            .Replace("@@ORDERS@@", orders, StringComparison.Ordinal);

    private const string FilterAndSort = """
	<div class="box_btn_filter" style="margin:20px 0px 15px;">
		<button style="margin-right: 2px; margin-bottom:5px;" class="btn btn-ar btn-primary" onclick="filterOrdersItems();">@@FILTER@@</button>
		<button style="margin-right: 2px; margin-bottom:5px;" class="btn btn-ar btn-primary" onclick="unsetFilterOrdersItems();">@@CLEAR@@</button>
		<button style="margin-right: 2px; margin-bottom:5px;" class="btn btn-ar btn-primary" onclick="location='@@LANG@@/shop/orders';">@@ORDERS@@</button>
	</div>
	
	<style>
	@media screen and (min-width: 768px) {
		.box_btn_filter .btn{
			display:inline-block;
		}
		.box_btn_filter .btn[onclick="location='@@LANG@@/shop/orders';"]{
			float:right;
		}
	}
	@media screen and (max-width: 767px) {
		.box_btn_filter .btn{
			display:block;
			float:none;
			width: 100%;
		}
	}
	</style>
	
	<script>
	// ------------------------------------------------------------------------------------------------
	//Устновка cookie в соответствии с фильтром
	function filterOrdersItems()
	{
		var my_orders_items_filter = new Object;
		
		//1. Время с
		my_orders_items_filter.time_from = encodeURIComponent(document.getElementById("time_from").value);
		
		//2. Время по
		my_orders_items_filter.time_to = encodeURIComponent(document.getElementById("time_to").value);
		
		//3. Номер заказа
		my_orders_items_filter.order_id = encodeURIComponent(document.getElementById("order_id").value);
		
		//4. Статус заказа
		my_orders_items_filter.order_status = encodeURIComponent(document.getElementById("order_status").value);
		
		//5. Оплачен
		my_orders_items_filter.paid = encodeURIComponent(document.getElementById("paid").value);
		
		//6. Статус позиции
		my_orders_items_filter.order_item_status = encodeURIComponent(document.getElementById("order_item_status").value);
		
		
		//Устанавливаем cookie (на полгода)
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = "my_orders_items_filter="+JSON.stringify(my_orders_items_filter)+"; path=/; expires=" + date.toUTCString();
		
		//Обновляем страницу
		location='@@LANG@@/shop/orders/items';
	}
	// ------------------------------------------------------------------------------------------------
	//Снять все фильтры
	function unsetFilterOrdersItems()
	{
		var my_orders_items_filter = new Object;
		
		//1. Время с
		my_orders_items_filter.time_from = "";
		
		//2. Время по
		my_orders_items_filter.time_to = "";
		
		//3. Номер заказа
		my_orders_items_filter.order_id = "";
		
		//4. Статус заказа
		my_orders_items_filter.order_status = 0;
		
		//5. Товар
		my_orders_items_filter.paid = -1;
		
		//6. Статус позиции
		my_orders_items_filter.order_item_status = 0;
		
		//Устанавливаем cookie (на полгода)
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = "my_orders_items_filter="+JSON.stringify(my_orders_items_filter)+"; path=/; expires=" + date.toUTCString();
		
		//Обновляем страницу
		location='@@LANG@@/shop/orders/items';
	}
	// ------------------------------------------------------------------------------------------------
	</script>
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	<script>
	// ------------------------------------------------------------------------------------------------
	//Установка куки сортировки позиций заказов
	function sortOrdersItems(field)
	{
		var asc_desc = "asc";//Направление по умолчанию
		
		//Берем из куки текущий вариант сортировки
		var current_sort_cookie = getCookie("my_orders_items_sort");
		if(current_sort_cookie != undefined)
		{
			current_sort_cookie = JSON.parse(getCookie("my_orders_items_sort"));
			//Если поле это же - обращаем направление
			if(current_sort_cookie.field == field)
			{
				if(current_sort_cookie.asc_desc == "asc")
				{
					asc_desc = "desc";
				}
				else
				{
					asc_desc = "asc";
				}
			}
		}
		
		
		var my_orders_items_sort = new Object;
		my_orders_items_sort.field = field;//Поле, по которому сортировать
		my_orders_items_sort.asc_desc = asc_desc;//Направление сортировки
		
		//Устанавливаем cookie (на полгода)
		var date = new Date(new Date().getTime() + 15552000 * 1000);
		document.cookie = "my_orders_items_sort="+JSON.stringify(my_orders_items_sort)+"; path=/; expires=" + date.toUTCString();
		
		//Обновляем страницу
		location='@@LANG@@/shop/orders/items';
	}
	// ------------------------------------------------------------------------------------------------
	// возвращает cookie с именем name, если есть, если нет, то undefined
	function getCookie(name) 
	{
		var matches = document.cookie.match(new RegExp(
			"(?:^|; )" + name.replace(/([\.$?*|{}\(\)\[\]\\\/\+^])/g, '\\$1') + "=([^;]*)"
		));
		return matches ? decodeURIComponent(matches[1]) : undefined;
	}
	// ------------------------------------------------------------------------------------------------
	</script>
	
	
""";

    private static void AddFilter(StorefrontMyOrders.PhpValue filter, string key, string sql, StringBuilder where, List<object?> args, long? sentinel = null)
    {
        var value = filter.Get(key);
        var enabled = sentinel is null ? !value.LooseEqualsEmptyString() : !value.LooseEqualsInt(sentinel.Value);
        if (enabled)
        {
            where.Append(sql);
            args.Add(value.IntCast());
        }
    }

    public static string Pagination(long all, long limit, long page, string url)
        => StorefrontMyOrders.Pagination(all, limit, 2, page, "active", url);

    private static string NumberFormat(string? value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? number.ToString("#,0.00", CultureInfo.InvariantCulture).Replace(',', ' ')
            : "0.00";

    private static string H(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static async Task<List<RefRow>> RefsAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
        => (await RowsAsync(connection, sql, [], cancellationToken).ConfigureAwait(false))
            .Select(row => new RefRow(row[0] ?? "", row[1], row[2])).ToList();

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

    private static async Task<List<Dictionary<string, string?>>> NamedRowsAsync(DbConnection connection, string sql, object?[] args, CancellationToken cancellationToken)
    {
        var rows = new List<Dictionary<string, string?>>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new Dictionary<string, string?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
            }

            rows.Add(row);
        }

        return rows;
    }
}
