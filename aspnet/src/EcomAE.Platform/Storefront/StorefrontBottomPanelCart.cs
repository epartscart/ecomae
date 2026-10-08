using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// The cart part of PHP <c>modules/shop/bottom_panel/bottom_panel.php</c>: the "added" label (string 4225) and the
/// <c>updateCartInfo()</c> / <c>showAdded()</c> / <c>hideAdded()</c> script that the add-to-cart scripts call after a
/// successful add. <c>updateCartInfo()</c> posts the session's <c>csrf_guard_key</c> to <c>ajax_get_cart_info.php</c>
/// and runs once on load when the <c>session</c> cookie is set. Same text as PHP (golden <c>Fixtures/BottomPanelCart</c>).
/// </summary>
public static class StorefrontBottomPanelCart
{
    public const int AddedStringId = 4225;

    public const long NewFrontTemplateId = 63;

    public static string Script(string csrfGuardKey, bool hasSessionCookie, bool template63)
        => "<script>\r\n"
           + "\t\t\t//Функция обновления информации по корзине\r\n"
           + "\t\t\tfunction updateCartInfo()\r\n"
           + "\t\t\t{\r\n"
           + "\t\t\t\t//updateCartInfoHeader();\r\n"
           + "\t\t\t\t\r\n"
           + "\t\t\t\tjQuery.ajax({\r\n"
           + "\t\t\t\t\ttype: \"POST\",\r\n"
           + "\t\t\t\t\tasync: true,\r\n"
           + "\t\t\t\t\turl: \"/content/shop/order_process/ajax_get_cart_info.php\",\r\n"
           + "\t\t\t\t\tdataType: \"json\",\r\n"
           + "\t\t\t\t\tdata: \"csrf_guard_key="
           + csrfGuardKey
           + "\",\r\n"
           + "\t\t\t\t\tsuccess: function(answer)\r\n"
           + "\t\t\t\t\t{\r\n"
           + "\t\t\t\t\t\tif(document.getElementById(\"cart_items_sum\")){\r\n"
           + "\t\t\t\t\t\t\tdocument.getElementById(\"cart_items_sum\").innerHTML = answer.cart_items_sum;\r\n"
           + "\t\t\t\t\t\t}\r\n"
           + "\t\t\t\t\t\t\r\n"
           + "\t\t\t\t\t\tif(document.getElementById(\"cart_items_count\")){\r\n"
           + "\t\t\t\t\t\t\tdocument.getElementById(\"cart_items_count\").innerHTML = answer.cart_items_count;\r\n"
           + "\t\t\t\t\t\t\t\r\n"
           + "\t\t\t\t\t\t\tif( answer.cart_items_count == 0 )\r\n"
           + "\t\t\t\t\t\t\t{\r\n"
           + "\t\t\t\t\t\t\t\tdocument.getElementById(\"cart_items_count\").setAttribute(\"class\", \"badge badge-default badge-round \");//Указатель количества\r\n"
           + "\t\t\t\t\t\t\t}\r\n"
           + "\t\t\t\t\t\t\telse\r\n"
           + "\t\t\t\t\t\t\t{\r\n"
           + "\t\t\t\t\t\t\t\tdocument.getElementById(\"cart_items_count\").setAttribute(\"class\", \"badge badge-primary badge-round \");//Указатель количества\r\n"
           + "\t\t\t\t\t\t\t}\r\n"
           + "\t\t\t\t\t\t}\r\n"
           + "\t\t\t\t\t\t\r\n"
           + "\t\t\t\t\t\tif(document.getElementById(\"header_cart_items_sum\")){\r\n"
           + "\t\t\t\t\t\t\tdocument.getElementById(\"header_cart_items_sum\").innerHTML = answer.cart_items_sum;\r\n"
           + "\t\t\t\t\t\t}\r\n"
           + "\t\t\t\t\t\t\r\n"
           + "\t\t\t\t\t\tif(document.getElementById(\"header_cart_items_count\")){\r\n"
           + "\t\t\t\t\t\t\t\r\n"
           + "\t\t\t\t\t\t\tdocument.getElementById(\"header_cart_items_count\").innerHTML = answer.cart_items_count;\r\n"
           + "\t\t\t\t\t\t\t\r\n"
           + "\t\t\t\t\t\t\t"
           + (template63
               ? "\t\t\t\t\t\t\tif( answer.cart_items_count == 0 ){\r\n"
                 + "\t\t\t\t\t\t\t\tdocument.getElementById(\"header_cart_items_count\").setAttribute(\"class\", \"hidden badge badge-default badge-round \");//Указатель количества\r\n"
                 + "\t\t\t\t\t\t\t}\r\n"
                 + "\t\t\t\t\t\t\telse{\r\n"
                 + "\t\t\t\t\t\t\t\tdocument.getElementById(\"header_cart_items_count\").setAttribute(\"class\", \"badge badge-primary badge-round\");//Указатель количества\r\n"
                 + "\t\t\t\t\t\t\t}\r\n"
                 + "\t\t\t\t\t\t\t"
               : string.Empty)
           + "\t\t\t\t\t\t}\r\n"
           + "\t\t\t\t\t\t\r\n"
           + "\t\t\t\t\t\tif(document.getElementById(\"header_cart_items_count_mobile\")){\r\n"
           + "\t\t\t\t\t\t\tdocument.getElementById(\"header_cart_items_count_mobile\").innerHTML = answer.cart_items_count;\r\n"
           + "\t\t\t\t\t\t\t\r\n"
           + "\t\t\t\t\t\t\tif( answer.cart_items_count == 0 ){\r\n"
           + "\t\t\t\t\t\t\t\tdocument.getElementById(\"header_cart_items_count_mobile\").setAttribute(\"class\", \"hidden badge badge-default badge-round \");//Указатель количества\r\n"
           + "\t\t\t\t\t\t\t}\r\n"
           + "\t\t\t\t\t\t\telse{\r\n"
           + "\t\t\t\t\t\t\t\tdocument.getElementById(\"header_cart_items_count_mobile\").setAttribute(\"class\", \"badge badge-primary badge-round\");//Указатель количества\r\n"
           + "\t\t\t\t\t\t\t}\r\n"
           + "\t\t\t\t\t\t}\r\n"
           + "\t\t\t\t\t}\r\n"
           + "\t\t\t\t});\r\n"
           + "\t\t\t}\r\n"
           + "\t\t\t"
           + (hasSessionCookie
               ? "\t\t\tupdateCartInfo();//После загрузки страницы обновляем модуль корзины\r\n"
                 + "\t\t\t"
               : string.Empty)
           + "\t\t\t\r\n"
           + "\t\t\t//Функция показа лэйбла \"Добавлено\"\r\n"
           + "\t\t\tfunction showAdded()\r\n"
           + "\t\t\t{\r\n"
           + "\t\t\t\tif(document.getElementById(\"mark_popup_added\")){\r\n"
           + "\t\t\t\t\tdocument.getElementById(\"mark_popup_added\").setAttribute(\"class\", \"badge-primary badge-round panel-primary\");\r\n"
           + "\t\t\t\t\t\r\n"
           + "\t\t\t\t\tsetTimeout(function() {\r\n"
           + "\t\t\t\t\t\thideAdded();\r\n"
           + "\t\t\t\t\t}, 5000);\r\n"
           + "\t\t\t\t}\r\n"
           + "\t\t\t}\r\n"
           + "\t\t\t//Функция скрытия лэйбла \"Добавлено\"\r\n"
           + "\t\t\tfunction hideAdded()\r\n"
           + "\t\t\t{\r\n"
           + "\t\t\t\tif(document.getElementById(\"mark_popup_added\")){\r\n"
           + "\t\t\t\t\tdocument.getElementById(\"mark_popup_added\").setAttribute(\"class\", \"badge-primary badge-round panel-primary hidden\");\r\n"
           + "\t\t\t\t}\r\n"
           + "\t\t\t}\r\n"
           + "\t\t\t</script>";

    /// <summary>PHP <c>DP_User::getUserSession()["csrf_guard_key"]</c>: the row for the <c>session</c> and <c>u_id</c> cookies.</summary>
    public static async Task<string> CsrfGuardKeyAsync(DbConnection connection, string? sessionCookie, string? userCookie, CancellationToken cancellationToken)
    {
        if (sessionCookie is null || userCookie is null)
        {
            return string.Empty;
        }

        return await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT IFNULL(`csrf_guard_key`, '') FROM `sessions` WHERE `session` = ? AND `user_id` = ? LIMIT 1"),
            cancellationToken,
            sessionCookie,
            userCookie).ConfigureAwait(false) ?? string.Empty;
    }

    /// <summary>PHP <c>$DP_Template->id</c>: the current front template, 0 when none is marked current.</summary>
    public static async Task<long> FrontTemplateIdAsync(DbConnection connection, CancellationToken cancellationToken)
        => await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `templates` WHERE `current` = ? AND `is_frontend` = ? LIMIT 1"),
            cancellationToken,
            1,
            1).ConfigureAwait(false);
}
