namespace EcomAE.Platform.Storefront;

/// <summary>
/// Storefront login and registration includes: <c>content/users/users_agreement_module.php</c> (the agreement checkbox,
/// its <c>users_agreement</c> cookie and <c>check_user_agreement()</c>) and
/// <c>content/users/epc_storefront_auth_layout.php</c> (the centred card around the forms). The output is PHP's, byte
/// for byte; translations are echoed unescaped as PHP echoes them.
/// </summary>
public static class StorefrontAuthPartials
{
    public const string AuthCssHref = "/content/users/epc_storefront_auth.css?v=20260606b";

    /// <summary>The translation ids <see cref="UsersAgreementModule"/> prints.</summary>
    public static IReadOnlyList<int> UsersAgreementStringIds { get; } = [4751, 4752, 4753, 4754];

    private static readonly string AgreementHead = Crlf(
        "<div id=\"users_agreement_div\" style=\"padding: 0px 15px; border: 1px solid #ddd; background: #f7f7f7; margin:20px 0px;\">\n"
        + "\t<table>\n"
        + "\t\t<tr>\n"
        + "\t\t\t<td><input style=\"width:25px; height:25px; cursor:pointer;\" type=\"checkbox\" id=\"users_agreement\" onchange=\"on_agreement_changed();\"/></td>\n"
        + "\t\t\t<td style=\"line-height: 1.2em; padding: 15px 5px;\"><label style=\"cursor:pointer;\" for=\"users_agreement\">");

    private static readonly string AgreementScript = Crlf(
        "</label></td>\n"
        + "\t\t</tr>\n"
        + "\t</table>\n"
        + "</div>\n"
        + "<script>\n"
        + "\tdocument.cookie = \"users_agreement=no; path=/;\";//Предвартельно снимаем согласие\n"
        + "\t//Обработка галочки \"Согласие с пользовательским соглашением\"\n"
        + "\tfunction on_agreement_changed()\n"
        + "\t{\n"
        + "\t\tif( document.getElementById(\"users_agreement\").checked )\n"
        + "\t\t{\n"
        + "\t\t\tdocument.cookie = \"users_agreement=yes; path=/;\";\n"
        + "\t\t}\n"
        + "\t\telse\n"
        + "\t\t{\n"
        + "\t\t\tdocument.cookie = \"users_agreement=no; path=/;\";\n"
        + "\t\t}\n"
        + "\t}\n"
        + "\t//Проверка согласия с обработкой персональных данных\n"
        + "\tfunction check_user_agreement()\n"
        + "\t{\n"
        + "\t\t//Проверка согласия с пользовательским соглашением\n"
        + "\t\tif(!document.getElementById(\"users_agreement\").checked)\n"
        + "\t\t{\n"
        + "\t\t\talert(\"");

    private static readonly string AgreementTail = Crlf(
        "\");\n"
        + "\t\t\treturn false;\n"
        + "\t\t}\n"
        + "\t\treturn true;\n"
        + "\t}\n"
        + "</script>");

    /// <summary>PHP <c>users_agreement_module.php</c>; <paramref name="langHref"/> is <c>$multilang_params['lang_href']</c>.</summary>
    public static string UsersAgreementModule(string langHref, Func<int, string> translate)
        => AgreementHead + translate(4751)
            + " <a style=\"text-decoration:underline;\" target=\"_blank\" href=\"" + langHref + "/polzovatelskoe-soglashenie\">" + translate(4752) + "</a> "
            + translate(4753) + AgreementScript + translate(4754) + AgreementTail;

    public static async Task<string> UsersAgreementModuleAsync(StorefrontPhpTranslator translator, string langHref, CancellationToken cancellationToken = default)
    {
        var strings = new Dictionary<int, string>();
        foreach (var id in UsersAgreementStringIds)
        {
            strings[id] = await translator.TextAsync(id, cancellationToken).ConfigureAwait(false);
        }

        return UsersAgreementModule(langHref, id => strings[id]);
    }

    private static string Crlf(string text) => text.Replace("\n", "\r\n", StringComparison.Ordinal);

    /// <summary>
    /// PHP <c>epc_storefront_auth_layout_open()</c> and <c>_close()</c> for one page: the stylesheet link is printed
    /// once, before the first card, as the PHP function's static flag does.
    /// </summary>
    public sealed class AuthLayout
    {
        private bool _cssDone;

        public string Css()
        {
            if (_cssDone)
            {
                return string.Empty;
            }

            _cssDone = true;
            return "<link rel=\"stylesheet\" href=\"" + AuthCssHref + "\" />";
        }

        public string Open(string variant = "default")
            => Css() + "<div class=\"" + (variant == "wide" ? "epc-auth-page epc-auth-page--wide" : "epc-auth-page") + "\">";

        public static string Close() => "</div>";
    }
}
