namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/shop/general/shop_button.php</c>: the shop functions page (cart and orders links) with the
/// visitor's language prefix. PHP eats the newline after <c>?&gt;</c>, so the translation sits against the
/// following tab. Verified against PHP 8.3 by <c>Fixtures/StorefrontFragments/golden.json</c>.
/// </summary>
public static class StorefrontShopButton
{
    public static string Render(string? langHref)
    {
        var prefix = langHref ?? string.Empty;
        return "<h1>{4409}</h1>\n\n<div class=\"cat-item\">\n\t<a href=\"" + prefix
            + "/shop/cart\">\n\t\t{4410}\t</a>\n</div>\n\n<div class=\"cat-item\">\n\t<a href=\"" + prefix
            + "/shop/orders\">\n\t\t{4411}\t</a>\n</div>\n";
    }
}
