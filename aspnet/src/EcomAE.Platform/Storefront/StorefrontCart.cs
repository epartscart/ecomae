using System.Data.Common;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/shop/order_process/cart.php</c>. The renderer keeps the legacy client protocol and performs the two
/// GET-side writes from PHP: clearing a blocked guest cart and unchecking inaccessible type-1 catalogue rows.
/// Both writes include the current shopper/session ownership predicate.
/// </summary>
/// <remarks>Intentional security deviation: database values rendered into HTML/JavaScript are escaped.</remarks>
public static class StorefrontCart
{
    private static readonly JsonSerializerOptions PhpJson = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        PropertyNamingPolicy = null,
    };

    private static readonly Lazy<string> BlockedTemplate = new(() => Template("CartBlocked.html"));
    private static readonly Lazy<string> GuestEmptyTemplate = new(() => Template("CartGuestEmpty.html"));
    private static readonly Lazy<string> SignedEmptyTemplate = new(() => Template("CartSignedEmpty.html"));
    private static readonly Lazy<string> GuestTemplate = new(() => Template("CartGuest.html"));
    private static readonly Lazy<string> SignedTemplate = new(() => Template("CartSigned.html"));

    public sealed record Input(
        int UserId,
        long SessionId,
        string CsrfKey,
        bool GuestCommerceBlocked,
        bool TradeCheckoutBlocked,
        string TradeCheckoutMessage,
        string LangHref,
        string ProductUrlMode,
        string PriceStylesHtml,
        string WhatsAppStylesHtml,
        string WhatsAppScriptHtml);

    private sealed record CartRow(
        long Id,
        string Price,
        int ProductType,
        int CountNeed,
        int CheckedForOrder,
        int Access,
        string TimeToExe,
        int MinOrder,
        string Name,
        string Manufacturer,
        string Article,
        string ImageSrc,
        int ProductId,
        string UrlRefresh);

    public static async Task<string> RenderAsync(
        DbConnection connection,
        Input input,
        Func<string, bool> productImageExists,
        CancellationToken cancellationToken)
    {
        if (input.UserId <= 0 && input.GuestCommerceBlocked)
        {
            await ClearGuestCartAsync(connection, input.SessionId, cancellationToken).ConfigureAwait(false);
            return Fill(BlockedTemplate.Value, input)
                .Replace("{{LOGIN_HREF}}", H(input.LangHref + "/login"), StringComparison.Ordinal)
                .Replace("{{SIGNUP_HREF}}", H(input.LangHref + "/reg"), StringComparison.Ordinal);
        }

        var sessionId = input.UserId > 0 ? 0 : input.SessionId;
        var ids = await IdsAsync(connection, input.UserId, sessionId, cancellationToken).ConfigureAwait(false);
        var rows = new List<CartRow>(ids.Count);
        foreach (var id in ids)
        {
            var row = await CartRowAsync(connection, input, id, sessionId, productImageExists, cancellationToken).ConfigureAwait(false);
            if (row is not null)
            {
                rows.Add(row);
            }
        }

        var garage = await GarageOptionsAsync(connection, input.UserId, cancellationToken).ConfigureAwait(false);
        var template = rows.Count == 0
            ? input.UserId > 0 ? SignedEmptyTemplate.Value : GuestEmptyTemplate.Value
            : input.UserId > 0 ? SignedTemplate.Value : GuestTemplate.Value;
        var html = Fill(template, input).Replace("{{GARAGE_OPTIONS}}", garage, StringComparison.Ordinal);
        if (rows.Count == 0)
        {
            return html;
        }

        return html
            .Replace("{{CART_JSON}}", JsonSerializer.Serialize(rows.Select(JsonRow), PhpJson), StringComparison.Ordinal)
            .Replace("{{CHECKOUT_JS}}", CheckoutJs(input), StringComparison.Ordinal);
    }

    private static object JsonRow(CartRow row) => new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["id"] = row.Id,
        ["price"] = row.Price,
        ["product_type"] = row.ProductType,
        ["count_need"] = row.CountNeed,
        ["checked_for_order"] = row.CheckedForOrder,
        ["access"] = row.Access,
        ["time_to_exe"] = row.TimeToExe,
        ["min_order"] = row.MinOrder,
        ["name"] = H(row.Name),
        ["manufacturer"] = H(row.Manufacturer),
        ["article"] = H(row.Article),
        ["image_src"] = H(row.ImageSrc),
        ["product_id"] = row.ProductId == 0 ? string.Empty : row.ProductId,
        ["url_refresh"] = row.UrlRefresh,
    };

    private static async Task<CartRow?> CartRowAsync(
        DbConnection connection,
        Input input,
        long id,
        long sessionId,
        Func<string, bool> productImageExists,
        CancellationToken cancellationToken)
    {
        var rows = await RowsAsync(
            connection,
            "SELECT `id`,`price`,`product_type`,`count_need`,`checked_for_order`,`t2_time_to_exe`,`t2_time_to_exe_guaranteed`,`t2_min_order`,`t2_name`,`t2_manufacturer`,`t2_article`,`product_id`,`image` "
            + "FROM `shop_carts` WHERE `id` = ? AND `user_id` = ? AND `session_id` = ?",
            [id, input.UserId, sessionId],
            cancellationToken).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return null;
        }

        var r = rows[0];
        var productType = Int(r[2]);
        var checkedForOrder = Int(r[4]);
        var access = 1;
        var image = string.Empty;
        var productId = 0;
        var refresh = string.Empty;
        var manufacturer = r[9] ?? string.Empty;
        var article = r[10] ?? string.Empty;

        if (productType == 1)
        {
            productId = Int(r[11]);
            var storage = await RowsAsync(
                connection,
                "SELECT `price`, CAST((SELECT `price` FROM `shop_carts_details` WHERE `cart_record_id` = ?) AS decimal(20,2)) "
                + "FROM `shop_storages_data` WHERE `id` = (SELECT `storage_record_id` FROM `shop_carts_details` WHERE `cart_record_id` = ?) AND `product_id` = ?",
                [id, id, productId],
                cancellationToken).ConfigureAwait(false);
            if (storage.Count == 0 || !PhpNumericEqual(storage[0][0], storage[0][1]))
            {
                access = 0;
                checkedForOrder = 0;
                await ExecuteAsync(
                    connection,
                    "UPDATE `shop_carts` SET `checked_for_order` = 0 WHERE `id` = ? AND `user_id` = ? AND `session_id` = ? AND `product_type` = 1",
                    [id, input.UserId, sessionId],
                    cancellationToken).ConfigureAwait(false);
            }

            var product = await RowsAsync(connection, "SELECT `category_id`,`alias` FROM `shop_catalogue_products` WHERE `id` = ?", [productId], cancellationToken).ConfigureAwait(false);
            if (product.Count > 0)
            {
                var picture = await RowsAsync(connection, "SELECT `file_name` FROM `shop_products_images` WHERE `product_id` = ? ORDER BY `id` ASC LIMIT 1", [productId], cancellationToken).ConfigureAwait(false);
                if (picture.Count > 0)
                {
                    image = ImageUrl(picture[0][0], productImageExists);
                }

                var category = await ScalarAsync(connection, "SELECT `url` FROM `shop_catalogue_categories` WHERE `id` = ?", [Int(product[0][0])], cancellationToken).ConfigureAwait(false);
                if (category is not null)
                {
                    refresh = "/" + category.Trim() + "/" + (input.ProductUrlMode == "id" ? productId.ToString(CultureInfo.InvariantCulture) : (product[0][1] ?? string.Empty).Trim());
                }
            }
        }
        else if (productType == 2)
        {
            image = ImageUrl(r[12], productImageExists, typeTwo: true);
            refresh = "/parts/" + System.Net.WebUtility.HtmlEncode(manufacturer) + "/" + article;
        }

        var minOrder = Int(r[7]);
        if (minOrder <= 0)
        {
            minOrder = 1;
        }

        var firstTerm = Int(r[5]);
        var guaranteed = Int(r[6]);
        var term = firstTerm < guaranteed
            ? firstTerm.ToString(CultureInfo.InvariantCulture) + " - " + guaranteed.ToString(CultureInfo.InvariantCulture)
            : firstTerm.ToString(CultureInfo.InvariantCulture);
        term = firstTerm == 0 && guaranteed == 0 ? "{4197}" : term + " {4097}.";
        return new(
            id,
            r[1] ?? string.Empty,
            productType,
            Int(r[3]),
            checkedForOrder,
            access,
            term,
            minOrder,
            CleanName(r[8]),
            manufacturer,
            article,
            image,
            productId,
            refresh);
    }

    private static string ImageUrl(string? raw, Func<string, bool> exists, bool typeTwo = false)
    {
        var file = (raw ?? string.Empty).Trim();
        if (file.Length == 0)
        {
            return string.Empty;
        }

        if (typeTwo)
        {
            var slash = file.IndexOf('/', StringComparison.Ordinal);
            if (slash > 0)
            {
                return file;
            }
        }
        else if (file.Contains('/', StringComparison.Ordinal) || file.StartsWith("auto_price/", StringComparison.Ordinal))
        {
            return file.StartsWith("/", StringComparison.Ordinal) ? file : "/content/files/images/" + file;
        }

        return exists(file) ? "/content/files/images/products_images/" + file : string.Empty;
    }

    private static string CheckoutJs(Input input)
    {
        if (!input.TradeCheckoutBlocked)
        {
            var href = input.LangHref + (input.UserId > 0 ? "/shop/checkout/how_get" : "/shop/checkout/login_offer");
            return "\t\t\tcart_html += '<div style=\"text-align: right;\"><a class=\"btn btn-ar btn-primary\" href=\""
                + JsHtml(href) + "\">{4500}</a></div>';\n\t\t\t";
        }

        var message = "<div class=\"alert alert-warning\" style=\"margin-top:12px;text-align:left;\">"
            + H(input.TradeCheckoutMessage)
            + "</div><div style=\"text-align: right;\"><span class=\"btn btn-default disabled\" style=\"opacity:.65;cursor:not-allowed;\">{4500} (awaiting approval)</span></div>";
        return "\t\t\tcart_html += " + PhpJsonString(message) + ";\n\t\t\t";
    }

    private static async Task<string> GarageOptionsAsync(DbConnection connection, int userId, CancellationToken cancellationToken)
    {
        var rows = await RowsAsync(
            connection,
            "SELECT `shop_docpart_garage`.`id`,(SELECT `caption` FROM `shop_docpart_cars` WHERE `id` = `shop_docpart_garage`.`mark_id`),`model`,`year`,`shop_docpart_garage`.`caption` FROM `shop_docpart_garage` WHERE `user_id` = ?",
            [userId],
            cancellationToken).ConfigureAwait(false);
        var sb = new StringBuilder();
        foreach (var r in rows)
        {
            sb.Append("<option value=\"").Append(H(r[0])).Append("\">")
                .Append(H(r[1])).Append(' ').Append(H(r[2])).Append(' ').Append(H(r[3]))
                .Append(" {4321} - ").Append(H(r[4])).Append("</option>");
        }
        return sb.ToString();
    }

    private static async Task ClearGuestCartAsync(DbConnection connection, long sessionId, CancellationToken cancellationToken)
    {
        if (sessionId <= 0)
        {
            return;
        }

        var ids = await IdsAsync(connection, 0, sessionId, cancellationToken).ConfigureAwait(false);
        if (ids.Count == 0)
        {
            return;
        }

        var placeholders = string.Join(',', Enumerable.Repeat("?", ids.Count));
        await ExecuteAsync(connection, "DELETE FROM `shop_carts_details` WHERE `cart_record_id` IN (" + placeholders + ")", ids.Cast<object?>().ToArray(), cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, "DELETE FROM `shop_carts` WHERE `user_id` = 0 AND `session_id` = ?", [sessionId], cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<long>> IdsAsync(DbConnection connection, int userId, long sessionId, CancellationToken cancellationToken)
        => (await RowsAsync(connection, "SELECT `id` FROM `shop_carts` WHERE `user_id` = ? AND `session_id` = ?", [userId, sessionId], cancellationToken).ConfigureAwait(false))
            .Select(r => (long)Int(r[0])).ToList();

    private static string Fill(string template, Input input) => template
        .Replace("{{PRICE_STYLES}}", input.PriceStylesHtml, StringComparison.Ordinal)
        .Replace("{{WA_STYLES}}", input.WhatsAppStylesHtml, StringComparison.Ordinal)
        .Replace("{{WA_SCRIPT}}", input.WhatsAppScriptHtml, StringComparison.Ordinal)
        .Replace("{{CSRF}}", JsHtml(input.CsrfKey), StringComparison.Ordinal);

    private static string CleanName(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        foreach (var remove in new[] { "'", "\"", "\n", "\t", "\r", "\\" })
        {
            text = text.Replace(remove, string.Empty, StringComparison.Ordinal);
        }
        return text;
    }

    private static bool PhpNumericEqual(string? left, string? right)
        => decimal.TryParse(left, NumberStyles.Float, CultureInfo.InvariantCulture, out var l)
           && decimal.TryParse(right, NumberStyles.Float, CultureInfo.InvariantCulture, out var r)
           && l == r;

    private static string PhpJsonString(string value)
    {
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) });
        return json.Replace("/", "\\/", StringComparison.Ordinal);
    }

    private static string JsHtml(string? value) => (value ?? string.Empty)
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("'", "\\'", StringComparison.Ordinal)
        .Replace("\r", "\\r", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("<", "\\u003C", StringComparison.Ordinal)
        .Replace(">", "\\u003E", StringComparison.Ordinal)
        .Replace("&", "\\u0026", StringComparison.Ordinal);

    private static string H(string? value) => StorefrontSupplierLpoNotifier.H(value ?? string.Empty);
    private static int Int(string? value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0;

    private static string Template(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EcomAE.Platform.Storefront.Templates." + name)
            ?? throw new InvalidOperationException("Missing storefront cart template " + name);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static async Task<string?> ScalarAsync(DbConnection connection, string sql, object?[] args, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql, object?[] args, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<List<string?[]>> RowsAsync(DbConnection connection, string sql, object?[] args, CancellationToken cancellationToken)
    {
        var rows = new List<string?[]>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var row = new string?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++)
            {
                row[i] = reader.IsDBNull(i) ? null : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
            }
            rows.Add(row);
        }
        return rows;
    }
}
