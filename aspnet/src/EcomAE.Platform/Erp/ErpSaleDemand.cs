using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// PHP <c>epc_erp_inventory_record_sale_demand</c>, run by <c>epc_einvoice_save_document</c> after a tax invoice (not a 381
/// credit note) commits: one <c>sale_out</c> movement per sold ERP item so Order planning and SCM forecast from billed sales.
/// Stock is taken best-effort (clamped to what is on hand), and the <c>SALEINV-&lt;id&gt;</c> reference makes it run once per
/// invoice. Any database error stops it without touching the invoice.
/// </summary>
public static class ErpSaleDemand
{
    /// <summary>Records the demand of saved invoice <paramref name="documentId"/>; returns the movements written.</summary>
    public static async Task<int> RecordForSavedInvoiceAsync(DbConnection connection, long documentId, int adminId, CancellationToken cancellationToken)
    {
        if (documentId <= 0)
        {
            return 0;
        }

        try
        {
            string? category = null, typeCode = null;
            long orderId = 0, issueDate = 0;
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = ErpDb.Positional("SELECT `doc_category`, `invoice_type_code`, `order_id`, `issue_date` FROM `epc_einvoice_documents` WHERE `id` = ? LIMIT 1");
                ErpDb.AddParameters(command, documentId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return 0;
                }

                category = reader.IsDBNull(0) ? null : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture);
                typeCode = reader.IsDBNull(1) ? null : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture);
                orderId = reader.IsDBNull(2) ? 0 : Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture);
                issueDate = reader.IsDBNull(3) ? 0 : Convert.ToInt64(reader.GetValue(3), CultureInfo.InvariantCulture);
            }

            if ((category ?? "tax_invoice") != "tax_invoice" || (typeCode ?? "380") == "381")
            {
                return 0;
            }

            var lines = new List<(string Name, decimal Qty)>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = ErpDb.Positional("SELECT `item_name`, `quantity` FROM `epc_einvoice_lines` WHERE `document_id` = ? ORDER BY `line_no`, `id`");
                ErpDb.AddParameters(command, documentId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    lines.Add((
                        reader.IsDBNull(0) ? string.Empty : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty,
                        reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture)));
                }
            }

            return await RecordAsync(connection, documentId, orderId, lines, issueDate > 0 ? issueDate : DateTimeOffset.UtcNow.ToUnixTimeSeconds(), adminId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is DbException or ErpWriteException)
        {
            return 0;
        }
    }

    /// <summary>
    /// The body of PHP <c>epc_erp_inventory_record_sale_demand</c>: the order's product lines mapped to active ERP items, else the
    /// invoice lines matched by item name. Throws on a database error, like PHP.
    /// </summary>
    public static async Task<int> RecordAsync(
        DbConnection connection,
        long documentId,
        long orderId,
        IReadOnlyList<(string Name, decimal Qty)> documentLines,
        long when,
        int adminId,
        CancellationToken cancellationToken)
    {
        if (documentId <= 0)
        {
            return 0;
        }

        var reference = "SALEINV-" + documentId.ToString(CultureInfo.InvariantCulture);
        if (await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_inv_movements` WHERE `movement_type` = 'sale_out' AND `reference` = ?"),
                cancellationToken,
                reference).ConfigureAwait(false) > 0)
        {
            return 0;
        }

        if (when <= 0)
        {
            when = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        var demand = new Dictionary<long, decimal>();
        var order = new List<long>();
        void Add(long itemId, decimal qty)
        {
            if (!demand.TryAdd(itemId, qty))
            {
                demand[itemId] += qty;
            }
            else
            {
                order.Add(itemId);
            }
        }

        if (orderId > 0)
        {
            var products = new List<(long ProductId, decimal Qty)>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = ErpDb.Positional("SELECT `product_id`, SUM(`count_need`) AS q FROM `shop_orders_items` WHERE `order_id` = ? AND `product_id` > 0 GROUP BY `product_id`");
                ErpDb.AddParameters(command, orderId);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    products.Add((
                        Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                        reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1), CultureInfo.InvariantCulture)));
                }
            }

            foreach (var (productId, qty) in products)
            {
                if (qty <= 0m)
                {
                    continue;
                }

                var itemId = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `id` FROM `epc_erp_inv_items` WHERE `product_id` = ? AND `active` = 1 LIMIT 1"),
                    cancellationToken,
                    productId).ConfigureAwait(false);
                if (itemId > 0)
                {
                    Add(itemId, qty);
                }
            }
        }

        if (demand.Count == 0)
        {
            foreach (var (name, qty) in documentLines)
            {
                var trimmed = name.Trim();
                if (qty <= 0m || trimmed.Length == 0)
                {
                    continue;
                }

                var itemId = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `id` FROM `epc_erp_inv_items` WHERE `name` = ? AND `active` = 1 LIMIT 1"),
                    cancellationToken,
                    trimmed).ConfigureAwait(false);
                if (itemId > 0)
                {
                    Add(itemId, qty);
                }
            }
        }

        var recorded = 0;
        foreach (var itemId in order)
        {
            var qty = demand[itemId];
            var warehouseId = await PickWarehouseAsync(connection, itemId, cancellationToken).ConfigureAwait(false);
            if (warehouseId <= 0)
            {
                continue;
            }

            var row = await ErpInventoryMovementWriteService.GetStockRowAsync(connection, warehouseId, itemId, string.Empty, string.Empty, cancellationToken).ConfigureAwait(false);
            var cost = row?.AvgUnitCost ?? 0m;
            if (row is not null)
            {
                var take = Math.Min(row.QtyOnHand, qty);
                if (take > 0m)
                {
                    try
                    {
                        await ErpInventoryMovementWriteService.UpsertStockAsync(connection, warehouseId, itemId, -take, cost, string.Empty, string.Empty, null, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is DbException or ErpWriteException)
                    {
                    }
                }
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("""
                    INSERT INTO `epc_erp_inv_movements`
                    (`movement_type`,`warehouse_id`,`item_id`,`qty`,`unit_cost`,`total_cost`,`order_id`,`reference`,`note`,`movement_date`,`admin_id`,`active`)
                    VALUES ('sale_out',?,?,?,?,?,?,?,?,?,?,1)
                    """),
                cancellationToken,
                warehouseId,
                itemId,
                qty,
                cost,
                Math.Round(qty * cost, 2, MidpointRounding.AwayFromZero),
                orderId,
                reference,
                "Sales invoice demand",
                when,
                adminId).ConfigureAwait(false);
            recorded++;
        }

        return recorded;
    }

    /// <summary>PHP <c>epc_erp_inventory_pick_warehouse_for_item</c>: where the item holds most stock, else the first active warehouse.</summary>
    private static async Task<long> PickWarehouseAsync(DbConnection connection, long itemId, CancellationToken cancellationToken)
    {
        var warehouseId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `warehouse_id` FROM `epc_erp_inv_stock` WHERE `item_id` = ? ORDER BY `qty_on_hand` DESC LIMIT 1"),
            cancellationToken,
            itemId).ConfigureAwait(false);
        return warehouseId > 0
            ? warehouseId
            : await ErpDb.LongAsync(connection, null, "SELECT `id` FROM `epc_erp_inv_warehouses` WHERE `active` = 1 ORDER BY `id` LIMIT 1", cancellationToken).ConfigureAwait(false);
    }
}
