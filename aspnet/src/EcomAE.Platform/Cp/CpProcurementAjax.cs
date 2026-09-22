using System.Globalization;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;

namespace EcomAE.Platform.Cp;

public sealed record CpProcurementAjaxResult(bool Ok, string Message, object Payload);

/// <summary>
/// Action switch of PHP <c>cp/content/shop/procurement/ajax_procurement.php</c>: same action names, same
/// <c>{status, message, ...extra}</c> JSON shape.
/// </summary>
public static class CpProcurementAjax
{
    public static readonly IReadOnlyList<string> Actions =
    [
        "create_supplier",
        "update_supplier",
        "sync_suppliers",
        "create_purchase",
        "supplier_payment",
        "record_advance",
        "purchase_from_order",
        "supplier_settlement",
        "purchase_adjustment",
    ];

    public static async Task<CpProcurementAjaxResult> DispatchAsync(
        string action,
        IFormCollection form,
        ICpProcurementWriteService writes,
        int adminId,
        CancellationToken cancellationToken)
    {
        switch (action)
        {
            case "create_supplier":
            {
                var id = await writes.CreateSupplierAsync(SupplierInput(form), cancellationToken).ConfigureAwait(false);
                return Ok("Supplier created", new { status = true, message = "Supplier created", id });
            }

            case "update_supplier":
            {
                await writes.UpdateSupplierAsync(LiveWriteFormBinder.Long(form, "supplier_id"), SupplierInput(form), cancellationToken).ConfigureAwait(false);
                return Ok("Supplier profile saved", new { status = true, message = "Supplier profile saved" });
            }

            case "sync_suppliers":
            {
                var r = await writes.SyncSuppliersFromWarehousesAsync(cancellationToken).ConfigureAwait(false);
                var msg = "Warehouses synced: " + r.Created.ToString(CultureInfo.InvariantCulture) + " new supplier(s), "
                    + r.Updated.ToString(CultureInfo.InvariantCulture) + " name/code refresh(es)";
                return Ok(msg, new { status = true, message = msg, created = r.Created, updated = r.Updated });
            }

            case "create_purchase":
            {
                var r = await writes.CreatePurchaseAsync(
                    new ErpPurchaseInvoiceInput
                    {
                        SupplierId = LiveWriteFormBinder.Int(form, "supplier_id"),
                        OrderId = LiveWriteFormBinder.Long(form, "order_id"),
                        StorageId = LiveWriteFormBinder.Long(form, "storage_id"),
                        InvoiceNumber = LiveWriteFormBinder.Text(form, "invoice_number"),
                        PurchaseDate = UnixDate(LiveWriteFormBinder.Text(form, "purchase_date")),
                        AmountExVat = LiveWriteFormBinder.Dec(form, "amount_ex_vat"),
                        Import = LiveWriteFormBinder.Flag(form, "import"),
                        Note = LiveWriteFormBinder.Text(form, "note"),
                    },
                    adminId,
                    cancellationToken).ConfigureAwait(false);
                return Ok("Purchase bill recorded", new { status = true, message = "Purchase bill recorded", id = r.PurchaseId, vat_amount = r.VatAmount, total_amount = r.TotalAmount });
            }

            case "supplier_payment":
            {
                var r = await writes.SupplierPaymentAsync(PaymentInput(form), adminId, cancellationToken).ConfigureAwait(false);
                return Ok("Supplier payment recorded", new { status = true, message = "Supplier payment recorded", cash_entry_id = r.CashEntryId });
            }

            case "record_advance":
            {
                var id = await writes.RecordAdvanceAsync(PaymentInput(form), adminId, cancellationToken).ConfigureAwait(false);
                return Ok("Advance payment recorded", new { status = true, message = "Advance payment recorded", id });
            }

            case "purchase_from_order":
            {
                var r = await writes.PurchaseFromOrderAsync(
                    LiveWriteFormBinder.Long(form, "order_id"),
                    LiveWriteFormBinder.Int(form, "supplier_id"),
                    adminId,
                    cancellationToken).ConfigureAwait(false);
                var msg = "Purchase bill generated from order";
                if (r.InventoryLineCount > 0)
                {
                    msg += r.InventoryReceiptPosted
                        ? " — inventory received (" + r.InventoryLineCount.ToString(CultureInfo.InvariantCulture) + " line(s))"
                        : " — inventory NOT posted (link the order storage to an ERP warehouse)";
                }

                return Ok(msg, new
                {
                    status = true,
                    message = msg,
                    purchase_id = r.PurchaseId,
                    order_id = r.OrderId,
                    invoice_number = r.InvoiceNumber,
                    amount_ex_vat = r.AmountExVat,
                    inventory_lines = r.InventoryLineCount,
                    inventory_receipt_posted = r.InventoryReceiptPosted ? 1 : 0,
                });
            }

            case "supplier_settlement":
            {
                var r = await writes.SupplierSettlementAsync(
                    new ErpSupplierSettlementInput
                    {
                        SupplierId = LiveWriteFormBinder.Int(form, "supplier_id"),
                        Amount = LiveWriteFormBinder.Dec(form, "amount"),
                        Direction = FirstNonEmpty(LiveWriteFormBinder.Text(form, "direction"), "decrease"),
                        EntryKind = FirstNonEmpty(LiveWriteFormBinder.Text(form, "entry_kind"), "adjustment"),
                        PurchaseId = LiveWriteFormBinder.Long(form, "purchase_id"),
                        OrderId = LiveWriteFormBinder.Long(form, "order_id"),
                        Reference = LiveWriteFormBinder.Text(form, "reference"),
                        Note = LiveWriteFormBinder.Text(form, "note"),
                        PostGl = LiveWriteFormBinder.Flag(form, "post_gl"),
                    },
                    adminId,
                    cancellationToken).ConfigureAwait(false);
                return Ok("Supplier adjustment posted", new { status = true, message = "Supplier adjustment posted", ledger_id = r.LedgerId, gl_journal_id = r.GlJournalId });
            }

            case "purchase_adjustment":
            {
                var r = await writes.PurchaseAdjustmentAsync(
                    LiveWriteFormBinder.Long(form, "purchase_id"),
                    LiveWriteFormBinder.Dec(form, "delta_ex_vat"),
                    LiveWriteFormBinder.Text(form, "reference"),
                    LiveWriteFormBinder.Text(form, "note"),
                    LiveWriteFormBinder.Flag(form, "post_gl"),
                    adminId,
                    cancellationToken).ConfigureAwait(false);
                return Ok("Purchase adjusted", new { status = true, message = "Purchase adjusted", purchase_id = r.PurchaseId, delta_ex_vat = r.DeltaExVat, new_total = r.NewTotal, ledger_id = r.LedgerId, gl_journal_id = r.GlJournalId });
            }

            default:
                return new CpProcurementAjaxResult(false, "Unknown action", new { status = false, message = "Unknown action" });
        }
    }

