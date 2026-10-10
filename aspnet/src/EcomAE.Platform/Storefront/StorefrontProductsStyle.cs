using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/shop/catalogue/set_cookie_products_style.php</c>: <c>(int)</c> of <c>$_GET["products_style"]</c>,
/// keep 1 / 2 / 3, set the <c>products_style</c> cookie on <c>/</c> for a long time, and echo <c>json_encode</c> of
/// that integer as a string. Verified against PHP 8.3 by <c>Fixtures/StorefrontFragments/golden_http.json</c>.
/// </summary>
public static class StorefrontProductsStyle
{
    public const string Path = "/content/shop/catalogue/set_cookie_products_style.php";
    public const string CookieName = "products_style";
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(9_999_999);

    public static IResult Apply(HttpRequest request, HttpResponse response)
    {
        var value = StorefrontPhpQuery.Get(request, CookieName);
        if (!value.Present)
        {
            return Results.Text(string.Empty, "text/html; charset=utf-8");
        }

        var style = value.IsArray
            ? StorefrontPhpInt.CastArray(true, value.ArrayEmpty)
            : StorefrontPhpInt.Cast(value.Scalar);
        var encoded = PhpHtmlEntities.Encode(style.ToString(CultureInfo.InvariantCulture));
        if (encoded is not ("1" or "2" or "3"))
        {
            return Results.Text(string.Empty, "text/html; charset=utf-8");
        }

        response.Cookies.Append(
            CookieName,
            encoded,
            new CookieOptions
            {
                Path = "/",
                Expires = DateTimeOffset.UtcNow.Add(Lifetime),
                MaxAge = Lifetime,
                HttpOnly = false,
                SameSite = SameSiteMode.Unspecified
            });
        return Results.Text(JsonSerializer.Serialize(encoded), "text/html; charset=utf-8");
    }
}
