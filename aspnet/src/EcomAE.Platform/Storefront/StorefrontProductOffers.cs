using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// The offers block of the PHP customer product page (<c>content/shop/catalogue/</c>): for every office of the
/// shopper's <c>my_city</c> geo node, the in-stock warehouse rows priced by the office/storage/group markup, then the
/// CP sell-from-purchase stack, delivery days, <c>price_rounding</c> and the <c>check_hash</c> that
/// <c>ajax_add_to_basket.php</c> verifies. Byte-for-byte the PHP output (golden <c>Fixtures/ProductOffers</c>).
/// </summary>
public static class StorefrontProductOffers
{
    public sealed record Request(
        long ProductId,
        long UserId,
        string? CityCookie,
        string MinOrder,
        string LangHref,
        long Now,
        string PriceRounding,
        string TechKey);

    /// <summary>
    /// The product page's offers block followed by the <c>common_add_to_basket.php</c> script, translated for the
    /// page language, with the visitor session's <c>csrf_guard_key</c> (PHP <c>DP_User::getUserSession()</c>).
    /// </summary>
    public static async Task<string> RenderPageAsync(
        DbConnection connection,
        long productId,
        long userId,
        string? cityCookie,
        string langHref,
        string? sessionToken,
        string? sessionUserId,
        IReadOnlyDictionary<string, string> config,
        CancellationToken cancellationToken)
    {
        var translator = new StorefrontPhpTranslator(connection, StorefrontRegFormLoader.LangCode(langHref.TrimEnd('/')));
        var request = new Request(
            productId,
            userId,
            cityCookie,
            "1",
            langHref.TrimEnd('/'),
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            config.TryGetValue("price_rounding", out var rounding) ? rounding : string.Empty,
            config.TryGetValue("tech_key", out var techKey) ? techKey : string.Empty);
        var offers = await RenderAsync(connection, request, (key, ct) => translator.TextAsync(key, ct), cancellationToken).ConfigureAwait(false);

        var csrf = string.Empty;
        if (!string.IsNullOrEmpty(sessionToken) && sessionUserId is not null)
        {
            try
            {
                csrf = await ErpDb.StringAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT IFNULL(`csrf_guard_key`, '') FROM `sessions` WHERE `session` = ? AND `user_id` = ? LIMIT 1"),
                    cancellationToken,
                    sessionToken,
                    sessionUserId).ConfigureAwait(false) ?? string.Empty;
            }
            catch (DbException)
            {
            }
        }

        var strings = new Dictionary<int, string>();
        foreach (var id in StorefrontCommonAddToBasket.StringIds)
        {
            strings[id] = await translator.TextAsync(id, cancellationToken).ConfigureAwait(false);
        }

