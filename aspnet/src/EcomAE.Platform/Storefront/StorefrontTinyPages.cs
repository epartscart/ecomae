using System.Globalization;
using System.Text;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Small storefront PHP includes closed in this slice, verified against PHP 8.3 by
/// <c>Fixtures/TinyPages/golden.json</c>:
/// <list type="bullet">
/// <item><c>content/general_pages/users_agreement.php</c></item>
/// <item><c>content/shop/docpart/epc_cata_bridge.php</c> (<c>epc_cata_bridge_js_config</c>)</item>
/// <item><c>content/general_pages/epc_eparts_product_route.php</c> (<c>epc_storefront_vin_label</c>, <c>epc_eparts_product_url</c>)</item>
/// <item><c>api/UCatalog/ucatalog_index.php</c></item>
/// <item><c>content/shop/pos/epc_pos_shell_js.php</c> (<c>epc_pos_cp_footer_scripts</c>)</item>
/// <item><c>content/general/docpart_href.php</c></item>
/// <item><c>plugins/error_pages/error_pages.php</c></item>
/// </list>
/// </summary>
public static class StorefrontTinyPages
{
    public const string UsersAgreementPath = "content/general_pages/users_agreement.php";
    public const string CataBridgePath = "content/shop/docpart/epc_cata_bridge.php";
    public const string EpartsProductRoutePath = "content/general_pages/epc_eparts_product_route.php";
    public const string VehicleIndexPath = "api/UCatalog/ucatalog_index.php";
    public const string PosShellPath = "content/shop/pos/epc_pos_shell_js.php";
    public const string DocpartHrefPath = "content/general/docpart_href.php";
    public const string ErrorPagesPath = "plugins/error_pages/error_pages.php";
    public const string PosUiVersion = "20260722posui1";
    public const string DocpartUrl = "https://docpart.net/";
    public const int DocpartTitleId = 4037;
    public const int UsersAgreementStringId = 4039;

    public static readonly IReadOnlyDictionary<string, object> CataBridgeJsConfig = new Dictionary<string, object>
    {
        ["bridge"] = false,
        ["use_cata_first"] = false,
        ["cata_sync"] = false,
        ["cata_api"] = "/api/eparts_cata_proxy.php",
        ["partsapi_api"] = "/api/partsapi_proxy.php"
    };

    public static string UsersAgreementPage(Func<int, string>? translate = null)
        => (translate ?? (id => "{" + id.ToString(CultureInfo.InvariantCulture) + "}"))(UsersAgreementStringId);

    public static string VinLabel() => "VIN";

    public static string EpartsProductUrl(string langHref, IReadOnlyDictionary<string, string?>? parameters = null)
    {
        var url = langHref.TrimEnd('/') + "/eparts-product";
        if (parameters is null || parameters.Count == 0)
        {
            return url;
        }

        var query = PhpBuildQuery(parameters);
        return query.Length == 0 ? url : url + "?" + query;
    }

    public static string VehicleIndexHtml(bool garagePresent, string? garage)
    {
        var html = "<!-- UCatalog -->\r\n<div id=\"UCatalog_container\"></div>\r\n\r\n"
            + "<link href=\"/api/UCatalog/style.css\" rel=\"stylesheet\" type=\"text/css\"/>\r\n"
            + "<script src=\"/api/UCatalog/api.js\"></script>\r\n\r\n"
            + "<link href=\"/lib/Lightbox/css/lightbox.css\" rel=\"stylesheet\" type=\"text/css\"/>\r\n"
            + "<script type=\"text/javascript\" src=\"/lib/Lightbox/js/lightbox.js\"></script>\r\n\r\n";
        if (!garagePresent)
        {
            return html;
        }

        return html + "<script>\r\njQuery(document).ready(function () {\r\n\tUCatalog_get_garage("
            + garage
            + ");\r\n\t\r\n\t//Удалить параметр из адреса\r\n\tlet url = new URL(document.location);\r\n"
            + "\tlet searchParams = url.searchParams;\r\n\tsearchParams.delete(\"UCatalog_get_garage\");\r\n"
            + "\twindow.history.pushState({}, '', url.toString());\r\n});\r\n</script>\r\n";
    }

    public static string PosFooterScripts(string contentUrl)
    {
        contentUrl = contentUrl.Trim('/');
        var src = contentUrl switch
        {
            "shop/pos/terminal" => "/content/shop/pos/epc_pos_terminal_js.php",
            "control/portal/epc_pos_tenant_manage" => "/content/shop/pos/epc_pos_tenant_manage_js.php",
            _ => null
        };
        if (src is null)
        {
            return string.Empty;
        }

        src += "?v=" + Uri.EscapeDataString(PosUiVersion);
        return "<script src=\"" + HtmlSpecialChars(src) + "\"></script>\n";
    }

    public static (string Href, string Title) DocpartHref(Func<int, string>? translate = null)
        => (DocpartUrl, (translate ?? (id => "{" + id.ToString(CultureInfo.InvariantCulture) + "}"))(DocpartTitleId));

    public static ErrorPageContent? ErrorPage(
        string? errorPage,
        IReadOnlyDictionary<string, string?>? pluginValues,
        Func<string, string>? translate = null)
    {
        if (errorPage is null || pluginValues is null)
        {
            return null;
        }

        string T(string key)
        {
            pluginValues.TryGetValue(errorPage + "_" + key, out var raw);
            return (translate ?? (value => "{" + value + "}"))(raw ?? string.Empty);
        }

        return new ErrorPageContent(T("value"), T("title_tag"), T("description_tag"), T("keywords_tag"), T("author_tag"), T("content_type"), T("content"));
    }

    /// <summary>PHP <c>http_build_query</c> (RFC1738): spaces become <c>+</c>, null values are omitted.</summary>
    public static string PhpBuildQuery(IReadOnlyDictionary<string, string?> parameters)
    {
        var parts = new List<string>(parameters.Count);
        foreach (var pair in parameters)
        {
            if (pair.Value is null)
            {
                continue;
            }

            parts.Add(Rfc1738(pair.Key) + "=" + Rfc1738(pair.Value));
        }

        return string.Join("&", parts);
    }

    public static string HtmlSpecialChars(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length + 8);
        foreach (var ch in value)
        {
            builder.Append(ch switch
            {
                '&' => "&amp;",
                '"' => "&quot;",
                '\'' => "&#039;",
                '<' => "&lt;",
                '>' => "&gt;",
                _ => ch.ToString()
            });
        }

        return builder.ToString();
    }

    private static string Rfc1738(string value)
        => Uri.EscapeDataString(value).Replace("%20", "+", StringComparison.Ordinal);

    public sealed record ErrorPageContent(
        string Value,
        string TitleTag,
        string DescriptionTag,
        string KeywordsTag,
        string AuthorTag,
        string ContentType,
        string Content);
}
