using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// SHAM Phase-3 tail: entity × BU scoped purchase receipt.
///
/// Thin gate in front of <see cref="IErpInventoryMovementWriteService"/> — the
/// shared engine writes <c>epc_erp_inv_movements</c> and updates
/// <c>epc_erp_inv_stock</c> (UPSERT with weighted average cost). This service adds
/// the SHAM scope chain:
///
/// 1. Entity resolves active in <c>epc_erp_pm_legal_entities</c>.
/// 2. BU resolves inside that entity; its <c>default_warehouse_id</c> becomes the
///    receiving warehouse — fail-closed when the BU has none.
/// 3. <see cref="IErpShamFoundationService.AccessAllowedAsync"/> proves the
///    caller's entity × BU × action <c>po-receive</c> grant.
///
/// The PO number rides as the movement <c>reference</c>; <c>purchaseId</c> links
/// the purchasing document when the caller supplies it. Physical stock is the
/// BU's warehouse pool — entity isolation lives on reservations (holds carry
/// <c>owner_company_id</c>), matching the blueprint's shared-warehouse model.
/// </summary>
public sealed record ErpShamPoReceiveResult(bool Ok, string Code, string Message);

public interface IErpShamPoReceiveService
{
    Task<ErpShamPoReceiveResult> ReceiveAsync(
        string entityCode,
        string? buCode,
        string poNumber,
        long itemId,
        string sku,
        decimal qty,
        decimal unitCost,
        long purchaseId,
        string movementDate,
        string userEmail,
        int adminId,
        string? receiptRef = null,
        CancellationToken cancellationToken = default);
}

public sealed class ErpShamPoReceiveService : IErpShamPoReceiveService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpShamFoundationService _foundation;
    private readonly IErpInventoryMovementWriteService _movements;

    public ErpShamPoReceiveService(
        IErpWriteConnectionFactory connections,
        IErpShamFoundationService foundation,
        IErpInventoryMovementWriteService movements)
    {
        _connections = connections;
        _foundation = foundation;
        _movements = movements;
    }

    public async Task<ErpShamPoReceiveResult> ReceiveAsync(
        string entityCode,
        string? buCode,
        string poNumber,
        long itemId,
        string sku,
        decimal qty,
        decimal unitCost,
        long purchaseId,
        string movementDate,
        string userEmail,
        int adminId,
        string? receiptRef = null,
        CancellationToken cancellationToken = default)
    {
        var entity = (entityCode ?? string.Empty).Trim().ToUpperInvariant();
        var bu = (buCode ?? string.Empty).Trim().ToUpperInvariant();
        if (entity.Length == 0 || bu.Length == 0)
        {
            return new(false, "invalid", "Entity (?cmp=) and business unit (?bu=) are required.");
        }

        if (itemId <= 0 || qty <= 0m)
        {
            return new(false, "invalid", "A positive item id and quantity are required.");
        }

        if (!_connections.IsConfigured)
        {
            return new(false, "db", "TenantRegistry DB is not configured.");
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
                return new(false, "entity", "Unknown or inactive legal entity '" + entity + "'.");
            }

            var warehouseId = await ErpDb.LongAsync(connection, null,
                ErpDb.Positional("SELECT `default_warehouse_id` FROM `epc_erp_org_business_units` WHERE `code`=? AND `entity_id`=? AND `status`='active' LIMIT 1"),
                cancellationToken, bu, entityId).ConfigureAwait(false);
            if (warehouseId <= 0)
            {
                return new(false, "warehouse", "Business unit '" + bu + "' does not exist under entity " + entity + " or has no default warehouse — re-run the foundation seed.");
            }

            var allowed = await _foundation.AccessAllowedAsync(userEmail, entity, bu, "po-receive", cancellationToken).ConfigureAwait(false);
            if (!allowed)
            {
                return new(false, "access", "No active grant for '" + userEmail + "' to receive stock in " + entity + "/" + bu + ".");
            }

            var reference = string.IsNullOrWhiteSpace(poNumber) ? null : "PO-" + poNumber.Trim();
            if (!string.IsNullOrWhiteSpace(receiptRef) && reference is not null)
            {
                // A real second delivery carries its own receipt/GRN reference —
                // identical deliveries without one are indistinguishable from
                // retries and stay deduplicated by design.
                reference += "/" + receiptRef.Trim();
            }

            // Idempotency: a retry of the same PO+warehouse receipt must not add
            // stock a second time. Claims are keyed on the movement reference —
            // the shared writer has no unique constraint to lean on.
            if (reference is not null)
            {
                var prior = await ErpDb.LongAsync(connection, null,
                    ErpDb.Positional("SELECT `id` FROM `epc_erp_inv_movements` WHERE `movement_type`='purchase_in' AND `warehouse_id`=? AND `item_id`=? AND `qty`=? AND `reference`=? LIMIT 1"),
                    cancellationToken, warehouseId, itemId, qty, reference).ConfigureAwait(false);
                if (prior > 0)
                {
                    return new(true, "ok", "Receipt " + reference + " already recorded for item " + itemId + " in warehouse " + warehouseId + " (movement " + prior + ").");
                }
            }

            // The linked purchase must exist — prevents receipts claiming bogus ids
            // (per-entity ownership is not recorded on purchases yet). Tenants
            // without the purchasing schema skip the check rather than blocking.
            if (purchaseId > 0)
            {
                try
                {
                    var purchaseExists = await ErpDb.LongAsync(connection, null,
                        ErpDb.Positional("SELECT EXISTS(SELECT 1 FROM `epc_erp_purchases` WHERE `id`=? AND `active`=1)"),
                        cancellationToken, purchaseId).ConfigureAwait(false);
                    if (purchaseExists == 0)
                    {
                        return new(false, "purchase", "Purchase " + purchaseId + " does not exist.");
                    }
                }
                catch (DbException)
                {
                    // Purchasing schema absent on this tenant — receipt proceeds.
                }
            }

            var result = await _movements.RecordMovementAsync(
                new ErpInventoryMovementWriteRequest(
                    AdminUserId: adminId,
                    MovementType: "purchase_in",
                    WarehouseId: warehouseId,
                    ItemId: itemId,
                    Qty: qty,
                    UnitCost: unitCost,
                    Reference: reference,
                    Note: "SHAM " + entity + "/" + bu + " receipt" + (string.IsNullOrWhiteSpace(sku) ? string.Empty : " — " + sku.Trim()),
                    MovementDate: movementDate,
                    PurchaseId: purchaseId),
                cancellationToken).ConfigureAwait(false);

            return new(result.Succeeded, result.Succeeded ? "ok" : "movement", result.Succeeded
                ? "Received " + qty.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " into " + entity + "/" + bu + " warehouse " + warehouseId + (reference is null ? string.Empty : " against " + reference) + "."
                : result.Message);
        }
        catch (Exception)
        {
            return new(false, "error", "PO receive failed.");
        }
    }
}
