using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/shop/order_process/my_orders.php</c> (<c>/shop/orders</c>): for a customer the order filter (kept in the
/// <c>my_orders_filter</c> cookie, or set from <c>?garage=</c>), the orders table sorted by the <c>my_orders_sort</c>
/// cookie with the hidden line rows, and PHP's pagination; for a visitor the login panel. Both end with the guest order
/// note. Shared references come from the port of PHP's <c>orders_background.php</c> include.
/// </summary>
/// <remarks>
/// Intended deviation: PHP prints the filter cookie values, garage captions and order line texts without escaping. The
/// page escapes them with <c>htmlspecialchars</c>.
/// </remarks>
public static partial class StorefrontMyOrders
{
    public const string LoginFormPostfix = "my_orders";

    public const string LoginFormTarget = "shop/orders";

    public const int LoginTextStringId = 4559;

    public const string FilterCookie = "my_orders_filter";

    public const string SortCookie = "my_orders_sort";

    private static readonly string[] SortFields = ["id", "time", "price_sum", "paid", "paid_type", "status", "obtain_caption", "office_id", "garage"];

    /// <param name="UserId">PHP <c>DP_User::getUserId()</c>.</param>
    /// <param name="ContentUrl">PHP <c>$DP_Content->url</c>, the base of the pagination links.</param>
    /// <param name="PageLimit">PHP <c>$DP_Config->list_page_limit</c>.</param>
    /// <param name="Page">PHP <c>$_GET['page']</c>.</param>
    /// <param name="Garage">PHP <c>$_GET['garage']</c>.</param>
    /// <param name="Read">PHP <c>$_GET['read']</c>.</param>
    /// <param name="FilterCookieValue">PHP <c>$_COOKIE['my_orders_filter']</c> (URL-decoded).</param>
    /// <param name="SortCookieValue">PHP <c>$_COOKIE['my_orders_sort']</c> (URL-decoded).</param>
    /// <param name="LoginFormHtml">The output of <c>modules/login/login_form_general.php</c> for a visitor.</param>
    public sealed record Input(
        long UserId,
        string LangHref,
        string ContentUrl,
        int PageLimit,
        string? Page,
        string? Garage,
        string? Read,
        string? FilterCookieValue,
        string? SortCookieValue,
        string LoginFormHtml);

    /// <summary>PHP <c>date("d.m.Y", $t)."&lt;br&gt;&lt;small&gt;".date("G:i", $t)."&lt;/small&gt;"</c> in the server time zone.</summary>
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
            await AppendOrdersAsync(sb, connection, input, t, date, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            sb.Append("    <p>").Append(await t("4559").ConfigureAwait(false)).Append("</p>\n    \n\t\n\t<div class=\"panel panel-primary\">\n\t").Append(input.LoginFormHtml).Append("\t</div>\n\t\n    ");
        }

        sb.Append("\n\n\n\n<div id=\"users_agreement_div\" style=\"padding: 0px 15px; border: 1px solid #ddd; background: #f7f7f7; margin:20px 0px;\">\n\t<table>\n\t\t<tr>\n\t\t\t<td><i class=\"fa fa-info-circle\" aria-hidden=\"true\"></i></td>\n\t\t\t<td style=\"line-height: 1.2em; padding: 15px 5px;\">").Append(await t("4560").ConfigureAwait(false)).Append(" <a class=\"text_a\" href=\"").Append(input.LangHref)
            .Append("/shop/orders/zakaz-bez-registracii\">").Append(await t("4561").ConfigureAwait(false)).Append("</a></td>\n\t\t</tr>\n\t</table>\n</div>\n\n\n\n\n");
        return sb.ToString();
    }

    private static async Task AppendOrdersAsync(
        StringBuilder sb,
        DbConnection connection,
        Input input,
        Func<string, Task<string>> t,
        Func<long, string> date,
        CancellationToken cancellationToken)
    {
        var background = await StorefrontOrdersBackground.LoadAsync(connection, cancellationToken).ConfigureAwait(false);
        var statuses = new Dictionary<string, (string? Name, string? Color)>(StringComparer.Ordinal);
        foreach (var (id, row) in background.OrderStatuses)
        {
            statuses[id.ToString(CultureInfo.InvariantCulture)] = (row["name"], row["color"]);
        }

        var itemStatuses = new Dictionary<string, (string? Name, string? Color)>(StringComparer.Ordinal);
        foreach (var (id, row) in background.ItemStatuses)
        {
            itemStatuses[id.ToString(CultureInfo.InvariantCulture)] = (row["name"], row["color"]);
        }

        var notCount = background.ItemStatusesNotCount.Select(id => (object?)id).ToList();
        var offices = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (id, row) in background.Offices)
        {
            offices[id.ToString(CultureInfo.InvariantCulture)] = row["caption"];
        }

        var paidTypes = new List<(string Id, string? Name)>();
        foreach (var row in await RowsAsync(connection, "SELECT `id`, `name` FROM `shop_orders_paid_type` WHERE `active` = 1 ORDER BY `order`;", [], cancellationToken).ConfigureAwait(false))
        {
            Put(paidTypes, row[0] ?? string.Empty, row[1]);
        }

        var garages = new List<(string Id, string? Caption)>();
        foreach (var row in await RowsAsync(connection, "SELECT `id`, `caption` FROM `shop_docpart_garage` WHERE `user_id` = ? ORDER BY `active` DESC, `caption` ASC;", [input.UserId], cancellationToken).ConfigureAwait(false))
        {
            Put(garages, row[0] ?? string.Empty, StripCaption(row[1]));
        }

        var filterCookie = input.FilterCookieValue;
        if (input.Garage is { } garageQuery && garages.Any(g => g.Id == garageQuery))
        {
            var garageId = UmapiImageProxy.PhpIntCast(garageQuery).ToString(CultureInfo.InvariantCulture);
            sb.Append("\t\t\t<script>\n\t\t\t//Удалить параметр из адреса\n\t\t\tlet url = new URL(document.location);\n\t\t\tlet searchParams = url.searchParams;\n\t\t\tsearchParams.delete(\"garage\");\n\t\t\twindow.history.pushState({}, '', url.toString());\n\t\t\t\n\t\t\t//Устанавливаем cookie (на полгода)\n\t\t\tvar my_orders_filter = new Object;\n\t\t\t\tmy_orders_filter.time_from = \"\";\n\t\t\t\tmy_orders_filter.time_to = \"\";\n\t\t\t\tmy_orders_filter.order_id = \"\";\n\t\t\t\tmy_orders_filter.status = 0;\n\t\t\t\tmy_orders_filter.paid = -1;\n\t\t\t\tmy_orders_filter.paid_type = -1;\n\t\t\t\tmy_orders_filter.garage = ").Append(garageId).Append(";\n\t\t\t\t\n\t\t\tvar date = new Date(new Date().getTime() + 15552000 * 1000);\n\t\t\tdocument.cookie = \"my_orders_filter=\"+JSON.stringify(my_orders_filter)+\"; path=/; expires=\" + date.toUTCString();\n\t\t\t</script>\n\t\t\t");
            filterCookie = "{\"time_from\":\"\",\"time_to\":\"\",\"order_id\":\"\",\"status\":0,\"paid\":-1,\"paid_type\":-1,\"garage\":" + garageId + "}";
        }

        var filter = PhpValue.JsonDecode(filterCookie);
        var timeFrom = PhpValue.Of(string.Empty);
        var timeTo = PhpValue.Of(string.Empty);
        var orderId = PhpValue.Of(string.Empty);
        long status = 0;
        long paid = -1;
        long paidType = -1;
        long garage = 0;
        if (filter.Truthy())
        {
            timeFrom = filter.Get("time_from");
            timeTo = filter.Get("time_to");
            orderId = filter.Get("order_id");
            status = filter.Get("status").IntCast();
            paid = filter.Get("paid").IntCast();
            paidType = filter.Get("paid_type").IntCast();
            garage = filter.Get("garage").IntCast();
        }

        const string green = "style=\"background:#b9fcab;\"";
        sb.Append("\t\n\t\n\t<div class=\"row\">\n\t\t<div class=\"col-md-2\">\n            <div>\n                <label style=\"margin-bottom: 0;\" for=\"time_from_show\">").Append(await t("3237").ConfigureAwait(false)).Append("</label>\n            </div>\n\t\t\t<div style=\"position: relative; height: 36px;\">\n\t\t\t\t<input style=\"position:absolute; z-index:2; opacity:0;width:100%;\" type=\"text\"  id=\"time_from\" value=\"").Append(H(timeFrom.Echo())).Append("\" />\n\t\t\t\t<input style=\" ")
            .Append(timeFrom.IsEmptyString ? string.Empty : "background:#b9fcab;").Append(" position:absolute; z-index:1;width:100%;\" type=\"text\" id=\"time_from_show\" class=\"form-control\" />\n\t\t\t\t<script>\n\t\t\t\t//Инициализируем datetimepicker\n\t\t\t\tjQuery(\"#time_from\").datetimepicker({\n\t\t\t\t\tlang:\"ru\",\n\t\t\t\t\tcloseOnDateSelect:true,\n\t\t\t\t\tcloseOnTimeSelect:false,\n\t\t\t\t\tdayOfWeekStart:1,\n\t\t\t\t\tformat:'unixtime',\n\t\t\t\t\tonClose:function(current_time, input)//При закрытии datetimepicker - отображаем в поле индикации\n\t\t\t\t\t{\n\t\t\t\t\t\tvar time_string = \"\";\n\t\t\t\t\t\tvar date_ob = new Date(current_time);\n\t\t\t\t\t\ttime_string += date_ob.getDate()+\".\";\n\t\t\t\t\t\ttime_string += (date_ob.getMonth() + 1)+\".\";\n\t\t\t\t\t\ttime_string += date_ob.getFullYear()+\" \";\n\t\t\t\t\t\ttime_string += date_ob.getHours()+\":\"+date_ob.getMinutes();\n\t\t\t\t\t\tdocument.getElementById(\"time_from_show\").value = time_string;//Показываем время в понятном виде\n\t\t\t\t\t}\n\t\t\t\t\t");
        if (!timeFrom.LooseEqualsEmptyString())
        {
            sb.Append("\t\t\t\t\t\t,\n\t\t\t\t\t\tonGenerate:function(current_time, input)//При закрытии datetimepicker - отображаем в поле индикации\n\t\t\t\t\t\t{\n\t\t\t\t\t\t\tvar time_string = \"\";\n\t\t\t\t\t\t\tvar date_ob = new Date(current_time);\n\t\t\t\t\t\t\ttime_string += date_ob.getDate()+\".\";\n\t\t\t\t\t\t\ttime_string += (date_ob.getMonth() + 1)+\".\";\n\t\t\t\t\t\t\ttime_string += date_ob.getFullYear()+\" \";\n\t\t\t\t\t\t\ttime_string += date_ob.getHours()+\":\"+date_ob.getMinutes();\n\t\t\t\t\t\t\tdocument.getElementById(\"time_from_show\").value = time_string;//Показываем время в понятном виде\n\t\t\t\t\t\t}\n\t\t\t\t\t\t");
        }

        sb.Append("\t\t\t\t});\n\t\t\t\t</script>\n\t\t\t</div>\n        </div>\n\t\t\n\t\t<div class=\"col-md-2\">\n            <div>\n                <label style=\"margin-bottom: 0;\" for=\"time_to_show\">").Append(await t("3238").ConfigureAwait(false)).Append("</label>\n            </div>\n\t\t\t<div style=\"position: relative; height: 36px;\">\n\t\t\t\t<input style=\"position:absolute; z-index:2; opacity:0;width:100%;\" type=\"text\"  id=\"time_to\" value=\"").Append(H(timeTo.Echo())).Append("\" />\n\t\t\t\t<input style=\" ")
            .Append(timeTo.IsEmptyString ? string.Empty : "background:#b9fcab;").Append(" position:absolute; z-index:1;width:100%;\" type=\"text\" id=\"time_to_show\" class=\"form-control\" />\n\t\t\t\t<script>\n\t\t\t\t//Инициализируем datetimepicker\n\t\t\t\tjQuery(\"#time_to\").datetimepicker({\n\t\t\t\t\tlang:\"ru\",\n\t\t\t\t\tcloseOnDateSelect:true,\n\t\t\t\t\tcloseOnTimeSelect:false,\n\t\t\t\t\tdayOfWeekStart:1,\n\t\t\t\t\tformat:'unixtime',\n\t\t\t\t\tonClose:function(current_time, input)//При закрытии datetimepicker - отображаем в поле индикации\n\t\t\t\t\t{\n\t\t\t\t\t\tvar time_string = \"\";\n\t\t\t\t\t\tvar date_ob = new Date(current_time);\n\t\t\t\t\t\ttime_string += date_ob.getDate()+\".\";\n\t\t\t\t\t\ttime_string += (date_ob.getMonth() + 1)+\".\";\n\t\t\t\t\t\ttime_string += date_ob.getFullYear()+\" \";\n\t\t\t\t\t\ttime_string += date_ob.getHours()+\":\"+date_ob.getMinutes();\n\t\t\t\t\t\tdocument.getElementById(\"time_to_show\").value = time_string;//Показываем время в понятном виде\n\t\t\t\t\t}\n\t\t\t\t\t");
        if (!timeTo.LooseEqualsEmptyString())
        {
            sb.Append("\t\t\t\t\t\t,\n\t\t\t\t\t\tonGenerate:function(current_time, input)//При закрытии datetimepicker - отображаем в поле индикации\n\t\t\t\t\t\t{\n\t\t\t\t\t\t\tvar time_string = \"\";\n\t\t\t\t\t\t\tvar date_ob = new Date(current_time);\n\t\t\t\t\t\t\ttime_string += date_ob.getDate()+\".\";\n\t\t\t\t\t\t\ttime_string += (date_ob.getMonth() + 1)+\".\";\n\t\t\t\t\t\t\ttime_string += date_ob.getFullYear()+\" \";\n\t\t\t\t\t\t\ttime_string += date_ob.getHours()+\":\"+date_ob.getMinutes();\n\t\t\t\t\t\t\tdocument.getElementById(\"time_to_show\").value = time_string;//Показываем время в понятном виде\n\t\t\t\t\t\t}\n\t\t\t\t\t\t");
        }

        sb.Append("\t\t\t\t});\n\t\t\t\t</script>\n\t\t\t</div>\n        </div>\n\t\t\n\t\t<div class=\"col-md-2\">\n            <div>\n                <label style=\"margin-bottom: 0;\" for=\"order_id\">").Append(await t("3244").ConfigureAwait(false)).Append("</label>\n            </div>\n            <div>\n                <input ").Append(orderId.IsEmptyString ? string.Empty : green)
            .Append(" type=\"text\"  id=\"order_id\" value=\"").Append(H(orderId.Echo())).Append("\" class=\"form-control\" />\n            </div>\n        </div>\n\n\t\t<div class=\"col-md-2\">\n            <div>\n                <label style=\"margin-bottom: 0;\" for=\"paid\">").Append(await t("4350").ConfigureAwait(false))
            .Append("</label>\n            </div>\n            <div>\n                <select ").Append(paid != -1 ? green : string.Empty).Append(" id=\"paid\" class=\"form-control\">\n                    <option value=\"-1\">").Append(await t("2094").ConfigureAwait(false))
            .Append("</option>\n                    <option value=\"1\">").Append(await t("3514").ConfigureAwait(false)).Append("</option>\n                    <option value=\"0\">").Append(await t("3513").ConfigureAwait(false))
            .Append("</option>\n                    <option value=\"2\">").Append(await t("3515").ConfigureAwait(false)).Append("</option>\n                </select>\n                <script>\n                    document.getElementById(\"paid\").value = ").Append(paid.ToString(CultureInfo.InvariantCulture))
            .Append(";\n                </script>\n            </div>\n        </div>\n\t\t\n\t\t<div class=\"col-md-2 hidden\">\n            <div>\n                <label style=\"margin-bottom: 0;\" for=\"paid_type\">").Append(await t("4645").ConfigureAwait(false)).Append("</label>\n            </div>\n            <div>\n                <select ").Append(paidType != -1 ? green : string.Empty)
            .Append(" id=\"paid_type\" class=\"form-control\">\n                    <option value=\"-1\">").Append(await t("2094").ConfigureAwait(false)).Append("</option>\n\t\t\t\t\t");
        foreach (var (id, name) in paidTypes)
        {
            sb.Append("\t\t\t\t\t<option value=\"").Append(id).Append("\">").Append(name ?? string.Empty).Append("</option>\n\t\t\t\t\t");
        }

        sb.Append("                    <option value=\"0\">").Append(await t("3253").ConfigureAwait(false)).Append("</option>\n                </select>\n                <script>\n                    document.getElementById(\"paid_type\").value = ").Append(paidType.ToString(CultureInfo.InvariantCulture))
            .Append(";\n                </script>\n            </div>\n        </div>\n\t\t\n\t\t<div class=\"col-md-2\">\n            <div>\n                <label style=\"margin-bottom: 0;\" for=\"garage\">").Append(await t("627").ConfigureAwait(false)).Append("</label>\n            </div>\n            <div>\n                <select ").Append(garage != 0 ? green : string.Empty)
            .Append(" id=\"garage\" class=\"form-control\">\n                    <option value=\"0\">").Append(await t("2094").ConfigureAwait(false)).Append("</option>\n\t\t\t\t\t");
        foreach (var (id, caption) in garages)
        {
            sb.Append("\t\t\t\t\t<option value=\"").Append(id).Append("\">").Append(H(caption)).Append("</option>\n\t\t\t\t\t");
        }

        sb.Append("                    <option value=\"-1\">").Append(await t("3253").ConfigureAwait(false)).Append("</option>\n                </select>\n                <script>\n                    document.getElementById(\"garage\").value = ").Append(garage.ToString(CultureInfo.InvariantCulture))
            .Append(";\n                </script>\n            </div>\n        </div>\n\t\t\n\t\t<div class=\"col-md-2\">\n            <div>\n                <label style=\"margin-bottom: 0;\" for=\"status-select\">").Append(await t("2081").ConfigureAwait(false)).Append("</label>\n            </div>\n            <div>\n                <select ").Append(status != 0 ? green : string.Empty)
            .Append(" id=\"status-select\" class=\"form-control\">\n                <option value=\"0\">").Append(await t("2094").ConfigureAwait(false)).Append("</option>\n                ");
        foreach (var (id, statusRow) in statuses)
        {
            sb.Append("                    <option value=\"").Append(id).Append("\" ")
                .Append(PhpValue.NumericEquals(id, status) ? "selected=\"selected\"" : string.Empty)
                .Append(">").Append(await t(statusRow.Name ?? string.Empty).ConfigureAwait(false)).Append("</option>\n                    ");
        }

        sb.Append("                </select>\n            </div>\n        </div>    \n    </div>\n\t\n\t<div class=\"box_btn_filter\" style=\"margin:20px 0px 15px;\">\n\t\t<button style=\"margin-right: 2px; margin-bottom:5px;\" class=\"btn btn-ar btn-primary\" onclick=\"filterOrders();\">").Append(await t("2232").ConfigureAwait(false)).Append("</button>\n\t\t<button style=\"margin-right: 2px; margin-bottom:5px;\" class=\"btn btn-ar btn-primary\" onclick=\"unsetFilterOrders();\">").Append(await t("2555").ConfigureAwait(false))
            .Append("</button>\n\t\t<button style=\"margin-right: 2px; margin-bottom:5px;\" class=\"btn btn-ar btn-primary\" onclick=\"location='").Append(input.LangHref).Append("/shop/orders/items';\">").Append(await t("4556").ConfigureAwait(false))
            .Append("</button>\n\t\t<button style=\"margin-right: 2px; margin-bottom:5px;\" class=\"hidden-xs hidden-sm btn btn-ar btn-primary\" onclick=\"show_orders_items();\"><i style=\"margin-right: 0px;\" class=\"fa fa-level-down\"></i></button>\n    </div>\n\t\n\t<style>\n\t@media screen and (min-width: 768px) {\n\t\t.box_btn_filter .btn{\n\t\t\tdisplay:inline-block;\n\t\t}\n\t\t.box_btn_filter .btn[onclick=\"location='").Append(input.LangHref).Append("/shop/orders/items';\"]{\n\t\t\tfloat:right;\n\t\t}\n\t\t.box_btn_filter .btn[onclick=\"show_orders_items();\"]{\n\t\t\tfloat:right;\n\t\t}\n\t}\n\t@media screen and (max-width: 767px) {\n\t\t.box_btn_filter .btn{\n\t\t\tdisplay:block;\n\t\t\tfloat:none;\n\t\t\twidth: 100%;\n\t\t}\n\t}\n\t</style>\n    \n    <script>\n    // ------------------------------------------------------------------------------------------------\n\tfunction show_orders_items(){\n\t\tif($('.orders-items-tr').css('display') == 'table-row'){\n\t\t\t$('.orders-items-tr').css('display', 'none');\n\t\t}else{\n\t\t\t$('.orders-items-tr').css('display', 'table-row');\n\t\t}\n\t}\n    // ------------------------------------------------------------------------------------------------\n    //Устновка cookie в соответствии с фильтром\n    function filterOrders()\n    {\n        var my_orders_filter = new Object;\n        \n        //1. Время с\n        my_orders_filter.time_from = encodeURIComponent(document.getElementById(\"time_from\").value);\n        //2. Время по\n        my_orders_filter.time_to = encodeURIComponent(document.getElementById(\"time_to\").value);\n        \n        //3. Номер заказа\n        my_orders_filter.order_id = encodeURIComponent(document.getElementById(\"order_id\").value);\n        \n        //4. Статус заказа\n        my_orders_filter.status = encodeURIComponent(document.getElementById(\"status-select\").value);\n        \n        //5. Оплачен\n        my_orders_filter.paid = encodeURIComponent(document.getElementById(\"paid\").value);\n\t\t\n\t\t//6. Способ оплаты\n        my_orders_filter.paid_type = encodeURIComponent(document.getElementById(\"paid_type\").value);\n\t\t\n\t\t//7. Гараж\n        my_orders_filter.garage = encodeURIComponent(document.getElementById(\"garage\").value);\n\t\t\n        //Устанавливаем cookie (на полгода)\n        var date = new Date(new Date().getTime() + 15552000 * 1000);\n        document.cookie = \"my_orders_filter=\"+JSON.stringify(my_orders_filter)+\"; path=/; expires=\" + date.toUTCString();\n        \n        //Обновляем страницу\n        location='").Append(input.LangHref).Append("/shop/orders';\n    }\n    // ------------------------------------------------------------------------------------------------\n    //Снять все фильтры\n    function unsetFilterOrders()\n    {\n        var my_orders_filter = new Object;\n        \n        //1. Время с\n        my_orders_filter.time_from = \"\";\n        //2. Время по\n        my_orders_filter.time_to = \"\";\n        \n        //3. Номер заказа\n        my_orders_filter.order_id = \"\";\n        \n        //4. Статус заказа\n        my_orders_filter.status = 0;\n        \n        //5. Оплачен\n        my_orders_filter.paid = -1;\n        \n\t\t//6. Способ оплаты\n        my_orders_filter.paid_type = -1;\n\t\t\n\t\t//7. Гараж\n        my_orders_filter.garage = 0;\n\t\t\n        //Устанавливаем cookie (на полгода)\n        var date = new Date(new Date().getTime() + 15552000 * 1000);\n        document.cookie = \"my_orders_filter=\"+JSON.stringify(my_orders_filter)+\"; path=/; expires=\" + date.toUTCString();\n        \n        //Обновляем страницу\n        location='").Append(input.LangHref)
            .Append("/shop/orders';\n    }\n    // ------------------------------------------------------------------------------------------------\n    </script>\n    \n    \n    \n    \n    \n    \n    \n    \n    \n    \n    <script>\n    // ------------------------------------------------------------------------------------------------\n    //Установка куки сортировки заказов\n    function sortOrders(field)\n    {\n        var asc_desc = \"asc\";//Направление по умолчанию\n        \n        //Берем из куки текущий вариант сортировки\n        var current_sort_cookie = getCookie(\"my_orders_sort\");\n        if(current_sort_cookie != undefined)\n        {\n            current_sort_cookie = JSON.parse(getCookie(\"my_orders_sort\"));\n            //Если поле это же - обращаем направление\n            if(current_sort_cookie.field == field)\n            {\n                if(current_sort_cookie.asc_desc == \"asc\")\n                {\n                    asc_desc = \"desc\";\n                }\n                else\n                {\n                    asc_desc = \"asc\";\n                }\n            }\n        }\n        \n        \n        var my_orders_sort = new Object;\n        my_orders_sort.field = field;//Поле, по которому сортировать\n        my_orders_sort.asc_desc = asc_desc;//Направление сортировки\n        \n        //Устанавливаем cookie (на полгода)\n        var date = new Date(new Date().getTime() + 15552000 * 1000);\n        document.cookie = \"my_orders_sort=\"+JSON.stringify(my_orders_sort)+\"; path=/; expires=\" + date.toUTCString();\n        \n        //Обновляем страницу\n        location='").Append(input.LangHref).Append("/shop/orders';\n    }\n    // ------------------------------------------------------------------------------------------------\n    // возвращает cookie с именем name, если есть, если нет, то undefined\n    function getCookie(name) \n    {\n        var matches = document.cookie.match(new RegExp(\n            \"(?:^|; )\" + name.replace(/([\\.$?*|{}\\(\\)\\[\\]\\\\\\/\\+^])/g, '\\\\$1') + \"=([^;]*)\"\n        ));\n        return matches ? decodeURIComponent(matches[1]) : undefined;\n    }\n    // ------------------------------------------------------------------------------------------------\n    </script>\n\t\n\t\n    <div style=\"overflow: hidden; overflow-x: auto;\">\n    <table class=\"table\">\n\t\t<tr>\n\t\t\t<th class=\"hidden\" style=\"vertical-align: middle; white-space: nowrap;\"><input type=\"checkbox\" id=\"check_uncheck_all\" name=\"check_uncheck_all\" onchange=\"on_check_uncheck_all();\"/></th>\n\t\t\t<th style=\"vertical-align: middle; white-space: nowrap;\"><a href=\"javascript:void(0);\" onclick=\"sortOrders('id');\" id=\"id_sorter\">ID</a></th>\n\t\t\t<th style=\"vertical-align: middle; white-space: nowrap;\"><a href=\"javascript:void(0);\" onclick=\"sortOrders('time');\" id=\"time_sorter\">").Append(await t("3250").ConfigureAwait(false))
            .Append("</a></th>\n\t\t\t<th style=\"vertical-align: middle; white-space: nowrap;\"><a href=\"javascript:void(0);\" onclick=\"sortOrders('price_sum');\" id=\"price_sum_sorter\">").Append(await t("3251").ConfigureAwait(false)).Append("</a></th>\n\t\t\t<th style=\"vertical-align: middle; white-space: nowrap;\"><a href=\"javascript:void(0);\" onclick=\"sortOrders('paid');\" id=\"paid_sorter\">").Append(await t("4557").ConfigureAwait(false))
            .Append("</a></th>\n\t\t\t<th style=\"vertical-align: middle; white-space: nowrap;\"><a href=\"javascript:void(0);\" onclick=\"sortOrders('paid_type');\" id=\"paid_type_sorter\">").Append(await t("4645").ConfigureAwait(false)).Append("</a></th>\n\t\t\t<th style=\"vertical-align: middle; white-space: nowrap;\"><a href=\"javascript:void(0);\" onclick=\"sortOrders('status');\" id=\"status_sorter\">").Append(await t("2081").ConfigureAwait(false))
            .Append("</a></th>\n\t\t\t<th style=\"vertical-align: middle; white-space: nowrap;\"><a href=\"javascript:void(0);\" onclick=\"sortOrders('obtain_caption');\" id=\"obtain_caption_sorter\">").Append(await t("3507").ConfigureAwait(false)).Append("</a></th>\n\t\t\t<th style=\"vertical-align: middle; white-space: nowrap;\"><a href=\"javascript:void(0);\" onclick=\"sortOrders('office_id');\" id=\"office_id_sorter\">").Append(await t("3506").ConfigureAwait(false))
            .Append("</a></th>\n\t\t\t<th style=\"vertical-align: middle; white-space: nowrap;\"></th>\n\t\t</tr>\n    \n            <script>\n                ");

        var sortField = "id";
        var sortDirection = "desc";
        if (!string.IsNullOrEmpty(input.SortCookieValue))
        {
            var sort = PhpValue.JsonDecode(input.SortCookieValue);
            var field = sort.Get("field");
            sortDirection = sort.Get("asc_desc").LooseEqualsString("asc") ? "asc" : "desc";
            sortField = field.Kind == PhpKind.String && SortFields.Contains(field.Echo(), StringComparer.Ordinal) ? field.Echo() : "id";
        }

        sb.Append("                document.getElementById(\"").Append(sortField).Append("_sorter\").innerHTML += \"<img src=\\\"/content/files/images/sort_").Append(sortDirection).Append(".png\\\" style=\\\"width:15px; vertical-align: initial;\\\" />\";\n            </script>\n \n        ");

        var args = new List<object?>(notCount);
        var notCountWhere = string.Concat(notCount.Select(_ => " AND `status` != ?"));
        args.Add(input.UserId);
        var where = new StringBuilder(" WHERE `user_id` = ?");
        var conditions = PhpValue.JsonDecode(filterCookie);
        if (!string.IsNullOrEmpty(filterCookie))
        {
            if (!conditions.Get("time_from").LooseEqualsEmptyString())
            {
                where.Append(" AND `time` > ?");
                args.Add(conditions.Get("time_from").IntCast());
            }

            if (!conditions.Get("time_to").LooseEqualsEmptyString())
            {
                where.Append(" AND `time` < ?");
                args.Add(conditions.Get("time_to").IntCast());
            }

            if (!conditions.Get("order_id").LooseEqualsEmptyString())
            {
                where.Append(" AND `id` = ?");
                args.Add(conditions.Get("order_id").IntCast());
            }

            if (!conditions.Get("status").LooseEqualsInt(0))
            {
                where.Append(" AND `status` = ?");
                args.Add(conditions.Get("status").IntCast());
            }

            if (!conditions.Get("paid").LooseEqualsInt(-1))
            {
                where.Append(" AND `paid` = ?");
                args.Add(conditions.Get("paid").IntCast());
            }

            if (!conditions.Get("paid_type").LooseEqualsInt(-1))
            {
                where.Append(" AND `paid_type` = ?");
                args.Add(conditions.Get("paid_type").IntCast());
            }

            if (!conditions.Get("garage").LooseEqualsInt(0))
            {
                if (conditions.Get("garage").LooseGreaterThanZero())
                {
                    where.Append(" AND `id` IN(SELECT `order_id` FROM `shop_docpart_garage_orders` WHERE `garage_id` = ?)");
                    args.Add(conditions.Get("garage").IntCast());
                }
                else
                {
                    where.Append(" AND `id` NOT IN(SELECT `order_id` FROM `shop_docpart_garage_orders` WHERE `garage_id` IN(SELECT `id` FROM `shop_docpart_garage` WHERE `user_id` = ?))");
                    args.Add(input.UserId);
                }
            }
        }

        if (input.Read is not null && UmapiImageProxy.PhpIntCast(input.Read) == 0)
        {
            where.Append(" AND `id` IN(SELECT DISTINCT `order_id` FROM `shop_orders_messages` WHERE `read` = 0 AND `is_customer` = 0 AND `order_id` > 0) ");
        }

        var page = input.Page is null ? 1 : UmapiImageProxy.PhpIntCast(input.Page);
        if (page == 0)
        {
            page = 1;
        }

        long limit = input.PageLimit;
        var from = (page * limit) - limit;
        var sql = "SELECT SQL_CALC_FOUND_ROWS *, `shop_orders`.`id` AS `id`, `shop_orders`.`time` AS `time`, "
            + "(SELECT `caption` FROM `shop_obtaining_modes` WHERE `id` = `shop_orders`.`how_get` ) AS `obtain_caption`, "
            + "`shop_orders`.`paid` AS `paid`, `shop_orders`.`status` AS `status`, "
            + " (SELECT COUNT(*) FROM `shop_orders_messages` WHERE `order_id` = `shop_orders`.`id` AND `read` = 0 AND `is_customer` = 0) AS `count_not_viewed_msg`, "
            + " (SELECT GROUP_CONCAT(`garage_id`) FROM `shop_docpart_garage_orders` WHERE `order_id` = `shop_orders`.`id`) AS `garage`, "
            + " CAST((SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id`= `shop_orders`.`id` " + notCountWhere + " ) AS DECIMAL(8,2)) AS `price_sum` "
            + " FROM `shop_orders` " + where + " ORDER BY `" + sortField + "` " + sortDirection + " LIMIT "
            + from.ToString(CultureInfo.InvariantCulture) + ", " + limit.ToString(CultureInfo.InvariantCulture);
        var orders = await NamedRowsAsync(connection, sql, [.. args], cancellationToken).ConfigureAwait(false);
        var allRows = UmapiImageProxy.PhpIntCast((await RowsAsync(connection, "SELECT FOUND_ROWS();", [], cancellationToken).ConfigureAwait(false))[0][0]);

        var forJs = new StringBuilder("var elements_array = new Array();\nvar elements_id_array = new Array();\n");
        var items = new Dictionary<string, List<Dictionary<string, string?>>>(StringComparer.Ordinal);
        var ids = orders.Select(o => UmapiImageProxy.PhpIntCast(o.GetValueOrDefault("id"))).Where(id => id > 0).ToList();
        if (ids.Count > 0)
        {
            var itemSql = "SELECT * FROM `shop_orders_items` WHERE `order_id` IN(" + string.Join(',', ids.Select(id => id.ToString(CultureInfo.InvariantCulture))) + ")";
            foreach (var item in await NamedRowsAsync(connection, itemSql, [], cancellationToken).ConfigureAwait(false))
            {
                var key = item.GetValueOrDefault("order_id") ?? string.Empty;
                if (!items.TryGetValue(key, out var list))
                {
                    items[key] = list = [];
                }

                list.Add(item);
            }
        }
        else
        {
            sb.Append("\t\t\t<tr>\n\t\t\t\t<td colspan=\"10\" style=\"text-align:center;padding:24px;color:#64748b;\">\n\t\t\t\t\tNo orders yet. <a href=\"").Append(H(input.LangHref + "/shop/cart")).Append("\">Open cart</a>\n\t\t\t\t\tor <a href=\"").Append(H(input.LangHref + "/parts")).Append("\">search parts</a>.\n\t\t\t\t</td>\n\t\t\t</tr>\n\t\t\t");
        }

        foreach (var order in orders)
        {
            var id = order.GetValueOrDefault("id") ?? string.Empty;
            forJs.Append("elements_array[elements_array.length] = \"checked_").Append(id).Append("\";\n")
                .Append("elements_id_array[elements_id_array.length] = ").Append(id).Append(";\n");
            var orderStatus = order.GetValueOrDefault("status") ?? string.Empty;
            statuses.TryGetValue(orderStatus, out var statusRow);
            var orderPaid = order.GetValueOrDefault("paid");
            var orderPaidType = order.GetValueOrDefault("paid_type") ?? string.Empty;
            var unread = order.GetValueOrDefault("count_not_viewed_msg");

            var orderGarage = string.Empty;
            if (order.GetValueOrDefault("garage") is { Length: > 0 } garageList && garageList != "0")
            {
                foreach (var garageId in garageList.Split(','))
                {
                    var match = garages.FindIndex(g => g.Id == garageId);
                    if (match >= 0)
                    {
                        if (orderGarage.Length > 0)
                        {
                            orderGarage += "; ";
                        }

                        orderGarage += garages[match].Caption ?? string.Empty;
                    }
                }
            }

            sb.Append("\t\t\t<tr style=\"background-color:").Append(statusRow.Color ?? string.Empty).Append("\">\n                <td class=\"hidden\" style=\"line-height: 1em; vertical-align: middle;\"><input style=\"margin-top: 0px;\" type=\"checkbox\" onchange=\"on_one_check_changed('checked_").Append(id).Append("');\" id=\"checked_").Append(id)
                .Append("\" name=\"checked_").Append(id).Append("\"/></td>\n                <td style=\"line-height: 1em; white-space: nowrap; vertical-align: middle;\">\n\t\t\t\t<a href=\"").Append(input.LangHref).Append("/shop/orders/order?order_id=").Append(id)
                .Append("\"><i class=\"fa fa-sign-in\" aria-hidden=\"true\"></i> ").Append(id).Append(" </a>\n\t\t\t\t");
            if (unread is not null && PhpValue.NumericGreaterThanZero(unread))
            {
                sb.Append("\t\t\t\t<small>\n\t\t\t\t\t<a style=\"white-space: nowrap;\" class=\"dropdown-toggle label-menu-corner\" href=\"").Append(input.LangHref).Append("/shop/orders/order?order_id=").Append(id).Append("\">\n\t\t\t\t\t\t<i class=\"fa fa-envelope\" aria-hidden=\"true\"></i>\n\t\t\t\t\t\t<span style=\"font-size: 9px;\">").Append(unread).Append("</span>\n\t\t\t\t\t</a>\n\t\t\t\t</small>\n\t\t\t\t");
            }

            sb.Append("\t\t\t\t</td>\n                <td style=\"line-height: 1em; vertical-align: middle;\">").Append(date(UmapiImageProxy.PhpIntCast(order.GetValueOrDefault("time"))))
                .Append("</td>\n                <td style=\"line-height: 1em; white-space: nowrap; vertical-align: middle;\">").Append(StorefrontReturnsPages.NumberFormat(order.GetValueOrDefault("price_sum")))
                .Append("</td>\n                <td style=\"line-height: 1em; white-space: nowrap; vertical-align: middle;\">\n\t\t\t\t\t");
            if (orderPaid is not null && PhpValue.NumericEquals(orderPaid, 1))
            {
                sb.Append(await t("3514").ConfigureAwait(false));
            }
            else if (orderPaid is not null && PhpValue.NumericEquals(orderPaid, 2))
            {
                sb.Append(await t("3515").ConfigureAwait(false));
            }
            else if (orderPaid is null || PhpValue.NumericEquals(orderPaid, 0))
            {
                sb.Append(await t("3513").ConfigureAwait(false));
            }

            var paidTypeName = paidTypes.FindIndex(p => p.Id == orderPaidType) is var p and >= 0 ? paidTypes[p].Name : null;
            sb.Append("\t\t\t\t</td>\n\t\t\t\t<td style=\"line-height: 1em; white-space: nowrap; vertical-align: middle;\">")
                .Append(!string.IsNullOrEmpty(paidTypeName) && paidTypeName != "0" ? await t(paidTypeName).ConfigureAwait(false) : string.Empty)
                .Append("</td>\n                <td style=\"line-height: 1em; vertical-align: middle;\">").Append(await t(statusRow.Name ?? string.Empty).ConfigureAwait(false))
                .Append("</td>\n                <td style=\"line-height: 1em; vertical-align: middle;\">").Append(await t(order.GetValueOrDefault("obtain_caption") ?? string.Empty).ConfigureAwait(false))
                .Append("</td>\n                <td style=\"line-height: 1em; vertical-align: middle;\">")
                .Append(await t(offices.TryGetValue(order.GetValueOrDefault("office_id") ?? string.Empty, out var office) ? office ?? string.Empty : string.Empty).ConfigureAwait(false))
                .Append("</td>\n                <td style=\"line-height: 1em; vertical-align: middle;\">")
                .Append(orderGarage.Length > 0 ? "<i style=\"margin-right: 0px; color: #999;\" class=\"fa fa-car\" title=\"" + H(orderGarage) + "\"></i>" : string.Empty)
                .Append("</td>\n            </tr>\n\t\t\t\n\t\t\t");
            if (items.TryGetValue(id, out var orderItems) && orderItems.Count > 0)
            {
                sb.Append("\t\t\t\t<tr class=\"orders-items-tr hidden-xs hidden-sm\" style=\"display:none;\">\n\t\t\t\t\t<td></td>\n\t\t\t\t\t<td></td>\n\t\t\t\t\t<td colspan=\"7\" style=\"padding:0;\">\n\t\t\t\t\t\t<table class=\"table\" style=\"margin: 0; font-size: 12px;\">\n\t\t\t\t\t\t");
                foreach (var item in orderItems)
                {
                    itemStatuses.TryGetValue(item.GetValueOrDefault("status") ?? string.Empty, out var itemStatus);
                    sb.Append("\t\t\t\t\t\t\t<tr style=\"background:").Append(itemStatus.Color ?? string.Empty).Append("\">\n\t\t\t\t\t\t\t\t<td style=\"padding: 5px; line-height: 1em; vertical-align: middle; width:20%;\">").Append(H(item.GetValueOrDefault("t2_manufacturer")))
                        .Append("</td>\n\t\t\t\t\t\t\t\t<td style=\"padding: 5px; line-height: 1em; vertical-align: middle; width:20%;\">").Append(H(item.GetValueOrDefault("t2_article"))).Append("</td>\n\t\t\t\t\t\t\t\t<td style=\"padding: 5px; line-height: 1em; vertical-align: middle; width:40%;\">").Append(H(item.GetValueOrDefault("t2_name")))
                        .Append("</td>\n\t\t\t\t\t\t\t\t<td style=\"padding: 5px; line-height: 1em; vertical-align: middle;\">").Append(itemStatus.Name ?? string.Empty).Append("</td>\n\t\t\t\t\t\t\t</tr>\n\t\t\t\t\t\t");
                }

                sb.Append("\t\t\t\t\t\t</table>\n\t\t\t\t\t</td>\n\t\t\t\t</tr>\n\t\t\t\t");
            }

            sb.Append("\t\t\t\n        ");
        }

        if (orders.Count == 0)
        {
            sb.Append("<tr><td colspan=\"9\">").Append(await t("4558").ConfigureAwait(false)).Append("</td></tr>");
        }

        sb.Append("\t</table>\n\t</div>\n\t\n\t");
        var pages = limit == 0 ? 0 : (long)Math.Ceiling(allRows / (double)limit);
        if (pages > 1)
        {
            sb.Append("    <div class=\"text-center\">\n\t\t<ul class=\"pagination\">\n\t\t").Append(Pagination(allRows, limit, 2, page, "active", input.ContentUrl)).Append("\t\t</ul>\n\t</div>\n\t");
        }

        sb.Append("    \n    <script>\n    // ----------------------------------------------------------------------------------------\n    ").Append(forJs).Append("    //Обработка переключения Выделить все/Снять все\n    function on_check_uncheck_all()\n    {\n        var state = document.getElementById(\"check_uncheck_all\").checked;\n        \n        for(var i=0; i<elements_array.length;i++)\n        {\n            document.getElementById(elements_array[i]).checked = state;\n        }\n    }//~function on_check_uncheck_all()\n    // ----------------------------------------------------------------------------------------\n    //Обработка переключения одного чекбокса\n    function on_one_check_changed(id)\n    {\n        //Если хотя бы один чекбокс снят - снимаем общий чекбокс\n        for(var i=0; i<elements_array.length;i++)\n        {\n            if(document.getElementById(elements_array[i]).checked == false)\n            {\n                document.getElementById(\"check_uncheck_all\").checked = false;\n                break;\n            }\n        }\n    }//~function on_one_check_changed(id)\n    // ----------------------------------------------------------------------------------------\n    //Получение массива id отмеченых элементов\n    function getCheckedElements()\n    {\n        var checked_ids = new Array();\n        //По массиву чекбоксов\n        for(var i=0; i<elements_array.length;i++)\n        {\n            if(document.getElementById(elements_array[i]).checked == true)\n            {\n                checked_ids.push(elements_id_array[i]);\n            }\n        }\n        \n        return checked_ids;\n    }\n    // ----------------------------------------------------------------------------------------\n    </script>\n    ");
    }

    /// <summary>
    /// PHP <c>pagination()</c> of the page. PHP reads <c>$multilang_params</c> there without <c>global</c>, so the links
    /// have no language prefix.
    /// </summary>
    public static string Pagination(long all, long lim, long prev, long current, string currentCss, string url)
    {
        var html = new StringBuilder();
        var pages = (long)Math.Ceiling(all / (double)lim);
        var first = current - prev;
        if (first < 1)
        {
            first = 1;
        }

        var last = current + prev;
        if (last > pages)
        {
            last = pages;
        }

        if (first > 1)
        {
            html.Append("<li><a href='/").Append(url).Append("'>1</a></li>");
        }

        var y = first - 1;
        if (first > prev)
        {
            html.Append("<li><a href='/").Append(url).Append("?page=").Append(y).Append("' >...</a></li>");
        }
        else
        {
            for (var i = 2L; i < first; i++)
            {
                html.Append("<li><a href='/").Append(url).Append("?page=").Append(y).Append("' >").Append(i).Append("</a></li>");
            }
        }

        for (var i = first; i < last + 1; i++)
        {
            if (i == current)
            {
                html.Append("<li class=\"").Append(currentCss).Append("\"><a>").Append(i).Append("</a></li>");
            }
            else
            {
                html.Append("<li><a href='/").Append(url);
                if (i != 1)
                {
                    html.Append("?page=").Append(i);
                }

                html.Append("'>").Append(i).Append("</a></li>");
            }
        }

        y = last + 1;
        if (last < pages && pages - last > 2)
        {
            html.Append("<li><a href='/").Append(url).Append("?page=").Append(y).Append("' >...</a></li>");
        }

        if (last < pages)
        {
            html.Append("<li><a href='/").Append(url).Append("?page=").Append(pages).Append("' >").Append(pages).Append("</a></li>");
        }

        return html.ToString();
    }

    /// <summary>
    /// PHP <c>$_COOKIE[$name]</c> from a raw <c>Cookie</c> header: pairs split on <c>;</c>, the value URL-decoded
    /// (<c>+</c> is a space) and the first pair with the name kept.
    /// </summary>
    public static string? PhpCookie(string? header, string name)
    {
        if (string.IsNullOrEmpty(header))
        {
            return null;
        }

        foreach (var part in header.Split(';'))
        {
            var pair = part.TrimStart(' ', '\t', '\r', '\n');
            var eq = pair.IndexOf('=', StringComparison.Ordinal);
            var key = eq < 0 ? pair : pair[..eq];
            if (string.Equals(System.Net.WebUtility.UrlDecode(key), name, StringComparison.Ordinal))
            {
                return eq < 0 ? string.Empty : System.Net.WebUtility.UrlDecode(pair[(eq + 1)..]);
            }
        }

        return null;
    }

    /// <summary>PHP's assignment to an array key: a new key goes last, an existing key keeps its place.</summary>
    private static void Put(List<(string Id, string? Value)> list, string id, string? value)
    {
        var index = list.FindIndex(e => e.Id == id);
        if (index >= 0)
        {
            list[index] = (id, value);
        }
        else
        {
            list.Add((id, value));
        }
    }

    /// <summary>PHP <c>str_replace(array('"',"'","\n","\r","\t"), '', $caption)</c>.</summary>
    private static string StripCaption(string? caption)
    {
        var sb = new StringBuilder();
        foreach (var c in caption ?? string.Empty)
        {
            if (c is not ('"' or '\'' or '\n' or '\r' or '\t'))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static string H(string? value) => StorefrontSupplierLpoNotifier.H(value ?? string.Empty);

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
                row[i] = Text(reader, i);
            }

            rows.Add(row);
        }

        return rows;
    }

    /// <summary>Rows by column name; a repeated name keeps the last column, as PDO does.</summary>
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
                row[reader.GetName(i)] = Text(reader, i);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static string? Text(DbDataReader reader, int i)
        => reader.IsDBNull(i)
            ? null
            : reader.GetValue(i) switch
            {
                bool flag => flag ? "1" : "0",
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                var value => Convert.ToString(value, CultureInfo.InvariantCulture),
            };

    internal enum PhpKind
    {
        Null,
        Bool,
        Int,
        Float,
        String,
        Array,
    }

    /// <summary>A PHP value from <c>json_decode($json, true)</c>, with the PHP 8 casts and loose comparisons the page uses.</summary>
    internal sealed partial class PhpValue
    {
        private static readonly PhpValue NullValue = new(PhpKind.Null, false, 0, 0, string.Empty, []);

        private PhpValue(PhpKind kind, bool flag, long integer, double number, string text, List<KeyValuePair<string, PhpValue>> items)
        {
            Kind = kind;
            Flag = flag;
            Integer = integer;
            Number = number;
            Text = text;
            Items = items;
        }

        public PhpKind Kind { get; }

        public List<KeyValuePair<string, PhpValue>> Items { get; }

        private bool Flag { get; }

        private long Integer { get; }

        private double Number { get; }

        private string Text { get; }

        public bool IsEmptyString => Kind == PhpKind.String && Text.Length == 0;

        public static PhpValue Of(string text) => new(PhpKind.String, false, 0, 0, text, []);

        /// <summary>PHP <c>json_decode($json, true)</c>; null for invalid JSON.</summary>
        public static PhpValue JsonDecode(string? json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return NullValue;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                return From(document.RootElement);
            }
            catch (JsonException)
            {
                return NullValue;
            }
        }

        /// <summary>PHP <c>$value[$key]</c>; null when the key is missing or the value is not an array.</summary>
        public PhpValue Get(string key)
        {
            if (Kind != PhpKind.Array)
            {
                return NullValue;
            }

            foreach (var item in Items)
            {
                if (item.Key == key)
                {
                    return item.Value;
                }
            }

            return NullValue;
        }

        /// <summary>PHP <c>echo</c>.</summary>
        public string Echo() => Kind switch
        {
            PhpKind.Bool => Flag ? "1" : string.Empty,
            PhpKind.Int => Integer.ToString(CultureInfo.InvariantCulture),
            PhpKind.Float => StorefrontPhpAjax.PhpFloatString(Number),
            PhpKind.String => Text,
            PhpKind.Array => "Array",
            _ => string.Empty,
        };

        /// <summary>PHP <c>(int)</c>.</summary>
        public long IntCast() => Kind switch
        {
            PhpKind.Bool => Flag ? 1 : 0,
            PhpKind.Int => Integer,
            PhpKind.Float => double.IsFinite(Number) && Math.Abs(Number) < 9.2e18 ? (long)Number : 0,
            PhpKind.String => UmapiImageProxy.PhpIntCast(Text),
            PhpKind.Array => Items.Count > 0 ? 1 : 0,
            _ => 0,
        };

        /// <summary>PHP truthiness (<c>!empty()</c>).</summary>
        public bool Truthy() => Kind switch
        {
            PhpKind.Bool => Flag,
            PhpKind.Int => Integer != 0,
            PhpKind.Float => Number != 0,
            PhpKind.String => Text.Length > 0 && Text != "0",
            PhpKind.Array => Items.Count > 0,
            _ => false,
        };

        /// <summary>PHP 8 <c>$value == ""</c>.</summary>
        public bool LooseEqualsEmptyString() => Kind switch
        {
            PhpKind.Null => true,
            PhpKind.Bool => !Flag,
            PhpKind.String => Text.Length == 0,
            _ => false,
        };

        /// <summary>PHP 8 <c>$value == $text</c> for a non-numeric <paramref name="text"/>.</summary>
        public bool LooseEqualsString(string text) => Kind switch
        {
            PhpKind.Null => text.Length == 0,
            PhpKind.Bool => Flag == (text.Length > 0 && text != "0"),
            PhpKind.Int or PhpKind.Float => Echo() == text,
            PhpKind.String => Text == text,
            _ => false,
        };

        /// <summary>PHP 8 <c>$value == $n</c>.</summary>
        public bool LooseEqualsInt(long n) => Kind switch
        {
            PhpKind.Null => n == 0,
            PhpKind.Bool => Flag == (n != 0),
            PhpKind.Int => Integer == n,
            PhpKind.Float => Number == n,
            PhpKind.String => NumericString(Text) is { } number ? number == n : Text == n.ToString(CultureInfo.InvariantCulture),
            _ => false,
        };

        /// <summary>PHP 8 <c>$value &gt; 0</c>.</summary>
        public bool LooseGreaterThanZero() => Kind switch
        {
            PhpKind.Bool => Flag,
            PhpKind.Int => Integer > 0,
            PhpKind.Float => Number > 0,
            PhpKind.String => NumericString(Text) is { } number ? number > 0 : string.CompareOrdinal(Text, "0") > 0,
            PhpKind.Array => true,
            _ => false,
        };

        /// <summary>PHP 8 <c>$db_value == $n</c> for a database value.</summary>
        public static bool NumericEquals(string value, long n) => Of(value).LooseEqualsInt(n);

        /// <summary>PHP 8 <c>$db_value &gt; 0</c> for a database value.</summary>
        public static bool NumericGreaterThanZero(string value) => Of(value).LooseGreaterThanZero();

        /// <summary>The value of a PHP 8 numeric string (leading and trailing whitespace allowed), else null.</summary>
        private static double? NumericString(string text)
            => NumericPattern().IsMatch(text) && double.TryParse(text.Trim(' ', '\t', '\n', '\r', '\v', '\f'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                ? number
                : null;

        private static PhpValue From(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.True:
                    return new(PhpKind.Bool, true, 0, 0, string.Empty, []);
                case JsonValueKind.False:
                    return new(PhpKind.Bool, false, 0, 0, string.Empty, []);
                case JsonValueKind.Number:
                    var raw = element.GetRawText();
                    if (raw.IndexOfAny(['.', 'e', 'E']) < 0 && element.TryGetInt64(out var integer))
                    {
                        return new(PhpKind.Int, false, integer, 0, string.Empty, []);
                    }

                    return new(PhpKind.Float, false, 0, element.GetDouble(), string.Empty, []);
                case JsonValueKind.String:
                    return Of(element.GetString() ?? string.Empty);
                case JsonValueKind.Array:
                    var list = new List<KeyValuePair<string, PhpValue>>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(new(list.Count.ToString(CultureInfo.InvariantCulture), From(item)));
                    }

                    return new(PhpKind.Array, false, 0, 0, string.Empty, list);
                case JsonValueKind.Object:
                    var map = new List<KeyValuePair<string, PhpValue>>();
                    foreach (var property in element.EnumerateObject())
                    {
                        var index = map.FindIndex(p => p.Key == property.Name);
                        if (index >= 0)
                        {
                            map[index] = new(property.Name, From(property.Value));
                        }
                        else
                        {
                            map.Add(new(property.Name, From(property.Value)));
                        }
                    }

                    return new(PhpKind.Array, false, 0, 0, string.Empty, map);
                default:
                    return NullValue;
            }
        }

        [GeneratedRegex(@"^[ \t\n\r\v\f]*[+-]?([0-9]+(\.[0-9]*)?|\.[0-9]+)([eE][+-]?[0-9]+)?[ \t\n\r\v\f]*$")]
        private static partial Regex NumericPattern();
    }
}
