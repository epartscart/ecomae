using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_order_fulfillment.php</c> twin: commerce order → ERP sales order + per-supplier
/// purchase orders, receipt-synced PO statuses, PO→purchase-invoice and SO/order→sales-invoice posting,
/// and mid-fulfillment supplier swaps. Operations are sequential (PHP is non-transactional); writes run
/// only behind confirm_writes. Bootstrap runs PHP's additive fulfillment schema-ensure (voucher/extended
/// ensures stay Classic); the other operations fail closed when it has not run. The best-effort process-flow
/// sync runs where PHP runs it: <c>epc_pf_sync_po_case</c> after each new PO and every PO status change, and
/// <c>epc_pf_sync_order_case</c> at the end of bootstrap and in every fulfilment status read. Deviations: (1) PHP
/// only syncs a new PO's case when <c>epc_erp_processflow.php</c> is already loaded (never in checkout); ASP.NET
/// always does; (2) the APAI supplier hint is decoded from <c>t2_json_params</c> only.
/// </summary>
public interface IErpOrderFulfillmentWriteService
{
    /// <param name="processFlowActor">PHP <c>epc_pf_admin_id()</c> / <c>epc_pf_user_id()</c>; defaults to <paramref name="adminId"/> for both.</param>
    Task<ErpFulfillmentResult> BootstrapAsync(long orderId, int adminId, CancellationToken cancellationToken = default, ErpPfActor? processFlowActor = null);
    Task<ErpFulfillmentResult> StatusAsync(long orderId, int adminId, CancellationToken cancellationToken = default);
    Task<ErpFulfillmentResult> SyncAsync(long orderId, int adminId, CancellationToken cancellationToken = default);
    Task<ErpFulfillmentResult> PostPoInvoiceAsync(long poId, int adminId, CancellationToken cancellationToken = default);
    Task<ErpFulfillmentResult> PostSalesInvoiceAsync(long orderId, int adminId, CancellationToken cancellationToken = default);
    Task<ErpFulfillmentResult> AutoPostAsync(long orderId, int adminId, CancellationToken cancellationToken = default);
    Task<ErpFulfillmentResult> SwapLineSupplierAsync(long orderId, long orderItemId, long newStorageId, int adminId, CancellationToken cancellationToken = default);
}

public sealed record ErpPfActor(int AdminId, long SessionUserId);

public sealed record ErpFulfillmentResult(bool Ok, string Message, object? Payload, int Writes)
{
    public static ErpFulfillmentResult Fail(string message) => new(false, message, null, 0);
    public static ErpFulfillmentResult Success(object? payload, int writes) => new(true, "ok", payload, writes);
}

public sealed record ErpFulfillmentBootstrapPayload(
    [property: JsonPropertyName("shop_order_id")] long ShopOrderId,
    [property: JsonPropertyName("sales_order_id")] long SalesOrderId,
    [property: JsonPropertyName("po_ids")] IReadOnlyList<long> PoIds,
    [property: JsonPropertyName("created")] bool Created);

public sealed class ErpOrderFulfillmentWriteService : IErpOrderFulfillmentWriteService
{
    private static readonly string[] OpenPoStatuses = ["draft", "approved", "partial", "received"];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpVoucherNumberService _vouchers;
    private readonly IErpTaxAmountCalculator _tax;
    private readonly IErpSupplierWriteService _suppliers;
    private readonly IErpPurchaseInvoiceWriteService _purchases;
    private readonly IErpSalesInvoiceWriteService _soInvoices;
    private readonly IErpInvoiceFromOrderWriteService _orderInvoices;
    private readonly IErpAuditLogWriter _audit;
    private readonly IErpProcessFlowSyncService? _processFlow;

    public ErpOrderFulfillmentWriteService(
        IErpWriteConnectionFactory connections,
        IErpVoucherNumberService vouchers,
        IErpTaxAmountCalculator tax,
        IErpSupplierWriteService suppliers,
        IErpPurchaseInvoiceWriteService purchases,
        IErpSalesInvoiceWriteService soInvoices,
        IErpInvoiceFromOrderWriteService orderInvoices,
        IErpAuditLogWriter audit,
        IErpProcessFlowSyncService? processFlow = null)
    {
        _connections = connections;
        _vouchers = vouchers;
        _tax = tax;
        _suppliers = suppliers;
        _purchases = purchases;
        _soInvoices = soInvoices;
        _orderInvoices = orderInvoices;
        _audit = audit;
        _processFlow = processFlow;
    }

    private async Task SyncOrderCaseAsync(long orderId, ErpPfActor actor, CancellationToken ct)
    {
        if (_processFlow is not null)
        {
            await _processFlow.SyncOrderCaseAsync(orderId, actor.AdminId, actor.SessionUserId, ct).ConfigureAwait(false);
        }
    }

    private async Task SyncPoCaseAsync(long poId, ErpPfActor actor, CancellationToken ct)
    {
        if (_processFlow is not null)
        {
            await _processFlow.SyncPoCaseAsync(poId, actor.AdminId, actor.SessionUserId, ct).ConfigureAwait(false);
        }
    }

