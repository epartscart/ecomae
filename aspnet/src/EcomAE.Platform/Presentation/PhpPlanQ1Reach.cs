using System.Globalization;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-reach storefront geo picker. PHP identifiers kept for the inventory:
/// <c>epc_geo_render_flat_list</c>, <c>set_my_city</c>,
/// <c>openPopupWindow_CityList</c>, <c>printGeoNodes</c>.
/// Path: <c>modules/shop/geo/point_geo_node.php</c>.
/// GET never mints a session cookie. Leftover unique user helper stays injected.
/// </summary>
public static class PhpPlanQ1Reach
{
    public const string PointGeoNodePath = "modules/shop/geo/point_geo_node.php";
    public const string SetMyCity = "set_my_city";
    public const string OpenPopupWindowCityList = "openPopupWindow_CityList";

    public static Func<List<Dictionary<string, object?>>>? QueryRows { get; set; }
    public static Func<string?, Dictionary<string, object?>?>? QueryById { get; set; }
    public static Func<int>? CountRows { get; set; }
    public static Func<Dictionary<string, object?>?>? QueryFirst { get; set; }
    public static Func<Dictionary<string, object?>?>? QuerySingle { get; set; }
    public static Func<bool>? QueryFailed { get; set; }
    public static Func<string, string>? TranslateById { get; set; }
    public static Func<Dictionary<string, object?>>? GetUserSession { get; set; }
    public static string? MyCity { get; set; }
    public static string LastOutput { get; private set; } = "";

    public static void Reset()
    {
        QueryRows = null;
        QueryById = null;
        CountRows = null;
        QueryFirst = null;
        QuerySingle = null;
        QueryFailed = null;
        TranslateById = null;
        GetUserSession = null;
        MyCity = null;
        LastOutput = "";
    }

    public static void EpcGeoRenderFlatList()
    {
        if (QueryFailed?.Invoke() == true)
        {
            Echo("<div class=\"geo_default_level\">Location list unavailable</div>");
            return;
        }

        foreach (var row in QueryRows?.Invoke() ?? [])
        {
            try
            {
                var level = ToInt(row.GetValueOrDefault("level"));
                var geoClass = level == 1 ? "geo_top_level" : level == 2 ? "geo_second_level" : "geo_default_level";
                var raw = Str(row.GetValueOrDefault("value"));
                var label = raw;
                if (raw != "" && TranslateById is not null)
                {
                    label = TranslateById(raw);
                }

                label = H(label);
                if (geoClass == "geo_default_level")
                {
                    Echo("<div class=\"" + geoClass + "\" onclick=\"" + SetMyCity + "(" + ToInt(row.GetValueOrDefault("id")) + ");\" style=\"cursor:pointer\">" + label + "</div>");
                }
                else
                {
                    Echo("<div class=\"" + geoClass + "\">" + label + "</div>");
                }
            }
            catch
            {
                // Keep storefront rendering if one geo row fails.
            }
        }
    }

