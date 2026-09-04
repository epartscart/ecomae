using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>ajax_epc_orders_oms.php</c> / <c>ajax_delete_orders.php</c> twins.</summary>
public interface ICpOmsWriteService
{
    Task<ErpSimpleWriteResult> SetItemStatusAsync(long orderId, long itemId, int status, int adminUserId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetItemsStatusAsync(long orderId, int status, IReadOnlyList<long> itemIds, int adminUserId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SendMessageAsync(long orderId, string text, long itemId, int adminUserId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SetCourierAsync(long orderId, decimal deliveryPrice, string? country, int adminUserId, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteUnpaidOrdersAsync(IReadOnlyList<long> orderIds, CancellationToken cancellationToken = default);
}

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

    private static void Add(DbCommand command, string name, object? value)
    {
        var p = command.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        command.Parameters.Add(p);
    }
}
