using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// SHAM Phase-3: entity × BU scoped atomic stock reservation.
///
/// Thin gate in front of <see cref="IErpInventoryReservationWriteService"/> — the
/// shared engine already serializes per item with a MySQL named lock, computes ATP,
/// honours partial/backorder, and stamps <c>owner_company_id</c>. This service adds
/// the SHAM scope chain:
///
/// 1. Entity resolves in <c>epc_erp_pm_legal_entities</c> and is active — its id
///    becomes <c>OwnerCompanyId</c>, so holds are scoped to the legal entity.
/// 2. BU resolves inside that entity in <c>epc_erp_org_business_units</c>; its
///    <c>default_warehouse_id</c> (seeded SHAM-&lt;short&gt; warehouses) becomes the
///    hold's warehouse — fail-closed when the BU has none.
/// 3. <see cref="IErpShamFoundationService.AccessAllowedAsync"/> proves the caller's
///    entity × BU × action <c>stock-reserve</c> grant.
///
/// Source types match the engine's vocabulary: <c>sales_order</c> (default) or
/// <c>quote</c>. <c>sourceId</c> + <c>sourceLineKey</c> idempotently identify the
/// demanding document line.
/// </summary>
public sealed record ErpShamReserveResult(
    bool Ok,
    string Code,
    string Message,
    long ReservationId,
    string Sku,
    decimal Qty,
    decimal Available);

public interface IErpShamStockReserveService
{
    Task<ErpShamReserveResult> ReserveAsync(
        string entityCode,
        string? buCode,
        long itemId,
        string sku,
        decimal qty,
        string sourceType,
        long sourceId,
        string sourceLineKey,
        bool allowPartial,
        bool durable,
        string userEmail,
        int adminId,
        CancellationToken cancellationToken = default);