    private static CpProcurementAjaxResult Ok(string message, object payload) => new(true, message, payload);

    private static string FirstNonEmpty(string value, string fallback) => value.Trim().Length > 0 ? value.Trim() : fallback;

    private static CpProcurementSupplierInput SupplierInput(IFormCollection form) => new(
        LiveWriteFormBinder.Text(form, "name"),
        LiveWriteFormBinder.Text(form, "vendor_code"),
        LiveWriteFormBinder.Text(form, "trn"),
        LiveWriteFormBinder.Text(form, "country_code"),
        LiveWriteFormBinder.Flag(form, "vat_registered"),
        LiveWriteFormBinder.Text(form, "contact_email"),
        LiveWriteFormBinder.Text(form, "contact_phone"),
        LiveWriteFormBinder.Long(form, "storage_id"),
        LiveWriteFormBinder.Text(form, "legal_reg_no"),
        LiveWriteFormBinder.Text(form, "legal_reg_type"),
        LiveWriteFormBinder.Text(form, "authority_name"),
        LiveWriteFormBinder.Text(form, "address_line1"),
        LiveWriteFormBinder.Text(form, "city"),
        LiveWriteFormBinder.Text(form, "emirate"),
        LiveWriteFormBinder.Text(form, "payment_terms"),
        LiveWriteFormBinder.Text(form, "notes"));

    private static ErpPaymentVoucherInput PaymentInput(IFormCollection form) => new()
    {
        SupplierId = LiveWriteFormBinder.Int(form, "supplier_id"),
        AccountId = LiveWriteFormBinder.Int(form, "account_id"),
        Amount = LiveWriteFormBinder.Dec(form, "amount"),
        PurchaseId = LiveWriteFormBinder.Long(form, "purchase_id"),
        IsAdvance = LiveWriteFormBinder.Flag(form, "is_advance"),
        Reference = LiveWriteFormBinder.Text(form, "reference"),
        Note = LiveWriteFormBinder.Text(form, "note"),
    };

    private static long UnixDate(string raw)
    {
        if (DateTime.TryParseExact(raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d))
        {
            return new DateTimeOffset(d).ToUnixTimeSeconds();
        }

        return 0;
    }
}
