using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using EcomAE.Platform.Data;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Live PHP <c>ajax_add_to_basket.php</c> type-2 twin: INSERT into <c>shop_carts</c> for authenticated customers.
/// </summary>
public interface IStorefrontCartAddService
{
    Task<StorefrontCartAddResult> AddAsync(
        int userId,
        StorefrontCartAddRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record StorefrontCartAddResult(
    bool Ok,
    string Status,
    string Code,
    string Message,
    int Writes,
    bool WritesBlocked,
    long? CartRecordId,
    object Intended)
{
    public object ToPayload(object session) => new
    {
        ok = Ok,
        status = Ok,
        surface = "storefront",
        status_token = Status,
        writes = Writes,
        writesBlocked = WritesBlocked,
        cutoverAllowed = true,
        phpAuthoritative = false,
        validation_code = Code,
        would_write = Ok && Writes > 0,
        cart_record_id = CartRecordId,
        intended = Intended,
        message = Message,
        detail = Message,
        note = Message,
        session
    };
}

public sealed class StorefrontCartAddService : IStorefrontCartAddService
{
    private readonly ITenantDbConnectionFactory _connections;

    public StorefrontCartAddService(ITenantDbConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<StorefrontCartAddResult> AddAsync(
        int userId,
        StorefrontCartAddRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var intended = new
        {
            product_type = request.ProductType,
            manufacturer = request.Manufacturer ?? "",
            article = request.Article ?? "",
            count_need = request.CountNeed,
            price = request.Price
        };

        if (userId <= 0)
        {
            return Fail("unauthorized", "auth", "Please log in or register to continue.", intended);
        }

        if (!_connections.IsConfigured)
        {
            return Fail("db_unavailable", "db", "Cart database is not configured.", intended);
        }

        if (request.ProductType != 2)
        {
            return Fail("unsupported", "unknown_product_type", "Only warehouse (docpart) lines can be added here.", intended);
        }

        var manufacturer = (request.Manufacturer ?? string.Empty).Trim();
        var article = (request.Article ?? string.Empty).Trim();
        var articleShow = string.IsNullOrWhiteSpace(request.ArticleShow) ? article : request.ArticleShow.Trim();
        var name = (request.Name ?? string.Empty).Trim();
        if (manufacturer.Length == 0 || article.Length == 0)
        {
            return Fail("invalid", "incorrect_data", "Manufacturer and article are required.", intended);
        }

        var price = Math.Round(request.Price, 2, MidpointRounding.AwayFromZero);
        if (price < 0)
        {
            return Fail("invalid", "incorrect_data", "Price must be >= 0.", intended);
        }

        var minOrder = request.MinOrder > 0 ? request.MinOrder : 1;
        var countNeed = request.CountNeed > 0 ? request.CountNeed : minOrder;
        if (countNeed < minOrder)
        {
            countNeed = minOrder;
        }

        var exist = request.Exist;
        if (exist > 0 && countNeed > exist)
        {
            return Fail("invalid", "not_enough", "Requested quantity exceeds available stock.", intended);
        }

        // PHP epc_pricing_offer_allows_cart: allow when cost unknown/redacted; block clear below-cost.
        var purchase = request.PricePurchase;
        if (purchase > 0 && price + 0.0001m < purchase)
        {
            return Fail(
                "no_margin",
                "no_margin",
                "Unable to add this item to your cart right now. Please refresh the page and try again.",
                intended);
        }

        var timeToExe = (request.TimeToExe ?? "0").Trim();
        var timeToExeG = string.IsNullOrWhiteSpace(request.TimeToExeGuaranteed)
            ? timeToExe
            : request.TimeToExeGuaranteed.Trim();
        var storage = request.Storage ?? string.Empty;
        var probability = request.Probability <= 0 ? 100 : request.Probability;
        var markup = request.Markup;
        var officeId = request.OfficeId;
        var storageId = request.StorageId;
        var jsonParams = request.JsonParams ?? string.Empty;
        var sessionId = 0; // authenticated customers use session_id=0 (PHP twin)

        await using var connection = await _connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);

        // Duplicate guard (non-used parts) — PHP already / code already.
        await using (var check = connection.CreateCommand())
        {
            check.CommandText = """
                SELECT COUNT(*) FROM `shop_carts` WHERE
                    `product_type` = 2 AND
                    `user_id` = @userId AND
                    `session_id` = @sessionId AND
                    `t2_manufacturer` = @mfr AND
                    `t2_article` = @article AND
                    `t2_exist` = @exist AND
                    `t2_time_to_exe` = @tte AND
                    `t2_time_to_exe_guaranteed` = @tteg AND
                    `t2_probability` = @prob AND
                    `t2_office_id` = @officeId AND
                    `t2_storage_id` = @storageId AND
                    CAST(`price` AS DECIMAL(18,4)) = CAST(@price AS DECIMAL(18,4))
                """;
            Add(check, "@userId", userId);
            Add(check, "@sessionId", sessionId);
            Add(check, "@mfr", manufacturer);
            Add(check, "@article", article);
            Add(check, "@exist", exist);
            Add(check, "@tte", timeToExe);
            Add(check, "@tteg", timeToExeG);
            Add(check, "@prob", probability);
            Add(check, "@officeId", officeId);
            Add(check, "@storageId", storageId);
            Add(check, "@price", price);
            var countObj = await check.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            var already = Convert.ToInt32(countObj ?? 0, CultureInfo.InvariantCulture);
            if (already > 0)
            {
                return Fail("already", "already", "This part is already in your cart.", intended);
            }
        }

        var productJson = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["product_type"] = 2,
            ["manufacturer"] = manufacturer,
            ["article"] = article,
            ["article_show"] = articleShow,
            ["name"] = name,
            ["exist"] = exist,
            ["time_to_exe"] = timeToExe,
            ["time_to_exe_guaranteed"] = timeToExeG,
            ["storage"] = storage,
            ["min_order"] = minOrder,
            ["probability"] = probability,
            ["price"] = price.ToString("0.00", CultureInfo.InvariantCulture),
            ["price_purchase"] = purchase.ToString("0.00", CultureInfo.InvariantCulture),
            ["markup"] = markup,
            ["office_id"] = officeId,
            ["storage_id"] = storageId,
            ["json_params"] = jsonParams,
            ["count_need"] = countNeed,
            ["check_hash"] = request.CheckHash ?? ""
        });

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText = """
                INSERT INTO `shop_carts` (
                    `product_type`, `price`, `count_need`, `time`, `user_id`, `session_id`,
                    `t2_manufacturer`, `t2_article`, `t2_article_show`, `t2_name`, `t2_exist`,
                    `t2_time_to_exe`, `t2_time_to_exe_guaranteed`, `t2_storage`, `t2_min_order`,
                    `t2_probability`, `t2_markup`, `t2_price_purchase`, `t2_office_id`, `t2_storage_id`,
                    `t2_product_json`, `t2_json_params`
                ) VALUES (
                    2, @price, @countNeed, @time, @userId, @sessionId,
                    @mfr, @article, @articleShow, @name, @exist,
                    @tte, @tteg, @storage, @minOrder,
                    @prob, @markup, @purchase, @officeId, @storageId,
                    @productJson, @jsonParams
                )
                """;
            Add(insert, "@price", price);
            Add(insert, "@countNeed", countNeed);
            Add(insert, "@time", now);
            Add(insert, "@userId", userId);
            Add(insert, "@sessionId", sessionId);
            Add(insert, "@mfr", manufacturer);
            Add(insert, "@article", article);
            Add(insert, "@articleShow", articleShow);
            Add(insert, "@name", name);
            Add(insert, "@exist", exist);
            Add(insert, "@tte", timeToExe);
            Add(insert, "@tteg", timeToExeG);
            Add(insert, "@storage", storage);
            Add(insert, "@minOrder", minOrder);
            Add(insert, "@prob", probability);
            Add(insert, "@markup", markup);
            Add(insert, "@purchase", purchase);
            Add(insert, "@officeId", officeId);
            Add(insert, "@storageId", storageId);
            Add(insert, "@productJson", productJson);
            Add(insert, "@jsonParams", jsonParams);

            var rows = await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (rows <= 0)
            {
                return Fail("insert_failed", "error", "Could not add to cart.", intended);
            }
        }

        long? newId = null;
        await using (var idCmd = connection.CreateCommand())
        {
            idCmd.CommandText = "SELECT LAST_INSERT_ID()";
            var idObj = await idCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (idObj is not null && idObj is not DBNull)
            {
                newId = Convert.ToInt64(idObj, CultureInfo.InvariantCulture);
            }
        }

        return new StorefrontCartAddResult(
            true,
            "written",
            "ok",
            "Added to cart.",
            1,
            false,
            newId,
            intended);
    }

    private static StorefrontCartAddResult Fail(string status, string code, string message, object intended)
        => new(false, status, code, message, 0, false, null, intended);

    private static void Add(DbCommand command, string name, object? value)
    {
        var p = command.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        command.Parameters.Add(p);
    }
}
