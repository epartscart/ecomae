using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>ajax_epc_orders_oms.php</c> / <c>ajax_delete_orders.php</c> twins.</summary>
public interface ICpOmsWriteService
{
    Task<ErpSimpleWriteResult> SetItemStatusAsync(long orderId, long itemId, int status, int adminUserId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetItemsStatusAsync(long orderId, int status, IReadOnlyList<long> itemIds, int adminUserId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SendMessageAsync(long orderId, string text, long itemId, int adminUserId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetCourierAsync(long orderId, decimal deliveryPrice, string? country, int adminUserId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteUnpaidOrdersAsync(IReadOnlyList<long> orderIds, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddCommentAsync(long orderId, string? text, int adminUserId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetViewedAsync(IReadOnlyList<long> orderIds, int viewedFlag, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetFulfillmentStageAsync(long orderId, string? supplierKey, string? stage, string? notes, int adminUserId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AdvanceFulfillmentAsync(long orderId, string? supplierKey, int adminUserId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> UpdateItemAsync(long orderId, CpOmsItemWritePatch patch, int adminUserId, CancellationToken cancellationToken = default, bool writeLog = true);

    Task<ErpSimpleWriteResult> UpdateItemsAsync(long orderId, IReadOnlyList<CpOmsItemWritePatch> patches, int adminUserId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> PayRefundAsync(
        long orderId,
        bool directRefund,
        decimal? paidSumOverride,
        int adminUserId,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> RefreshItemCostAsync(
        long orderId,
        long itemId,
        int adminUserId,
        CancellationToken cancellationToken = default);
}

/// <summary>PHP <c>ajax_epc_orders_oms.php</c> <c>update_item</c> / <c>update_items</c> field patch. Warehouse reprice is refused.</summary>
public sealed record CpOmsItemWritePatch(
    long ItemId,
    decimal? Price = null,
    int? CountNeed = null,
    decimal? Purchase = null,
    int? StorageId = null,
    string? Name = null,
    string? Manufacturer = null,
    string? Article = null,
    string? ArticleShow = null,
    bool RepriceFromWarehouse = false);

public sealed class CpOmsWriteService : ICpOmsWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpOmsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetItemStatusAsync(
        long orderId,
        long itemId,
        int status,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        if (orderId <= 0 || itemId <= 0 || status <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid item status.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var check = connection.CreateCommand();
        check.CommandText = "SELECT `id` FROM `shop_orders_items` WHERE `id` = @itemId AND `order_id` = @orderId LIMIT 1";
        Add(check, "@itemId", itemId);
        Add(check, "@orderId", orderId);
        var found = await check.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (found is null || found is DBNull)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Item not found.");
        }

        await using var update = connection.CreateCommand();
        update.CommandText = "UPDATE `shop_orders_items` SET `status` = @status WHERE `id` = @itemId AND `order_id` = @orderId";
        Add(update, "@status", status);
        Add(update, "@itemId", itemId);
        Add(update, "@orderId", orderId);
        var rows = await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (rows <= 0)
        {
            return ErpSimpleWriteResult.Fail("update_failed", "Could not set item status.");
        }

        await using var log = connection.CreateCommand();
        log.CommandText = """
            INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`)
            VALUES (@orderId, @time, @userId, 1, @text, 0)
            """;
        Add(log, "@orderId", orderId);
        Add(log, "@time", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Add(log, "@userId", adminUserId);
        Add(log, "@text", "OMS set item <b>id " + itemId.ToString(CultureInfo.InvariantCulture) + "</b> status to " + status.ToString(CultureInfo.InvariantCulture));
        await log.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return ErpSimpleWriteResult.Ok("Item status updated.", itemId);
    }

    public async Task<ErpSimpleWriteResult> SetItemsStatusAsync(
        long orderId,
        int status,
        IReadOnlyList<long> itemIds,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        var ids = (itemIds ?? []).Where(id => id > 0).Distinct().ToArray();
        if (orderId <= 0 || status <= 0 || ids.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select items and a status.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var writes = 0;
        foreach (var itemId in ids)
        {
            var one = await SetItemStatusAsync(orderId, itemId, status, adminUserId, cancellationToken).ConfigureAwait(false);
            if (!one.Succeeded)
            {
                return one;
            }

            writes += one.Writes;
        }

        return new ErpSimpleWriteResult(true, "ok", "Item statuses updated.", orderId, writes);
    }

    public async Task<ErpSimpleWriteResult> SendMessageAsync(
        long orderId,
        string text,
        long itemId,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        var body = (text ?? string.Empty).Trim();
        if (orderId <= 0 || body.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Message text is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_orders` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            orderId);
        if (exists <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Order not found.");
        }

        if (itemId > 0)
        {
            await using var itemCmd = connection.CreateCommand();
            itemCmd.CommandText = ErpDb.Positional("SELECT `id`, `t2_article`, `t2_manufacturer` FROM `shop_orders_items` WHERE `id` = ? AND `order_id` = ? LIMIT 1");
            ErpDb.AddParameters(itemCmd, itemId, orderId);
            await using var reader = await itemCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("not_found", "Item not found.");
            }

            var article = Convert.ToString(reader["t2_article"], CultureInfo.InvariantCulture) ?? "";
            var brand = Convert.ToString(reader["t2_manufacturer"], CultureInfo.InvariantCulture) ?? "";
            body = "[Item #" + itemId.ToString(CultureInfo.InvariantCulture) + " " + (brand + " " + article).Trim() + "] " + body;
        }

        var time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `shop_orders_messages` (`order_id`, `is_customer`, `text`, `time`, `return_id`, `read`) VALUES (?, 0, ?, ?, 0, 0)"),
            cancellationToken,
            orderId, body, time);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,1,?,0)"),
            cancellationToken,
            orderId, time, adminUserId,
            itemId > 0 ? "OMS message to customer (item #" + itemId.ToString(CultureInfo.InvariantCulture) + ")" : "OMS message to customer");
        return ErpSimpleWriteResult.Ok("Message sent.", orderId);
    }

    public async Task<ErpSimpleWriteResult> SetCourierAsync(
        long orderId,
        decimal deliveryPrice,
        string? country,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        if (orderId <= 0 || deliveryPrice < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Courier fee cannot be negative.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var paid = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `paid` FROM `shop_orders` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            orderId);
        var json = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `how_get_json` FROM `shop_orders` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            orderId);
        if (json is null)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Order not found.");
        }

        if (paid != 0)
        {
            return ErpSimpleWriteResult.Fail("paid", "Cannot change courier on a paid order.");
        }

        JsonObject how;
        try
        {
            how = JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            how = [];
        }

        var fee = Math.Round(deliveryPrice, 2, MidpointRounding.AwayFromZero);
        how["delivery_price"] = fee;
        how["rate"] = fee;
        how["courier_payer"] = "customer";
        var iso = (country ?? string.Empty).Trim().ToUpperInvariant();
        if (iso.Length >= 2)
        {
            how["country"] = iso[..2];
        }

        var time = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_orders` SET `how_get_json` = ? WHERE `id` = ?"),
            cancellationToken,
            how.ToJsonString(), orderId);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,1,?,0)"),
            cancellationToken,
            orderId, time, adminUserId,
            "OMS set courier fee (customer pays) ex-VAT=" + fee.ToString("0.00", CultureInfo.InvariantCulture)
            + " AED, ship=" + (how["country"]?.ToString() ?? "")
            + " (VAT calc remains PHP)");
        return ErpSimpleWriteResult.Ok("Courier fee updated.", orderId);
    }

    public async Task<ErpSimpleWriteResult> DeleteUnpaidOrdersAsync(
        IReadOnlyList<long> orderIds,
        CancellationToken cancellationToken = default)
    {
        var ids = (orderIds ?? []).Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select unpaid orders to delete.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var id in ids)
            {
                var paid = await ErpDb.LongAsync(
                    connection,
                    tx,
                    ErpDb.Positional("SELECT COUNT(*) FROM `shop_orders` WHERE `id` = ? AND `paid` != 0"),
                    cancellationToken,
                    id);
                if (paid > 0)
                {
                    throw new ErpWriteException("Cannot delete a paid or partly paid order.");
                }
            }

            var writes = 0;
            foreach (var id in ids)
            {
                writes += await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional("DELETE FROM `shop_orders` WHERE `id` = ? AND `paid` = 0"), cancellationToken, id);
                await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional("DELETE FROM `shop_orders_items` WHERE `order_id` = ?"), cancellationToken, id);
                await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional("DELETE FROM `shop_orders_items_details` WHERE `order_id` = ?"), cancellationToken, id);
                await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional("DELETE FROM `shop_orders_logs` WHERE `order_id` = ?"), cancellationToken, id);
                await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional("DELETE FROM `shop_orders_messages` WHERE `order_id` = ?"), cancellationToken, id);
                await ErpDb.ExecuteAsync(connection, tx, ErpDb.Positional("DELETE FROM `shop_orders_viewed` WHERE `order_id` = ?"), cancellationToken, id);
            }

            if (writes <= 0)
            {
                throw new ErpWriteException("Order not found.");
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(true, "ok", "Unpaid orders deleted.", ids[0], writes);
        }
        catch (Exception ex) when (ex is ErpWriteException or DbException)
        {
            await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Fail("delete_failed", ex.Message);
        }
    }

    public async Task<ErpSimpleWriteResult> AddCommentAsync(
        long orderId,
        string? text,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        var body = WebUtility.HtmlEncode((text ?? string.Empty).Trim());
        if (orderId <= 0 || body.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Comment text is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_orders` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            orderId);
        if (exists <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Order not found.");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`) VALUES (?,?,?,1,?)"),
            cancellationToken,
            orderId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), adminUserId, body);
        return ErpSimpleWriteResult.Ok("Comment added.", orderId);
    }

    public async Task<ErpSimpleWriteResult> SetViewedAsync(
        IReadOnlyList<long> orderIds,
        int viewedFlag,
        CancellationToken cancellationToken = default)
    {
        var ids = (orderIds ?? []).Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0 || viewedFlag is not (0 or 1))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Select orders and a viewed flag of 0 or 1.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var writes = 0;
        foreach (var id in ids)
        {
            writes += await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_orders_viewed` SET `viewed_flag` = ? WHERE `order_id` = ?"),
                cancellationToken,
                viewedFlag, id);
        }

        return new ErpSimpleWriteResult(true, "ok", "Viewed flag updated.", ids[0], Math.Max(writes, 1));
    }

    public async Task<ErpSimpleWriteResult> SetFulfillmentStageAsync(
        long orderId,
        string? supplierKey,
        string? stage,
        string? notes,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        var key = (supplierKey ?? string.Empty).Trim();
        var next = (stage ?? string.Empty).Trim();
        if (orderId <= 0 || key.Length == 0 || next.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Supplier key and stage are required.");
        }

        if (!CpOmsFulfillmentSetStageDryRun.AllowedStages.Contains(next, StringComparer.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Unknown fulfillment stage.");
        }

        if (key.Length > 64)
        {
            key = key[..64];
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureFulfillmentSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_orders` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            orderId);
        if (exists <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Order not found.");
        }

        var note = (notes ?? string.Empty).Trim();
        if (note.Length > 512)
        {
            note = note[..512];
        }

        var rows = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                UPDATE `epc_order_supplier_fulfillment`
                SET `stage` = ?, `notes` = ?, `time_updated` = ?, `updated_by` = ?
                WHERE `order_id` = ? AND `supplier_key` = ?
                """),
            cancellationToken,
            next, note, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), adminUserId, orderId, key);
        if (rows <= 0)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Supplier fulfillment row not found for this order.");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,1,?,0)"),
            cancellationToken,
            orderId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), adminUserId,
            "OMS supplier fulfillment <b>" + WebUtility.HtmlEncode(key) + "</b> → " + WebUtility.HtmlEncode(next));
        return ErpSimpleWriteResult.Ok("Fulfillment stage updated.", orderId);
    }

    public async Task<ErpSimpleWriteResult> AdvanceFulfillmentAsync(
        long orderId,
        string? supplierKey,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        var key = (supplierKey ?? string.Empty).Trim();
        if (orderId <= 0 || key.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Supplier key is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureFulfillmentSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var current = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `stage` FROM `epc_order_supplier_fulfillment` WHERE `order_id` = ? AND `supplier_key` = ? LIMIT 1"),
            cancellationToken,
            orderId, key);
        if (current is null)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Supplier fulfillment row not found for this order.");
        }

        var stages = CpOmsFulfillmentSetStageDryRun.AllowedStages;
        var idx = stages.ToList().FindIndex(s => s.Equals(string.IsNullOrWhiteSpace(current) ? "supplier_confirm" : current, StringComparison.Ordinal));
        if (idx < 0)
        {
            idx = 0;
        }

        if (idx >= stages.Count - 1)
        {
            return ErpSimpleWriteResult.Ok("Already at last stage.", orderId);
        }

        return await SetFulfillmentStageAsync(orderId, key, stages[idx + 1], "", adminUserId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ErpSimpleWriteResult> UpdateItemAsync(
        long orderId,
        CpOmsItemWritePatch patch,
        int adminUserId,
        CancellationToken cancellationToken = default,
        bool writeLog = true)
    {
        ArgumentNullException.ThrowIfNull(patch);
        if (orderId <= 0 || patch.ItemId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid item.");
        }

        if (patch.CountNeed is < 1)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Quantity must be at least 1.");
        }

        if (patch.Price is <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Price must be greater than 0.");
        }

        if (patch.RepriceFromWarehouse)
        {
            return ErpSimpleWriteResult.Fail("not_implemented", "Warehouse reprice stays PHP.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var select = connection.CreateCommand();
        select.CommandText = ErpDb.Positional("""
            SELECT `price`, `count_need`, `t2_price_purchase`, `t2_storage_id`, `t2_name`,
                   `t2_manufacturer`, `t2_article`, `t2_article_show`, `t2_json_params`
            FROM `shop_orders_items` WHERE `id` = ? AND `order_id` = ? LIMIT 1
            """);
        ErpDb.AddParameters(select, patch.ItemId, orderId);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("not_found", "Item not found.");
        }

        var price = patch.Price ?? Convert.ToDecimal(reader["price"], CultureInfo.InvariantCulture);
        var qty = patch.CountNeed ?? Convert.ToInt32(reader["count_need"], CultureInfo.InvariantCulture);
        var purchase = patch.Purchase ?? Convert.ToDecimal(reader["t2_price_purchase"], CultureInfo.InvariantCulture);
        var storageId = patch.StorageId ?? Convert.ToInt32(reader["t2_storage_id"], CultureInfo.InvariantCulture);
        var name = SanitizeOmsText(patch.Name ?? Convert.ToString(reader["t2_name"], CultureInfo.InvariantCulture));
        var origBrand = Convert.ToString(reader["t2_manufacturer"], CultureInfo.InvariantCulture) ?? "";
        var origArticle = Convert.ToString(reader["t2_article"], CultureInfo.InvariantCulture) ?? "";
        var brand = SanitizeOmsText(patch.Manufacturer ?? origBrand);
        var article = SanitizeOmsText(patch.Article ?? origArticle);
        var articleShowRaw = Convert.ToString(reader["t2_article_show"], CultureInfo.InvariantCulture);
        var articleShow = SanitizeOmsText(
            patch.ArticleShow
            ?? (string.IsNullOrWhiteSpace(articleShowRaw) ? article : articleShowRaw));
        var jsonParams = Convert.ToString(reader["t2_json_params"], CultureInfo.InvariantCulture) ?? "";
        await reader.CloseAsync().ConfigureAwait(false);

        if (qty < 1)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Quantity must be at least 1.");
        }

        if (price <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Price must be greater than 0.");
        }

        if (brand.Length == 0 || article.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Brand and article number are required.");
        }

        if (articleShow.Length == 0)
        {
            articleShow = article;
        }

        var isAlt = !string.Equals(brand, origBrand, StringComparison.Ordinal)
                    || !string.Equals(article, origArticle, StringComparison.Ordinal);
        var jsonOut = jsonParams;
        if (isAlt)
        {
            JsonObject meta;
            try
            {
                meta = JsonNode.Parse(string.IsNullOrWhiteSpace(jsonParams) ? "{}" : jsonParams) as JsonObject ?? [];
            }
            catch (JsonException)
            {
                meta = [];
            }

            if (string.IsNullOrWhiteSpace(meta["requested_manufacturer"]?.ToString()))
            {
                meta["requested_manufacturer"] = origBrand;
            }

            if (string.IsNullOrWhiteSpace(meta["requested_article"]?.ToString()))
            {
                meta["requested_article"] = origArticle;
            }

            meta["offer_alternative"] = 1;
            meta["alt_manufacturer"] = brand;
            meta["alt_article"] = article;
            meta["alt_storage_id"] = storageId;
            jsonOut = meta.ToJsonString();
        }

        var storageCaption = await StorageCaptionAsync(connection, storageId, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("""
                UPDATE `shop_orders_items`
                SET `price` = ?, `count_need` = ?, `t2_price_purchase` = ?, `t2_storage_id` = ?,
                    `t2_storage` = ?, `t2_name` = ?, `t2_manufacturer` = ?, `t2_article` = ?,
                    `t2_article_show` = ?, `t2_json_params` = ?
                WHERE `id` = ? AND `order_id` = ?
                """),
            cancellationToken,
            price, qty, purchase, storageId, storageCaption, name, brand, article, articleShow, jsonOut,
            patch.ItemId, orderId);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `shop_orders_items_details` SET `storage_id` = ? WHERE `order_item_id` = ? AND `order_id` = ?"),
                cancellationToken,
                storageId, patch.ItemId, orderId);
        }
        catch (DbException)
        {
            // PHP swallows missing shop_orders_items_details.
        }

        if (writeLog)
        {
            var log = "OMS updated item <b>id " + patch.ItemId.ToString(CultureInfo.InvariantCulture) + "</b>: "
                      + brand + " / " + article
                      + (isAlt ? " (alternative)" : "")
                      + ", price=" + price.ToString("0.00", CultureInfo.InvariantCulture)
                      + ", qty=" + qty.ToString(CultureInfo.InvariantCulture)
                      + ", purchase=" + purchase.ToString("0.00", CultureInfo.InvariantCulture)
                      + ", storage_id=" + storageId.ToString(CultureInfo.InvariantCulture);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,1,?,0)"),
                cancellationToken,
                orderId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), adminUserId, log);
        }

        return ErpSimpleWriteResult.Ok("Item updated.", patch.ItemId);
    }

    public async Task<ErpSimpleWriteResult> UpdateItemsAsync(
        long orderId,
        IReadOnlyList<CpOmsItemWritePatch> patches,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        var rows = (patches ?? [])
            .Where(p => p.ItemId > 0)
            .GroupBy(p => p.ItemId)
            .Select(g => g.Last())
            .ToArray();
        if (orderId <= 0 || rows.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "No items to update.");
        }

        if (rows.Any(p => p.RepriceFromWarehouse))
        {
            return ErpSimpleWriteResult.Fail("not_implemented", "Warehouse reprice stays PHP.");
        }

        if (rows.Any(p => p.CountNeed is < 1))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Quantity must be at least 1.");
        }

        if (rows.Any(p => p.Price is <= 0))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Price must be greater than 0.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var updated = 0;
        foreach (var patch in rows)
        {
            var one = await UpdateItemAsync(orderId, patch, adminUserId, cancellationToken, writeLog: false).ConfigureAwait(false);
            if (!one.Succeeded)
            {
                continue;
            }

            updated++;
        }

        if (updated <= 0)
        {
            return ErpSimpleWriteResult.Fail("update_failed", "No lines updated.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,1,?,0)"),
            cancellationToken,
            orderId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), adminUserId,
            "OMS batch-updated <b>" + updated.ToString(CultureInfo.InvariantCulture) + "</b> line(s)");
        return new ErpSimpleWriteResult(true, "ok", "Lines updated.", orderId, updated);
    }

    public async Task<ErpSimpleWriteResult> PayRefundAsync(
        long orderId,
        bool directRefund,
        decimal? paidSumOverride,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        if (orderId <= 0)
        {
            return ErpSimpleWriteResult.Fail("forbidden", "Forbidden");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var exists = await ErpDb.LongAsync(
                connection,
                tx,
                ErpDb.Positional("SELECT `id` FROM `shop_orders` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                orderId);
            if (exists <= 0)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("forbidden", "Forbidden");
            }

            var paid = await ErpDb.LongAsync(
                connection,
                tx,
                ErpDb.Positional("SELECT `paid` FROM `shop_orders` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                orderId);

            var orderUser = await ErpDb.LongAsync(
                connection,
                tx,
                ErpDb.Positional("SELECT `user_id` FROM `shop_orders` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                orderId);
            if (orderUser == 0 && !directRefund)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("forbidden", "Forbidden");
            }

            if (paid == 0)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("unpaid", "Order is not paid.");
            }

            var paidSum = paidSumOverride is > 0
                ? paidSumOverride.Value
                : await ErpDb.DecimalAsync(
                    connection,
                    tx,
                    ErpDb.Positional("""
                        SELECT CAST((IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active`=1 AND `income`=0 AND `order_id`=?),0)
                          - IFNULL((SELECT SUM(`amount`) FROM `shop_users_accounting` WHERE `active`=1 AND `income`=1 AND `order_id`=?),0)) AS DECIMAL(12,2))
                        """),
                    cancellationToken,
                    orderId, orderId);
            if (paidSum <= 0)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("forbidden", "Forbidden");
            }

            var refundCode = await ErpDb.LongAsync(
                connection,
                tx,
                ErpDb.Positional("SELECT `id` FROM `shop_accounting_codes` WHERE `key` = ? LIMIT 1"),
                cancellationToken,
                "5_refund_from_order_to_balance");
            if (refundCode <= 0)
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ErpSimpleWriteResult.Fail("invalid", "Forbidden");
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await ErpDb.ExecuteAsync(
                connection,
                tx,
                ErpDb.Positional("""
                    INSERT INTO `shop_users_accounting`
                    (`user_id`, `time`, `income`, `amount`, `operation_code`, `active`, `order_id`, `office_id`)
                    VALUES (?, ?, 1, ?, ?, 1, ?, (SELECT `office_id` FROM `shop_orders` WHERE `id` = ? LIMIT 1))
                    """),
                cancellationToken,
                orderUser, now, paidSum, refundCode, orderId, orderId);
            var writes = 1;

            if (paidSumOverride is null or <= 0)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional("UPDATE `shop_orders` SET `paid` = 0 WHERE `id` = ?"),
                    cancellationToken,
                    orderId);
                writes++;
            }

            if (directRefund)
            {
                var cashCode = await ErpDb.LongAsync(
                    connection,
                    tx,
                    ErpDb.Positional("SELECT `id` FROM `shop_accounting_codes` WHERE `key` = ? LIMIT 1"),
                    cancellationToken,
                    "6_refund_from_balance");
                if (cashCode <= 0)
                {
                    await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return ErpSimpleWriteResult.Fail("invalid", "Forbidden");
                }

                await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional("""
                        INSERT INTO `shop_users_accounting`
                        (`user_id`, `time`, `income`, `amount`, `operation_code`, `active`, `order_id`, `office_id`)
                        VALUES (?, ?, 0, ?, ?, 1, 0, (SELECT `office_id` FROM `shop_orders` WHERE `id` = ? LIMIT 1))
                        """),
                    cancellationToken,
                    orderUser, now, paidSum, cashCode, orderId);
                writes++;
            }

            var logText = directRefund
                ? "Refund <b>" + paidSum.ToString("0.00", CultureInfo.InvariantCulture) + "</b> (cash / direct)"
                : "Refund <b>" + paidSum.ToString("0.00", CultureInfo.InvariantCulture) + "</b> (to balance)";
            try
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`) VALUES (?, ?, ?, 1, ?)"),
                    cancellationToken,
                    orderId, now, adminUserId, logText);
                writes++;
            }
            catch (DbException)
            {
                // log table optional
            }

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ErpSimpleWriteResult(true, "ok", "Refund recorded.", orderId, writes);
        }
        catch
        {
            try
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // already rolled back
            }

