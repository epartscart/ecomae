using System.Globalization;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-tarn storefront login module.
/// Path: <c>modules/login/module.php</c> (basename is not unique).
/// GET never mints a session cookie. Leftover unique user helper stays injected.
/// </summary>
public static class PhpPlanQ1Tarn
{
    public const string LoginModulePath = "modules/login/module.php";

    public static Func<object?>? GetUserId { get; set; }
    public static Func<Dictionary<string, object?>>? GetUserSession { get; set; }
    public static Func<Dictionary<string, object?>?>? GetUserProfile { get; set; }
    public static Func<string>? LangHref { get; set; }
    public static Func<string, string>? TranslateById { get; set; }
    public static string LastOutput { get; private set; } = "";

    public static void Reset()
    {
        GetUserId = null;
        GetUserSession = null;
        GetUserProfile = null;
        LangHref = null;
        TranslateById = null;
        LastOutput = "";
    }

    public static string EpcLoginModule()
    {
        var session = GetUserSession?.Invoke() ?? new Dictionary<string, object?>(StringComparer.Ordinal) { ["csrf_guard_key"] = "tok-1" };
        var csrf = Str(session.GetValueOrDefault("csrf_guard_key"));
        var lang = LangHref?.Invoke() ?? "/en";
        var t4758 = Tr("4758");
        LastOutput = "<div id=\"open_login\">\n    <span>" + t4758 + "</span>\n</div>\n" +
            "<div id=\"module_login_box\">\n    <div id=\"module_login\" style=\"display: none\">\n";
        if (PhpEqZero(GetUserId?.Invoke()))
        {
            LastOutput +=
                "                    <form method=\"POST\">\n" +
                "                <input type=\"hidden\" name=\"csrf_guard_key\" value=\"" + csrf + "\" />\n" +
                "\t\t\t\t<input type=\"hidden\" name=\"authentication\" value=\"true\"/>\n" +
                "                <div class=\"wrong_authentication\" id=\"wrong_authentication\">\n" +
                "                </div>\n" +
                "                <input class=\"login_input\" type=\"text\" name=\"login\" value=\"\" placeholder=\"" + Tr("4759") + "\"/>\n" +
                "                <input class=\"login_input\" type=\"password\" name=\"password\" value=\"\" placeholder=\"" + Tr("1311") + "\"/>\n" +
                "                <div id=\"remember_me_div\">\n" +
                "                    " + Tr("4666") + " <input type=\"checkbox\" name=\"rememberme\" value=\"rememberme\"/>\n" +
                "                </div>\n" +
                "                <button type=\"submit\" class=\"btn\">" + Tr("4008") + "</button>\n" +
                "            </form>\n" +
                "            <script>\n" +
                "                document.getElementById(\"open_login\").innerHTML = \"<span>" + t4758 + "</span>\";\n" +
                "            </script>\n            \n" +
                "            <a href=\"" + lang + "/users/registration\" class=\"btn btn-success\">" + Tr("3987") + "</a> \n" +
                "            <a href=\"" + lang + "/users/forgot_password\" class=\"btn\">" + Tr("4667") + "</a>\n" +
                "            \n                ";
        }
        else
        {
            LastOutput +=
                "                        <div id=\"greeting\">\n                    " + Tr("4760") + "                </div>\n" +
                "                <div id=\"self_data_control\">\n" +
                "                    <a href=\"" + lang + "/users/profile\" class=\"btn btn-success\">" + Tr("4668") + "</a> \n" +
                "                    <a href=\"" + lang + "/users/editform\" class=\"btn\">" + Tr("4761") + "</a>\n" +
                "                </div>\n" +
                "                <form method=\"POST\">\n" +
                "\t\t\t\t\t<input type=\"hidden\" name=\"csrf_guard_key\" value=\"" + csrf + "\" />\n" +
                "                    <input type=\"hidden\" name=\"logout\" value=\"true\"/>\n" +
                "                    <button type=\"submit\" class=\"btn\">" + Tr("3996") + "</button>\n" +
                "                </form>\n" +
                "                <script>\n" +
                "\t\t\t\t\t                    document.getElementById(\"open_login\").innerHTML = \"<span>" + DisplayName() + "</span>\";\n" +
                "                </script>\n                ";
        }

        LastOutput += "</div>\n</div>\n\n\n<script>\n" +
            "$(\"#open_login\").click(function(){\n" +
            "    \tif ( $(\"#module_login\").css('display') == 'none' ) \n" +
            "    \t{\n" +
            "    \t    $(\"#module_login\").show(400);\n" +
            "    \t}\n" +
            "    \telse\n" +
            "    \t{\n" +
            "    \t    $(\"#module_login\").hide(200);\n" +
            "    \t}\n" +
            "\t});\n" +
            "</script>";
        return LastOutput;
    }

    private static string DisplayName()
    {
        var profile = GetUserProfile?.Invoke();
        var show = "";
        if (profile is not null && profile.TryGetValue("name", out var name) && name is not null)
        {
            show = Str(name);
        }

        if (profile is not null && profile.TryGetValue("surname", out var surname) && surname is not null)
        {
            if (show != "")
            {
                show += " ";
            }

            show += Str(surname);
        }

        return show == "" ? Tr("4762") : show;
    }

    private static bool PhpEqZero(object? value)
    {
        if (value is null or false or "")
        {
            return true;
        }

        try
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture) == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string Tr(string id)
        => TranslateById?.Invoke(id) ?? id;

    private static string Str(object? value)
        => value is null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
}
