using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// Live PHP twins for cart qty / delete / check-for-order (type-2 warehouse lines).
/// </summary>
public interface IStorefrontCartWriteService
{
    Task<StorefrontCartWriteResult> ChangeCountNeedAsync(int userId, long cartId, decimal countNeed, CancellationToken cancellationToken = default);
    Task<StorefrontCartWriteResult> DeleteAsync(int userId, IReadOnlyList<long> cartIds, CancellationToken cancellationToken = default);
    Task<StorefrontCartWriteResult> CheckForOrderAsync(int userId, long cartId, bool checkedForOrder, CancellationToken cancellationToken = default);
}

public sealed record StorefrontCartWriteResult(
    bool Ok,
    string Status,
    string Code,
    string Message,
    int Writes)
{
    public object ToPayload(object session) => new
    {
        ok = Ok,
        status = Ok,
        surface = "storefront",
        status_token = Status,
        writes = Writes,
        writesBlocked = false,
        cutoverAllowed = true,
        phpAuthoritative = false,
        validation_code = Code,
        would_write = Ok && Writes > 0,
        message = Message,
        note = Message,
        session
    };
}

public sealed class StorefrontCartWriteService : IStorefrontCartWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public StorefrontCartWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<StorefrontCartWriteResult> ChangeCountNeedAsync(
        int userId,
        long cartId,
        decimal countNeed,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Fail("auth", "Please log in or register to continue.");
        }

        if (!_connections.IsConfigured)
        {
            return Fail("db", "Cart database is not configured.");
        }

        if (cartId <= 0 || countNeed <= 0)
        {
            return Fail("invalid", "Cart line and a positive quantity are required.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var select = connection.CreateCommand();
        select.CommandText = """
            SELECT `id`, `product_type`, `count_need`, IFNULL(`t2_exist`,0) AS t2_exist, IFNULL(`t2_min_order`,1) AS t2_min_order
            FROM `shop_carts`
            WHERE `id` = @id AND `user_id` = @userId AND `session_id` = 0
            LIMIT 1
            """;
        Add(select, "@id", cartId);
        Add(select, "@userId", userId);
        await using var reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return Fail("not_found", "Cart line not found.");
        }

        var productType = Convert.ToInt32(reader["product_type"], CultureInfo.InvariantCulture);
        var exist = Convert.ToDecimal(reader["t2_exist"] is DBNull ? 0 : reader["t2_exist"], CultureInfo.InvariantCulture);
        var minOrder = Convert.ToDecimal(reader["t2_min_order"] is DBNull ? 1 : reader["t2_min_order"], CultureInfo.InvariantCulture);
        if (minOrder <= 0)
        {
            minOrder = 1;
        }

        await reader.DisposeAsync();

        if (productType != 2)
        {
            return Fail("unsupported", "Only warehouse cart lines can be updated here.");
        }

        var qty = countNeed;
        if (exist > 0 && qty > exist)
        {
            return Fail("not_enough", "Requested quantity exceeds available stock.");
        }

        if (qty < minOrder)
        {
            qty = minOrder;
        }

        await using var update = connection.CreateCommand();
        update.CommandText = "UPDATE `shop_carts` SET `count_need` = @qty WHERE `id` = @id AND `user_id` = @userId AND `session_id` = 0 AND `product_type` = 2";
        Add(update, "@qty", qty);
        Add(update, "@id", cartId);
        Add(update, "@userId", userId);
        var rows = await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (rows <= 0)
        {
            return Fail("update_failed", "Could not update quantity.");
        }

        return new(true, "written", "ok", "Quantity updated.", 1);
    }

    public async Task<StorefrontCartWriteResult> DeleteAsync(
        int userId,
        IReadOnlyList<long> cartIds,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Fail("auth", "Please log in or register to continue.");
        }

        if (!_connections.IsConfigured)
        {
            return Fail("db", "Cart database is not configured.");
        }

        var ids = (cartIds ?? []).Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return Fail("invalid", "Select a cart line to delete.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var writes = 0;
        foreach (var id in ids)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM `shop_carts` WHERE `id` = @id AND `user_id` = @userId AND `session_id` = 0 AND `product_type` = 2";
            Add(cmd, "@id", id);
            Add(cmd, "@userId", userId);
            writes += await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (writes <= 0)
        {
            return Fail("not_found", "Cart line not found or is not a warehouse line.");
        }

        return new(true, "written", "ok", "Removed from cart.", writes);
    }

    public async Task<StorefrontCartWriteResult> CheckForOrderAsync(
        int userId,
        long cartId,
        bool checkedForOrder,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
        {
            return Fail("auth", "Please log in or register to continue.");
        }

        if (!_connections.IsConfigured)
        {
            return Fail("db", "Cart database is not configured.");
        }

        if (cartId <= 0)
        {
            return Fail("invalid", "Cart line is required.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE `shop_carts` SET `checked_for_order` = @checked WHERE `id` = @id AND `user_id` = @userId AND `session_id` = 0";
        Add(cmd, "@checked", checkedForOrder ? 1 : 0);
        Add(cmd, "@id", cartId);
        Add(cmd, "@userId", userId);
        var rows = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (rows <= 0)
        {
            return Fail("not_found", "Cart line not found.");
        }

        return new(true, "written", "ok", checkedForOrder ? "Checked for order." : "Unchecked.", 1);
    }

    private static StorefrontCartWriteResult Fail(string code, string message)
        => new(false, "error", code, message, 0);

    private static void Add(DbCommand command, string name, object? value)
    {
        var p = command.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        command.Parameters.Add(p);
    }
}
