using System.Net;
using System.Text.RegularExpressions;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Browser-tab name for the company that is actually open.
/// eParts Cart stays the parts storefront. A client ERP uses that company's trade name.
/// </summary>
public static partial class ErpDocumentTitle
{
    public const string PlatformCart = "eParts Cart";

    public static string? Brand(HttpContext? http)
    {
        if (http is null)
        {
            return null;
        }

        var key = ClientErpPath.BoundSiteKey(http);
        if (!string.IsNullOrEmpty(key))
        {
            var tenant = http.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
            var trade = tenant?.DisplayName;
            var industry = tenant?.IndustryCode;
            if (string.IsNullOrWhiteSpace(trade))
            {
                var launch = ErpOnlyLaunchClients.All.FirstOrDefault(c =>
                    string.Equals(c.SiteKey, key, StringComparison.OrdinalIgnoreCase));
                trade = launch?.TradeName;
                if (string.IsNullOrWhiteSpace(industry))
                {
                    industry = launch?.IndustryCode;
                }
            }

            var label = LoginHostBrand.ForErpOnly(key, trade, industry).Label;
            return string.Equals(label, PlatformCart, StringComparison.Ordinal) ? null : label;
        }

        var hostBrand = LoginHostBrand.Resolve(http.Request.Host.Host, "erp");
        if (hostBrand.LogoKind is LoginHostBrand.Kind.AnimatedEparts or LoginHostBrand.Kind.Platform)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(hostBrand.Label)
            || string.Equals(hostBrand.Label, PlatformCart, StringComparison.Ordinal)
            ? null
            : hostBrand.Label;
    }

    public static string RewriteDocument(string html, string? brand)
    {
        if (string.IsNullOrEmpty(html)
            || string.IsNullOrWhiteSpace(brand)
            || string.Equals(brand, PlatformCart, StringComparison.Ordinal)
            || html.IndexOf(PlatformCart, StringComparison.Ordinal) < 0)
        {
            return html;
        }

        var encoded = WebUtility.HtmlEncode(brand);
        return TitleTag().Replace(html, match =>
        {
            var text = match.Groups["text"].Value.Replace(PlatformCart, encoded, StringComparison.Ordinal);
            return match.Groups["open"].Value + text + match.Groups["close"].Value;
        });
    }

    [GeneratedRegex("(?<open><title\\b[^>]*>)(?<text>.*?)(?<close></title>)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleTag();
}
