using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpPurchaseFromOrderWriteResult(
    long PurchaseId,
    long OrderId,
    decimal AmountExVat,
    int InventoryLineCount,
    bool InventoryReceiptPosted);

public sealed record ErpPurchaseAdjustmentWriteResult(
    long PurchaseId,
    decimal DeltaExVat,
    decimal NewTotal,
    long LedgerId,
    long GlJournalId);

/// <summary>
/// Live ASP.NET port of PHP <c>epc_erp_purchase_from_order</c> and <c>epc_erp_purchase_adjustment</c>
/// (<c>epc_erp_helpers.php</c>). Purchase creation delegates to <see cref="IErpPurchaseInvoiceWriteService"/>
/// and the adjustment delta is settled through <see cref="IErpCashWriteService.SupplierSettlementAsync"/>,
/// so numbering, AP ledger, audit and GL behaviour stay on the shared paths. PHP's optional inventory
/// receipt on from-order is not ported here; the result reports the receipt as pending.
/// </summary>
public interface IErpPurchaseFromOrderAdjustWriteService
{
    Task<ErpPurchaseFromOrderWriteResult> FromOrderAsync(
        long orderId,
        int supplierId,
        int adminId,
        CancellationToken cancellationToken = default);

    Task<ErpPurchaseAdjustmentWriteResult> AdjustAsync(
        long purchaseId,
        decimal deltaExVat,
        string? note,
        string? reference,
        bool postGl,
        int adminId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpPurchaseFromOrderAdjustWriteService : IErpPurchaseFromOrderAdjustWriteService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpPurchaseInvoiceWriteService _purchases;
    private readonly IErpTaxAmountCalculator _tax;
    private readonly IErpCashWriteService _cash;

    public ErpPurchaseFromOrderAdjustWriteService(
        IErpWriteConnectionFactory connections,
        IErpPurchaseInvoiceWriteService purchases,
        IErpTaxAmountCalculator tax,
        IErpCashWriteService cash)
    {
        _connections = connections;
        _purchases = purchases;
        _tax = tax;
        _cash = cash;
    }

    public async Task<ErpPurchaseFromOrderWriteResult> FromOrderAsync(
        long orderId,
        int supplierId,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        if (orderId <= 0 || supplierId <= 0)
        {
            throw new ErpWriteException("orderId and supplierId must be positive");
        }

        EnsureConfigured();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpOrderCompletionGuard.AssertCompleteAsync(
            connection,
            orderId,
            "Generate purchase from order",
            cancellationToken).ConfigureAwait(false);

        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_orders` WHERE `id` = ? AND `successfully_created` = 1 LIMIT 1"),
            cancellationToken,
            orderId).ConfigureAwait(false);
        if (exists <= 0)
        {
            throw new ErpWriteException("Order not found");
        }

        var amount = ErpTaxAmountCalculator.Round2(await ErpDb.DecimalAsync(
            connection,
            null,
            ErpDb.Positional(PurchaseExVatSql),
            cancellationToken,
            orderId).ConfigureAwait(false));
        if (amount <= 0m)
        {
            throw new ErpWriteException("Order has no purchase cost");
        }

        var (lineCount, storageId) = await LoadInventoryLinesAsync(connection, orderId, cancellationToken).ConfigureAwait(false);
        var tax = await _tax.CalcPurchaseAsync(connection, null, amount, supplierId, false, cancellationToken).ConfigureAwait(false);
        var reference = "ORD-" + orderId.ToString(CultureInfo.InvariantCulture);
        var note = "Auto from order #" + orderId.ToString(CultureInfo.InvariantCulture)
            + (tax.VatApplicable ? string.Empty : " (non-UAE supplier — no input VAT)");

        var created = await _purchases.CreateAsync(
            new ErpPurchaseInvoiceInput
            {
                SupplierId = supplierId,
                OrderId = orderId,
                StorageId = storageId,
                InvoiceNumber = reference,
                AmountExVat = amount,
                Note = note,
            },
            adminId,
            cancellationToken).ConfigureAwait(false);

        var posted = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `inv_receipt_posted` FROM `epc_erp_purchases` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            created.PurchaseId).ConfigureAwait(false);

        return new ErpPurchaseFromOrderWriteResult(created.PurchaseId, orderId, amount, lineCount, posted > 0);
    }

    public async Task<ErpPurchaseAdjustmentWriteResult> AdjustAsync(
        long purchaseId,
        decimal deltaExVat,
        string? note,
        string? reference,
        bool postGl,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        var delta = ErpTaxAmountCalculator.Round2(deltaExVat);
        if (purchaseId <= 0 || Math.Abs(delta) < 0.01m)
        {
            throw new ErpWriteException("Purchase ID and non-zero adjustment amount required");
        }

        EnsureConfigured();
        var cleanNote = (note ?? string.Empty).Trim();
        var cleanReference = (reference ?? string.Empty).Trim();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var purchase = await LoadPurchaseAsync(connection, purchaseId, cancellationToken).ConfigureAwait(false)
            ?? throw new ErpWriteException("Purchase not found");
        if (purchase.OrderId > 0)
        {
            await ErpOrderCompletionGuard.AssertCompleteAsync(
                connection,
                purchase.OrderId,
                "Purchase adjustment linked to order",
                cancellationToken).ConfigureAwait(false);
        }

        var newEx = ErpTaxAmountCalculator.Round2(purchase.AmountExVat + delta);
        if (newEx < 0m)
        {
            throw new ErpWriteException("Adjustment would make purchase amount negative");
        }

        var tax = await _tax.CalcPurchaseAsync(connection, null, newEx, purchase.SupplierId, false, cancellationToken).ConfigureAwait(false);
        var newVat = ErpTaxAmountCalculator.Round2(tax.VatAmount);
        var newTotal = ErpTaxAmountCalculator.Round2(tax.TotalAmount);
        var deltaTotal = ErpTaxAmountCalculator.Round2(newTotal - purchase.TotalAmount);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_erp_purchases` SET `amount_ex_vat` = ?, `vat_amount` = ?, `total_amount` = ?, `vat_applicable` = ?,"
                + " `vat_rate` = ?, `note` = CONCAT(IFNULL(`note`,''), ?) WHERE `id` = ?"),
            cancellationToken,
            newEx,
            newVat,
            newTotal,
            tax.VatApplicable ? 1 : 0,
            tax.TaxRate,
            NoteSuffix(delta, cleanNote, DateTimeOffset.UtcNow),
            purchaseId).ConfigureAwait(false);