        return offers + StorefrontCommonAddToBasket.Script(csrf, id => strings.TryGetValue(id, out var text) ? text : string.Empty);
    }

    private const string CurrencyRate = "(SELECT `rate` FROM `shop_currencies` WHERE `iso_code` = (SELECT `currency` FROM `shop_storages` WHERE `id` = `shop_storages_data`.`storage_id`) )";

    public static async Task<string> RenderAsync(
        DbConnection connection,
        Request request,
        Func<string, CancellationToken, Task<string>> translate,
        CancellationToken cancellationToken)
    {
        async Task<string> T(object key) => await translate(Convert.ToString(key, CultureInfo.InvariantCulture) ?? string.Empty, cancellationToken).ConfigureAwait(false);

        var profileGroups = await StorefrontPhpAjax.UserProfileGroupsAsync(connection, request.UserId, cancellationToken).ConfigureAwait(false);
        var groupId = await EpcPricing.ResolveCustomerGroupIdAsync(
            connection,
            request.UserId,
            cancellationToken,
            profileGroups.Count > 0 ? (int)profileGroups[0] : 0).ConfigureAwait(false);
        var offices = await StorefrontCustomerOffices.LoadAsync(connection, request.CityCookie, cancellationToken).ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.Append("\n\n<div class=\"row\"></div>\n<div class=\"col-lg-12\">\n\t<h2 class=\"section-title\">").Append(await T(4164)).Append("</h2>\n")
            .Append("\t<div class=\"product_suggestions_box\">\n\t\t<div class=\"product_suggestions product_suggestions_header\">\n")
            .Append("\t\t\t<div class=\"price\">\n\t\t\t\t").Append(await T(2751)).Append("\t\t\t</div>\n")
            .Append("\t\t\t<div class=\"exist\">\n\t\t\t\t").Append(await T(1717)).Append("\t\t\t</div>\n")
            .Append("\t\t\t<div class=\"reserved\">\n\t\t\t\t").Append(await T(4098)).Append("\t\t\t</div>\n")
            .Append("\t\t\t<div class=\"exist_details\">\n\t\t\t\t").Append(await T(3550)).Append("\t\t\t</div>\n")
            .Append("\t\t</div>\n\n\t\t");

        foreach (var officeId in offices)
        {
            var (caption, city, address) = await OfficeAsync(connection, officeId, cancellationToken).ConfigureAwait(false);
            sb.Append("\t\t\t<div class=\"product_office\">\n\t\t\t\t\t\t\t\t<span>")
                .Append((await T(caption)).Trim()).Append("<div><small>").Append((await T(city)).Trim()).Append(", ").Append((await T(address)).Trim()).Append("</small></div>")
                .Append("</span>\n\t\t\t</div>\n\t\t\t");

            var rows = await RowsAsync(connection, officeId, request.ProductId, cancellationToken).ConfigureAwait(false);
            foreach (var row in rows)
            {
                var divId = officeId.ToString(CultureInfo.InvariantCulture) + "_" + row.StorageId.ToString(CultureInfo.InvariantCulture) + "_" + row.Id.ToString(CultureInfo.InvariantCulture);
                var price = await PriceAsync(connection, officeId, row, groupId, request.PriceRounding, cancellationToken).ConfigureAwait(false);
                var timeToExe = TimeToExe(row, request.Now);
                var exist = PhpNumber(row.Exist);
                var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(
                    request.ProductId.ToString(CultureInfo.InvariantCulture) + officeId.ToString(CultureInfo.InvariantCulture)
                    + row.StorageId.ToString(CultureInfo.InvariantCulture) + row.Id.ToString(CultureInfo.InvariantCulture)
                    + price + request.TechKey))).ToLowerInvariant();

                sb.Append("\t\t\t\t<div\n")
                    .Append("\t\t\t\t\tid = \"").Append(divId).Append("\"\n")
                    .Append("\t\t\t\t\tproduct_id = \"").Append(request.ProductId.ToString(CultureInfo.InvariantCulture)).Append("\"\n")
                    .Append("\t\t\t\t\toffice_id = \"").Append(officeId.ToString(CultureInfo.InvariantCulture)).Append("\"\n")
                    .Append("\t\t\t\t\tstorage_id = \"").Append(row.StorageId.ToString(CultureInfo.InvariantCulture)).Append("\"\n")
                    .Append("\t\t\t\t\tstorage_record_id = \"").Append(row.Id.ToString(CultureInfo.InvariantCulture)).Append("\"\n")
                    .Append("\t\t\t\t\tprice = \"").Append(price).Append("\"\n")
                    .Append("\t\t\t\t\ttime_to_exe = \"").Append(timeToExe.ToString(CultureInfo.InvariantCulture)).Append("\"\n")
                    .Append("\t\t\t\t\texist = \"").Append(row.Exist).Append("\"\n")
                    .Append("\t\t\t\t\tcheck_hash = \"").Append(hash).Append("\"\n")
                    .Append("\t\t\t\t></div>\n\t\t\t\t\n\t\t\t\t\n")
                    .Append("\t\t\t\t<div class=\"product_suggestions\">\n")
                    .Append("\t\t\t\t\t<div class=\"price\">\n\t\t\t\t\t\t").Append(price).Append("\t\t\t\t\t</div>\n")
                    .Append("\t\t\t\t\t<div class=\"exist\">\n\t\t\t\t\t\t").Append(row.Exist).Append("\t\t\t\t\t</div>\n")
                    .Append("\t\t\t\t\t<div class=\"reserved\">\n\t\t\t\t\t\t").Append(row.Reserved).Append("\t\t\t\t\t</div>\n")
                    .Append("\t\t\t\t\t<div class=\"exist_details\">\n\t\t\t\t\t\t");

                var action = string.Empty;
                if (timeToExe == 0 && exist > 0)
                {
                    sb.Append("\t\t\t\t\t\t\t").Append(await T(4094)).Append("\t\t\t\t\t\t\t");
                    action = await PurchaseButtonAsync(divId, row.Exist, request.MinOrder, T).ConfigureAwait(false);
                }
                else if (exist > 0)
                {
                    sb.Append("\t\t\t\t\t\t\t").Append(timeToExe.ToString(CultureInfo.InvariantCulture)).Append(" дн.\n\t\t\t\t\t\t\t");
                    action = await PurchaseButtonAsync(divId, row.Exist, request.MinOrder, T).ConfigureAwait(false);
                }
                else
                {
                    sb.Append("\t\t\t\t\t\t\t").Append(await T(4098)).Append("\t\t\t\t\t\t\t");
                }

                sb.Append("\t\t\t\t\t</div>\n")
                    .Append("\t\t\t\t\t<div class=\"purchase\">\n\t\t\t\t\t\t").Append(action).Append("\t\t\t\t\t</div>\n")
                    .Append("\t\t\t\t</div>\n\t\t\t\t");
            }

            if (rows.Count == 0)
            {
                sb.Append("\t\t\t\t<div class=\"product_suggestions\">\n")
                    .Append("\t\t\t\t\t<div style=\"padding-left:20px;\">\n\t\t\t\t\t\t").Append(await T(4165)).Append("\t\t\t\t\t</div>\n")
                    .Append("\t\t\t\t\t<div class=\"purchase\">\n")
                    .Append("\t\t\t\t\t\t<div class=\"btn-ar btn-primary cart_btn_purchase_action\">\n")
                    .Append("\t\t\t\t\t\t\t<table><tr><td><div class=\"product_div_count_need\"></div></td><td><a href=\"").Append(request.LangHref)
                    .Append("/zapros-prodavczu\" target=\"_blank\">").Append(await T(4115)).Append("</a></td></tr></table>\n")
                    .Append("\t\t\t\t\t\t</div>\n\t\t\t\t\t</div>\n\t\t\t\t</div>\n\t\t\t\t");
            }
        }

        sb.Append("\n\t</div>\n</div>\n\n\n\n\n\n\n");
        return sb.ToString();
    }

    private sealed record Row(long Id, long StorageId, string Exist, string Reserved, string Price, long TimeToExe, long ArrivalTime, long AdditionalTime);

    private static async Task<string> PurchaseButtonAsync(string divId, string exist, string minOrder, Func<object, Task<string>> t)
        => "<div class=\"btn-ar btn-primary cart_btn_purchase_action\">\n"
           + "\t\t\t\t\t\t\t\t<table><tr><td><div class=\"product_div_count_need\"><a class=\"count_need_minus\" href=\"javascript:void(0);\" onclick=\"minusCountNeed('" + divId + "', " + exist + ", " + minOrder + ");\">-</a>"
           + "<input class=\"count_need_input count_need_" + divId + "\" type=\"text\" value=\"" + minOrder + "\" onchange=\"onKeyUpCountNeed('" + divId + "', " + exist + ", " + minOrder + ");\"/>"
           + "<a class=\"count_need_plus\" href=\"javascript:void(0);\" onclick=\"plusCountNeed('" + divId + "', " + exist + ", " + minOrder + ");\">+</a></div></td>"
           + "<td><a href=\"javascript:void(0);\" onclick=\"purchase_action('" + divId + "');\">" + await t(4096).ConfigureAwait(false) + "</a></td></tr></table>\n"
           + "\t\t\t\t\t\t\t</div>";

    private static async Task<(string Caption, string City, string Address)> OfficeAsync(DbConnection connection, int officeId, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional("SELECT `caption`, `city`, `address` FROM `shop_offices` WHERE `id` = ?;");
        ErpDb.AddParameters(cmd, officeId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return (string.Empty, string.Empty, string.Empty);
        }

        string S(int i) => reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;
        return (S(0), S(1), S(2));
    }

    private static async Task<List<Row>> RowsAsync(DbConnection connection, int officeId, long productId, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = ErpDb.Positional(
            "SELECT `id`, `storage_id`, `exist`, `reserved`, `time_to_exe`, `arrival_time`, CAST(`price`*" + CurrencyRate + " AS decimal(10,2)) AS `price`, "
            + "(SELECT `additional_time` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = `shop_storages_data`.`storage_id` LIMIT 1) AS `additional_time` "
            + "FROM `shop_storages_data` WHERE `product_id` = ? AND (`price`>0 AND (`exist`>0 OR `reserved`>0)) "
            + "AND `storage_id` IN(SELECT DISTINCT `storage_id` FROM `shop_offices_storages_map` WHERE `office_id` = ?) ORDER BY `price`, `exist`;");
        ErpDb.AddParameters(cmd, officeId, productId, officeId);
        var rows = new List<Row>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            string S(int i) => reader.IsDBNull(i) ? string.Empty : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;
            rows.Add(new Row(
                Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture),
                S(2),
                S(3),
                S(6),
                (long)PhpNumber(S(4)),
                (long)PhpNumber(S(5)),
                (long)PhpNumber(S(7))));
        }

        return rows;
    }

    /// <summary>The PHP float/int <c>$price</c> after markup, sell stack and rounding, as PHP echoes it.</summary>
    private static async Task<string> PriceAsync(DbConnection connection, int officeId, Row row, int groupId, string rounding, CancellationToken cancellationToken)
    {
        var sqlPrice = PhpNumber(row.Price);
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = ErpDb.Positional("SELECT `markup`/100 as `markup` FROM `shop_offices_storages_map` WHERE `office_id` = ? AND `storage_id` = ? AND `group_id` = ? AND `min_point` <= ? AND `max_point` > ?;");
            ErpDb.AddParameters(cmd, officeId, row.StorageId, groupId, row.Price, row.Price);
            var markup = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var markupValue = markup is null or DBNull ? 0d : PhpNumber(Convert.ToString(markup, CultureInfo.InvariantCulture));
            var price = sqlPrice + (sqlPrice * markupValue);

            var purchase = decimal.TryParse(row.Price, NumberStyles.Float, CultureInfo.InvariantCulture, out var p) ? p : 0m;
            var sell = await EpcPricing.SellFromPurchaseAsync(connection, groupId, string.Empty, purchase, string.Empty, (int)row.StorageId, cancellationToken).ConfigureAwait(false);
            if (sell.Visible && sell.Price > 0)
            {
                price = (double)sell.Price;
            }

            return Rounded(price, rounding);
        }
    }

    private static string Rounded(double price, string rounding)
    {
        switch (rounding)
        {
            case "1":
                var whole = (long)price;
                return (price != whole ? whole + 1 : whole).ToString(CultureInfo.InvariantCulture);
            case "2":
            {
                var value = (long)price;
                var last = value.ToString(CultureInfo.InvariantCulture)[^1] - '0';
                if (last is > 0 and < 5)
                {
                    value += 5 - last;
                }
                else if (last is > 5 and <= 9)
                {
                    value += 10 - last;
                }

                return value.ToString(CultureInfo.InvariantCulture);
            }
            case "3":
            {
                var value = (long)price;
                var last = value.ToString(CultureInfo.InvariantCulture)[^1] - '0';
                if (last != 0)
                {
                    value += 10 - last;
                }

                return value.ToString(CultureInfo.InvariantCulture);
            }
            default:
                return StorefrontPhpAjax.PhpFloatString(price);
        }
    }

    private static long TimeToExe(Row row, long now)
    {
        if (now < row.ArrivalTime)
        {
            return (long)((row.ArrivalTime + (row.AdditionalTime * 3600) - now) / 86400d);
        }

        return row.TimeToExe > 0
            ? row.TimeToExe + (long)(row.AdditionalTime / 24d)
            : (long)(row.AdditionalTime / 24d);
    }

    private static double PhpNumber(string? value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : 0d;
}