            throw;
        }
    }

    public async Task<ErpSimpleWriteResult> RefreshItemCostAsync(
        long orderId,
        long itemId,
        int adminUserId,
        CancellationToken cancellationToken = default)
    {
        if (orderId <= 0 || itemId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid item");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        int storageId;
        string brand;
        string articleShow;
        string article;
        decimal stored;
        decimal sell;
        string jsonParams;
        int productId;
        await using (var select = connection.CreateCommand())
        {
            select.CommandText = ErpDb.Positional("""
                SELECT `t2_storage_id`, `t2_manufacturer`, `t2_article_show`, `t2_article`,
                       `t2_price_purchase`, `price`, `t2_json_params`, `product_id`
                FROM `shop_orders_items` WHERE `id` = ? AND `order_id` = ? LIMIT 1
                """);
            ErpDb.AddParameters(select, itemId, orderId);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return ErpSimpleWriteResult.Fail("not_found", "Item not found");
            }

            storageId = Convert.ToInt32(reader["t2_storage_id"] is DBNull ? 0 : reader["t2_storage_id"], CultureInfo.InvariantCulture);
            brand = Convert.ToString(reader["t2_manufacturer"], CultureInfo.InvariantCulture) ?? "";
            articleShow = Convert.ToString(reader["t2_article_show"], CultureInfo.InvariantCulture) ?? "";
            article = Convert.ToString(reader["t2_article"], CultureInfo.InvariantCulture) ?? "";
            stored = Round4(reader["t2_price_purchase"]);
            sell = Round4(reader["price"]);
            jsonParams = Convert.ToString(reader["t2_json_params"], CultureInfo.InvariantCulture) ?? "";
            productId = Convert.ToInt32(reader["product_id"] is DBNull ? 0 : reader["product_id"], CultureInfo.InvariantCulture);
        }

        var lookupArticle = !string.IsNullOrWhiteSpace(articleShow) ? articleShow : article;
        if (storageId > 0 && !string.IsNullOrWhiteSpace(lookupArticle))
        {
            var offer = await LookupWarehouseOfferAsync(connection, storageId, brand, lookupArticle, cancellationToken)
                .ConfigureAwait(false);
            if (offer is { } warehouse)
            {
                await using var tx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional("UPDATE `shop_orders_items` SET `t2_price_purchase` = ?, `price` = ? WHERE `id` = ? AND `order_id` = ?"),
                    cancellationToken,
                    warehouse.Purchase, warehouse.Sell, itemId, orderId).ConfigureAwait(false);
                var warehouseLog =
                    "OMS refreshed warehouse price for item <b>id " + itemId.ToString(CultureInfo.InvariantCulture)
                    + "</b>: purchase=" + warehouse.Purchase.ToString("0.00", CultureInfo.InvariantCulture)
                    + " sell=" + warehouse.Sell.ToString("0.00", CultureInfo.InvariantCulture)
                    + " (storage " + storageId.ToString(CultureInfo.InvariantCulture) + ")";
                await ErpDb.ExecuteAsync(
                    connection,
                    tx,
                    ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,1,?,0)"),
                    cancellationToken,
                    orderId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), adminUserId, warehouseLog).ConfigureAwait(false);
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new ErpSimpleWriteResult(true, "ok", "Warehouse price refreshed.", itemId, 2);
            }
        }

        var (unit, source) = await EffectivePurchaseAsync(
            connection, itemId, stored, sell, jsonParams, storageId, productId, cancellationToken).ConfigureAwait(false);
        if (unit <= 0)
        {
            return ErpSimpleWriteResult.Fail("no_cost", "No purchase cost found for this line");
        }

        await using var fallbackTx = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            fallbackTx,
            ErpDb.Positional("UPDATE `shop_orders_items` SET `t2_price_purchase` = ? WHERE `id` = ? AND `order_id` = ?"),
            cancellationToken,
            unit, itemId, orderId).ConfigureAwait(false);
        var fallbackLog =
            "OMS refreshed purchase cost for item <b>id " + itemId.ToString(CultureInfo.InvariantCulture)
            + "</b>: " + unit.ToString("0.00", CultureInfo.InvariantCulture)
            + " AED (source " + source + ")";
        await ErpDb.ExecuteAsync(
            connection,
            fallbackTx,
            ErpDb.Positional("INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,1,?,0)"),
            cancellationToken,
            orderId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), adminUserId, fallbackLog).ConfigureAwait(false);
        await fallbackTx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new ErpSimpleWriteResult(true, "ok", "Purchase cost refreshed.", itemId, 2);
    }

    /// <summary>PHP <c>epc_oms_norm_article</c> / <c>docpart_normalize_article_for_price</c> sweep + UPPER.</summary>
    public static string NormArticle(string? article)
    {
        if (string.IsNullOrEmpty(article))
        {
            return "";
        }

        var buffer = new char[article.Length];
        var n = 0;
        foreach (var c in article)
        {
            if (c is ' ' or '-' or '_' or '`' or '/' or '\'' or '"' or '.' or ',' or '#' or '\\' or '\r' or '\n' or '\t')
            {
                continue;
            }

            buffer[n++] = char.ToUpperInvariant(c);
        }

        return n == 0 ? "" : new string(buffer, 0, n);
    }

    private static async Task<(decimal Purchase, decimal Sell)?> LookupWarehouseOfferAsync(
        DbConnection connection,
        int storageId,
        string brand,
        string article,
        CancellationToken cancellationToken)
    {
        int priceId;
        try
        {
            var opts = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `connection_options` FROM `shop_storages` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                storageId).ConfigureAwait(false);
            priceId = ParsePriceId(opts);
        }
        catch (DbException)
        {
            return null;
        }

        var artNorm = NormArticle(article);
        if (priceId <= 0 || artNorm.Length == 0)
        {
            return null;
        }

        var brandNorm = brand.Trim().ToUpperInvariant();
        try
        {
            var purchase = await ErpDb.DecimalAsync(
                connection,
                null,
                ErpDb.Positional("""
                    SELECT `price`
                    FROM `shop_docpart_prices_data`
                    WHERE `price_id` = ?
                      AND (
                            REPLACE(REPLACE(REPLACE(UPPER(`article`), '-', ''), ' ', ''), '.', '') = ?
                         OR REPLACE(REPLACE(REPLACE(UPPER(`article_show`), '-', ''), ' ', ''), '.', '') = ?
                      )
                    ORDER BY
                        CASE WHEN UPPER(TRIM(`manufacturer`)) = ? THEN 0 ELSE 1 END,
                        `price` ASC
                    LIMIT 1
                    """),
                cancellationToken,
                priceId, artNorm, artNorm, brandNorm).ConfigureAwait(false);
            if (purchase <= 0)
            {
                return null;
            }

            var rounded = Math.Round(purchase, 2, MidpointRounding.AwayFromZero);
            // PHP sell uses epc_pricing_apply_sell_from_purchase when present; otherwise sell = purchase.
            return (rounded, rounded);
        }
        catch (DbException)
        {
            return null;
        }
    }

    private static async Task<(decimal Unit, string Source)> EffectivePurchaseAsync(
        DbConnection connection,
        long itemId,
        decimal stored,
        decimal sell,
        string jsonParams,
        int storageId,
        int productId,
        CancellationToken cancellationToken)
    {
        var unit = stored;
        var source = "t2_price_purchase";

        try
        {
            var avg = Math.Round(
                await ErpDb.DecimalAsync(
                    connection,
                    null,
                    ErpDb.Positional("""
                        SELECT IFNULL(SUM(`price_purchase` * GREATEST(`count_reserved` + `count_issued`, 1)) /
                            NULLIF(SUM(GREATEST(`count_reserved` + `count_issued`, 1)), 0), 0)
                        FROM `shop_orders_items_details` WHERE `order_item_id` = ? AND `price_purchase` > 0
                        """),
                    cancellationToken,
                    itemId).ConfigureAwait(false),
                4, MidpointRounding.AwayFromZero);
            if (avg > 0 && (stored <= 0 || NearlyEqual(stored, sell) || avg < stored - 0.0001m))
            {
                unit = avg;
                source = "order_item_details";
            }
        }
        catch (DbException)
        {
            // details table optional
        }

        var apaiCost = ReadApaiCost(jsonParams);
        if (apaiCost > 0 && (unit <= 0 || NearlyEqual(unit, sell) || apaiCost < unit - 0.0001m))
        {
            unit = apaiCost;
            source = "apai_cost";
        }

        if (unit <= 0 || NearlyEqual(unit, sell))
        {
            try
            {
                if (productId > 0)
                {
                    var sql = storageId > 0
                        ? "SELECT MAX(`price_purchase`) FROM `shop_storages_data` WHERE `product_id` = ? AND `price_purchase` > 0 AND `storage_id` = ?"
                        : "SELECT MAX(`price_purchase`) FROM `shop_storages_data` WHERE `product_id` = ? AND `price_purchase` > 0";
                    var wh = Math.Round(
                        storageId > 0
                            ? await ErpDb.DecimalAsync(connection, null, ErpDb.Positional(sql), cancellationToken, productId, storageId).ConfigureAwait(false)
                            : await ErpDb.DecimalAsync(connection, null, ErpDb.Positional(sql), cancellationToken, productId).ConfigureAwait(false),
                        4, MidpointRounding.AwayFromZero);
                    if (wh > 0)
                    {
                        unit = wh;
                        source = "shop_storages_data";
                    }
                }
            }
            catch (DbException)
            {
                // storages_data optional
            }
        }

        if (unit <= 0)
        {
            unit = stored > 0 ? stored : 0m;
            source = "t2_price_purchase";
        }

        return (Math.Round(unit, 4, MidpointRounding.AwayFromZero), source);
    }

    private static int ParsePriceId(string? connectionOptions)
    {
        if (string.IsNullOrWhiteSpace(connectionOptions))
        {
            return 0;
        }

        try
        {
            using var doc = JsonDocument.Parse(connectionOptions);
            if (!doc.RootElement.TryGetProperty("price_id", out var prop))
            {
                return 0;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var n))
            {
                return n;
            }

            return int.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static decimal ReadApaiCost(string jsonParams)
    {
        if (string.IsNullOrWhiteSpace(jsonParams))
        {
            return 0;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonParams);
            foreach (var key in new[] { "apai_cost", "import_warehouse_cost" })
            {
                if (!doc.RootElement.TryGetProperty(key, out var prop))
                {
                    continue;
                }

                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var n) && n > 0)
                {
                    return Math.Round(n, 4, MidpointRounding.AwayFromZero);
                }

                if (prop.ValueKind == JsonValueKind.String
                    && decimal.TryParse(prop.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                    && parsed > 0)
                {
                    return Math.Round(parsed, 4, MidpointRounding.AwayFromZero);
                }
            }
        }
        catch (JsonException)
        {
            // ignore malformed t2_json_params
        }

        return 0;
    }

    private static decimal Round4(object value)
        => Math.Round(value is DBNull ? 0m : Convert.ToDecimal(value, CultureInfo.InvariantCulture), 4, MidpointRounding.AwayFromZero);

    private static bool NearlyEqual(decimal a, decimal b) => Math.Abs(a - b) < 0.0001m;

    private static async Task<string> StorageCaptionAsync(
        DbConnection connection,
        int storageId,
        CancellationToken cancellationToken)
    {
        if (storageId <= 0)
        {
            return "";
        }

        try
        {
            return await ErpDb.StringAsync(
                       connection,
                       null,
                       ErpDb.Positional("SELECT COALESCE(NULLIF(TRIM(`short_name`), ''), `name`) FROM `shop_storages` WHERE `id` = ? LIMIT 1"),
                       cancellationToken,
                       storageId)
                   ?? "";
        }
        catch (DbException)
        {
            return "";
        }
    }

    private static string SanitizeOmsText(string? value)
        => (value ?? string.Empty)
            .Replace("\"", "", StringComparison.Ordinal)
            .Replace("\\", "", StringComparison.Ordinal)
            .Replace("'", "", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal)
            .Replace("\t", "", StringComparison.Ordinal)
            .Trim();

    private static Task EnsureFulfillmentSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
        => ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `epc_order_supplier_fulfillment` (
                `id` int(11) NOT NULL AUTO_INCREMENT,
                `order_id` int(11) NOT NULL,
                `supplier_key` varchar(64) NOT NULL DEFAULT '',
                `supplier_id` int(11) NOT NULL DEFAULT 0,
                `storage_id` int(11) NOT NULL DEFAULT 0,
                `supplier_name` varchar(255) NOT NULL DEFAULT '',
                `stage` varchar(48) NOT NULL DEFAULT 'supplier_confirm',
                `po_id` int(11) NOT NULL DEFAULT 0,
                `notes` varchar(512) NOT NULL DEFAULT '',
                `time_updated` int(11) NOT NULL DEFAULT 0,
                `updated_by` int(11) NOT NULL DEFAULT 0,
                PRIMARY KEY (`id`),
                UNIQUE KEY `u_order_supplier` (`order_id`, `supplier_key`),
                KEY `x_order` (`order_id`),
                KEY `x_stage` (`stage`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8
            """,
            cancellationToken);

    private static void Add(DbCommand command, string name, object? value)
    {
        var p = command.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        command.Parameters.Add(p);
    }
}
