using System.Text;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/general/actions_alert.php</c>: one dismissable alert for each non-empty <c>success_message</c>,
/// <c>error_message</c>, <c>warning_message</c> and <c>info_message</c> query value (escaped with <c>htmlentities</c>),
/// then the <c>clearAlert()</c> script. The PHP file uses CRLF line endings, which the output keeps.
/// </summary>
public static class StorefrontActionsAlert
{
    public static readonly int[] StringIds = [4036, 2122, 2385, 2307];

    private static readonly (string Key, string Css, string Div, string Icon, int StringId, string Bang)[] Kinds =
    [
        ("success_message", "alert-success", "success_div", "fa-check", 4036, "!"),
        ("error_message", "alert-danger", "error_div", "fa-times", 2122, "!"),
        ("warning_message", "alert-warning", "warning_div", "fa-warning", 2385, ""),
        ("info_message", "alert-info", "info_div", "fa-info", 2307, "!"),
    ];

    /// <param name="query">The last value of a query key, null when absent (PHP <c>$_GET</c>).</param>
    public static string Render(Func<string, string?> query, Func<int, string> t)
    {
        var sb = new StringBuilder("\r\n");
        foreach (var kind in Kinds)
        {
            var value = query(kind.Key);
            if (string.IsNullOrEmpty(value) || value == "0")
            {
                continue;
            }

            sb.Append("    <div class=\"alert ").Append(kind.Css).Append(" alert-dismissable\" id=\"").Append(kind.Div).Append("\">\r\n")
                .Append("        <button type=\"button\" class=\"close\" onclick=\"clearAlert('").Append(kind.Div).Append("');\">&times;</button>\r\n")
                .Append("\t\t<strong><i class=\"fa ").Append(kind.Icon).Append("\"></i> ").Append(t(kind.StringId)).Append(kind.Bang).Append("</strong>\r\n")
                .Append("        ").Append(PhpHtmlEntities.Encode(value))
                .Append("    </div>\r\n    ");
        }

        sb.Append("\r\n<script>\r\n    //Удаляем сообщение\r\n    function clearAlert(alert_div_id)\r\n    {\r\n        var alert_div = document.getElementById(alert_div_id);\r\n        alert_div.parentNode.removeChild(alert_div);\r\n    }\r\n</script>");
        return sb.ToString();
    }

    /// <summary>The value PHP puts in <c>$_GET[key]</c>: the last occurrence of the key.</summary>
    public static string? Last(IQueryCollection query, string key)
        => query.TryGetValue(key, out var values) && values.Count > 0 ? values[^1] : null;
}
