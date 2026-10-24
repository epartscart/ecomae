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
                ErpDb.Positional("SELECT `default_warehouse_id` FROM `epc_erp_pm_business_units` WHERE `code`=? AND `legal_entity_id`=? AND `active`=1 LIMIT 1"),
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

            // Canonical scheme is "PO:<po>[|<ref>]" — every legacy row starts
            // "PO-", so a canonical key can NEVER equal a legacy key no matter
            // what characters the identifiers contain. Parts escape '\' and '|'
            // (injective); '/' needs no escaping since it is not the separator.
            static string EscapePart(string s) => s.Replace("\\", "\\\\").Replace("|", "\\|");

            var reference = string.IsNullOrWhiteSpace(poNumber) ? null : "PO:" + EscapePart(poNumber.Trim());
            if (!string.IsNullOrWhiteSpace(receiptRef) && reference is not null)
            {
                // A real second delivery carries its own receipt/GRN reference —
                // identical deliveries without one are indistinguishable from
                // retries and stay deduplicated by design.
                reference += "|" + EscapePart(receiptRef.Trim());
            }

            // The movement `reference` column is 128 chars — an overlong claim must
            // truncate DETERMINISTICALLY (with a hash tail) so idempotency still
            // works and the writer never fails after stock already moved.
            var canonicalReference = reference;
            if (canonicalReference is not null && canonicalReference.Length > 120)
            {
                var hash = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonicalReference)))[..10];
                canonicalReference = canonicalReference[..109] + "~" + hash;
            }

            // Dedupe against prior single-PO key shapes as well — all of them
            // only ever encode the po number, so they are only checked for
            // receipt-less requests (a receipt-suffixed row can only belong to
            // the '|' scheme, which needs no second key):
            //   legacy  "PO-<po>" verbatim
            //   interim "PO-<po with /→\>" from the first escape fix
            // Between the two, "PO-A\B" is ambiguous (legacy po="A\B" or interim
            // po="A/B") — matching it is still the safer failure: the row is a
            // receipt for a key this PO could have written.
            var legacyKeys = string.IsNullOrWhiteSpace(receiptRef) && !string.IsNullOrWhiteSpace(poNumber)
                ? new[] { "PO-" + poNumber.Trim(), "PO-" + poNumber.Trim().Replace('/', '\\') }
                : [];

            // Idempotency: a retry of the same PO+warehouse receipt must not add
            // stock a second time. Claims are keyed on the movement reference —
            // the shared writer has no unique constraint to lean on.
            if (canonicalReference is not null)
            {
                var prior = await ErpDb.LongAsync(connection, null,
                    ErpDb.Positional("SELECT `id` FROM `epc_erp_inv_movements` WHERE `movement_type`='purchase_in' AND `warehouse_id`=? AND `item_id`=? AND `qty`=? AND `reference` IN (?, ?, ?) LIMIT 1"),
                    cancellationToken, warehouseId, itemId, qty,
                    canonicalReference,
                    legacyKeys.Length > 0 ? legacyKeys[0] : canonicalReference,
                    legacyKeys.Length > 1 ? legacyKeys[1] : canonicalReference).ConfigureAwait(false);
                if (prior > 0)
                {
                    return new(true, "ok", "Receipt " + canonicalReference + " already recorded for item " + itemId + " in warehouse " + warehouseId + " (movement " + prior + ").");
                }
            }

            // The linked purchase must exist — prevents receipts claiming bogus ids
            // (per-entity ownership is not recorded on purchases yet). Only tenants
            // without the purchasing TABLE skip the check; any real DB error fails.
            if (purchaseId > 0)
            {
                var purchasesTable = await ErpDb.LongAsync(connection, null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `information_schema`.`TABLES` WHERE `TABLE_SCHEMA`=DATABASE() AND `TABLE_NAME`='epc_erp_purchases'"),
                    cancellationToken).ConfigureAwait(false);
                if (purchasesTable > 0)
                {
                    var purchaseExists = await ErpDb.LongAsync(connection, null,
                        ErpDb.Positional("SELECT EXISTS(SELECT 1 FROM `epc_erp_purchases` WHERE `id`=? AND `active`=1)"),
                        cancellationToken, purchaseId).ConfigureAwait(false);
                    if (purchaseExists == 0)
                    {
                        return new(false, "purchase", "Purchase " + purchaseId + " does not exist.");
                    }
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
                    Reference: canonicalReference,
                    Note: "SHAM " + entity + "/" + bu + " receipt" + (string.IsNullOrWhiteSpace(sku) ? string.Empty : " — " + sku.Trim()),
                    MovementDate: movementDate,
                    PurchaseId: purchaseId),
                cancellationToken).ConfigureAwait(false);

            return new(result.Succeeded, result.Succeeded ? "ok" : "movement", result.Succeeded
                ? "Received " + qty.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " into " + entity + "/" + bu + " warehouse " + warehouseId + (canonicalReference is null ? string.Empty : " against " + canonicalReference) + "."
                : result.Message);
        }
        catch (Exception)
        {
            return new(false, "error", "PO receive failed.");
        }
    }
}
