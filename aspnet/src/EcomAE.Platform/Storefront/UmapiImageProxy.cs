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
        if (normalized == "emsh")
        {
            // emshcloud part photos: id is the image/… path from the gorsel payload.
            var path = (id ?? string.Empty).Trim();
            if (path.Length is < 10 or > 200 || !path.StartsWith("image/", StringComparison.Ordinal)
                || path.Contains("..") || path.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '/' or '.' or '_' or '-')))
            {
                return false;
            }

            url = "https://tecdoc.emshcloud.com/v1/parca-gorsel-dosya?y=" + Uri.EscapeDataString(path);
            return true;
        }

        if (normalized == "article")
        {
            // Article photos are keyed by the MEDIA_FILE name from the Articles payload.
            var file = (id ?? string.Empty).Trim();
            if (file.Length is < 8 or > 128 || file.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')))
            {
                return false;
            }

            url = "https://image.umapi.ru/ARTICLES/" + file;
            return true;
        }

        var parsed = PhpIntCast(id);
        if (parsed < 1 || normalized is not ("supplier" or "manufacturer" or "model"))
        {
            return false;
        }

        var collection = normalized switch
        {
            "manufacturer" => "MANUFACTURERS",
            "model" => "MODELS",
            _ => "SUPPLIERS",
        };
        url = "https://image.umapi.ru/" + collection + "/" + parsed.ToString(CultureInfo.InvariantCulture) + ".png";
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

    // Fetch-once/save: every successful CDN fetch is stored in the tenant DB's
    // epc_image_cache; later clicks for the same image are served locally.
    private static async Task<IResult> HandleAsync(
        HttpContext context,
        IHttpClientFactory httpClientFactory,
        Erp.IErpWriteConnectionFactory connections,
        CancellationToken cancellationToken)
    {
        var kind = context.Request.Query["kind"];
        var id = context.Request.Query["id"];
        var kindValue = kind.Count > 0 ? kind[^1] : null;
        var idValue = id.Count > 0 ? id[^1] : null;
        // kind=supplier also accepts a brand name — resolve it through epc_umapi_brands.
        if (string.Equals(kindValue, "supplier", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(idValue) && PhpIntCast(idValue) < 1
            && connections.IsConfigured)
        {
            try
            {
                await using var lookup = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
                await using var cmd = lookup.CreateCommand();
                cmd.CommandText = "SELECT `sup_id` FROM `epc_umapi_brands` WHERE UPPER(TRIM(`brand`)) = @brand LIMIT 1";
                var bp = cmd.CreateParameter();
                bp.ParameterName = "@brand";
                bp.Value = idValue.Trim().ToUpperInvariant();
                cmd.Parameters.Add(bp);
                var found = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (found is not null and not DBNull)
                {
                    idValue = Convert.ToString(found, CultureInfo.InvariantCulture);
                }
            }
            catch (Exception)
            {
            }
        }

        if (!TryImageUrl(kindValue, idValue, out var url))
        {
            return Results.Text("Bad request", "text/plain; charset=utf-8", Encoding.UTF8, StatusCodes.Status400BadRequest);
        }

        var version = context.Request.Query["v"];
        var versionValue = version.Count > 0 ? version[^1] : null;
        if (!string.IsNullOrEmpty(versionValue))
        {
            url += "?v=" + Uri.EscapeDataString(versionValue);
        }

        var cacheKey = System.Convert.ToHexString(
            System.Security.Cryptography.SHA1.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
        try
        {
            await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await EnsureImageCacheAsync(connection, cancellationToken).ConfigureAwait(false);
            await using (var read = connection.CreateCommand())
            {
                read.CommandText = "SELECT `content_type`, `body` FROM `epc_image_cache` WHERE `cache_key` = @k LIMIT 1";
                var p = read.CreateParameter(); p.ParameterName = "@k"; p.Value = cacheKey; read.Parameters.Add(p);
                await using var rs = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await rs.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var cachedType = rs.GetString(0);
                    var cachedBody = (byte[])rs.GetValue(1);
                    if (IsUmapiPlaceholder(cachedBody))
                    {
                        return Results.Text("Not found", "text/plain; charset=utf-8", Encoding.UTF8, StatusCodes.Status404NotFound);
                    }
                    context.Response.Headers.CacheControl = "public, max-age=604800, immutable";
                    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                    return Results.File(cachedBody, cachedType);
                }
            }
        }
        catch (Exception)
        {
            // Cache layer is best-effort — fall through to a live fetch.
        }

        try
        {
            var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(10);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", "ePartsCart-UmapiImage/1.0");
            if (AsciiLower(kindValue ?? string.Empty) == "emsh")
            {
                var emshKey = await EmshcloudVinDecode.ApiKeyAsync(context, cancellationToken).ConfigureAwait(false);
                if (emshKey.Length > 0)
                {
                    request.Headers.TryAddWithoutValidation("X-API-Key", emshKey);
                }
            }
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode || bytes.Length < 64 || IsUmapiPlaceholder(bytes))
            {
                return Results.Text("Not found", "text/plain; charset=utf-8", Encoding.UTF8, StatusCodes.Status404NotFound);
            }

            var type = response.Content.Headers.ContentType?.ToString();
            var contentType = string.IsNullOrWhiteSpace(type) ? "image/png" : type;
            try
            {
                await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
                await EnsureImageCacheAsync(connection, cancellationToken).ConfigureAwait(false);
                await using var write = connection.CreateCommand();
                write.CommandText = "REPLACE INTO `epc_image_cache` (`cache_key`, `content_type`, `body`, `last_sync`) VALUES (@k, @t, @b, @s)";
                foreach (var (n, v) in new (string, object)[]
                {
                    ("@k", cacheKey), ("@t", contentType), ("@b", bytes),
                    ("@s", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                })
                {
                    var p = write.CreateParameter(); p.ParameterName = n; p.Value = v; write.Parameters.Add(p);
                }

                await write.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A failed cache write never blocks serving the image.
            }

            context.Response.Headers.CacheControl = "public, max-age=604800, immutable";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(bytes, contentType);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Results.Text("Not found", "text/plain; charset=utf-8", Encoding.UTF8, StatusCodes.Status404NotFound);
        }
    }

    /// <summary>image.umapi.ru answers HTTP 200 with a generic "no photo" JPEG for every missing
    /// image — all supplier logos included — so bytes must be checked, not the status code.</summary>
    private static bool IsUmapiPlaceholder(byte[] body)
        => body.Length == 43525
            && string.Equals(
                System.Convert.ToHexString(System.Security.Cryptography.MD5.HashData(body)).ToLowerInvariant(),
                "04c19007f273129fe271a162913f1de5",
                StringComparison.Ordinal);

    private static async Task EnsureImageCacheAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS `epc_image_cache` ("
            + "`cache_key` VARCHAR(64) NOT NULL, "
            + "`content_type` VARCHAR(64) NOT NULL DEFAULT 'image/png', "
            + "`body` MEDIUMBLOB NOT NULL, "
            + "`last_sync` INT NOT NULL DEFAULT 0, "
            + "PRIMARY KEY (`cache_key`)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
