namespace EcomAE.Platform.Presentation;

/// <summary>
/// PHP <c>epc_epartscart_cp_brochure.php</c> → <c>epc_cp_full_brochure_render_html</c>
/// brand epartscart. HTML is the PHP render (client/super/all × deck/catalog).
/// </summary>
public static class TenantCpBrochure
{
    public const string Dir = "content/general_pages/epc_rendered_tenant";

    public static string NormalizeScope(string? scope)
    {
        var value = new string((scope ?? "").Trim().ToLowerInvariant().Where(c => c is >= 'a' and <= 'z').ToArray());
        return value is "client" or "super" or "all" ? value : "client";
    }

    public static string NormalizeView(string? view)
        => string.Equals(view?.Trim(), "catalog", StringComparison.OrdinalIgnoreCase) ? "catalog" : "deck";

    public static string HtmlFor(string? scope, string? view, bool print)
    {
        var file = $"{Dir}/epartscart-cp-{NormalizeScope(scope)}-{NormalizeView(view)}.html";
        var html = PhpHomeWidgetHtml.RenderStatic(file);
        if (html.Length == 0 || !print)
        {
            return html;
        }

        const string script = "<script>window.addEventListener(\"load\",function(){setTimeout(function(){window.print()},400)});</script>";
        var close = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return close < 0 ? html + script : html.Insert(close, script);
    }
}
