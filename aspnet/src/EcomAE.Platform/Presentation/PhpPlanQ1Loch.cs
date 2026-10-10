namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-loch storefront search-string module. PHP identifiers kept for the inventory:
/// <c>submit_form</c>.
/// Path: <c>modules/shop/search_string/search_string.php</c>.
/// GET never mints a session cookie. Leftover unique user helper stays injected.
/// </summary>
public static class PhpPlanQ1Loch
{
    public const string SearchStringPath = "modules/shop/search_string/search_string.php";
    public const string SubmitForm = "submit_form";

    public static Func<string, string>? TranslateById { get; set; }
    public static Func<Dictionary<string, object?>>? GetUserSession { get; set; }
    public static Func<string>? LangHref { get; set; }
    public static Dictionary<string, string> Query { get; set; } = new(StringComparer.Ordinal);
    public static string LastOutput { get; private set; } = "";

    public static void Reset()
    {
        TranslateById = null;
        GetUserSession = null;
        LangHref = null;
        Query = new Dictionary<string, string>(StringComparer.Ordinal);
        LastOutput = "";
    }

    public static string EpcSearchStringModule()
    {
        var article = ArticleValue();
        var lang = LangHref?.Invoke() ?? "/en";
        var csrf = Csrf();
        var t4772 = Tr("4772");
        var t4773 = Tr("4773");
        var t4774 = Tr("4774");
        var t2379 = Tr("2379");
        LastOutput =
            "\n\n\n\n<div class=\"search_mode_wrap\">\n" +
            "    <input type=\"radio\" id=\"radio_search_mode_1\" name=\"search_mode\" value=\"docpart_article\" checked=\"checked\" /> <label for=\"radio_search_mode_1\"> " + t4772 + "</label>\n" +
            "    <input type=\"radio\" id=\"radio_search_mode_2\" name=\"search_mode\" value=\"treelax_catalogue\" /> <label for=\"radio_search_mode_2\"> " + t4773 + "</label>\n" +
            "</div>\n\n<div class=\"search_block_container\">\n" +
            "    <input type=\"text\" class=\"search_string_input\" id=\"entered_string\" value=\"" + article + "\" placeholder=\"" + t4774 + "...\" />\n" +
            "    <a class=\"search_string_button\" href=\"javascript:void(0);\" onclick=\"" + SubmitForm + "();\">" + t2379 + "</a>\n" +
            "</div>\n<script>\n" +
            "document.getElementById('entered_string').onkeypress = function(e){\n" +
            "    if (!e) e = window.event;\n" +
            "    var keyCode = e.keyCode || e.which;\n" +
            "    if (keyCode == '13')\n" +
            "\t{\n" +
            "\t\t" + SubmitForm + "();\n" +
            "    }\n" +
            "  }\n" +
            "</script>\n\n\n\n\n<script>\n" +
            "//Метод отправки формы\n" +
            "function " + SubmitForm + "()\n" +
            "{\n" +
            "    var search_mode = $('input[name=\"search_mode\"]:checked').val();\n" +
            "    \n" +
            "    \n" +
            "    var entered_string = document.getElementById(\"entered_string\").value;\n" +
            "    \n" +
            "    //Поиск по наименованию в каталоге Treelax\n" +
            "    if(search_mode == \"treelax_catalogue\")\n" +
            "    {\n" +
            "        document.getElementById(\"search_string\").value = entered_string;\n" +
            "        document.forms[\"treelax_search_form\"].submit();\n" +
            "    }\n" +
            "    else if(search_mode == \"docpart_article\")//Поиск автозапчастей по артикулу\n" +
            "    {\n" +
            "        document.getElementById(\"article\").value = entered_string;\n" +
            "        document.forms[\"part_search_form\"].submit();\n" +
            "    }\n" +
            "    \n" +
            "}\n" +
            "</script>\n\n\n\n\n\n" +
            "<!-- Форма поиска по наименованию в каталоге Трилакс -->\n" +
            "<form action=\"" + lang + "/shop/search\" method=\"GET\" style=\"display:none\" name=\"treelax_search_form\">\n" +
            "    <input type=\"hidden\" name=\"search_string\" id=\"search_string\" value=\"\" />\n" +
            "\t<input type=\"hidden\" name=\"csrf_guard_key\" value=\"" + csrf + "\" />\n" +
            "</form>\n\n\n" +
            "<!-- Форма поиска по артикулу у поставщиков автозапчастей -->\n" +
            "<form action=\"" + lang + "/shop/part_search\" method=\"GET\" style=\"display:none\" name=\"part_search_form\">\n" +
            "    <input type=\"hidden\" name=\"article\" id=\"article\" value=\"\" />\n" +
            "\t<input type=\"hidden\" name=\"csrf_guard_key\" value=\"" + csrf + "\" />\n" +
            "</form>";
        return LastOutput;
    }

    private static string ArticleValue()
    {
        if (!Query.TryGetValue("article", out var raw))
        {
            return "";
        }

        return PhpEmpty(raw) ? "" : raw;
    }

    private static bool PhpEmpty(string? value)
        => value is null or "" or "0";

    private static string Csrf()
    {
        var session = GetUserSession?.Invoke() ?? new Dictionary<string, object?>(StringComparer.Ordinal) { ["csrf_guard_key"] = "tok-1" };
        return Convert.ToString(session.GetValueOrDefault("csrf_guard_key")) ?? "";
    }

    private static string Tr(string id)
        => TranslateById?.Invoke(id) ?? id;
}
