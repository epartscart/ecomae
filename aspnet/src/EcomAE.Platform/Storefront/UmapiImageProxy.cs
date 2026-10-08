using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>api/umapi_image.php</c>: the supplier and manufacturer logos of image.umapi.ru, fetched server side and
/// served with a week of public caching. <c>kind</c> is lower-cased but not trimmed and <c>id</c> is PHP's
/// <c>(int)</c>, so <c>?kind=Supplier&amp;id=12abc</c> serves <c>SUPPLIERS/12.png</c>; anything else is a 400.
/// </summary>
public static class UmapiImageProxy
{
    public const string Path = "/api/umapi_image.php";

    public static void Map(IEndpointRouteBuilder endpoints) => endpoints.MapGet(Path, HandleAsync);

    public static bool TryImageUrl(string? kind, string? id, out string url)
    {
        url = string.Empty;
        var normalized = AsciiLower(kind ?? string.Empty);
        var parsed = PhpIntCast(id);
        if (parsed < 1 || normalized is not ("supplier" or "manufacturer"))
        {
            return false;
        }

        url = "https://image.umapi.ru/" + (normalized == "manufacturer" ? "MANUFACTURERS" : "SUPPLIERS") + "/" + parsed.ToString(CultureInfo.InvariantCulture) + ".png";
        return true;
    }

    /// <summary>PHP 8 <c>(int)</c> of a query string: the leading numeric part (exponent included), 0 without one.</summary>
    public static long PhpIntCast(string? raw)
    {
        var text = (raw ?? string.Empty).TrimStart(' ', '\t', '\n', '\r', '\v', '\f');
        var i = 0;
        if (i < text.Length && text[i] is '+' or '-')
        {
            i++;
        }

        var digits = 0;
        while (i < text.Length && char.IsAsciiDigit(text[i]))
        {
            i++;
            digits++;
        }

        var integerEnd = i;
        var isFloat = false;
        if (i < text.Length && text[i] == '.')
        {
            var j = i + 1;
            var fraction = 0;
            while (j < text.Length && char.IsAsciiDigit(text[j]))
            {
                j++;
                fraction++;
            }

            if (digits + fraction > 0)
            {
                digits += fraction;
                i = j;
                isFloat = true;
            }
        }

        if (digits == 0)
        {
            return 0;
        }

        if (i < text.Length && text[i] is 'e' or 'E')
        {
            var j = i + 1;
            if (j < text.Length && text[j] is '+' or '-')
            {
                j++;
            }

            var start = j;
            while (j < text.Length && char.IsAsciiDigit(text[j]))
            {
                j++;
            }

            if (j > start)
            {
                i = j;
                isFloat = true;
            }
        }

        if (!isFloat)
        {
            return long.TryParse(text[..integerEnd], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole)
                ? whole
                : text[0] == '-' ? long.MinValue : long.MaxValue;
        }

        var number = double.Parse(text[..i], NumberStyles.Float, CultureInfo.InvariantCulture);
        return double.IsFinite(number) && Math.Abs(number) < 9.2e18 ? (long)Math.Truncate(number) : 0;
    }

    private static string AsciiLower(string value)
    {
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is >= 'A' and <= 'Z')
            {
                chars[i] = (char)(chars[i] + 32);
            }
        }

        return new string(chars);
    }

    private static async Task<IResult> HandleAsync(HttpContext context, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        var kind = context.Request.Query["kind"];
        var id = context.Request.Query["id"];
        if (!TryImageUrl(kind.Count > 0 ? kind[^1] : null, id.Count > 0 ? id[^1] : null, out var url))
        {
            return Results.Text("Bad request", "text/plain; charset=utf-8", Encoding.UTF8, StatusCodes.Status400BadRequest);
        }

        try
        {
            var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(10);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", "ePartsCart-UmapiImage/1.0");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode || bytes.Length < 64)
            {
                return Results.Text("Not found", "text/plain; charset=utf-8", Encoding.UTF8, StatusCodes.Status404NotFound);
            }

            var type = response.Content.Headers.ContentType?.ToString();
            context.Response.Headers.CacheControl = "public, max-age=604800, immutable";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(bytes, string.IsNullOrWhiteSpace(type) ? "image/png" : type);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Results.Text("Not found", "text/plain; charset=utf-8", Encoding.UTF8, StatusCodes.Status404NotFound);
        }
    }
}