    public async Task<ErpFulfillmentResult> BootstrapAsync(long orderId, int adminId, CancellationToken cancellationToken = default, ErpPfActor? processFlowActor = null)
    {
        var pfActor = processFlowActor ?? new ErpPfActor(adminId, adminId);
        if (!_connections.IsConfigured) return ErpFulfillmentResult.Fail("TenantRegistry DB is not configured.");
        if (orderId <= 0) return ErpFulfillmentResult.Fail("Order ID required");

        await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(c, cancellationToken).ConfigureAwait(false);
        if (!await ProvisionedAsync(c, cancellationToken).ConfigureAwait(false))
        {
            return ErpFulfillmentResult.Fail("Order fulfillment schema is not provisioned");
        }

        var order = await LoadOrderAsync(c, orderId, cancellationToken).ConfigureAwait(false);
        if (order is null) return ErpFulfillmentResult.Fail("Shop order not found");
        var items = await LoadItemsAsync(c, orderId, cancellationToken).ConfigureAwait(false);
        if (items.Count == 0) return ErpFulfillmentResult.Fail("Shop order has no line items");

        var writes = 0;
        var soId = await CreateSalesOrderAsync(c, orderId, order!, items, adminId, cancellationToken).ConfigureAwait(false);
        writes += soId.created ? soId.writes : 0;

        var bySupplier = new Dictionary<long, (string Name, List<Dictionary<string, object?>> Lines)>();
        foreach (var item in items)
        {
            var sup = await ResolveLineSupplierAsync(c, item, cancellationToken).ConfigureAwait(false);
            if (sup.Id <= 0) continue;
            var qty = Math.Max(0.0001, Num(item, "count_need"));
            var unitCost = Round(Num(item, "t2_price_purchase"), 4);
            var desc = Text(item, "t2_name");
            if (desc.Length == 0) desc = Text(item, "t2_article", "Line");
            if (!bySupplier.TryGetValue(sup.Id, out var bundle))
            {
                bundle = (sup.Name, new List<Dictionary<string, object?>>());
                bySupplier[sup.Id] = bundle;
            }
            bundle.Lines.Add(new Dictionary<string, object?>
            {
                ["shop_order_item_id"] = Id(item),
                ["storage_id"] = sup.StorageId,
                ["description"] = desc,
                ["qty"] = qty,
                ["unit_cost_ex_vat"] = unitCost,
                ["line_ex_vat"] = Round2(qty * unitCost),
            });
        }

        var poIds = new List<long>();
        foreach (var (supplierId, bundle) in bySupplier)
        {
            var created = await CreatePoForSupplierAsync(c, orderId, supplierId, bundle.Name, bundle.Lines, adminId, cancellationToken).ConfigureAwait(false);
            if (created.poId > 0)
            {
                poIds.Add(created.poId);
                writes += created.writes;
                if (created.writes > 0)
                {
                    await SyncPoCaseAsync(created.poId, pfActor, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        await SyncOrderCaseAsync(orderId, pfActor, cancellationToken).ConfigureAwait(false);
        return ErpFulfillmentResult.Success(new ErpFulfillmentBootstrapPayload(orderId, soId.id, poIds, true), writes);
    }

    /// <summary>PHP <c>epc_erp_order_fulfillment_ensure_schema()</c> fulfillment columns (additive; errors ignored like PHP).</summary>
    private static async Task EnsureSchemaAsync(DbConnection c, CancellationToken ct)
    {
        foreach (var (table, column, definition) in new[]
        {
            ("epc_erp_sales_orders", "shop_order_id", "int(11) NOT NULL DEFAULT 0"),
            ("epc_erp_sales_orders", "fulfillment_status", "varchar(16) NOT NULL DEFAULT 'open'"),
            ("epc_erp_sales_order_lines", "shop_order_item_id", "int(11) NOT NULL DEFAULT 0"),
            ("epc_erp_sales_order_lines", "supplier_id", "int(11) NOT NULL DEFAULT 0"),
        })
        {
            await AddColumnIfMissingAsync(c, table, column, definition, ct).ConfigureAwait(false);
        }

        await ErpDb.TryExecuteAsync(
            c,
            "CREATE TABLE IF NOT EXISTS `epc_erp_po_lines` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `po_id` int(11) NOT NULL,"
            + " `shop_order_item_id` int(11) NOT NULL DEFAULT 0,"
            + " `supplier_id` int(11) NOT NULL DEFAULT 0,"
            + " `storage_id` int(11) NOT NULL DEFAULT 0,"
            + " `line_no` int(11) NOT NULL DEFAULT 1,"
            + " `description` varchar(255) NOT NULL DEFAULT '',"
            + " `qty` decimal(14,3) NOT NULL DEFAULT 0.000,"
            + " `unit_cost_ex_vat` decimal(14,4) NOT NULL DEFAULT 0.0000,"
            + " `line_ex_vat` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `qty_received` decimal(14,3) NOT NULL DEFAULT 0.000,"
            + " `qty_cancelled` decimal(14,3) NOT NULL DEFAULT 0.000,"
            + " `time_updated` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`),"
            + " KEY `x_po` (`po_id`),"
            + " KEY `x_order_item` (`shop_order_item_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='PO lines linked to shop order items'",
            ct).ConfigureAwait(false);
        await AddColumnIfMissingAsync(c, "epc_erp_po_lines", "item_code", "varchar(32) NOT NULL DEFAULT ''", ct).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(c, "ALTER TABLE `epc_erp_sales_orders` ADD KEY `x_shop_order` (`shop_order_id`)", ct).ConfigureAwait(false);
    }

    private static async Task AddColumnIfMissingAsync(DbConnection c, string table, string column, string definition, CancellationToken ct)
    {
        try
        {
            var tableExists = await ErpDb.LongAsync(
                c, null,
                ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
                ct, table).ConfigureAwait(false);
            var columnExists = await ErpDb.LongAsync(
                c, null,
                ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
                ct, table, column).ConfigureAwait(false);
            if (tableExists > 0 && columnExists <= 0)
            {
                await ErpDb.ExecuteAsync(c, null, "ALTER TABLE `" + table + "` ADD `" + column + "` " + definition, ct).ConfigureAwait(false);
            }
        }
        catch (DbException)
        {
        }
    }

    public async Task<ErpFulfillmentResult> StatusAsync(long orderId, int adminId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured) return ErpFulfillmentResult.Fail("TenantRegistry DB is not configured.");
        await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ProvisionedAsync(c, cancellationToken).ConfigureAwait(false))
        {
            return ErpFulfillmentResult.Fail("Order fulfillment schema is not provisioned");
        }

        var writes = 0;
        writes += await SyncPoStatusesAsync(c, orderId, adminId, cancellationToken).ConfigureAwait(false);
        var fulfillment = await SyncSalesStatusAsync(c, orderId, cancellationToken).ConfigureAwait(false);
        if (fulfillment is not "none") writes++;
        await SyncOrderCaseAsync(orderId, new ErpPfActor(adminId, adminId), cancellationToken).ConfigureAwait(false);
        var status = await ReadStatusAsync(c, orderId, fulfillment, cancellationToken).ConfigureAwait(false);
        return ErpFulfillmentResult.Success(status, writes);
    }

    public async Task<ErpFulfillmentResult> SyncAsync(long orderId, int adminId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured) return ErpFulfillmentResult.Fail("TenantRegistry DB is not configured.");
        await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ProvisionedAsync(c, cancellationToken).ConfigureAwait(false))
        {
            return ErpFulfillmentResult.Fail("Order fulfillment schema is not provisioned");
        }

        var writes = 0;
        var poUpdates = new List<object>();
        var updated = await SyncPoStatusListAsync(c, orderId, adminId, cancellationToken).ConfigureAwait(false);
        writes += updated.writes;
        poUpdates.AddRange(updated.items);
        var fulfillment = await SyncSalesStatusAsync(c, orderId, cancellationToken).ConfigureAwait(false);
        if (fulfillment is not "none") writes++;
        await SyncOrderCaseAsync(orderId, new ErpPfActor(adminId, adminId), cancellationToken).ConfigureAwait(false);
        var status = await ReadStatusAsync(c, orderId, fulfillment, cancellationToken).ConfigureAwait(false);
        return ErpFulfillmentResult.Success(new
        {
            po_updates = poUpdates,
            fulfillment,
            status,
        }, writes);
    }

    public async Task<ErpFulfillmentResult> PostPoInvoiceAsync(long poId, int adminId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured) return ErpFulfillmentResult.Fail("TenantRegistry DB is not configured.");
        if (poId <= 0) return ErpFulfillmentResult.Fail("Purchase order not found");

