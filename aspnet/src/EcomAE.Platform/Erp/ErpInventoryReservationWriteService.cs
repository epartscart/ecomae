using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// First-click available-to-promise hold used when many sales users search the same item.
/// Soft hold (quote / draft order) lasts <see cref="HoldSeconds"/>; confirm removes the
/// expiry; invoice consumes; cancel / delete / timeout releases. ATP = on hand − active holds.
/// Serialized with a per-item MySQL named lock so the first click wins.
/// </summary>
public interface IErpInventoryReservationWriteService
{
    Task<ErpReservationResult> ReserveAsync(ErpReserveInput input, CancellationToken cancellationToken = default);

    Task<ErpReservationResult> ReserveAsync(
        DbConnection connection,
        DbTransaction? transaction,
        ErpReserveInput input,
        CancellationToken cancellationToken = default);

    Task<ErpReservationResult> ReleaseAsync(long reservationId, int adminId, CancellationToken cancellationToken = default);

    Task ReleaseBySourceAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sourceType,
        long sourceId,
        int adminId,
        CancellationToken cancellationToken = default);

    Task ConfirmBySourceAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sourceType,
        long sourceId,
        int adminId,
        CancellationToken cancellationToken = default,
        int ownerCompanyId = 0);

    Task ConsumeBySourceAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sourceType,
        long sourceId,
        int adminId,
        CancellationToken cancellationToken = default,
        int ownerCompanyId = 0);

    Task ReconcileDocumentAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sourceType,
        long sourceId,
        IReadOnlyList<ErpReservationLineNeed> lines,
        int adminId,
        bool confirm,
        CancellationToken cancellationToken = default,
        int ownerCompanyId = 0);

    Task<ErpItemAtp> AtpAsync(long itemId, string? sku, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ErpItemAtp>> ListAtpAsync(IReadOnlyList<long> itemIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ErpReservationHold>> ListActiveHoldsAsync(int limit = 80, CancellationToken cancellationToken = default);
}

public sealed record ErpReserveInput
{
    public long ItemId { get; init; }

    public string Sku { get; init; } = string.Empty;

    public decimal Qty { get; init; } = 1m;

    public string SourceType { get; init; } = "sales_order";

    public long SourceId { get; init; }

    public string SourceLineKey { get; init; } = string.Empty;

    public int AdminId { get; init; }

    /// <summary>Order confirmation. The row stays until fulfilment, cancel, or an authorized release.</summary>
    public bool Durable { get; init; }

    public int WarehouseId { get; init; }

    public int OwnerCompanyId { get; init; }

    public bool AllowPartial { get; init; }

    public string SellingCompanyId { get; init; } = string.Empty;

    public string OwnerCompanyKey { get; init; } = string.Empty;

    public string Fulfilment { get; init; } = string.Empty;

    public string StockKind { get; init; } = ErpAsapStock.StockOwned;
}

public sealed record ErpReservationLineNeed(string ItemCode, decimal Qty);

public sealed record ErpReservationResult(
    bool Succeeded,
    string Code,
    string Message,
    long ReservationId,
    long ItemId,
    string Sku,
    decimal Qty,
    decimal OnHand,
    decimal Reserved,
    decimal Available,
    string Status,
    long ExpiresAt)
{
    public decimal BackorderQty { get; init; }

    public static ErpReservationResult Fail(string code, string message, ErpItemAtp? atp = null)
        => new(
            false,
            code,
            message,
            0,
            atp?.ItemId ?? 0,
            atp?.Sku ?? string.Empty,
            0,
            atp?.OnHand ?? 0,
            atp?.Reserved ?? 0,
            atp?.Available ?? 0,
            string.Empty,
            0);
}

public sealed record ErpItemAtp(
    long ItemId,
    string Sku,
    string Name,
    decimal OnHand,
    decimal Reserved,
    decimal Available);

public sealed record ErpReservationHold(
    long Id,
    long ItemId,
    string Sku,
    decimal Qty,
    string SourceType,
    long SourceId,
    string Status,
    int ReservedBy,
    long ReservedAt,
    long ExpiresAt);

public sealed class ErpInventoryReservationWriteService : IErpInventoryReservationWriteService
{
    public const int HoldSeconds = 30 * 60;
    public const int LockWaitSeconds = 8;
    public const string SourceQuote = "quote";
    public const string SourceSalesOrder = "sales_order";
    public const string StatusHeld = "held";
    public const string StatusConfirmed = "confirmed";
    public const string StatusConsumed = "consumed";
    public const string StatusReleased = "released";
    public const string StatusExpired = "expired";

    private static readonly HashSet<string> Sources = new(StringComparer.Ordinal)
    {
        SourceQuote, SourceSalesOrder,
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpInventoryReservationWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static decimal Available(decimal onHand, decimal reserved)
        => decimal.Max(0m, decimal.Round(onHand, 3, MidpointRounding.AwayFromZero) - decimal.Round(reserved, 3, MidpointRounding.AwayFromZero));

    public static bool CountsAgainstAtp(string? status, long expiresAt, long now)
    {
        if (!IsOpenStatus(status))
        {
            return false;
        }

        return expiresAt == 0 || expiresAt > now;
    }

    public static bool IsOpenStatus(string? status)
        => string.Equals(status, StatusHeld, StringComparison.Ordinal)
            || string.Equals(status, StatusConfirmed, StringComparison.Ordinal);

    public static string NormalizeSource(string? sourceType)
    {
        var raw = (sourceType ?? string.Empty).Trim();
        return Sources.Contains(raw) ? raw : SourceSalesOrder;
    }

    public static string LockName(string? schema, long itemId)
    {
        var prefix = (schema ?? string.Empty).Trim();
        if (prefix.Length > 28)
        {
            prefix = prefix[..28];
        }

        var name = prefix + ":erp_inv_rsv_" + itemId.ToString(CultureInfo.InvariantCulture);
        return name.Length <= 64 ? name : name[..64];
    }

    public static string FirstClickLostMessage(string sku, decimal available)
        => "Another sales user already reserved "
            + (string.IsNullOrWhiteSpace(sku) ? "this item" : sku)
            + ". Available now: "
            + available.ToString("0.###", CultureInfo.InvariantCulture)
            + ".";

    public async Task<ErpReservationResult> ReserveAsync(ErpReserveInput input, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpReservationResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await ReserveAsync(connection, transaction, input, cancellationToken).ConfigureAwait(false);
            if (result.Succeeded)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ErpReservationResult> ReserveAsync(
        DbConnection connection,
        DbTransaction? transaction,
        ErpReserveInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(input);

        var qty = decimal.Round(input.Qty, 3, MidpointRounding.AwayFromZero);
        if (qty <= 0m)
        {
            return ErpReservationResult.Fail("invalid", "Quantity must be greater than zero.");
        }

        var item = await ResolveItemAsync(connection, transaction, input.ItemId, input.Sku, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return ErpReservationResult.Fail("not_found", "Inventory item not found.");
        }

        var sourceType = NormalizeSource(input.SourceType);
        var lineKey = Clip(input.SourceLineKey, 64);
        var lockName = await ItemLockNameAsync(connection, transaction, item.Value.Id, cancellationToken).ConfigureAwait(false);
        var locked = await TryLockAsync(connection, transaction, lockName, cancellationToken).ConfigureAwait(false);
        if (!locked)
        {
            return ErpReservationResult.Fail("busy", "This item is being reserved by another request — retry.");
        }

        try
        {
            await ExpireDueAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(input.Fulfilment)
                && !ErpAsapStock.MayReserve(input.SellingCompanyId, input.OwnerCompanyKey, input.Fulfilment, input.StockKind))
            {
                return ErpReservationResult.Fail("ownership", "This company cannot reserve that owner's stock.");
            }

            var existing = await FindOpenHoldAsync(
                connection,
                transaction,
                item.Value.Id,
                sourceType,
                input.SourceId,
                lineKey,
                input.AdminId,
                input.OwnerCompanyId,
                cancellationToken).ConfigureAwait(false);
            var already = existing?.Qty ?? 0m;
            var atp = await LoadAtpAsync(connection, transaction, item.Value.Id, input.WarehouseId, input.OwnerCompanyId, cancellationToken).ConfigureAwait(false);
            var others = decimal.Max(0m, atp.Reserved - already);
            var decision = ErpAsapStock.Decide(atp.OnHand, others, qty, already, input.AllowPartial);
            if (!decision.Accepted)
            {
                return ErpReservationResult.Fail("unavailable", FirstClickLostMessage(item.Value.Sku, decision.AvailableAfter), atp);
            }

            if (decision.AddedQty == 0m && existing is not null)
            {
                return new ErpReservationResult(
                    true,
                    "ok",
                    "Item already reserved for you.",
                    existing.Value.Id,
                    item.Value.Id,
                    item.Value.Sku,
                    existing.Value.Qty,
                    atp.OnHand,
                    atp.Reserved,
                    atp.Available,
                    existing.Value.Status,
                    existing.Value.ExpiresAt);
            }

            var storeQty = decision.ReservedQty;
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var durable = input.Durable || string.Equals(existing?.Status, StatusConfirmed, StringComparison.Ordinal);
            var expires = durable ? 0L : now + HoldSeconds;
            long reservationId;
            string status;
            if (existing is not null)
            {
                status = durable ? StatusConfirmed : existing.Value.Status;
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "UPDATE `epc_erp_inv_reservations` SET `qty`=?, `backorder_qty`=?, `warehouse_id`=?, `owner_company_id`=?, `status`=?, `time_updated`=?, `expires_at`=? WHERE `id`=?"),
                    cancellationToken,
                    storeQty,
                    decision.BackorderQty,
                    input.WarehouseId,
                    input.OwnerCompanyId,
                    status,
                    now,
                    expires,
                    existing.Value.Id).ConfigureAwait(false);
                reservationId = existing.Value.Id;
            }
            else
            {
                status = durable ? StatusConfirmed : StatusHeld;
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_erp_inv_reservations` (`item_id`,`sku`,`warehouse_id`,`owner_company_id`,`qty`,`backorder_qty`,`source_type`,`source_id`,`source_line_key`,`status`,`reserved_by`,`reserved_at`,`expires_at`,`time_updated`)"
                        + " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
                    cancellationToken,
                    item.Value.Id,
                    Clip(item.Value.Sku, 100),
                    input.WarehouseId,
                    input.OwnerCompanyId,
                    storeQty,
                    decision.BackorderQty,
                    sourceType,
                    input.SourceId,
                    lineKey,
                    status,
                    input.AdminId,
                    now,
                    expires,
                    now).ConfigureAwait(false);
                reservationId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            }

            var after = await LoadAtpAsync(connection, transaction, item.Value.Id, input.WarehouseId, input.OwnerCompanyId, cancellationToken).ConfigureAwait(false);
            var message = durable
                ? "Reserved. This stays until the order is picked, dispatched, cancelled, or released."
                : decision.Code == "partial"
                    ? "Reserved " + storeQty.ToString("0.###", CultureInfo.InvariantCulture) + ". Backorder " + decision.BackorderQty.ToString("0.###", CultureInfo.InvariantCulture) + "."
                    : "Reserved for you. Confirm the quote or order to keep it, or it releases in 30 minutes.";
            return new ErpReservationResult(
                true,
                decision.Code == "partial" ? "partial" : "ok",
                message,
                reservationId,
                item.Value.Id,
                item.Value.Sku,
                storeQty,
                after.OnHand,
                after.Reserved,
                after.Available,
                status,
                expires)
            {
                BackorderQty = decision.BackorderQty,
            };
        }
        finally
        {
            await ReleaseLockAsync(connection, transaction, lockName).ConfigureAwait(false);
        }
    }

    public async Task<ErpReservationResult> ReleaseAsync(long reservationId, int adminId, CancellationToken cancellationToken = default)
    {
        if (reservationId <= 0)
        {
            return ErpReservationResult.Fail("invalid", "Reservation is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpReservationResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var affected = await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_erp_inv_reservations` SET `status`=?, `released_at`=?, `time_updated`=? WHERE `id`=? AND `status` IN ('held','confirmed')"),
            cancellationToken,
            StatusReleased,
            now,
            now,
            reservationId).ConfigureAwait(false);
        return affected > 0
            ? new ErpReservationResult(true, "ok", "Reservation released.", reservationId, 0, string.Empty, 0, 0, 0, 0, StatusReleased, 0)
            : ErpReservationResult.Fail("not_found", "Reservation is not open.");
    }

    public async Task ReleaseBySourceAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sourceType,
        long sourceId,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        if (sourceId <= 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional(
                "UPDATE `epc_erp_inv_reservations` SET `status`=?, `released_at`=?, `time_updated`=? WHERE `source_type`=? AND `source_id`=? AND `status` IN ('held','confirmed')"),
            cancellationToken,
            StatusReleased,
            now,
            now,
            NormalizeSource(sourceType),
            sourceId).ConfigureAwait(false);
    }

    public async Task ConfirmBySourceAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sourceType,
        long sourceId,
        int adminId,
        CancellationToken cancellationToken = default,
        int ownerCompanyId = 0)
    {
        if (sourceId <= 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var ownerClause = ownerCompanyId > 0 ? " AND `owner_company_id`=?" : string.Empty;
        await using var confirmCmd = connection.CreateCommand();
        confirmCmd.Transaction = transaction;
        confirmCmd.CommandText = ErpDb.Positional(
            "UPDATE `epc_erp_inv_reservations` SET `status`=?, `expires_at`=0, `time_updated`=? WHERE `source_type`=? AND `source_id`=? AND `status`='held'" + ownerClause);
        if (ownerCompanyId > 0)
        {
            ErpDb.AddParameters(confirmCmd, StatusConfirmed, now, NormalizeSource(sourceType), sourceId, ownerCompanyId);
        }
        else
        {
            ErpDb.AddParameters(confirmCmd, StatusConfirmed, now, NormalizeSource(sourceType), sourceId);
        }

        await confirmCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ConsumeBySourceAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sourceType,
        long sourceId,
        int adminId,
        CancellationToken cancellationToken = default,
        int ownerCompanyId = 0)
    {
        if (sourceId <= 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var ownerClause = ownerCompanyId > 0 ? " AND `owner_company_id`=?" : string.Empty;
        await using var consumeCmd = connection.CreateCommand();
        consumeCmd.Transaction = transaction;
        consumeCmd.CommandText = ErpDb.Positional(
            "UPDATE `epc_erp_inv_reservations` SET `status`=?, `consumed_at`=?, `expires_at`=0, `time_updated`=? WHERE `source_type`=? AND `source_id`=? AND `status` IN ('held','confirmed')" + ownerClause);
        if (ownerCompanyId > 0)
        {
            ErpDb.AddParameters(consumeCmd, StatusConsumed, now, now, NormalizeSource(sourceType), sourceId, ownerCompanyId);
        }
        else
        {
            ErpDb.AddParameters(consumeCmd, StatusConsumed, now, now, NormalizeSource(sourceType), sourceId);
        }

        await consumeCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReconcileDocumentAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sourceType,
        long sourceId,
        IReadOnlyList<ErpReservationLineNeed> lines,
        int adminId,
        bool confirm,
        CancellationToken cancellationToken = default,
        int ownerCompanyId = 0)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (sourceId <= 0)
        {
            return;
        }

        await ExpireDueAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

        var needed = new Dictionary<long, (string Sku, decimal Qty)>();
        foreach (var line in lines ?? [])
        {
            var code = (line.ItemCode ?? string.Empty).Trim();
            if (code.Length == 0)
            {
                continue;
            }

            var item = await ResolveItemAsync(connection, transaction, 0, code, cancellationToken).ConfigureAwait(false);
            if (item is null)
            {
                continue;
            }

            if (needed.TryGetValue(item.Value.Id, out var existingNeed))
            {
                needed[item.Value.Id] = (item.Value.Sku, existingNeed.Qty + line.Qty);
            }
            else
            {
                needed[item.Value.Id] = (item.Value.Sku, line.Qty);
            }
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var source = NormalizeSource(sourceType);

        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional(
                "UPDATE `epc_erp_inv_reservations` SET `source_id`=?, `time_updated`=? WHERE `source_type`=? AND `source_id`=0 AND `reserved_by`=? AND `status` IN ('held','confirmed')"),
            cancellationToken,
            sourceId,
            now,
            source,
            adminId).ConfigureAwait(false);

        foreach (var (itemId, need) in needed)
        {
            var result = await ReserveAsync(
                connection,
                transaction,
                new ErpReserveInput
                {
                    ItemId = itemId,
                    Sku = need.Sku,
                    Qty = need.Qty,
                    SourceType = source,
                    SourceId = sourceId,
                    SourceLineKey = "doc",
                    AdminId = adminId,
                    Durable = confirm,
                    OwnerCompanyId = ownerCompanyId,
                },
                cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw new ErpWriteException(result.Message);
            }
        }

        await using var list = connection.CreateCommand();
        list.Transaction = transaction;
        var extrasOwnerClause = ownerCompanyId > 0 ? " AND `owner_company_id`=?" : string.Empty;
        list.CommandText = ErpDb.Positional(
            "SELECT `id`, `item_id` FROM `epc_erp_inv_reservations` WHERE `source_type`=? AND `source_id`=? AND `status` IN ('held','confirmed')" + extrasOwnerClause);
        if (ownerCompanyId > 0)
        {
            ErpDb.AddParameters(list, source, sourceId, ownerCompanyId);
        }
        else
        {
            ErpDb.AddParameters(list, source, sourceId);
        }
        var extras = new List<long>();
        await using (var reader = await list.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var itemId = reader.GetInt64(1);
                if (!needed.ContainsKey(itemId))
                {
                    extras.Add(reader.GetInt64(0));
                }
            }
        }

        foreach (var extraId in extras)
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "UPDATE `epc_erp_inv_reservations` SET `status`=?, `released_at`=?, `time_updated`=? WHERE `id`=?"),
                cancellationToken,
                StatusReleased,
                now,
                now,
                extraId).ConfigureAwait(false);
        }

        if (confirm)
        {
            await ConfirmBySourceAsync(connection, transaction, source, sourceId, adminId, cancellationToken, ownerCompanyId).ConfigureAwait(false);
        }
    }

    public async Task<ErpItemAtp> AtpAsync(long itemId, string? sku, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new ErpItemAtp(0, sku ?? string.Empty, string.Empty, 0, 0, 0);
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ExpireDueAsync(connection, null, cancellationToken).ConfigureAwait(false);
        var item = await ResolveItemAsync(connection, null, itemId, sku, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return new ErpItemAtp(0, sku ?? string.Empty, string.Empty, 0, 0, 0);
        }

        return await LoadAtpAsync(connection, null, item.Value.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ErpItemAtp>> ListAtpAsync(IReadOnlyList<long> itemIds, CancellationToken cancellationToken = default)
    {
        if (itemIds.Count == 0 || !_connections.IsConfigured)
        {
            return [];
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ExpireDueAsync(connection, null, cancellationToken).ConfigureAwait(false);
        var list = new List<ErpItemAtp>();
        foreach (var id in itemIds.Where(v => v > 0).Distinct())
        {
            list.Add(await LoadAtpAsync(connection, null, id, cancellationToken).ConfigureAwait(false));
        }

        return list;
    }

    public async Task<IReadOnlyList<ErpReservationHold>> ListActiveHoldsAsync(int limit = 80, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ExpireDueAsync(connection, null, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var take = limit < 1 ? 80 : Math.Min(limit, 200);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `id`,`item_id`,IFNULL(`sku`,''),`qty`,`source_type`,`source_id`,`status`,`reserved_by`,`reserved_at`,`expires_at`"
            + " FROM `epc_erp_inv_reservations` WHERE `status` IN ('held','confirmed') AND (`expires_at`=0 OR `expires_at`>?)"
            + " ORDER BY `reserved_at` DESC, `id` DESC LIMIT "
            + take.ToString(CultureInfo.InvariantCulture));
        ErpDb.AddParameters(command, now);
        var rows = new List<ErpReservationHold>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ErpReservationHold(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetString(4),
                reader.GetInt64(5),
                reader.GetString(6),
                reader.GetInt32(7),
                reader.GetInt64(8),
                reader.GetInt64(9)));
        }

        return rows;
    }

    internal static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_inv_reservations` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `item_id` int(11) NOT NULL DEFAULT 0,"
            + " `sku` varchar(100) NOT NULL DEFAULT '',"
            + " `warehouse_id` int(11) NOT NULL DEFAULT 0,"
            + " `owner_company_id` int(11) NOT NULL DEFAULT 0,"
            + " `qty` decimal(14,3) NOT NULL DEFAULT 0.000,"
            + " `backorder_qty` decimal(14,3) NOT NULL DEFAULT 0.000,"
            + " `source_type` varchar(24) NOT NULL DEFAULT '',"
            + " `source_id` int(11) NOT NULL DEFAULT 0,"
            + " `source_line_key` varchar(64) NOT NULL DEFAULT '',"
            + " `status` varchar(16) NOT NULL DEFAULT 'held',"
            + " `reserved_by` int(11) NOT NULL DEFAULT 0,"
            + " `reserved_at` int(11) NOT NULL DEFAULT 0,"
            + " `expires_at` int(11) NOT NULL DEFAULT 0,"
            + " `released_at` int(11) NOT NULL DEFAULT 0,"
            + " `consumed_at` int(11) NOT NULL DEFAULT 0,"
            + " `time_updated` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`),"
            + " KEY `x_item_status` (`item_id`,`status`),"
            + " KEY `x_source` (`source_type`,`source_id`),"
            + " KEY `x_expires` (`status`,`expires_at`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='First-click ATP holds for quotes and sales orders'",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_erp_inv_reservations` ADD COLUMN `owner_company_id` int(11) NOT NULL DEFAULT 0",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_erp_inv_reservations` ADD COLUMN `backorder_qty` decimal(14,3) NOT NULL DEFAULT 0.000",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_erp_inv_stock` ADD COLUMN `owner_company_id` int(11) NOT NULL DEFAULT 0",
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExpireDueAsync(DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional(
                "UPDATE `epc_erp_inv_reservations` SET `status`=?, `released_at`=?, `time_updated`=? WHERE `status`='held' AND `expires_at`>0 AND `expires_at`<=?"),
            cancellationToken,
            StatusExpired,
            now,
            now,
            now).ConfigureAwait(false);
    }

    private static async Task<(long Id, string Sku, string Name)?> ResolveItemAsync(
        DbConnection connection,
        DbTransaction? transaction,
        long itemId,
        string? sku,
        CancellationToken cancellationToken)
    {
        var code = (sku ?? string.Empty).Trim();
        if (itemId <= 0 && code.Length == 0)
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        if (itemId > 0)
        {
            command.CommandText = ErpDb.Positional(
                "SELECT `id`, IFNULL(`sku`,''), IFNULL(`name`,'') FROM `epc_erp_inv_items` WHERE `id`=? AND `active`=1 LIMIT 1");
            ErpDb.AddParameters(command, itemId);
        }
        else
        {
            command.CommandText = ErpDb.Positional(
                "SELECT `id`, IFNULL(`sku`,''), IFNULL(`name`,'') FROM `epc_erp_inv_items` WHERE `sku`=? AND `active`=1 LIMIT 1");
            ErpDb.AddParameters(command, Clip(code, 64));
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return (reader.GetInt64(0), reader.GetString(1), reader.GetString(2));
    }

    private static Task<ErpItemAtp> LoadAtpAsync(
        DbConnection connection,
        DbTransaction? transaction,
        long itemId,
        CancellationToken cancellationToken)
        => LoadAtpAsync(connection, transaction, itemId, 0, 0, cancellationToken);

    private static async Task<ErpItemAtp> LoadAtpAsync(
        DbConnection connection,
        DbTransaction? transaction,
        long itemId,
        int warehouseId,
        int ownerCompanyId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sku = await ErpDb.StringAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT IFNULL(`sku`,'') FROM `epc_erp_inv_items` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            itemId).ConfigureAwait(false) ?? string.Empty;
        var name = await ErpDb.StringAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT IFNULL(`name`,'') FROM `epc_erp_inv_items` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            itemId).ConfigureAwait(false) ?? string.Empty;
        var onHand = await ErpDb.DecimalAsync(
            connection,
            transaction,
            ErpDb.Positional(
                "SELECT COALESCE(SUM(`qty_on_hand`),0) FROM `epc_erp_inv_stock` WHERE `item_id`=? AND (?=0 OR `warehouse_id`=?) AND (?=0 OR `owner_company_id`=? OR `owner_company_id`=0)"),
            cancellationToken,
            itemId,
            warehouseId,
            warehouseId,
            ownerCompanyId,
            ownerCompanyId).ConfigureAwait(false);
        var reserved = await ErpDb.DecimalAsync(
            connection,
            transaction,
            ErpDb.Positional(
                "SELECT COALESCE(SUM(`qty`),0) FROM `epc_erp_inv_reservations` WHERE `item_id`=? AND `status` IN ('held','confirmed') AND (`expires_at`=0 OR `expires_at`>?)"
                + " AND (?=0 OR `warehouse_id`=? OR `warehouse_id`=0) AND (?=0 OR `owner_company_id`=? OR `owner_company_id`=0)"),
            cancellationToken,
            itemId,
            now,
            warehouseId,
            warehouseId,
            ownerCompanyId,
            ownerCompanyId).ConfigureAwait(false);
        return new ErpItemAtp(itemId, sku, name, onHand, reserved, Available(onHand, reserved));
    }

    private static async Task<(long Id, decimal Qty, string Status, long ExpiresAt)?> FindOpenHoldAsync(
        DbConnection connection,
        DbTransaction? transaction,
        long itemId,
        string sourceType,
        long sourceId,
        string lineKey,
        int adminId,
        int ownerCompanyId,
        CancellationToken cancellationToken)
    {
        // Owner scope: a hold another legal entity placed on the same document/item
        // must never be reused or overwritten — it belongs to that entity's books.
        var ownerClause = ownerCompanyId > 0 ? " AND `owner_company_id`=?" : string.Empty;
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        if (sourceId > 0)
        {
            command.CommandText = ErpDb.Positional(
                "SELECT `id`,`qty`,`status`,`expires_at` FROM `epc_erp_inv_reservations`"
                + " WHERE `item_id`=? AND `source_type`=? AND `source_id`=? AND `status` IN ('held','confirmed')"
                + ownerClause
                + " ORDER BY `id` ASC LIMIT 1");
            if (ownerCompanyId > 0)
            {
                ErpDb.AddParameters(command, itemId, sourceType, sourceId, ownerCompanyId);
            }
            else
            {
                ErpDb.AddParameters(command, itemId, sourceType, sourceId);
            }
        }
        else if (lineKey.Length > 0)
        {
            command.CommandText = ErpDb.Positional(
                "SELECT `id`,`qty`,`status`,`expires_at` FROM `epc_erp_inv_reservations`"
                + " WHERE `item_id`=? AND `source_type`=? AND `source_id`=0 AND `source_line_key`=? AND `reserved_by`=? AND `status` IN ('held','confirmed')"
                + ownerClause
                + " ORDER BY `id` ASC LIMIT 1");
            if (ownerCompanyId > 0)
            {
                ErpDb.AddParameters(command, itemId, sourceType, lineKey, adminId, ownerCompanyId);
            }
            else
            {
                ErpDb.AddParameters(command, itemId, sourceType, lineKey, adminId);
            }
        }
        else
        {
            return null;
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return (reader.GetInt64(0), reader.GetDecimal(1), reader.GetString(2), reader.GetInt64(3));
    }

    private static async Task<string> ItemLockNameAsync(
        DbConnection connection,
        DbTransaction? transaction,
        long itemId,
        CancellationToken cancellationToken)
    {
        var schema = await ErpDb.StringAsync(connection, transaction, "SELECT DATABASE()", cancellationToken).ConfigureAwait(false);
        return LockName(schema, itemId);
    }

    private static async Task<bool> TryLockAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string lockName,
        CancellationToken cancellationToken)
    {
        var acquired = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT COALESCE(GET_LOCK(?, ?), 0)"),
            cancellationToken,
            lockName,
            LockWaitSeconds).ConfigureAwait(false);
        return acquired == 1;
    }

    private static async Task ReleaseLockAsync(DbConnection connection, DbTransaction? transaction, string lockName)
    {
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("DO RELEASE_LOCK(?)"),
            CancellationToken.None,
            lockName).ConfigureAwait(false);
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
