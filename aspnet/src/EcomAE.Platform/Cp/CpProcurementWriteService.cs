using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

public sealed record CpProcurementSupplierInput(
    string Name,
    string VendorCode,
    string Trn,
    string CountryCode,
    bool VatRegistered,
    string ContactEmail,
    string ContactPhone,
    long StorageId,
    string LegalRegNo,
    string LegalRegType,
    string AuthorityName,
    string AddressLine1,
    string City,
    string Emirate,
    string PaymentTerms,
    string Notes);

public sealed record CpProcurementSyncResult(int Created, int Updated);

public sealed record CpProcurementPurchaseFromOrderResult(
    long PurchaseId,
    long OrderId,
    string InvoiceNumber,
    decimal AmountExVat,
    int InventoryLineCount,
    bool InventoryReceiptPosted);

public sealed record CpProcurementAdjustmentResult(long PurchaseId, decimal DeltaExVat, decimal NewTotal, long LedgerId, long GlJournalId);

/// <summary>
/// Write twin of PHP <c>cp/content/shop/procurement/ajax_procurement.php</c>: procurement-specific writes live here,
/// accounting/tax/inventory postings are delegated to the live ERP services.
/// </summary>
public interface ICpProcurementWriteService
{
    Task<long> CreateSupplierAsync(CpProcurementSupplierInput input, CancellationToken cancellationToken = default);

    Task UpdateSupplierAsync(long supplierId, CpProcurementSupplierInput input, CancellationToken cancellationToken = default);

    Task<CpProcurementSyncResult> SyncSuppliersFromWarehousesAsync(CancellationToken cancellationToken = default);

    Task<ErpPurchaseInvoiceResult> CreatePurchaseAsync(ErpPurchaseInvoiceInput input, int adminId, CancellationToken cancellationToken = default);

    Task<ErpCashEntryResult> SupplierPaymentAsync(ErpPaymentVoucherInput input, int adminId, CancellationToken cancellationToken = default);

    Task<long> RecordAdvanceAsync(ErpPaymentVoucherInput input, int adminId, CancellationToken cancellationToken = default);

    Task<CpProcurementPurchaseFromOrderResult> PurchaseFromOrderAsync(long orderId, int supplierId, int adminId, CancellationToken cancellationToken = default);

    Task<ErpCashEntryResult> SupplierSettlementAsync(ErpSupplierSettlementInput input, int adminId, CancellationToken cancellationToken = default);

    Task<CpProcurementAdjustmentResult> PurchaseAdjustmentAsync(long purchaseId, decimal deltaExVat, string reference, string note, bool postGl, int adminId, CancellationToken cancellationToken = default);
}

public sealed class CpProcurementWriteService : ICpProcurementWriteService
{
    private static readonly string[] LegalRegTypes = ["TL", "EID", "PAS", "CD"];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpPurchaseInvoiceWriteService _purchases;
    private readonly IErpCashWriteService _cash;
    private readonly IErpInventoryMovementWriteService _inventory;
    private readonly IErpTaxAmountCalculator _tax;

    public CpProcurementWriteService(
        IErpWriteConnectionFactory connections,
        IErpPurchaseInvoiceWriteService purchases,
        IErpCashWriteService cash,
        IErpInventoryMovementWriteService inventory,
        IErpTaxAmountCalculator tax)
    {
        _connections = connections;
        _purchases = purchases;
        _cash = cash;
        _inventory = inventory;
        _tax = tax;
    }

    /// <summary>PHP <c>epc_uae_vat_normalize_country</c>.</summary>
    public static string NormalizeCountry(string? code)
    {
        var value = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (value.Length == 0 || value is "UAE" or "ARE" or "UNITED ARAB EMIRATES" or "U.A.E." or "U.A.E")
        {
            return "AE";
        }

        return value;
    }

    public static string NormalizeLegalRegType(string? type) =>
        LegalRegTypes.Contains(type ?? string.Empty, StringComparer.Ordinal) ? type! : "TL";