        await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ProvisionedAsync(c, cancellationToken).ConfigureAwait(false))
        {
            return ErpFulfillmentResult.Fail("Order fulfillment schema is not provisioned");
        }

        var po = await LoadPoAsync(c, poId, cancellationToken).ConfigureAwait(false);
        if (po is null) return ErpFulfillmentResult.Fail("Purchase order not found");
        var writes = 0;
        var orderId = po.OrderId;

        if (po.PurchaseId > 0)
        {
            var piNo = await ErpDb.StringAsync(
                c, null, ErpDb.Positional("SELECT `invoice_number` FROM `epc_erp_purchases` WHERE `id` = ? AND `active` = 1 LIMIT 1"),
                cancellationToken, po.PurchaseId).ConfigureAwait(false);
            return ErpFulfillmentResult.Success(new
            {
                po_id = poId,
                purchase_id = po.PurchaseId,
                voucher_no = piNo ?? string.Empty,
                shop_order_id = orderId,
                already_posted = true,
            }, 0);
        }

        var status = po.Status;
        if (!OpenPoStatuses.Contains(status, StringComparer.Ordinal))
        {
            writes += await SyncPoStatusesAsync(c, orderId, adminId, cancellationToken).ConfigureAwait(false);
            po = (await LoadPoAsync(c, poId, cancellationToken).ConfigureAwait(false))!;
            status = po.Status;
        }
        if (status == "approved")
        {
            writes += await SyncReceivedQtyAsync(c, orderId, cancellationToken).ConfigureAwait(false);
            var openQty = await ScalarDoubleAsync(
                c, null,
                ErpDb.Positional("SELECT IFNULL(SUM(GREATEST(0, `qty` - `qty_received` - `qty_cancelled`)), 0) FROM `epc_erp_po_lines` WHERE `po_id` = ?"),
                cancellationToken, poId).ConfigureAwait(false);
            if (openQty <= 0.001)
            {
                writes += await SetPoStatusAsync(c, poId, "received", adminId, cancellationToken).ConfigureAwait(false);
                writes += await ErpDb.ExecuteAsync(
                    c, null,
                    ErpDb.Positional("UPDATE `epc_erp_purchase_orders` SET `received_at` = ?, `time_updated` = ? WHERE `id` = ?"),
                    cancellationToken, Now(), Now(), poId).ConfigureAwait(false);
                po = (await LoadPoAsync(c, poId, cancellationToken).ConfigureAwait(false))!;
                status = po.Status;
            }
        }
        if (status is not ("partial" or "received"))
        {
            return ErpFulfillmentResult.Fail("PO must be partial or received before purchase invoice posting");
        }

        var amountEx = 0m;
        await using (var lr = await ExecReaderAsync(
            c, "SELECT `qty`, `qty_received`, `unit_cost_ex_vat` FROM `epc_erp_po_lines` WHERE `po_id` = ? ORDER BY `line_no`",
            cancellationToken, poId).ConfigureAwait(false))
        {
            while (await lr.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var recv = Convert.ToDecimal(lr.GetValue(1), CultureInfo.InvariantCulture);
                var qty = recv > 0m ? recv : Convert.ToDecimal(lr.GetValue(0), CultureInfo.InvariantCulture);
                amountEx += qty * Convert.ToDecimal(lr.GetValue(2), CultureInfo.InvariantCulture);
            }
        }
        amountEx = Math.Round(amountEx, 2);
        if (amountEx <= 0m) amountEx = (decimal)Math.Round(po.AmountExVat, 2);

        var purchase = await _purchases.CreateAsync(
            new ErpPurchaseInvoiceInput
            {
                SupplierId = (int)po.SupplierId,
                OrderId = orderId,
                PurchaseOrderId = poId,
                InvoiceNumber = string.Empty,
                AmountExVat = amountEx,
                Note = "From PO " + po.PoNo + " (order #" + orderId + ")",
                Status = "confirmed",
                AllowOpenOrder = true,
            },
            adminId,
            cancellationToken).ConfigureAwait(false);
        writes += 2;

        await _audit.LogAsync(
            c, null, adminId, "po_to_purchase_order_flow", "purchase_order", poId,
            "PO posted to purchase invoice (open order OK)",
            new Dictionary<string, string?> { ["purchase_id"] = purchase.PurchaseId.ToString(CultureInfo.InvariantCulture), ["shop_order_id"] = orderId.ToString(CultureInfo.InvariantCulture) },
            cancellationToken).ConfigureAwait(false);
        writes++;

        if (orderId > 0)
        {
            var f = await SyncSalesStatusAsync(c, orderId, cancellationToken).ConfigureAwait(false);
            if (f is not "none") writes++;
        }

        return ErpFulfillmentResult.Success(new
        {
            po_id = poId,
            purchase_id = purchase.PurchaseId,
            voucher_no = purchase.InvoiceNumber,
            shop_order_id = orderId,
        }, writes);
    }

    public async Task<ErpFulfillmentResult> PostSalesInvoiceAsync(long orderId, int adminId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured) return ErpFulfillmentResult.Fail("TenantRegistry DB is not configured.");
        if (orderId <= 0) return ErpFulfillmentResult.Fail("Order ID required");

        await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ProvisionedAsync(c, cancellationToken).ConfigureAwait(false))
        {
            return ErpFulfillmentResult.Fail("Order fulfillment schema is not provisioned");
        }

        await ErpOrderCompletionGuard.AssertCompleteAsync(c, orderId, "Sales invoice from order", cancellationToken).ConfigureAwait(false);

        var writes = 0;
        var so = await FindSalesOrderAsync(c, orderId, cancellationToken).ConfigureAwait(false);
        if (so is not null && so.SalesInvoiceId > 0)
        {
            return ErpFulfillmentResult.Success(new
            {
                shop_order_id = orderId,
                sales_order_id = so.Id,
                sales_invoice_id = so.SalesInvoiceId,
                already_posted = true,
            }, 0);
        }

        var existingId = await ErpDb.LongAsync(
            c, null,
            ErpDb.Positional("SELECT `id` FROM `epc_einvoice_documents` WHERE `order_id` = ? AND `active` = 1 ORDER BY `id` DESC LIMIT 1"),
            cancellationToken, orderId).ConfigureAwait(false);
        if (existingId <= 0 && so is not null)
        {
            existingId = await ErpDb.LongAsync(
                c, null,
                ErpDb.Positional("SELECT `id` FROM `epc_einvoice_documents` WHERE `sales_order_id` = ? AND `active` = 1 ORDER BY `id` DESC LIMIT 1"),
                cancellationToken, so.Id).ConfigureAwait(false);
        }
        if (existingId > 0)
        {
            if (so is not null)
            {
                writes += await ErpDb.ExecuteAsync(
                    c, null,
                    ErpDb.Positional("UPDATE `epc_erp_sales_orders` SET `status` = 'invoiced', `sales_invoice_id` = ?, `fulfillment_status` = 'invoiced', `time_updated` = ? WHERE `id` = ?"),
                    cancellationToken, existingId, Now(), so.Id).ConfigureAwait(false);
                writes += await ErpDb.ExecuteAsync(
                    c, null,
                    ErpDb.Positional("UPDATE `epc_einvoice_documents` SET `order_id` = ? WHERE `id` = ? AND `order_id` = 0"),
                    cancellationToken, orderId, existingId).ConfigureAwait(false);
            }
            return ErpFulfillmentResult.Success(new
            {
                shop_order_id = orderId,
                sales_order_id = so?.Id ?? 0,
                sales_invoice_id = existingId,
                already_posted = true,
            }, writes);
        }

        if (so is not null && so.Status is "draft" or "confirmed")
        {
            var converted = await _soInvoices.ConvertSalesOrderAsync(so.Id, adminId, cancellationToken).ConfigureAwait(false);
            writes += await ErpDb.ExecuteAsync(
                c, null,
                ErpDb.Positional("UPDATE `epc_erp_sales_orders` SET `fulfillment_status` = 'invoiced' WHERE `id` = ?"),
                cancellationToken, so.Id).ConfigureAwait(false);
            return ErpFulfillmentResult.Success(new
            {
                sales_order_id = converted.SalesOrderId,
                sales_invoice_id = converted.SalesInvoiceId,
                shop_order_id = orderId,
                already_posted = false,
            }, writes + 1);
        }

        var invoice = await _orderInvoices.ConvertAsync(orderId, adminId, cancellationToken).ConfigureAwait(false);
        if (so is not null)
        {
            writes += await ErpDb.ExecuteAsync(
                c, null,
                ErpDb.Positional("UPDATE `epc_erp_sales_orders` SET `status` = 'invoiced', `sales_invoice_id` = ?, `fulfillment_status` = 'invoiced', `time_updated` = ? WHERE `id` = ?"),
                cancellationToken, invoice.SalesInvoiceId, Now(), so.Id).ConfigureAwait(false);
        }
        return ErpFulfillmentResult.Success(new
        {
            shop_order_id = orderId,
            sales_order_id = so?.Id ?? 0,
            sales_invoice_id = invoice.SalesInvoiceId,
            already_posted = false,
        }, writes + 1);
    }

    public async Task<ErpFulfillmentResult> AutoPostAsync(long orderId, int adminId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured) return ErpFulfillmentResult.Fail("TenantRegistry DB is not configured.");
        if (orderId <= 0) return ErpFulfillmentResult.Fail("Order ID required");

        await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ProvisionedAsync(c, cancellationToken).ConfigureAwait(false))
        {
            return ErpFulfillmentResult.Fail("Order fulfillment schema is not provisioned");
        }

        var writes = await SyncPoStatusesAsync(c, orderId, adminId, cancellationToken).ConfigureAwait(false);

        var purchaseInvoices = new List<object>();
        var poIds = new List<long>();
        await using (var r = await ExecReaderAsync(
            c, "SELECT `id` FROM `epc_erp_purchase_orders` WHERE `order_id` = ? AND `status` IN ('partial','received') AND `purchase_id` = 0",
            cancellationToken, orderId).ConfigureAwait(false))
        {
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false)) poIds.Add(Convert.ToInt64(r.GetValue(0)));
        }
        foreach (var pid in poIds)
        {
            var r = await PostPoInvoiceAsync(pid, adminId, cancellationToken).ConfigureAwait(false);
            writes += r.Writes;
            purchaseInvoices.Add(r.Ok
                ? r.Payload!
                : new { po_id = pid, error = r.Message });
        }

        object? salesInvoice = null;
        if (await ErpOrderCompletionGuard.IsCompleteAsync(c, orderId, cancellationToken).ConfigureAwait(false))
        {
            var r = await PostSalesInvoiceAsync(orderId, adminId, cancellationToken).ConfigureAwait(false);
            writes += r.Writes;
            salesInvoice = r.Ok ? r.Payload : new { error = r.Message };
        }

        await SyncOrderCaseAsync(orderId, new ErpPfActor(adminId, adminId), cancellationToken).ConfigureAwait(false);
        var status = await ReadStatusAsync(c, orderId, null, cancellationToken).ConfigureAwait(false);
        return ErpFulfillmentResult.Success(new
        {
            shop_order_id = orderId,
            purchase_invoices = purchaseInvoices,
            sales_invoice = salesInvoice,
            status,
        }, writes);
    }

    public async Task<ErpFulfillmentResult> SwapLineSupplierAsync(long orderId, long orderItemId, long newStorageId, int adminId, CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured) return ErpFulfillmentResult.Fail("TenantRegistry DB is not configured.");
        if (orderId <= 0 || orderItemId <= 0 || newStorageId <= 0)
        {
            return ErpFulfillmentResult.Fail("Order, line, and new storage are required");
        }

        await using var c = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ProvisionedAsync(c, cancellationToken).ConfigureAwait(false))
        {
            return ErpFulfillmentResult.Fail("Order fulfillment schema is not provisioned");
        }

        var item = await LoadItemAsync(c, orderItemId, orderId, cancellationToken).ConfigureAwait(false);
        if (item is null) return ErpFulfillmentResult.Fail("Order line not found");

        PoLine? poLine = null;
        await using (var r = await ExecReaderAsync(
            c,
            "SELECT pl.`id`, pl.`qty`, pl.`supplier_id`, po.`id`, po.`status`, po.`purchase_id`, po.`po_no` FROM `epc_erp_po_lines` pl"
            + " INNER JOIN `epc_erp_purchase_orders` po ON po.`id` = pl.`po_id`"
            + " WHERE pl.`shop_order_item_id` = ? AND po.`order_id` = ? AND po.`status` != 'cancelled' AND po.`purchase_id` = 0"
            + " ORDER BY pl.`id` DESC LIMIT 1",
            cancellationToken, orderItemId, orderId).ConfigureAwait(false))
        {
            if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                poLine = new PoLine(
                    Convert.ToInt64(r.GetValue(0)),
                    Convert.ToDouble(r.GetValue(1), CultureInfo.InvariantCulture),
                    Convert.ToInt64(r.GetValue(2)),
                    Convert.ToInt64(r.GetValue(3)),
                    Convert.ToString(r.GetValue(4), CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToInt64(r.GetValue(5)),
                    Convert.ToString(r.GetValue(6), CultureInfo.InvariantCulture) ?? string.Empty);
            }
        }
        if (poLine is null) return ErpFulfillmentResult.Fail("No open PO line found for this order item");

        var received = await ReceivedQtyAsync(c, orderItemId, cancellationToken).ConfigureAwait(false);
        var remaining = Math.Round(Math.Max(0.0, poLine.Qty - received), 3);
        if (remaining <= 0) return ErpFulfillmentResult.Fail("Nothing left to reassign on this line");

        var swappedItem = item! with { StorageId = newStorageId };
        var sup = await ResolveLineSupplierAsync(c, swappedItem, cancellationToken).ConfigureAwait(false);
        if (sup.Id <= 0)
        {
            return ErpFulfillmentResult.Fail("Could not resolve supplier for storage #" + newStorageId);
        }
        if (sup.Id == poLine.SupplierId)
        {
            return ErpFulfillmentResult.Fail("Line is already assigned to this supplier");
        }

        var writes = 0;
        writes += await ErpDb.ExecuteAsync(
            c, null,
            ErpDb.Positional("UPDATE `epc_erp_po_lines` SET `qty_cancelled` = ?, `time_updated` = ? WHERE `id` = ?"),
            cancellationToken, remaining, Now(), poLine.Id).ConfigureAwait(false);

        var oldPoId = poLine.PoId;
        var openCount = await ErpDb.LongAsync(
            c, null,
            ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_po_lines` WHERE `po_id` = ? AND (`qty` - `qty_received` - `qty_cancelled`) > 0.001"),
            cancellationToken, oldPoId).ConfigureAwait(false);
        if (openCount == 0)
        {
            writes += await SetPoStatusAsync(c, oldPoId, "cancelled", adminId, cancellationToken).ConfigureAwait(false);
        }

        writes += await ErpDb.ExecuteAsync(
            c, null,
            ErpDb.Positional("UPDATE `shop_orders_items` SET `t2_storage_id` = ? WHERE `id` = ? AND `order_id` = ?"),
            cancellationToken, newStorageId, orderItemId, orderId).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                c, null,
                ErpDb.Positional("UPDATE `shop_orders_items_details` SET `storage_id` = ? WHERE `order_item_id` = ?"),
                cancellationToken, newStorageId, orderItemId).ConfigureAwait(false);
        }
        catch (Exception)
        {
        }

        var unitCost = Round(item.PricePurchase, 4);
        var desc = item.Name;
        if (desc.Length == 0) desc = item.Article.Length > 0 ? item.Article : "Line";
        var newLine = new Dictionary<string, object?>
        {
            ["shop_order_item_id"] = orderItemId,
            ["storage_id"] = newStorageId,
            ["description"] = desc,
            ["qty"] = remaining,
            ["unit_cost_ex_vat"] = unitCost,
            ["line_ex_vat"] = Round2(remaining * unitCost),
        };

        var existingPoId = await ErpDb.LongAsync(
            c, null,
            ErpDb.Positional(
                "SELECT p.`id` FROM `epc_erp_purchase_orders` p INNER JOIN `epc_erp_po_lines` pl ON pl.`po_id` = p.`id`"
                + " WHERE p.`order_id` = ? AND p.`supplier_id` = ? AND p.`status` NOT IN ('cancelled','received') AND p.`purchase_id` = 0 LIMIT 1"),
            cancellationToken, orderId, sup.Id).ConfigureAwait(false);
        long newPoId;
        if (existingPoId > 0)
        {
            newPoId = existingPoId;
            writes += await AppendPoLinesAsync(c, existingPoId, [newLine], cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var created = await CreatePoForSupplierAsync(c, orderId, sup.Id, sup.Name, [newLine], adminId, cancellationToken).ConfigureAwait(false);
            newPoId = created.poId;
            writes += created.writes;
        }

        var so = await FindSalesOrderAsync(c, orderId, cancellationToken).ConfigureAwait(false);
        if (so is not null)
        {
            writes += await ErpDb.ExecuteAsync(
                c, null,
                ErpDb.Positional("UPDATE `epc_erp_sales_order_lines` SET `supplier_id` = ? WHERE `sales_order_id` = ? AND `shop_order_item_id` = ?"),
                cancellationToken, sup.Id, so.Id, orderItemId).ConfigureAwait(false);
        }

        await _audit.LogAsync(
            c, null, adminId, "po_supplier_swap", "purchase_order", newPoId, "Supplier swap on order line",
            new Dictionary<string, string?>
            {
                ["shop_order_id"] = orderId.ToString(CultureInfo.InvariantCulture),
                ["order_item_id"] = orderItemId.ToString(CultureInfo.InvariantCulture),
                ["old_po_id"] = oldPoId.ToString(CultureInfo.InvariantCulture),
                ["new_storage_id"] = newStorageId.ToString(CultureInfo.InvariantCulture),
                ["cancelled_qty"] = remaining.ToString(CultureInfo.InvariantCulture),
            },
            cancellationToken).ConfigureAwait(false);
        writes++;

        return ErpFulfillmentResult.Success(new
        {
            order_item_id = orderItemId,
            old_po_id = oldPoId,
            new_po_id = newPoId,
            new_supplier_id = sup.Id,
            cancelled_qty = remaining,
        }, writes);
    }

    // ---- internals (PHP helper order) ----

    private async Task<(long id, bool created, int writes)> CreateSalesOrderAsync(
        DbConnection c, long orderId, OrderRow order, List<ItemRow> items, int adminId, CancellationToken ct)
    {
        var existing = await FindSalesOrderAsync(c, orderId, ct).ConfigureAwait(false);
        if (existing is not null) return (existing.Id, false, 0);

        var customerId = order.UserId;
        if (customerId <= 0)
        {
            throw new ErpWriteException("Guest orders need a customer user_id before ERP sales order bootstrap");
        }

        var lines = new List<(long ItemId, long SupplierId, string Desc, double Qty, double Unit, double Net)>();
        foreach (var item in items)
        {
            var qty = Math.Max(0.0001, Num(item, "count_need"));
            var unit = Round(Num(item, "price"), 4);
            var net = Round2(qty * unit);
            if (net <= 0) continue;
            var sup = await ResolveLineSupplierAsync(c, item, ct).ConfigureAwait(false);
            var desc = Text(item, "t2_name");
            if (desc.Length == 0) desc = Text(item, "t2_article", "Line");
            lines.Add((Id(item), sup.Id, desc, qty, unit, net));
        }

        var amountEx = Round2(lines.Sum(l => l.Net));
        if (amountEx <= 0)
        {
            throw new ErpWriteException("Order has no billable lines for ERP sales order");
        }

        var tax = await _tax.CalcAsync(c, null, (decimal)amountEx, (int)customerId, 0, false, ct).ConfigureAwait(false);
        var now = Now();
        var soNo = await _vouchers.NextAsync(c, null, "SO", ct).ConfigureAwait(false);
        var writes = 0;
        writes += await ErpDb.ExecuteAsync(
            c, null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_sales_orders` (`so_no`,`shop_order_id`,`customer_user_id`,`contact_id`,`title`,`amount_ex_vat`,`vat_amount`,`total_amount`,`status`,`fulfillment_status`,`notes`,`admin_id`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
            ct,
            soNo, orderId, customerId, 0L, "Shop order #" + orderId, (double)tax.AmountExVat, (double)tax.VatAmount, (double)tax.TotalAmount,
            "confirmed", "open", "Auto-linked from commerce order #" + orderId, adminId, now, now).ConfigureAwait(false);
        var soId = await ErpDb.LastInsertIdAsync(c, null, ct).ConfigureAwait(false);

        var lineNo = 1;
        foreach (var l in lines)
        {
            writes += await ErpDb.ExecuteAsync(
                c, null,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_sales_order_lines` (`sales_order_id`,`shop_order_item_id`,`supplier_id`,`line_no`,`description`,`qty`,`unit_price_ex_vat`,`line_ex_vat`) VALUES (?,?,?,?,?,?,?,?)"),
                ct, soId, l.ItemId, l.SupplierId, lineNo++, l.Desc.Length > 255 ? l.Desc[..255] : l.Desc, l.Qty, l.Unit, l.Net).ConfigureAwait(false);
        }

        await _audit.LogAsync(
            c, null, adminId, "so_from_shop_order", "sales_order", soId, "Sales order from shop order",
            new Dictionary<string, string?> { ["shop_order_id"] = orderId.ToString(CultureInfo.InvariantCulture), ["so_no"] = soNo },
            ct).ConfigureAwait(false);
        writes++;
        return (soId, true, writes);
    }

    private async Task<(long poId, int writes)> CreatePoForSupplierAsync(
        DbConnection c, long orderId, long supplierId, string supplierName, List<Dictionary<string, object?>> lines, int adminId, CancellationToken ct)
    {
        var existingId = await ErpDb.LongAsync(
            c, null,
            ErpDb.Positional(
                "SELECT p.`id` FROM `epc_erp_purchase_orders` p INNER JOIN `epc_erp_po_lines` pl ON pl.`po_id` = p.`id`"
                + " WHERE p.`order_id` = ? AND p.`supplier_id` = ? AND p.`status` NOT IN ('cancelled','received') LIMIT 1"),
            ct, orderId, supplierId).ConfigureAwait(false);
        if (existingId > 0) return (existingId, 0);

        var amountEx = Round2(lines.Sum(l => Convert.ToDouble(l["line_ex_vat"], CultureInfo.InvariantCulture)));
        if (amountEx <= 0 || supplierId <= 0) return (0, 0);

        var tax = await _tax.CalcPurchaseAsync(c, null, (decimal)amountEx, (int)supplierId, false, ct).ConfigureAwait(false);
        var now = Now();
        var poNo = await _vouchers.NextAsync(c, null, "PO", ct).ConfigureAwait(false);
        var writes = 0;
        writes += await ErpDb.ExecuteAsync(
            c, null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_purchase_orders` (`po_no`,`voucher_no`,`supplier_id`,`title`,`amount_ex_vat`,`vat_amount`,`total_amount`,`status`,`order_id`,`notes`,`admin_id`,`time_created`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)"),
            ct,
            poNo, poNo, supplierId, "PO for order #" + orderId + " — " + supplierName, (double)tax.AmountExVat, (double)tax.VatAmount, (double)tax.TotalAmount,
            "draft", orderId, "Customer order ref #" + orderId, adminId, now, now).ConfigureAwait(false);
        var poId = await ErpDb.LastInsertIdAsync(c, null, ct).ConfigureAwait(false);

        var lineNo = 1;
        foreach (var l in lines)
        {
            var d = Convert.ToString(l["description"], CultureInfo.InvariantCulture) ?? "Line";
            writes += await ErpDb.ExecuteAsync(
                c, null,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_po_lines` (`po_id`,`shop_order_item_id`,`supplier_id`,`storage_id`,`line_no`,`description`,`qty`,`unit_cost_ex_vat`,`line_ex_vat`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?)"),
                ct, poId, Convert.ToInt64(l["shop_order_item_id"]), supplierId, Convert.ToInt64(l["storage_id"]), lineNo++,
                d.Length > 255 ? d[..255] : d, Convert.ToDouble(l["qty"]), Convert.ToDouble(l["unit_cost_ex_vat"]), Convert.ToDouble(l["line_ex_vat"]), now).ConfigureAwait(false);
        }

        await _audit.LogAsync(
            c, null, adminId, "po_from_shop_order", "purchase_order", poId, "Draft PO from shop order",
            new Dictionary<string, string?> { ["shop_order_id"] = orderId.ToString(CultureInfo.InvariantCulture), ["po_no"] = poNo, ["supplier_id"] = supplierId.ToString(CultureInfo.InvariantCulture) },
            ct).ConfigureAwait(false);
        writes++;
        return (poId, writes);
    }

    private async Task<int> AppendPoLinesAsync(DbConnection c, long poId, List<Dictionary<string, object?>> lines, CancellationToken ct)
    {
        var po = await LoadPoAsync(c, poId, ct).ConfigureAwait(false)
            ?? throw new ErpWriteException("Cannot append lines to this purchase order");
        if (po.Status == "cancelled" || po.PurchaseId > 0)
        {
            throw new ErpWriteException("Cannot append lines to this purchase order");
        }

        var lineNo = await ErpDb.LongAsync(c, null, ErpDb.Positional("SELECT IFNULL(MAX(`line_no`),0) FROM `epc_erp_po_lines` WHERE `po_id` = ?"), ct, poId).ConfigureAwait(false);
        var amountAdd = 0m;
        var writes = 0;
        foreach (var l in lines)
        {
            var lineEx = Round2(Convert.ToDouble(l["line_ex_vat"], CultureInfo.InvariantCulture));
            if (lineEx <= 0) continue;
            var d = Convert.ToString(l["description"], CultureInfo.InvariantCulture) ?? "Line";
            writes += await ErpDb.ExecuteAsync(
                c, null,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_po_lines` (`po_id`,`shop_order_item_id`,`supplier_id`,`storage_id`,`line_no`,`item_code`,`description`,`qty`,`unit_cost_ex_vat`,`line_ex_vat`,`time_updated`) VALUES (?,?,?,?,?,?,?,?,?,?,?)"),
                ct, poId, Convert.ToInt64(l["shop_order_item_id"]), po.SupplierId, Convert.ToInt64(l["storage_id"]), ++lineNo,
                Convert.ToString(l.GetValueOrDefault("item_code"), CultureInfo.InvariantCulture) ?? string.Empty,
                d.Length > 255 ? d[..255] : d, Convert.ToDouble(l["qty"]), Convert.ToDouble(l["unit_cost_ex_vat"]), lineEx, Now()).ConfigureAwait(false);
            amountAdd += (decimal)lineEx;
        }
        if (amountAdd > 0)
        {
            var newEx = Round2(po.AmountExVat + (double)amountAdd);
            var tax = await _tax.CalcPurchaseAsync(c, null, (decimal)newEx, (int)po.SupplierId, false, ct).ConfigureAwait(false);
            writes += await ErpDb.ExecuteAsync(
                c, null,
                ErpDb.Positional("UPDATE `epc_erp_purchase_orders` SET `amount_ex_vat` = ?, `vat_amount` = ?, `total_amount` = ?, `time_updated` = ? WHERE `id` = ?"),
                ct, newEx, (double)tax.VatAmount, (double)tax.TotalAmount, Now(), poId).ConfigureAwait(false);
        }
        return writes;
    }

    private async Task<int> SyncReceivedQtyAsync(DbConnection c, long orderId, CancellationToken ct)
    {
        var writes = 0;
        var lines = new List<(long Id, long ItemId, double Qty)>();
        await using (var r = await ExecReaderAsync(
            c, "SELECT pl.`id`, pl.`shop_order_item_id`, pl.`qty` FROM `epc_erp_po_lines` pl INNER JOIN `epc_erp_purchase_orders` po ON po.`id` = pl.`po_id` WHERE po.`order_id` = ?",
            ct, orderId).ConfigureAwait(false))
        {
            while (await r.ReadAsync(ct).ConfigureAwait(false))
            {
                lines.Add((Convert.ToInt64(r.GetValue(0)), Convert.ToInt64(r.GetValue(1)), Convert.ToDouble(r.GetValue(2), CultureInfo.InvariantCulture)));
            }
        }
        foreach (var l in lines)
        {
            if (l.ItemId <= 0) continue;
            var received = await ReceivedQtyAsync(c, l.ItemId, ct).ConfigureAwait(false);
            var cancelled = Math.Max(0.0, l.Qty - received);
            writes += await ErpDb.ExecuteAsync(
                c, null,
                ErpDb.Positional("UPDATE `epc_erp_po_lines` SET `qty_received` = ?, `qty_cancelled` = ?, `time_updated` = ? WHERE `id` = ?"),
                ct, received, cancelled, Now(), l.Id).ConfigureAwait(false);
        }
        return writes;
    }

    private async Task<int> SyncPoStatusesAsync(DbConnection c, long orderId, int adminId, CancellationToken ct)
        => (await SyncPoStatusListAsync(c, orderId, adminId, ct).ConfigureAwait(false)).writes;

    private async Task<(int writes, List<object> items)> SyncPoStatusListAsync(DbConnection c, long orderId, int adminId, CancellationToken ct)
    {
        var writes = await SyncReceivedQtyAsync(c, orderId, ct).ConfigureAwait(false);
        var items = new List<object>();
        var pos = new List<(long Id, string Status)>();
        await using (var r = await ExecReaderAsync(
            c, "SELECT `id`, `status` FROM `epc_erp_purchase_orders` WHERE `order_id` = ? AND `status` NOT IN ('cancelled','received')",
            ct, orderId).ConfigureAwait(false))
        {
            while (await r.ReadAsync(ct).ConfigureAwait(false))
            {
                pos.Add((Convert.ToInt64(r.GetValue(0)), Convert.ToString(r.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty));
            }
        }
        foreach (var (poId, status) in pos)
        {
            var qtyTotal = await ScalarDoubleAsync(c, null, ErpDb.Positional("SELECT IFNULL(SUM(`qty`),0) FROM `epc_erp_po_lines` WHERE `po_id` = ?"), ct, poId).ConfigureAwait(false);
            var qtyRecv = await ScalarDoubleAsync(c, null, ErpDb.Positional("SELECT IFNULL(SUM(`qty_received`),0) FROM `epc_erp_po_lines` WHERE `po_id` = ?"), ct, poId).ConfigureAwait(false);
            var openQty = await ScalarDoubleAsync(c, null, ErpDb.Positional("SELECT IFNULL(SUM(GREATEST(0, `qty` - `qty_received` - `qty_cancelled`)), 0) FROM `epc_erp_po_lines` WHERE `po_id` = ?"), ct, poId).ConfigureAwait(false);

            var newStatus = status;
            if (qtyTotal > 0 && openQty <= 0.001 && qtyRecv > 0) newStatus = "received";
            else if (qtyRecv > 0 && openQty > 0.001) newStatus = "partial";
            else if (qtyRecv > 0 && openQty <= 0.001) newStatus = "received";
            else if (newStatus == "draft") newStatus = "approved";

            if (!string.Equals(newStatus, status, StringComparison.Ordinal))
            {
                writes += await SetPoStatusAsync(c, poId, newStatus, adminId, ct).ConfigureAwait(false);
                items.Add(new { po_id = poId, status = newStatus });
            }
        }
        return (writes, items);
    }

    private async Task<string> SyncSalesStatusAsync(DbConnection c, long orderId, CancellationToken ct)
    {
        var so = await FindSalesOrderAsync(c, orderId, ct).ConfigureAwait(false);
        if (so is null) return "none";
        if (so.Status == "invoiced") return "invoiced";

        long poTotal = 0, poDone = 0;
        await using (var r = await ExecReaderAsync(
            c, "SELECT COUNT(*), SUM(CASE WHEN `status` = 'received' OR `purchase_id` > 0 THEN 1 ELSE 0 END) FROM `epc_erp_purchase_orders` WHERE `order_id` = ? AND `status` != 'cancelled'",
            ct, orderId).ConfigureAwait(false))
        {
            if (await r.ReadAsync(ct).ConfigureAwait(false))
            {
                poTotal = Convert.ToInt64(r.GetValue(0));
                poDone = r.IsDBNull(1) ? 0 : Convert.ToInt64(r.GetValue(1));
            }
        }

        var orderComplete = await ErpOrderCompletionGuard.IsCompleteAsync(c, orderId, ct).ConfigureAwait(false);
        var fulfillment = "open";
        if (poTotal > 0 && poDone > 0 && poDone < poTotal) fulfillment = "partial";
        else if (poTotal > 0 && poDone >= poTotal) fulfillment = "fulfilled";
        if (orderComplete) fulfillment = "fulfilled";

        await ErpDb.ExecuteAsync(
            c, null,
            ErpDb.Positional("UPDATE `epc_erp_sales_orders` SET `fulfillment_status` = ?, `time_updated` = ? WHERE `id` = ?"),
            ct, fulfillment, Now(), so.Id).ConfigureAwait(false);
        return fulfillment;
    }

    private async Task<object> ReadStatusAsync(DbConnection c, long orderId, string? fulfillment, CancellationToken ct)
    {
        var so = await FindSalesOrderAsync(c, orderId, ct).ConfigureAwait(false);
        var pos = new List<object>();
        await using (var r = await ExecReaderAsync(
            c,
            "SELECT po.`id`, po.`po_no`, po.`supplier_id`, s.`name`, po.`status`, po.`amount_ex_vat`, po.`total_amount`, po.`purchase_id`,"
            + " (SELECT COUNT(*) FROM `epc_erp_po_lines` WHERE `po_id` = po.`id`) FROM `epc_erp_purchase_orders` po"
            + " LEFT JOIN `epc_erp_suppliers` s ON s.`id` = po.`supplier_id` WHERE po.`order_id` = ? ORDER BY po.`id`",
            ct, orderId).ConfigureAwait(false))
        {
            while (await r.ReadAsync(ct).ConfigureAwait(false))
            {
                pos.Add(new
                {
                    id = Convert.ToInt64(r.GetValue(0)),
                    po_no = Convert.ToString(r.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                    supplier_id = Convert.ToInt64(r.GetValue(2)),
                    supplier_name = r.IsDBNull(3) ? null : Convert.ToString(r.GetValue(3), CultureInfo.InvariantCulture),
                    status = Convert.ToString(r.GetValue(4), CultureInfo.InvariantCulture) ?? string.Empty,
                    amount_ex_vat = Convert.ToDecimal(r.GetValue(5), CultureInfo.InvariantCulture),
                    total_amount = Convert.ToDecimal(r.GetValue(6), CultureInfo.InvariantCulture),
                    purchase_id = Convert.ToInt64(r.GetValue(7)),
                    line_count = Convert.ToInt64(r.GetValue(8)),
                });
            }
        }

        var purchaseInvoices = new List<object>();
        await using (var r = await ExecReaderAsync(
            c, "SELECT `id`, `invoice_number`, `supplier_id`, `total_amount`, `status`, `gl_journal_id`, `po_id` FROM `epc_erp_purchases` WHERE `active` = 1 AND `order_id` = ? ORDER BY `id`",
            ct, orderId).ConfigureAwait(false))
        {
            while (await r.ReadAsync(ct).ConfigureAwait(false))
            {
                purchaseInvoices.Add(new
                {
                    id = Convert.ToInt64(r.GetValue(0)),
                    invoice_number = Convert.ToString(r.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                    supplier_id = Convert.ToInt64(r.GetValue(2)),
                    total_amount = Convert.ToDecimal(r.GetValue(3), CultureInfo.InvariantCulture),
                    status = Convert.ToString(r.GetValue(4), CultureInfo.InvariantCulture) ?? string.Empty,
                    gl_journal_id = Convert.ToInt64(r.GetValue(5)),
                    po_id = Convert.ToInt64(r.GetValue(6)),
                });
            }
        }

        object? salesInvoice = null;
        var invId = so?.SalesInvoiceId ?? 0;
        if (invId > 0)
        {
            salesInvoice = await LoadInvoiceAsync(c, "SELECT `id`, `invoice_number`, `validation_ok`, `order_id` FROM `epc_einvoice_documents` WHERE `id` = ? LIMIT 1", ct, invId).ConfigureAwait(false);
        }
        else
        {
            salesInvoice = await LoadInvoiceAsync(c, "SELECT `id`, `invoice_number`, `validation_ok`, `order_id` FROM `epc_einvoice_documents` WHERE `order_id` = ? ORDER BY `id` DESC LIMIT 1", ct, orderId).ConfigureAwait(false);
        }

        object? courier = null;
        object? vat = null;
        try
        {
            var order = await LoadAnyOrderRowAsync(c, orderId, ct).ConfigureAwait(false);
            var userId = order?.UserId ?? 0;
            var regCountry = await ProfileAsync(c, userId, "epc_reg_country", ct).ConfigureAwait(false);
            var dest = ErpDashboardReadService.DestinationCountry(order?.HowGetJson ?? string.Empty, regCountry);
            if (dest.Length == 0) dest = "AE";
            var zeroRated = dest != "AE";
            var courierNet = Math.Max(0m, ErpDashboardReadService.CourierAmount(order?.HowGetJson ?? string.Empty));
            var how = ParseHow(order?.HowGetJson ?? string.Empty);
            courier = new
            {
                unit_net = courierNet,
                line_net = courierNet,
                destination_country = dest,
                charges_customer = true,
                carrier = HowText(how, "carrier"),
                service = HowText(how, "service"),
                city = HowText(how, "city"),
            };
            var custType = await ProfileAsync(c, userId, "epc_customer_type", ct).ConfigureAwait(false);
            var taxStatus = await ProfileAsync(c, userId, "epc_tax_exempt_cert_status", ct).ConfigureAwait(false);
            var vatType = ErpDashboardReadService.ResolveVatType(dest, custType, taxStatus == "approved");
            vat = new
            {
                type = vatType,
                label = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(vatType.Replace('_', ' ')),
                zero_rated = zeroRated,
                documentation = zeroRated
                    ? "Export / non-UAE supply: zero-rated (tax category Z). Keep shipping docs, commercial invoice, and buyer country evidence for FTA."
                    : "UAE domestic supply: standard rate on goods and courier. Tax invoice (PINT-AE) required; keep TRN and invoice XML/PDF.",
            };
        }
        catch (Exception)
        {
            courier = null;
            vat = null;
        }

        var costPosted = purchaseInvoices.Count > 0;
        return new
        {
            shop_order_id = orderId,
            order_complete = await ErpOrderCompletionGuard.IsCompleteAsync(c, orderId, ct).ConfigureAwait(false),
            fulfillment_status = fulfillment ?? so?.FulfillmentStatus ?? "open",
            sales_order = so is null ? null : new
            {
                id = so.Id,
                so_no = so.SoNo,
                status = so.Status,
                fulfillment_status = so.FulfillmentStatus,
                sales_invoice_id = so.SalesInvoiceId,
                amount_ex_vat = so.AmountExVat,
                total_amount = so.TotalAmount,
            },
            purchase_orders = pos,
            purchase_invoices = purchaseInvoices,
            sales_invoice = salesInvoice,
            courier,
            vat,
            accounting = new { cost_posted = costPosted, revenue_posted = salesInvoice is not null },
        };
    }

    private async Task<(long Id, string Name, long StorageId)> ResolveLineSupplierAsync(DbConnection c, ItemRow item, CancellationToken ct)
    {
        var storageId = item.StorageId;
        long supplierId = 0;
        var supplierName = string.Empty;

        var meta = item.JsonParams;
        if (meta.Length > 0 && meta[0] == '{')
        {
            try
            {
                using var doc = JsonDocument.Parse(meta);
                var root = doc.RootElement;
                supplierId = TryLong(root, "apai_supplier_id");
                supplierName = (TryText(root, "apai_supplier_name") ?? TryText(root, "apai_fulfillment_source") ?? string.Empty).Trim();
            }
            catch (JsonException)
            {
            }
        }

        if (supplierId <= 0 && storageId > 0)
        {
            await using var r = await ExecReaderAsync(
                c, "SELECT `id`, `name` FROM `epc_erp_suppliers` WHERE `active` = 1 AND `storage_id` = ? LIMIT 1", ct, storageId).ConfigureAwait(false);
            if (await r.ReadAsync(ct).ConfigureAwait(false))
            {
                supplierId = Convert.ToInt64(r.GetValue(0));
                supplierName = Convert.ToString(r.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }

        if (supplierId <= 0 && storageId > 0)
        {
            var storageName = (await ErpDb.StringAsync(
                c, null, ErpDb.Positional("SELECT `name` FROM `shop_storages` WHERE `id` = ? LIMIT 1"), ct, storageId).ConfigureAwait(false)) ?? string.Empty;
            if (storageName.Trim().Length > 0)
            {
                supplierName = storageName.Trim();
                supplierId = await _suppliers.CreateAsync(
                    new ErpSupplierCreateInput { Name = supplierName, StorageId = storageId, CountryCode = "AE" },
                    ct).ConfigureAwait(false);
            }
        }

        if (supplierName.Length == 0 && supplierId > 0)
        {
            supplierName = (await ErpDb.StringAsync(
                c, null, ErpDb.Positional("SELECT `name` FROM `epc_erp_suppliers` WHERE `id` = ? LIMIT 1"), ct, supplierId).ConfigureAwait(false))?.Trim() ?? string.Empty;
        }
        if (supplierName.Length == 0) supplierName = "Unassigned supplier";

        return (supplierId, supplierName, storageId);
    }

    private async Task<int> SetPoStatusAsync(DbConnection c, long poId, string status, int adminId, CancellationToken ct)
    {
        if (!new[] { "draft", "approved", "partial", "received", "cancelled" }.Contains(status, StringComparer.Ordinal))
        {
            throw new ErpWriteException("Invalid PO status");
        }
        var now = Now();
        var stamp = status switch
        {
            "approved" => ", `approved_at` = " + now,
            "received" => ", `received_at` = " + now,
            _ => string.Empty,
        };
        var writes = await ErpDb.ExecuteAsync(
            c, null,
            "UPDATE `epc_erp_purchase_orders` SET `status` = '" + status + "', `time_updated` = " + now + stamp + " WHERE `id` = " + poId,
            ct).ConfigureAwait(false);
        await SyncPoCaseAsync(poId, new ErpPfActor(adminId, adminId), ct).ConfigureAwait(false);
        return writes;
    }

    private async Task<OrderRow?> LoadOrderAsync(DbConnection c, long orderId, CancellationToken ct)
    {
        await using var r = await ExecReaderAsync(
            c, "SELECT `id`, `user_id`, `how_get_json` FROM `shop_orders` WHERE `id` = ? AND `successfully_created` = 1 LIMIT 1", ct, orderId).ConfigureAwait(false);
        return await r.ReadAsync(ct).ConfigureAwait(false)
            ? new OrderRow(Convert.ToInt64(r.GetValue(0)), Convert.ToInt64(r.GetValue(1)), ReadText(r, 2))
            : null;
    }

    private async Task<OrderRow?> LoadAnyOrderRowAsync(DbConnection c, long orderId, CancellationToken ct)
    {
        await using var r = await ExecReaderAsync(
            c, "SELECT `id`, `user_id`, `how_get_json` FROM `shop_orders` WHERE `id` = ? LIMIT 1", ct, orderId).ConfigureAwait(false);
        return await r.ReadAsync(ct).ConfigureAwait(false)
            ? new OrderRow(Convert.ToInt64(r.GetValue(0)), Convert.ToInt64(r.GetValue(1)), ReadText(r, 2))
            : new OrderRow(orderId, 0, string.Empty);
    }

    private async Task<List<ItemRow>> LoadItemsAsync(DbConnection c, long orderId, CancellationToken ct)
    {
        var items = new List<ItemRow>();
        await using var r = await ExecReaderAsync(
            c, "SELECT `id`, `count_need`, `price`, `t2_storage_id`, `t2_name`, `t2_article`, `t2_price_purchase`, `t2_json_params` FROM `shop_orders_items` WHERE `order_id` = ? ORDER BY `id`",
            ct, orderId).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false))
        {
            items.Add(new ItemRow(r));
        }
        return items;
    }

    private async Task<ItemRow?> LoadItemAsync(DbConnection c, long itemId, long orderId, CancellationToken ct)
    {
        await using var r = await ExecReaderAsync(
            c, "SELECT `id`, `count_need`, `price`, `t2_storage_id`, `t2_name`, `t2_article`, `t2_price_purchase`, `t2_json_params` FROM `shop_orders_items` WHERE `id` = ? AND `order_id` = ? LIMIT 1",
            ct, itemId, orderId).ConfigureAwait(false);
        return await r.ReadAsync(ct).ConfigureAwait(false) ? new ItemRow(r) : null;
    }

    private async Task<double> ReceivedQtyAsync(DbConnection c, long orderItemId, CancellationToken ct)
        => await ScalarDoubleAsync(
            c, null,
            ErpDb.Positional("SELECT IFNULL(SUM(`count_reserved` + `count_issued`), 0) FROM `shop_orders_items_details` WHERE `order_item_id` = ?"),
            ct, orderItemId).ConfigureAwait(false);

    private async Task<SalesOrderRow?> FindSalesOrderAsync(DbConnection c, long orderId, CancellationToken ct)
    {
        await using var r = await ExecReaderAsync(
            c, "SELECT `id`, `so_no`, `status`, `fulfillment_status`, `sales_invoice_id`, `amount_ex_vat`, `total_amount` FROM `epc_erp_sales_orders` WHERE `shop_order_id` = ? ORDER BY `id` DESC LIMIT 1",
            ct, orderId).ConfigureAwait(false);
        return await r.ReadAsync(ct).ConfigureAwait(false)
            ? new SalesOrderRow(
                Convert.ToInt64(r.GetValue(0)),
                Convert.ToString(r.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(r.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(r.GetValue(3), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt64(r.GetValue(4)),
                Convert.ToDouble(r.GetValue(5), CultureInfo.InvariantCulture),
                Convert.ToDouble(r.GetValue(6), CultureInfo.InvariantCulture))
            : null;
    }

    private async Task<Po?> LoadPoAsync(DbConnection c, long poId, CancellationToken ct)
    {
        await using var r = await ExecReaderAsync(
            c, "SELECT `id`, `po_no`, `supplier_id`, `order_id`, `purchase_id`, `status`, `amount_ex_vat`, `voucher_no` FROM `epc_erp_purchase_orders` WHERE `id` = ? LIMIT 1",
            ct, poId).ConfigureAwait(false);
        return await r.ReadAsync(ct).ConfigureAwait(false)
            ? new Po(
                Convert.ToInt64(r.GetValue(0)),
                Convert.ToString(r.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToInt64(r.GetValue(2)),
                Convert.ToInt64(r.GetValue(3)),
                Convert.ToInt64(r.GetValue(4)),
                Convert.ToString(r.GetValue(5), CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToDouble(r.GetValue(6), CultureInfo.InvariantCulture),
                Convert.ToString(r.GetValue(7), CultureInfo.InvariantCulture) ?? string.Empty)
            : null;
    }

    private static async Task<object?> LoadInvoiceAsync(DbConnection c, string sql, CancellationToken ct, params object[] args)
    {
        await using var r = await ExecReaderAsync(c, sql, ct, args).ConfigureAwait(false);
        return await r.ReadAsync(ct).ConfigureAwait(false)
            ? new
            {
                id = Convert.ToInt64(r.GetValue(0)),
                invoice_number = Convert.ToString(r.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                validation_ok = Convert.ToInt64(r.GetValue(2)),
                order_id = Convert.ToInt64(r.GetValue(3)),
            }
            : null;
    }

    private async Task<string> ProfileAsync(DbConnection c, long userId, string key, CancellationToken ct)
    {
        if (userId <= 0) return string.Empty;
        try
        {
            return (await ErpDb.StringAsync(
                c, null,
                ErpDb.Positional("SELECT `data_value` FROM `users_profiles` WHERE `user_id` = ? AND `data_key` = ? LIMIT 1"),
                ct, userId, key).ConfigureAwait(false)) ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static async Task<bool> ProvisionedAsync(DbConnection c, CancellationToken ct)
    {
        foreach (var (table, column) in new[]
        {
            ("epc_erp_sales_orders", "shop_order_id"),
            ("epc_erp_sales_orders", "fulfillment_status"),
            ("epc_erp_sales_order_lines", "shop_order_item_id"),
            ("epc_erp_sales_order_lines", "supplier_id"),
            ("epc_erp_po_lines", "shop_order_item_id"),
            ("epc_erp_purchase_orders", "order_id"),
        })
        {
            var n = await ErpDb.LongAsync(
                c, null,
                ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
                ct, table, column).ConfigureAwait(false);
            if (n <= 0) return false;
        }
        return true;
    }

    private static async Task<double> ScalarDoubleAsync(DbConnection c, DbTransaction? tx, string sql, CancellationToken ct, params object[] args)
    {
        var command = c.CreateCommand();
        command.CommandText = sql;
        if (tx is not null) command.Transaction = tx;
        ErpDb.AddParameters(command, args);
        var v = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return v is null or DBNull ? 0.0 : Convert.ToDouble(v, CultureInfo.InvariantCulture);
    }

    private static async Task<DbDataReader> ExecReaderAsync(DbConnection c, string sql, CancellationToken ct, params object[] args)
    {
        var command = c.CreateCommand();
        command.CommandText = ErpDb.Positional(sql);
        ErpDb.AddParameters(command, args);
        return await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
    }

    private static JsonElement? ParseHow(string json)
    {
        if (json.Length == 0 || json[0] != '{') return null;
        try
        {
            return JsonDocument.Parse(json).RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string HowText(JsonElement? how, string key)
    {
        if (how is null) return string.Empty;
        return how.Value.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String
            ? (p.GetString() ?? string.Empty)
            : string.Empty;
    }

    private static long TryLong(JsonElement root, string key)
        => root.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt64() : 0;

    private static string? TryText(JsonElement root, string key)
        => root.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static string ReadText(DbDataReader r, int ordinal) => r.IsDBNull(ordinal) ? string.Empty : Convert.ToString(r.GetValue(ordinal), CultureInfo.InvariantCulture) ?? string.Empty;

    private static long Id(ItemRow item) => item.Id;

    private static double Num(ItemRow item, string key) => item[key];

    private static string Text(ItemRow item, string key, string fallback = "") => item.Text(key, fallback);

    private static double Round(double v, int digits) => Math.Round(v, digits, MidpointRounding.AwayFromZero);

    private static double Round2(double v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private sealed record OrderRow(long Id, long UserId, string HowGetJson);

    private sealed record ItemRow
    {
        public ItemRow(DbDataReader r)
        {
            Id = Convert.ToInt64(r.GetValue(0));
            QtyNeed = Convert.ToDouble(r.GetValue(1), CultureInfo.InvariantCulture);
            Price = Convert.ToDouble(r.GetValue(2), CultureInfo.InvariantCulture);
            StorageId = Convert.ToInt64(r.GetValue(3));
            Name = ReadText(r, 4);
            Article = ReadText(r, 5);
            PricePurchase = Convert.ToDouble(r.GetValue(6), CultureInfo.InvariantCulture);
            JsonParams = ReadText(r, 7);
        }

        public long Id { get; }
        public double QtyNeed { get; }
        public double Price { get; }
        public long StorageId { get; init; }
        public string Name { get; }
        public string Article { get; }
        public double PricePurchase { get; }
        public string JsonParams { get; }

        public double this[string key] => key switch
        {
            "count_need" => QtyNeed,
            "price" => Price,
            "t2_price_purchase" => PricePurchase,
            _ => 0.0,
        };

        public string Text(string key, string fallback) => key switch
        {
            "t2_name" => Name.Length > 0 ? Name : fallback,
            "t2_article" => Article.Length > 0 ? Article : fallback,
            _ => fallback,
        };
    }

    private sealed record SalesOrderRow(long Id, string SoNo, string Status, string FulfillmentStatus, long SalesInvoiceId, double AmountExVat, double TotalAmount);

    private sealed record Po(long Id, string PoNo, long SupplierId, long OrderId, long PurchaseId, string Status, double AmountExVat, string VoucherNo);

    private sealed record PoLine(long Id, double Qty, long SupplierId, long PoId, string Status, long PurchaseId, string PoNo);
}