    public static void PrintGeoNodes(object? geoTreeDump, int depth = 0)
    {
        if (geoTreeDump is not IList<object?> list || depth > 32)
        {
            return;
        }

        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] is not Dictionary<string, object?> node)
            {
                continue;
            }

            var level = ToInt(node.GetValueOrDefault("level"));
            var geoClass = level switch
            {
                1 => "geo_top_level",
                2 => "geo_second_level",
                _ => "geo_default_level"
            };
            var value = Str(node.GetValueOrDefault("value"));
            if (geoClass == "geo_default_level")
            {
                Echo("\t\t<div class=\"" + geoClass + "\" onclick=\"" + SetMyCity + "(" + Str(node.GetValueOrDefault("id")) + ");\" style=\"cursor:pointer\">" + value + "</div>\n\t\t");
            }
            else
            {
                Echo("\t\t<div class=\"" + geoClass + "\">" + value + "</div>\n\t\t");
            }

            var child = node.GetValueOrDefault("data") as IList<object?> ?? [];
            if (child.Count > 0)
            {
                PrintGeoNodes(child, depth + 1);
            }
        }
    }

    public static void EpcGeoRenderModule()
    {
        if (MyCity is not null)
        {
            var node = QueryById?.Invoke(MyCity);
            if (node is null || node.Count == 0)
            {
                MyCity = null;
            }
        }

        var session = GetUserSession?.Invoke() ?? new Dictionary<string, object?>(StringComparer.Ordinal) { ["csrf_guard_key"] = "tok-1" };
        var csrf = Str(session.GetValueOrDefault("csrf_guard_key"));
        Echo("\n\n<script>\n");
        Echo("//Устновка своего города\n");
        Echo("function " + SetMyCity + "(id)\n{\n");
        Echo("    //Увеличиваем наличие на сервере и только после этого отображаем\n");
        Echo("    jQuery.ajax({\n");
        Echo("        type: \"POST\",\n");
        Echo("        async: false, //Запрос синхронный\n");
        Echo("        url: \"/modules/shop/geo/ajax_set_my_city.php\",\n");
        Echo("        dataType: \"json\",//Тип возвращаемого значения\n");
        Echo("        data: \"geo_id=\"+id+\"&csrf_guard_key=" + csrf + "\",\n");
        Echo("        success: function(answer)\n        {\n");
        Echo("            if(answer == 1)\n            {\n");
        Echo("                location.reload();\n            }\n        }\n    });\n}\n</script>\n\n\n\n");

        var count = CountRows?.Invoke() ?? (QueryRows?.Invoke()?.Count ?? 0);
        if (count > 1)
        {
            var first = QueryFirst?.Invoke() ?? QueryRows?.Invoke()?.FirstOrDefault();
            var cityName = first is null ? "" : Tr(Str(first.GetValueOrDefault("value")));
            Echo("\t\n    <!-- Start Модальное окно -->\n");
            Echo("        <div style=\"display:none\" id=\"modal_content_div\">\n");
            Echo("        \t<div class=\"popup_content\">\n");
            Echo("        \t\t<a href=\"javascript:void(0);\" class=\"popup_window_close\" style=\"text-decoration:none;\"><span style=\"position: relative; top: -5px; left: -3px;\">X</span></a>\n");
            Echo("\t\t\t\t<font style=\"font-weight:bold;\">" + Tr("4771") + "</font>\n");
            Echo("        \t\t<div class=\"geo_list\">\n        \t\t\t");
            EpcGeoRenderFlatList();
            Echo("        \t\t</div>\n        \t</div>\n        </div>\n");
            Echo("        <script>\n");
            Echo("        \t//Создание модального окна\n");
            Echo("        \tvar div_modal = document.createElement('div');//Объект DIV\n");
            Echo("        \tdiv_modal.setAttribute('class', 'popup_window');//Класс в соответствии со стилем\n");
            Echo("        \tdiv_modal.innerHTML = document.getElementById(\"modal_content_div\").innerHTML;//Содержимое окна берем из образца\n");
            Echo("        \tdocument.body.insertBefore(div_modal, document.body.firstChild);//Добавляем окно в самое начало BODY. Т.о. окно будет выше всех\n");
            Echo("        </script>\n        <script>\n");
            Echo("        \tmodal_geo_list = $('.popup_window');\n");
            Echo("        \tmodal_geo_list.click(function(event) {\n");
            Echo("        \t\te = event || window.event\n");
            Echo("        \t\tif (e.target == this) {\n");
            Echo("        \t\t\t$(modal_geo_list).css('display', 'none')\n");
            Echo("        \t\t}\n        \t});\n");
            Echo("        \t$('.popup_window_close').click(function() {\n");
            Echo("        \t\tmodal_geo_list.css('display', 'none');\n        \t});\n        \t\n");
            Echo("        \t// ----------------------------------------------------------------\n");
            Echo("        \tfunction " + OpenPopupWindowCityList + "()\n        \t{\n");
            Echo("        \t\tmodal_geo_list.css('display', 'block');\n        \t}\n");
            Echo("        \t// ----------------------------------------------------------------\n");
            Echo("        </script>\n    <!-- End Модальное окно -->\n\t");
            if (MyCity is not null)
            {
                var rec = QueryById?.Invoke(MyCity);
                var value = rec is null ? "" : Str(rec.GetValueOrDefault("value"));
                if (rec is not null && value != "")
                {
                    cityName = Tr(value);
                    Echo("\t\t\t<div class=\"customer_city\" id=\"customer_city\" onclick=\"" + OpenPopupWindowCityList + "();\">\n");
                    Echo("\t\t\t\t<i style=\"font-size: 1.2em;\" class=\"fa fa-map-marker\" aria-hidden=\"true\"></i> <span>" + cityName + "</span>\n");
                    Echo("\t\t\t</div>\n\t\t\t");
                    return;
                }
            }

            Echo("        <script>\n\t\t\t" + OpenPopupWindowCityList + "();\n        </script>\n        ");
        }
        else if (string.IsNullOrEmpty(MyCity))
        {
            var single = QuerySingle?.Invoke() ?? QueryRows?.Invoke()?.FirstOrDefault();
            var id = ToInt(single?.GetValueOrDefault("id"));
            if (id != 0)
            {
                Echo("\t\t<script>\n\t\t\t" + SetMyCity + "(" + id + ");\n\t\t</script>\n\t\t");
            }
        }
    }

    public static string Capture(Action render)
    {
        LastOutput = "";
        render();
        return LastOutput;
    }

    private static string Tr(string raw)
        => TranslateById is null || raw == "" ? raw : TranslateById(raw);

    private static void Echo(string html) => LastOutput += html;

    private static string H(object? value)
    {
        var s = Str(value);
        return s.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#039;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static int ToInt(object? value)
    {
        if (value is null or false or "")
        {
            return 0;
        }

        try
        {
            return Convert.ToInt32(Convert.ToDouble(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static string Str(object? value, string fallback = "")
        => value is null ? fallback : Convert.ToString(value, CultureInfo.InvariantCulture) ?? fallback;
}