    Task<ErpShamReserveResult> ReleaseAsync(
        string entityCode,
        string? buCode,
        long reservationId,
        string userEmail,
        int adminId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpShamStockReserveService : IErpShamStockReserveService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpShamFoundationService _foundation;
    private readonly IErpInventoryReservationWriteService _reservations;

    public ErpShamStockReserveService(
        IErpWriteConnectionFactory connections,
        IErpShamFoundationService foundation,
        IErpInventoryReservationWriteService reservations)
    {
        _connections = connections;
        _foundation = foundation;
        _reservations = reservations;
    }

    public async Task<ErpShamReserveResult> ReserveAsync(
        string entityCode,
        string? buCode,
        long itemId,
        string sku,
        decimal qty,
        string sourceType,
        long sourceId,
        string sourceLineKey,
        bool allowPartial,
        bool durable,
        string userEmail,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveScopeAsync(entityCode, buCode, userEmail, cancellationToken).ConfigureAwait(false);
        if (!scope.Ok)
        {
            return new(false, scope.Code, scope.Message, 0, string.Empty, 0m, 0m);
        }

        if (itemId <= 0 && string.IsNullOrWhiteSpace(sku))
        {
            return new(false, "invalid", "An item id or SKU is required.", 0, string.Empty, 0m, 0m);
        }

        if (sourceId <= 0)
        {
            return new(false, "invalid", "A source document id is required (sourceId).", 0, string.Empty, 0m, 0m);
        }

        var result = await _reservations.ReserveAsync(
            new ErpReserveInput
            {
                ItemId = itemId,
                Sku = sku ?? string.Empty,
                Qty = qty,
                SourceType = string.IsNullOrWhiteSpace(sourceType)
                    ? ErpInventoryReservationWriteService.SourceSalesOrder
                    : sourceType.Trim(),
                SourceId = sourceId,
                SourceLineKey = sourceLineKey ?? string.Empty,
                AdminId = adminId,
                Durable = durable,
                AllowPartial = allowPartial,
                WarehouseId = scope.WarehouseId,
                OwnerCompanyId = scope.EntityId,
            },
            cancellationToken).ConfigureAwait(false);

        return new(result.Succeeded, result.Code, result.Message, result.ReservationId, result.Sku, result.Qty, result.Available);
    }

    public async Task<ErpShamReserveResult> ReleaseAsync(
        string entityCode,
        string? buCode,
        long reservationId,
        string userEmail,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveScopeAsync(entityCode, buCode, userEmail, cancellationToken).ConfigureAwait(false);
        if (!scope.Ok)
        {
            return new(false, scope.Code, scope.Message, 0, string.Empty, 0m, 0m);
        }

        if (reservationId <= 0)
        {
            return new(false, "invalid", "A reservation id is required.", 0, string.Empty, 0m, 0m);
        }

        // Scope check: the hold must belong to the resolved entity — a grant in one
        // BU must not release another entity's reservation.
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            long ownerId = 0;
            long holdWarehouseId = 0;
            await using var holdCmd = connection.CreateCommand();
            holdCmd.CommandText = ErpDb.Positional("SELECT `owner_company_id`, `warehouse_id` FROM `epc_erp_inv_reservations` WHERE `id`=? LIMIT 1");
            ErpDb.AddParameters(holdCmd, reservationId);
            await using (var holdReader = await holdCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                if (await holdReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    ownerId = holdReader.GetInt64(0);
                    holdWarehouseId = holdReader.GetInt64(1);
                }
            }

            if (ownerId == 0)
            {
                return new(false, "not_found", "Reservation " + reservationId + " does not exist.", 0, string.Empty, 0m, 0m);
            }

            if (ownerId != scope.EntityId)
            {
                return new(false, "scope", "Reservation " + reservationId + " belongs to a different legal entity — the grant cannot release it.", 0, string.Empty, 0m, 0m);
            }

            if (holdWarehouseId != 0 && holdWarehouseId != scope.WarehouseId)
            {
                return new(false, "scope", "Reservation " + reservationId + " is held in another business unit's warehouse — the grant cannot release it.", 0, string.Empty, 0m, 0m);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new(false, "error", ex.Message, 0, string.Empty, 0m, 0m);
        }

        var result = await _reservations.ReleaseAsync(reservationId, adminId, cancellationToken).ConfigureAwait(false);
        return new(result.Succeeded, result.Code, result.Message, result.ReservationId, result.Sku, result.Qty, result.Available);
    }

    private async Task<ShamScope> ResolveScopeAsync(
        string entityCode,
        string? buCode,
        string userEmail,
        CancellationToken cancellationToken)
    {
        var entity = (entityCode ?? string.Empty).Trim().ToUpperInvariant();
        var bu = (buCode ?? string.Empty).Trim().ToUpperInvariant();
        if (entity.Length == 0)
        {
            return ShamScope.Fail("invalid", "Entity code is required (?cmp= or the entity field).");
        }

        if (bu.Length == 0)
        {
            return ShamScope.Fail("invalid", "A business unit is required (?bu= or the bu field) — reservations allocate through the BU's warehouse.");
        }

        if (!_connections.IsConfigured)
        {
            return ShamScope.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpShamFoundationService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

            var entityId = await ErpDb.LongAsync(connection, null,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code`=? AND `active`=1 LIMIT 1"),
                cancellationToken, entity).ConfigureAwait(false);
            if (entityId == 0)
            {
                return ShamScope.Fail("entity", "Unknown or inactive legal entity '" + entity + "'.");
            }

            long warehouseId = 0;
            await using (var buCommand = connection.CreateCommand())
            {
                buCommand.CommandText = ErpDb.Positional(
                    "SELECT `default_warehouse_id` FROM `epc_erp_org_business_units` WHERE `code`=? AND `entity_id`=? AND `status`='active' LIMIT 1");
                var p0 = buCommand.CreateParameter(); p0.ParameterName = "@p0"; p0.Value = bu; buCommand.Parameters.Add(p0);
                var p1 = buCommand.CreateParameter(); p1.ParameterName = "@p1"; p1.Value = entityId; buCommand.Parameters.Add(p1);
                var raw = await buCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (raw is null || raw is DBNull)
                {
                    return ShamScope.Fail("bu", "Business unit '" + bu + "' does not exist under entity " + entity + " (or is disabled).");
                }

                warehouseId = Convert.ToInt64(raw);
            }

            if (warehouseId <= 0)
            {
                return ShamScope.Fail("warehouse", "Business unit '" + bu + "' has no default warehouse — re-run the foundation seed.");
            }

            var allowed = await _foundation.AccessAllowedAsync(userEmail, entity, bu, "stock-reserve", cancellationToken).ConfigureAwait(false);
            if (!allowed)
            {
                return ShamScope.Fail("access", "No active grant for '" + userEmail + "' to reserve stock in " + entity + "/" + bu + ".");
            }

            return new(true, "ok", string.Empty, (int)entityId, (int)warehouseId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ShamScope.Fail("error", ex.Message);
        }
    }

    private sealed record ShamScope(bool Ok, string Code, string Message, int EntityId, int WarehouseId)
    {
        public static ShamScope Fail(string code, string message) => new(false, code, message, 0, 0);
    }
}