        var settlement = await _cash.SupplierSettlementAsync(
            new ErpSupplierSettlementInput
            {
                SupplierId = purchase.SupplierId,
                Amount = Math.Abs(deltaTotal),
                Direction = deltaTotal >= 0m ? "increase" : "decrease",
                EntryKind = "adjustment",
                PurchaseId = purchaseId,
                OrderId = purchase.OrderId,
                Reference = cleanReference.Length > 0 ? cleanReference : "PUR-ADJ-" + purchaseId.ToString(CultureInfo.InvariantCulture),
                Note = cleanNote.Length > 0 ? cleanNote : "Purchase #" + purchaseId.ToString(CultureInfo.InvariantCulture) + " cost adjustment",
                PostGl = postGl,
            },
            adminId,
            cancellationToken).ConfigureAwait(false);

        return new ErpPurchaseAdjustmentWriteResult(purchaseId, delta, newTotal, settlement.LedgerId, settlement.GlJournalId);
    }

    /// <summary>PHP appends <c>"\n[Y-m-d adjustment {delta}] {note}"</c> to the purchase note.</summary>
    public static string NoteSuffix(decimal delta, string note, DateTimeOffset now)
        => "\n[" + now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " adjustment "
            + delta.ToString("0.##", CultureInfo.InvariantCulture) + "] " + note;

    /// <summary>
    /// PHP <c>epc_erp_order_sum_sql</c> purchase subquery: COGS of counting item lines in
    /// <c>for_finish</c> statuses (<c>where_plain</c> + <c>item_finish_where</c>).
    /// </summary>
    public const string PurchaseExVatSql =
        "SELECT IFNULL(SUM(`t2_price_purchase`*`count_need`),0) FROM `shop_orders_items` WHERE `order_id` = ?"
        + " AND `status` IN (SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `for_finish` = 1)"
        + " AND `status` NOT IN (SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `count_flag` = 0)";

    private static async Task<(int LineCount, long StorageId)> LoadInventoryLinesAsync(
        DbConnection connection,
        long orderId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `t2_article`, `t2_storage_id`, `count_need` FROM `shop_orders_items` WHERE `order_id` = ?");
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@p0";
        parameter.Value = orderId;
        command.Parameters.Add(parameter);
        var count = 0;
        long storage = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var sku = reader.IsDBNull(0) ? string.Empty : reader.GetString(0).Trim();
            var qty = reader.IsDBNull(2) ? 0m : Convert.ToDecimal(reader.GetValue(2), CultureInfo.InvariantCulture);
            if (sku.Length == 0 || qty <= 0m)
            {
                continue;
            }

            var lineStorage = reader.IsDBNull(1) ? 0L : Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
            if (storage <= 0 && lineStorage > 0)
            {
                storage = lineStorage;
            }

            count++;
        }

        return (count, storage);
    }

    private static async Task<(int SupplierId, long OrderId, decimal AmountExVat, decimal TotalAmount)?> LoadPurchaseAsync(
        DbConnection connection,
        long purchaseId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `supplier_id`, `order_id`, `amount_ex_vat`, `total_amount` FROM `epc_erp_purchases`"
            + " WHERE `id` = ? AND `active` = 1 LIMIT 1");
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@p0";
        parameter.Value = purchaseId;
        command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return (
            reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
            reader.IsDBNull(1) ? 0L : reader.GetInt64(1),
            reader.IsDBNull(2) ? 0m : reader.GetDecimal(2),
            reader.IsDBNull(3) ? 0m : reader.GetDecimal(3));
    }

    private void EnsureConfigured()
    {
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("No database");
        }
    }
}
