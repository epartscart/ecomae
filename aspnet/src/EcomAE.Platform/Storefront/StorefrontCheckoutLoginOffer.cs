using System.Globalization;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/shop/order_process/checkout_login_offer.php</c> (<c>/shop/checkout/login_offer</c>, where the cart sends
/// a visitor who is not signed in): the shared sign-in form aimed at <c>shop/checkout/how_get</c>, and the
/// "order without registration" button when DP_Config <c>order_without_auth == 1</c>.
/// </summary>
public static class StorefrontCheckoutLoginOffer
{
    public const string Postfix = "login_offer";
    public const string Target = "shop/checkout/how_get";
    public const int GuestButtonStringId = 4523;

    public static string Render(StorefrontLoginForm.Input input, Func<int, string> t, IReadOnlyDictionary<string, string> config)
    {
        var html = "\n\n<div class=\"panel panel-primary\">\n"
            + StorefrontLoginForm.General(input, t, StorefrontLoginForm.Postfix(Postfix, 1), Target)
            + "\n\t\n\t";
        if (GuestButtonShown(config))
        {
            html += "\t\t\t<div class=\"panel-body\" style=\"color:#777;\">\n\t\t\t\t<a class=\"btn btn-ar btn-default\" href=\""
                + input.LangHref + "/shop/checkout/how_get\">" + t(GuestButtonStringId) + "</a>\n\t\t\t</div>\n\t\t\t";
        }

        return html + "\n\t\n</div>\n\n\n\n\n\n";
    }

    /// <summary>The page: <c>isset($DP_Config->order_without_auth) &amp;&amp; $DP_Config->order_without_auth == 1</c>.</summary>
    public static bool GuestButtonShown(IReadOnlyDictionary<string, string> config)
        => config.TryGetValue("order_without_auth", out var raw) && LooseEqualsOne(raw);

    /// <summary>
    /// <c>ajax_checkout_create.php</c>: a guest order is refused only when the setting is present and <c>!= 1</c>,
    /// so an unset setting still lets guests order.
    /// </summary>
    public static bool GuestOrdersAllowed(IReadOnlyDictionary<string, string> config)
        => !config.TryGetValue("order_without_auth", out var raw) || LooseEqualsOne(raw);

    /// <summary>PHP 8 <c>$string == 1</c>: numeric strings (surrounding whitespace allowed) compare by value, others never equal.</summary>
    public static bool LooseEqualsOne(string? raw)
    {
        var text = (raw ?? string.Empty).Trim(' ', '\t', '\n', '\r', '\v', '\f');
        if (text.Length == 0 || text.Any(c => !(char.IsAsciiDigit(c) || c is '+' or '-' or '.' or 'e' or 'E')))
        {
            return false;
        }

        return double.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var value)
            && value == 1d;
    }
}