    private void EnsureConfigured()
    {
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("TenantRegistry DB is not configured.");
        }
    }

    /// <summary>PHP <c>epc_procurement_create_supplier</c> (= <c>epc_erp_create_supplier</c> + profile update).</summary>
    public async Task<long> CreateSupplierAsync(CpProcurementSupplierInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureConfigured();
        var name = input.Name.Trim();
        if (name.Length == 0)
        {
            throw new ErpWriteException("Supplier name required");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await CpProcurementSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_erp_suppliers` (`storage_id`, `name`, `contact_email`, `contact_phone`, `trn`, `currency_code`, `country_code`, `vat_registered`, `time_created`) VALUES (?, ?, ?, ?, ?, 'AED', ?, ?, ?)"),
            cancellationToken,
            input.StorageId > 0 ? input.StorageId : null,
            name,
            input.ContactEmail.Trim(),
            input.ContactPhone.Trim(),
            input.Trn.Trim(),
            NormalizeCountry(input.CountryCode),
            input.VatRegistered ? 1 : 0,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        await UpdateSupplierCoreAsync(connection, id, input, cancellationToken).ConfigureAwait(false);
        return id;
    }

    /// <summary>PHP <c>epc_procurement_update_supplier</c>.</summary>
    public async Task UpdateSupplierAsync(long supplierId, CpProcurementSupplierInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureConfigured();
        if (supplierId <= 0)
        {
            throw new ErpWriteException("Invalid supplier");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await CpProcurementSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        await UpdateSupplierCoreAsync(connection, supplierId, input, cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpdateSupplierCoreAsync(DbConnection connection, long id, CpProcurementSupplierInput input, CancellationToken cancellationToken)
    {
        var vendorCode = CpProcurementDeskService.NormalizeVendorCode(input.VendorCode);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_erp_suppliers` SET `name` = ?, `vendor_code` = ?, `contact_email` = ?, `contact_phone` = ?, `trn` = ?,"
                + " `country_code` = ?, `vat_registered` = ?, `storage_id` = ?, `legal_reg_no` = ?, `legal_reg_type` = ?, `authority_name` = ?,"
                + " `address_line1` = ?, `city` = ?, `emirate` = ?, `payment_terms` = ?, `notes` = ? WHERE `id` = ? AND `active` = 1"),
            cancellationToken,
            input.Name.Trim(),
            vendorCode.Length > 0 ? vendorCode : null,
            input.ContactEmail.Trim(),
            input.ContactPhone.Trim(),
            input.Trn.Trim(),
            NormalizeCountry(input.CountryCode),
            input.VatRegistered ? 1 : 0,
            input.StorageId > 0 ? input.StorageId : null,
            input.LegalRegNo.Trim(),
            NormalizeLegalRegType(input.LegalRegType),
            input.AuthorityName.Trim(),
            input.AddressLine1.Trim(),
            input.City.Trim(),
            input.Emirate.Trim(),
            input.PaymentTerms.Trim(),
            input.Notes.Trim(),
            id).ConfigureAwait(false);
    }

    /// <summary>PHP <c>epc_procurement_sync_suppliers_from_warehouses</c> (incl. <c>epc_erp_sync_suppliers_from_storages</c>).</summary>
    public async Task<CpProcurementSyncResult> SyncSuppliersFromWarehousesAsync(CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await CpProcurementSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);

        var storages = new List<(long Id, string Name, string Short)>();
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = "SELECT `id`, IFNULL(`name`,''), IFNULL(`short_name`,'') FROM `shop_storages`";
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                storages.Add((Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture), r.GetString(1), r.GetString(2)));
            }
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var created = 0;
        foreach (var s in storages)
        {
            var name = s.Name.Trim();
            if (name.Length == 0)
            {
                name = s.Short.Trim();
            }

            if (name.Length == 0)
            {
                continue;
            }

            created += await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `epc_erp_suppliers` (`storage_id`, `name`, `time_created`) SELECT ?, ?, ? FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM `epc_erp_suppliers` WHERE `storage_id` = ? AND `active` = 1)"),
                cancellationToken,
                s.Id, name, now, s.Id).ConfigureAwait(false);
        }

        var updated = 0;
        foreach (var s in storages)
        {
            var full = s.Name.Trim();
            var code = CpProcurementDeskService.NormalizeVendorCode(s.Short);
            if (full.Length == 0 && code.Length == 0)
            {
                continue;
            }

            if (full.Length == 0)
            {
                full = code;
            }

            long rowId = 0;
            string curName = "", rawCode = "", vendorAccount = "";
            await using (var c = connection.CreateCommand())
            {
                c.CommandText = ErpDb.Positional("SELECT `id`, IFNULL(`name`,''), IFNULL(`vendor_code`,''), IFNULL(`vendor_account`,'') FROM `epc_erp_suppliers` WHERE `storage_id` = ? AND `active` = 1 LIMIT 1");
                ErpDb.AddParameters(c, s.Id);
                await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rowId = Convert.ToInt64(r.GetValue(0), CultureInfo.InvariantCulture);
                    curName = r.GetString(1).Trim();
                    rawCode = r.GetString(2);
                    vendorAccount = r.GetString(3);
                }
            }

            if (rowId <= 0)
            {
                continue;
            }

            var curCode = CpProcurementDeskService.NormalizeVendorCode(rawCode);
            if (curCode.Length == 0)
            {
                curCode = CpProcurementDeskService.NormalizeVendorCode(vendorAccount);
            }

            var newName = curName;
            if (curName.Length == 0
                || string.Equals(curName, code, StringComparison.OrdinalIgnoreCase)
                || string.Equals(curName, curCode, StringComparison.OrdinalIgnoreCase)
                || (full.Length > curName.Length && curName.Length <= 16))
            {
                newName = full;
            }

            var newCode = code.Length > 0 ? code : curCode;
            if (newName != curName || newCode != CpProcurementDeskService.NormalizeVendorCode(rawCode))
            {
                updated += await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("UPDATE `epc_erp_suppliers` SET `name` = ?, `vendor_code` = ? WHERE `id` = ? AND `active` = 1"),
                    cancellationToken,
                    newName, newCode.Length > 0 ? newCode : null, rowId).ConfigureAwait(false);
            }
        }

        return new CpProcurementSyncResult(created, updated);
    }

    public Task<ErpPurchaseInvoiceResult> CreatePurchaseAsync(ErpPurchaseInvoiceInput input, int adminId, CancellationToken cancellationToken = default)
        => _purchases.CreateAsync(input, adminId, cancellationToken);

    public Task<ErpCashEntryResult> SupplierPaymentAsync(ErpPaymentVoucherInput input, int adminId, CancellationToken cancellationToken = default)
        => _cash.PaymentVoucherAsync(input, adminId, cancellationToken);

    /// <summary>PHP <c>epc_procurement_record_advance</c>: supplier payment with <c>is_advance=1</c> + advances log row.</summary>
    public async Task<long> RecordAdvanceAsync(ErpPaymentVoucherInput input, int adminId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureConfigured();
        var amount = ErpTaxAmountCalculator.Round2(input.Amount);
        if (input.SupplierId <= 0 || amount <= 0m)
        {
            throw new ErpWriteException("Supplier and amount required");
        }

        var reference = input.Reference.Trim();
        var note = input.Note.Trim();
        var cash = await _cash.PaymentVoucherAsync(
            input with
            {
                Amount = amount,
                IsAdvance = true,
                Reference = reference.Length > 0 ? reference : "Advance payment",
                Note = note.Length > 0 ? note : "Procurement advance",
            },
            adminId,
            cancellationToken).ConfigureAwait(false);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await CpProcurementSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_procurement_advances` (`supplier_id`, `time`, `amount`, `reference`, `note`, `cash_entry_id`, `admin_id`) VALUES (?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            input.SupplierId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), amount, reference, note, cash.CashEntryId, adminId).ConfigureAwait(false);
        return await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>PHP <c>epc_erp_purchase_from_order</c> + <c>epc_erp_inventory_receive_purchase</c>.</summary>
    public async Task<CpProcurementPurchaseFromOrderResult> PurchaseFromOrderAsync(long orderId, int supplierId, int adminId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        if (orderId <= 0 || supplierId <= 0)
        {
            throw new ErpWriteException("Order and supplier required");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await CpProcurementSchema.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpOrderCompletionGuard.AssertCompleteAsync(connection, orderId, "Generate purchase from order", cancellationToken).ConfigureAwait(false);

        var exists = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT COUNT(*) FROM `shop_orders` WHERE `id` = ? AND `successfully_created` = 1"), cancellationToken, orderId).ConfigureAwait(false);
        if (exists <= 0)
        {
            throw new ErpWriteException("Order not found");
        }

        var lines = new List<(string Sku, string Name, decimal Qty, decimal UnitCost, long StorageId)>();
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = ErpDb.Positional("SELECT IFNULL(`t2_article`,''), IFNULL(`t2_name`,''), IFNULL(`t2_storage_id`,0), IFNULL(`count_need`,0), IFNULL(`t2_price_purchase`,0) FROM `shop_orders_items` WHERE `order_id` = ?");
            ErpDb.AddParameters(c, orderId);
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                lines.Add((
                    r.GetString(0).Trim(),
                    r.GetString(1).Trim(),
                    Convert.ToDecimal(r.GetValue(3), CultureInfo.InvariantCulture),
                    decimal.Round(Convert.ToDecimal(r.GetValue(4), CultureInfo.InvariantCulture), 4, MidpointRounding.AwayFromZero),
                    Convert.ToInt64(r.GetValue(2), CultureInfo.InvariantCulture)));
            }
        }

        var amount = ErpTaxAmountCalculator.Round2(lines.Sum(l => l.UnitCost * l.Qty));
        if (amount <= 0m)
        {
            throw new ErpWriteException("Order has no purchase cost");
        }

        var inventoryLines = lines.Where(l => l.Sku.Length > 0 && l.Qty > 0).ToList();
        var storageFromOrder = inventoryLines.Select(l => l.StorageId).FirstOrDefault(s => s > 0);
        var vat = await _tax.CalcPurchaseAsync(connection, null, amount, supplierId, false, cancellationToken).ConfigureAwait(false);
        var invoiceNumber = "ORD-" + orderId.ToString(CultureInfo.InvariantCulture);
        var purchase = await _purchases.CreateAsync(
            new ErpPurchaseInvoiceInput
            {
                SupplierId = supplierId,
                OrderId = orderId,
                StorageId = storageFromOrder,
                InvoiceNumber = invoiceNumber,
                AmountExVat = amount,
                Note = "Auto from order #" + orderId.ToString(CultureInfo.InvariantCulture) + (vat.VatApplicable ? string.Empty : " (non-UAE supplier — no input VAT)"),
            },
            adminId,
            cancellationToken).ConfigureAwait(false);

        var posted = false;
        if (inventoryLines.Count > 0)
        {
            posted = await ReceiveInventoryAsync(connection, purchase.PurchaseId, storageFromOrder, inventoryLines, orderId, adminId, cancellationToken).ConfigureAwait(false);
        }

        return new CpProcurementPurchaseFromOrderResult(purchase.PurchaseId, orderId, invoiceNumber, amount, inventoryLines.Count, posted);
    }

    private async Task<bool> ReceiveInventoryAsync(
        DbConnection connection,
        long purchaseId,
        long storageId,
        List<(string Sku, string Name, decimal Qty, decimal UnitCost, long StorageId)> lines,
        long orderId,
        int adminId,
        CancellationToken cancellationToken)
    {
        var defaultWh = await WarehouseByStorageAsync(connection, storageId, cancellationToken).ConfigureAwait(false);
        var resolved = new List<(long Wh, long ItemId, decimal Qty, decimal UnitCost)>();
        foreach (var line in lines)
        {
            var wh = await WarehouseByStorageAsync(connection, line.StorageId, cancellationToken).ConfigureAwait(false);
            if (wh <= 0)
            {
                wh = defaultWh;
            }

            if (wh <= 0)
            {
                return false;
            }

            var itemId = await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT IFNULL((SELECT `id` FROM `epc_erp_inv_items` WHERE `sku` = ? AND `active` = 1 LIMIT 1),0)"), cancellationToken, line.Sku).ConfigureAwait(false);
            if (itemId <= 0)
            {
                var createdItem = await _inventory.CreateItemAsync(new ErpInventoryItemWriteRequest(line.Sku, line.Name.Length > 0 ? line.Name : line.Sku, "standard", "pcs"), cancellationToken).ConfigureAwait(false);
                if (!createdItem.Succeeded)
                {
                    throw new ErpWriteException("Unknown SKU: " + line.Sku);
                }

                itemId = createdItem.Id;
            }

            resolved.Add((wh, itemId, line.Qty, line.UnitCost));
        }

        var reference = "ORD-" + orderId.ToString(CultureInfo.InvariantCulture);
        var posted = 0;
        foreach (var (wh, itemId, qty, unitCost) in resolved)
        {
            var movement = await _inventory.RecordMovementAsync(
                new ErpInventoryMovementWriteRequest(adminId, "purchase_in", wh, itemId, qty, unitCost, Reference: reference, PurchaseId: purchaseId, OrderId: orderId, MovementDate: DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                cancellationToken).ConfigureAwait(false);
            if (!movement.Succeeded)
            {
                throw new ErpWriteException(movement.Message);
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("INSERT INTO `epc_erp_purchase_inv_lines` (`purchase_id`,`warehouse_id`,`item_id`,`qty`,`unit_cost`,`batch_no`,`expiry_date`,`movement_id`) VALUES (?,?,?,?,?,NULL,NULL,?)"),
                cancellationToken,
                purchaseId, wh, itemId, qty, unitCost, movement.Id).ConfigureAwait(false);
            posted++;
        }

        if (posted > 0)
        {
            await ErpDb.ExecuteAsync(connection, null, ErpDb.Positional("UPDATE `epc_erp_purchases` SET `inv_receipt_posted` = 1 WHERE `id` = ?"), cancellationToken, purchaseId).ConfigureAwait(false);
        }

        return posted > 0;
    }

    private static async Task<long> WarehouseByStorageAsync(DbConnection connection, long storageId, CancellationToken cancellationToken)
    {
        if (storageId <= 0)
        {
            return 0;
        }

        try
        {
            return await ErpDb.LongAsync(connection, null, ErpDb.Positional("SELECT IFNULL((SELECT `id` FROM `epc_erp_inv_warehouses` WHERE `storage_id` = ? AND `active` = 1 LIMIT 1),0)"), cancellationToken, storageId).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return 0;
        }
    }

    public Task<ErpCashEntryResult> SupplierSettlementAsync(ErpSupplierSettlementInput input, int adminId, CancellationToken cancellationToken = default)
        => _cash.SupplierSettlementAsync(input, adminId, cancellationToken);

    /// <summary>PHP <c>epc_erp_purchase_adjustment</c>.</summary>
    public async Task<CpProcurementAdjustmentResult> PurchaseAdjustmentAsync(long purchaseId, decimal deltaExVat, string reference, string note, bool postGl, int adminId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        var delta = ErpTaxAmountCalculator.Round2(deltaExVat);
        if (purchaseId <= 0 || Math.Abs(delta) < 0.01m)
        {
            throw new ErpWriteException("Purchase ID and non-zero adjustment amount required");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        int supplierId;
        long orderId;
        decimal amountEx, total;
        await using (var c = connection.CreateCommand())
        {
            c.CommandText = ErpDb.Positional("SELECT `supplier_id`, IFNULL(`order_id`,0), IFNULL(`amount_ex_vat`,0), IFNULL(`total_amount`,0) FROM `epc_erp_purchases` WHERE `id` = ? AND `active` = 1 LIMIT 1");
            ErpDb.AddParameters(c, purchaseId);
            await using var r = await c.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await r.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new ErpWriteException("Purchase not found");
            }

            supplierId = Convert.ToInt32(r.GetValue(0), CultureInfo.InvariantCulture);
            orderId = Convert.ToInt64(r.GetValue(1), CultureInfo.InvariantCulture);
            amountEx = Convert.ToDecimal(r.GetValue(2), CultureInfo.InvariantCulture);
            total = Convert.ToDecimal(r.GetValue(3), CultureInfo.InvariantCulture);
        }

        if (orderId > 0)
        {
            await ErpOrderCompletionGuard.AssertCompleteAsync(connection, orderId, "Purchase adjustment linked to order", cancellationToken).ConfigureAwait(false);
        }

        var newEx = ErpTaxAmountCalculator.Round2(amountEx + delta);
        if (newEx < 0m)
        {
            throw new ErpWriteException("Adjustment would make purchase amount negative");
        }

        var vat = await _tax.CalcPurchaseAsync(connection, null, newEx, supplierId, false, cancellationToken).ConfigureAwait(false);
        var newVat = ErpTaxAmountCalculator.Round2(vat.VatAmount);
        var newTotal = ErpTaxAmountCalculator.Round2(vat.TotalAmount);
        var deltaTotal = ErpTaxAmountCalculator.Round2(newTotal - total);
        var trimmedNote = note.Trim();
        var trimmedRef = reference.Trim();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_purchases` SET `amount_ex_vat` = ?, `vat_amount` = ?, `total_amount` = ?, `vat_applicable` = ?, `vat_rate` = ?, `note` = CONCAT(IFNULL(`note`,''), ?) WHERE `id` = ?"),
            cancellationToken,
            newEx, newVat, newTotal, vat.VatApplicable ? 1 : 0, vat.TaxRate,
            "\n[" + DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " adjustment " + delta.ToString(CultureInfo.InvariantCulture) + "] " + trimmedNote,
            purchaseId).ConfigureAwait(false);

        var settle = await _cash.SupplierSettlementAsync(
            new ErpSupplierSettlementInput
            {
                SupplierId = supplierId,
                Amount = Math.Abs(deltaTotal),
                Direction = deltaTotal >= 0m ? "increase" : "decrease",
                EntryKind = "adjustment",
                PurchaseId = purchaseId,
                OrderId = orderId,
                Reference = trimmedRef.Length > 0 ? trimmedRef : "PUR-ADJ-" + purchaseId.ToString(CultureInfo.InvariantCulture),
                Note = trimmedNote.Length > 0 ? trimmedNote : "Purchase #" + purchaseId.ToString(CultureInfo.InvariantCulture) + " cost adjustment",
                PostGl = postGl,
            },
            adminId,
            cancellationToken).ConfigureAwait(false);

        return new CpProcurementAdjustmentResult(purchaseId, delta, newTotal, settle.LedgerId, settle.GlJournalId);
    }
}
