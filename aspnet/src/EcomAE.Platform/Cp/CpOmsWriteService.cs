using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>ajax_epc_orders_oms.php</c> action <c>set_item_status</c>.</summary>
public interface ICpOmsWriteService
{
    Task<ErpSimpleWriteResult> SetItemStatusAsync(long orderId, long itemId, int status, int adminUserId, CancellationToken cancellationToken = default);
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
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        if (orderId <= 0 || itemId <= 0 || status <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid item status.");
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

    private static void Add(DbCommand command, string name, object? value)
    {
        var p = command.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        command.Parameters.Add(p);
    }
}
