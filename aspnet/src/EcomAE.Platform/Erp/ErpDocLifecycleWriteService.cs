using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpVoidResult(IReadOnlyList<long> ReversalJournalIds, IReadOnlyList<long> VoidedIds);

/// <summary>PHP <c>epc_erp_purchase_amend</c> payload — absent fields keep the stored value.</summary>
public sealed record ErpPurchaseAmendInput
{
    public long PurchaseId { get; init; }

    public string? InvoiceNumber { get; init; }

    public string? Note { get; init; }

    /// <summary>Draft purchases only: re-prices the document through the tenant tax kit.</summary>
    public decimal? AmountExVat { get; init; }
}

/// <summary>
/// Live ASP.NET port of PHP <c>content/shop/finance/epc_erp_doc_lifecycle.php</c>:
/// draft documents may be deleted or edited, posted documents are voided with a
/// reversing journal and an audit trail, and posted narrative is amend-only.
/// </summary>
public interface IErpDocLifecycleWriteService
{
    Task<ErpVoidResult> CashVoucherVoidAsync(
        long entryId,
        string reason,
        int adminId,
        CancellationToken cancellationToken = default);

    Task CashVoucherAmendAsync(
        long entryId,
        string? reference,
        string? note,
        int adminId,
        CancellationToken cancellationToken = default);

    Task<ErpVoidResult> PurchaseVoidAsync(
        long purchaseId,
        string reason,
        int adminId,
        CancellationToken cancellationToken = default);

    Task PurchaseDeleteAsync(long purchaseId, int adminId, CancellationToken cancellationToken = default);

    Task PurchaseAmendAsync(ErpPurchaseAmendInput input, int adminId, CancellationToken cancellationToken = default);

    Task InvoiceCancelAsync(
        long invoiceId,
        string reason,
        int adminId,
        CancellationToken cancellationToken = default);

    Task InvoiceDeleteAsync(long invoiceId, int adminId, CancellationToken cancellationToken = default);

    Task SalesOrderCancelAsync(
        long salesOrderId,
        string reason,
        int adminId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpDocLifecycleWriteService : IErpDocLifecycleWriteService
{
    private const decimal Epsilon = 0.005m;

    private static readonly string[] VoidablePurchaseStatuses = ["confirmed", "paid", "partial", "draft"];

    private static readonly string[] CancellableInvoiceStatuses = ["draft", "validated", "rejected"];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpGlLedgerWriteService _ledger;
    private readonly IErpTaxAmountCalculator _tax;
    private readonly IErpAuditLogWriter _audit;

    public ErpDocLifecycleWriteService(
        IErpWriteConnectionFactory connections,
        IErpGlLedgerWriteService ledger,
        IErpTaxAmountCalculator tax,
        IErpAuditLogWriter audit)
    {
        _connections = connections;
        _ledger = ledger;
        _tax = tax;
        _audit = audit;
    }

    public async Task<ErpVoidResult> CashVoucherVoidAsync(
        long entryId,
        string reason,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var entry = await CashEntryAsync(connection, entryId, cancellationToken).ConfigureAwait(false)
            ?? throw new ErpWriteException("Voucher / cash entry not found");
        if (entry.Active != 1 || entry.VoidedAt > 0)
        {
            throw new ErpWriteException("This voucher cannot be voided (already voided or inactive)");
        }

        var trimmedReason = (reason ?? string.Empty).Trim();
        if (trimmedReason.Length == 0)
        {
            trimmedReason = "Voided by operator";
        }

        // A transfer voucher writes a paired out/in row; PHP voids both sides together.
        var ids = new List<long> { entryId };
        if (entry.TransferPairId > 0 && entry.TransferPairId != entryId)
        {
            ids.Add(entry.TransferPairId);
        }

        var journals = new List<long>();
        foreach (var id in ids)
        {
            var journalId = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `gl_journal_id` FROM `epc_erp_cash_bank_entries` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                id).ConfigureAwait(false);
            if (journalId <= 0)
            {
                journalId = await SourceJournalIdAsync(connection, "cash", id, cancellationToken).ConfigureAwait(false);
            }

            if (journalId > 0)
            {
                journals.Add(journalId);
            }
        }

        var label = entry.VoucherNo.Trim().Length > 0
            ? entry.VoucherNo.Trim()
            : entry.Reference.Trim().Length > 0
                ? entry.Reference.Trim()
                : "#" + entryId.ToString(CultureInfo.InvariantCulture);
        var reversals = await ReverseJournalsAsync(
            connection,
            journals,
            "Void " + label + " — " + trimmedReason,
            adminId,
            cancellationToken).ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var id in ids)
        {
            await UnwindSettlementsAsync(connection, id, cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_erp_cash_bank_entries` SET `active` = 0, `voided_at` = ?, `void_reason` = ?,"
                    + " `voided_by` = ?, `reversal_journal_id` = ? WHERE `id` = ?"),
                cancellationToken,
                now,
                Truncate(trimmedReason, 255),
                adminId,
                reversals.Count > 0 ? reversals[0] : 0L,
                id).ConfigureAwait(false);
        }

        await _audit.LogAsync(
            connection,
            null,
            adminId,
            "void",
            "cash_entry",
            entryId,
            trimmedReason,
            new Dictionary<string, string?>
            {
                ["voided_ids"] = Join(ids),
                ["reversal_journal_ids"] = Join(reversals),
            },
            cancellationToken).ConfigureAwait(false);
        return new ErpVoidResult(reversals, ids);
    }

    public async Task CashVoucherAmendAsync(
        long entryId,
        string? reference,
        string? note,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var entry = await CashEntryAsync(connection, entryId, cancellationToken).ConfigureAwait(false);
        if (entry is null || entry.Active != 1 || entry.VoidedAt > 0)
        {
            throw new ErpWriteException("Voucher cannot be amended");
        }

        // Amounts, accounts and direction stay locked on a posted voucher.
        var newReference = reference is null ? entry.Reference : reference.Trim();
        var newNote = note is null ? entry.Note : note.Trim();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_erp_cash_bank_entries` SET `reference` = ?, `note` = ? WHERE `id` = ? AND `active` = 1"),
            cancellationToken,
            newReference,
            newNote,
            entryId).ConfigureAwait(false);

        await _audit.LogAsync(
            connection,
            null,
            adminId,
            "amend",
            "cash_entry",
            entryId,
            "Narrative amended",
            new Dictionary<string, string?>
            {
                ["reference"] = newReference,
                ["note"] = newNote,
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ErpVoidResult> PurchaseVoidAsync(
        long purchaseId,
        string reason,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var purchase = await PurchaseAsync(connection, purchaseId, cancellationToken).ConfigureAwait(false)
            ?? throw new ErpWriteException("Purchase invoice not found");
        if (purchase.Active != 1
            || purchase.VoidedAt > 0
            || !VoidablePurchaseStatuses.Contains(purchase.Status, StringComparer.Ordinal))
        {
            throw new ErpWriteException("Purchase invoice cannot be voided");
        }

        var trimmedReason = (reason ?? string.Empty).Trim();
        if (trimmedReason.Length == 0)
        {
            trimmedReason = "Voided by operator";
        }

        var journals = new List<long>();
        var journalId = purchase.GlJournalId > 0
            ? purchase.GlJournalId
            : await SourceJournalIdAsync(connection, "purchase", purchaseId, cancellationToken).ConfigureAwait(false);
        if (journalId > 0)
        {
            journals.Add(journalId);
        }

        var label = purchase.InvoiceNumber.Trim().Length > 0
            ? purchase.InvoiceNumber.Trim()
            : "#" + purchaseId.ToString(CultureInfo.InvariantCulture);
        var reversals = await ReverseJournalsAsync(
            connection,
            journals,
            "Void PI " + label + " — " + trimmedReason,
            adminId,
            cancellationToken).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_erp_supplier_accounting` SET `active` = 0 WHERE `purchase_id` = ? AND `active` = 1"),
            cancellationToken,
            purchaseId).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_erp_purchases` SET `active` = 0, `status` = 'voided', `voided_at` = ?, `void_reason` = ?,"
                + " `voided_by` = ?, `reversal_journal_id` = ? WHERE `id` = ?"),
            cancellationToken,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Truncate(trimmedReason, 255),
            adminId,
            reversals.Count > 0 ? reversals[0] : 0L,
            purchaseId).ConfigureAwait(false);

        await _audit.LogAsync(
            connection,
            null,
            adminId,
            "void",
            "purchase",
            purchaseId,
            trimmedReason,
            new Dictionary<string, string?> { ["reversal_journal_ids"] = Join(reversals) },
            cancellationToken).ConfigureAwait(false);
        return new ErpVoidResult(reversals, [purchaseId]);
    }

    public async Task PurchaseDeleteAsync(long purchaseId, int adminId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var purchase = await PurchaseAsync(connection, purchaseId, cancellationToken).ConfigureAwait(false)
            ?? throw new ErpWriteException("Purchase invoice not found");
        if (!string.Equals(purchase.Status, "draft", StringComparison.Ordinal)
            || purchase.GlJournalId > 0
            || purchase.Active != 1)
        {
            throw new ErpWriteException(
                "Only draft unposted purchases can be deleted — use Void for posted invoices");
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `epc_erp_supplier_accounting` WHERE `purchase_id` = ?"),
                cancellationToken,
                purchaseId).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `epc_erp_purchases` WHERE `id` = ?"),
                cancellationToken,
                purchaseId).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        await _audit.LogAsync(
            connection,
            null,
            adminId,
            "delete",
            "purchase",
            purchaseId,
            "Draft purchase deleted",
            null,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task PurchaseAmendAsync(
        ErpPurchaseAmendInput input,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureConfigured();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var purchase = await PurchaseAsync(connection, input.PurchaseId, cancellationToken).ConfigureAwait(false);
        if (purchase is null || purchase.Active != 1 || purchase.VoidedAt > 0)
        {
            throw new ErpWriteException("Purchase cannot be amended");
        }

        var note = input.Note is null ? purchase.Note : input.Note.Trim();
        var invoiceNumber = input.InvoiceNumber is null ? purchase.InvoiceNumber : input.InvoiceNumber.Trim();
        var draftEditable = string.Equals(purchase.Status, "draft", StringComparison.Ordinal)
            && purchase.GlJournalId <= 0;

        if (draftEditable && input.AmountExVat.HasValue)
        {
            var amountEx = ErpTaxAmountCalculator.Round2(input.AmountExVat.Value);
            var tax = await _tax.CalcPurchaseAsync(
                connection,
                null,
                amountEx,
                purchase.SupplierId,
                false,
                cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_erp_purchases` SET `invoice_number` = ?, `amount_ex_vat` = ?, `vat_amount` = ?,"
                    + " `total_amount` = ?, `vat_rate` = ?, `note` = ? WHERE `id` = ? AND `status` = 'draft'"),
                cancellationToken,
                invoiceNumber,
                amountEx,
                tax.VatAmount,
                tax.TotalAmount,
                tax.TaxRate,
                note,
                input.PurchaseId).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "UPDATE `epc_erp_purchases` SET `invoice_number` = ?, `note` = ? WHERE `id` = ? AND `active` = 1"),
                cancellationToken,
                invoiceNumber,
                note,
                input.PurchaseId).ConfigureAwait(false);
        }

        await _audit.LogAsync(
            connection,
            null,
            adminId,
            "amend",
            "purchase",
            input.PurchaseId,
            "Purchase amended",
            new Dictionary<string, string?>
            {
                ["invoice_number"] = invoiceNumber,
                ["note"] = note,
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task InvoiceCancelAsync(
        long invoiceId,
        string reason,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        var invoice = await InvoiceAsync(connection, invoiceId, cancellationToken).ConfigureAwait(false)
            ?? throw new ErpWriteException("Invoice not found");
        if (invoice.Active != 1 || !CancellableInvoiceStatuses.Contains(invoice.Status, StringComparer.Ordinal))
        {
            throw new ErpWriteException("Submitted invoices cannot be cancelled — issue a credit note instead");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `epc_einvoice_documents` SET `status` = 'cancelled', `active` = 0, `time_updated` = ?"
                + " WHERE `id` = ? AND `status` NOT IN ('submitted','accepted','queued')"),
            cancellationToken,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            invoiceId).ConfigureAwait(false);

        await _audit.LogAsync(
            connection,
            null,
            adminId,
            "cancel",
            "invoice",
            invoiceId,
            (reason ?? string.Empty).Trim().Length > 0 ? reason!.Trim() : "Invoice cancelled",
            null,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task InvoiceDeleteAsync(long invoiceId, int adminId, CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        var invoice = await InvoiceAsync(connection, invoiceId, cancellationToken).ConfigureAwait(false)
            ?? throw new ErpWriteException("Invoice not found");
        if (!string.Equals(invoice.Status, "draft", StringComparison.Ordinal))
        {
            throw new ErpWriteException("Only draft invoices can be deleted — cancel or credit-note others");
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `epc_einvoice_lines` WHERE `document_id` = ?"),
                cancellationToken,
                invoiceId).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("DELETE FROM `epc_einvoice_documents` WHERE `id` = ? AND `status` = 'draft'"),
                cancellationToken,
                invoiceId).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        await _audit.LogAsync(
            connection,
            null,
            adminId,
            "delete",
            "invoice",
            invoiceId,
            "Draft invoice deleted",
            null,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task SalesOrderCancelAsync(
        long salesOrderId,
        string reason,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        var order = await SalesOrderAsync(connection, salesOrderId, cancellationToken).ConfigureAwait(false)
            ?? throw new ErpWriteException("Sales order not found");
        if (!(string.Equals(order.Status, "draft", StringComparison.Ordinal)
                || string.Equals(order.Status, "confirmed", StringComparison.Ordinal)))
        {
            throw new ErpWriteException("Sales order cannot be cancelled");
        }

        if (order.SalesInvoiceId > 0 || string.Equals(order.Status, "invoiced", StringComparison.Ordinal))
        {
            throw new ErpWriteException(
                "Invoiced sales orders cannot be cancelled — issue a credit note on the invoice");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_sales_orders` SET `status` = 'cancelled', `time_updated` = ? WHERE `id` = ?"),
            cancellationToken,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            salesOrderId).ConfigureAwait(false);

        await _audit.LogAsync(
            connection,
            null,
            adminId,
            "cancel",
            "sales_order",
            salesOrderId,
            (reason ?? string.Empty).Trim().Length > 0 ? reason!.Trim() : "Sales order cancelled",
            null,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>PHP <c>epc_erp_doc_reverse_journals</c>: reverse once, reuse the mirror afterwards.</summary>
    private async Task<List<long>> ReverseJournalsAsync(
        DbConnection connection,
        IReadOnlyList<long> journalIds,
        string note,
        int adminId,
        CancellationToken cancellationToken)
    {
        var reversals = new List<long>();
        foreach (var journalId in journalIds.Where(id => id > 0).Distinct())
        {
            var already = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional(
                    "SELECT COALESCE(`reversed_by_journal_id`, 0) FROM `epc_erp_gl_journals` WHERE `id` = ? LIMIT 1"),
                cancellationToken,
                journalId).ConfigureAwait(false);
            if (already > 0)
            {
                reversals.Add(already);
                continue;
            }

            var reversal = await _ledger.ReverseJournalAsync(
                connection,
                journalId,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                note.Trim().Length > 0 ? note : "Document void",
                adminId,
                cancellationToken).ConfigureAwait(false);
            reversals.Add(reversal.JournalId);
        }

        return reversals;
    }

    /// <summary>PHP <c>epc_erp_doc_unwind_settlements</c>: give the paid amount back to the invoices.</summary>
    private static async Task UnwindSettlementsAsync(
        DbConnection connection,
        long cashEntryId,
        CancellationToken cancellationToken)
    {
        if (cashEntryId <= 0)
        {
            return;
        }

        var allocations = new List<(long Id, long InvoiceId, decimal Amount, string DocType)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = ErpDb.Positional(
                "SELECT `id`, `invoice_id`, `amount`, `doc_type` FROM `epc_erp_settlement_allocations`"
                + " WHERE `cash_entry_id` = ? AND `active` = 1");
            ErpDb.AddParameters(command, cashEntryId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                allocations.Add((
                    reader.GetInt64(0),
                    reader.IsDBNull(1) ? 0L : reader.GetInt64(1),
                    reader.IsDBNull(2) ? 0m : ErpTaxAmountCalculator.Round2(reader.GetDecimal(2)),
                    reader.IsDBNull(3) ? string.Empty : reader.GetString(3)));
            }
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var allocation in allocations)
        {
            if (allocation.Amount > Epsilon
                && allocation.InvoiceId > 0
                && string.Equals(allocation.DocType, "ar", StringComparison.Ordinal))
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        "UPDATE `epc_einvoice_documents`"
                        + " SET `amount_due` = ROUND(`total_incl_vat` - GREATEST(0, `paid_amount` - ?), 2),"
                        + " `paid_amount` = GREATEST(0, ROUND(`paid_amount` - ?, 2)), `time_updated` = ?"
                        + " WHERE `id` = ?"),
                    cancellationToken,
                    allocation.Amount,
                    allocation.Amount,
                    now,
                    allocation.InvoiceId).ConfigureAwait(false);
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_erp_settlement_allocations` SET `active` = 0 WHERE `id` = ?"),
                cancellationToken,
                allocation.Id).ConfigureAwait(false);
        }

        foreach (var table in new[] { "epc_erp_supplier_accounting", "shop_users_accounting" })
        {
            try
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        "UPDATE `" + table + "` SET `active` = 0 WHERE `cash_entry_id` = ? AND `active` = 1"),
                    cancellationToken,
                    cashEntryId).ConfigureAwait(false);
            }
            catch (DbException)
            {
                // Sub-ledger table absent on this tenant — PHP swallows the same failure.
            }
        }
    }

    private static async Task<long> SourceJournalIdAsync(
        DbConnection connection,
        string sourceType,
        long sourceId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional(
                    "SELECT `id` FROM `epc_erp_gl_journals` WHERE `source_type` = ? AND `source_id` = ? AND `active` = 1"
                    + " AND COALESCE(`reversed_by_journal_id`, 0) = 0 LIMIT 1"),
                cancellationToken,
                sourceType,
                sourceId).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return 0L;
        }
    }

    private static async Task<CashEntryRow?> CashEntryAsync(
        DbConnection connection,
        long entryId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `active`, COALESCE(`voided_at`, 0), COALESCE(`transfer_pair_id`, 0), COALESCE(`voucher_no`, ''),"
            + " COALESCE(`reference`, ''), COALESCE(`note`, '')"
            + " FROM `epc_erp_cash_bank_entries` WHERE `id` = ? LIMIT 1");
        ErpDb.AddParameters(command, entryId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new CashEntryRow(
            reader.GetInt32(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5));
    }

    private static async Task<PurchaseRow?> PurchaseAsync(
        DbConnection connection,
        long purchaseId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `active`, COALESCE(`voided_at`, 0), COALESCE(`status`, ''), COALESCE(`gl_journal_id`, 0),"
            + " COALESCE(`invoice_number`, ''), COALESCE(`note`, ''), COALESCE(`supplier_id`, 0)"
            + " FROM `epc_erp_purchases` WHERE `id` = ? LIMIT 1");
        ErpDb.AddParameters(command, purchaseId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new PurchaseRow(
            reader.GetInt32(0),
            reader.GetInt64(1),
            reader.GetString(2),
            reader.GetInt64(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetInt32(6));
    }

    private static async Task<(int Active, string Status)?> InvoiceAsync(
        DbConnection connection,
        long invoiceId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional(
                "SELECT `active`, COALESCE(`status`, '') FROM `epc_einvoice_documents` WHERE `id` = ? LIMIT 1");
            ErpDb.AddParameters(command, invoiceId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            return (reader.GetInt32(0), reader.GetString(1));
        }
        catch (DbException)
        {
            // E-invoicing was never installed on this tenant.
            return null;
        }
    }

    /// <summary>Sales orders carry no <c>active</c> flag; PHP's guard defaults it to 1 for this table.</summary>
    private static async Task<(string Status, long SalesInvoiceId)?> SalesOrderAsync(
        DbConnection connection,
        long salesOrderId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT COALESCE(`status`, ''), COALESCE(`sales_invoice_id`, 0)"
            + " FROM `epc_erp_sales_orders` WHERE `id` = ? LIMIT 1");
        ErpDb.AddParameters(command, salesOrderId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return (reader.GetString(0), reader.GetInt64(1));
    }

    private static string Join(IReadOnlyList<long> values)
        => string.Join(",", values.Select(v => v.ToString(CultureInfo.InvariantCulture)));

    private static string Truncate(string value, int length)
        => value.Length <= length ? value : value[..length];

    private void EnsureConfigured()
    {
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("No database");
        }
    }

    /// <summary>Port of PHP <c>epc_erp_doc_lifecycle_ensure_schema</c>.</summary>
    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        foreach (var statement in new[]
        {
            "ALTER TABLE `epc_erp_cash_bank_entries` ADD `voided_at` int(11) NOT NULL DEFAULT 0",
            "ALTER TABLE `epc_erp_cash_bank_entries` ADD `void_reason` varchar(255) DEFAULT NULL",
            "ALTER TABLE `epc_erp_cash_bank_entries` ADD `voided_by` int(11) NOT NULL DEFAULT 0",
            "ALTER TABLE `epc_erp_cash_bank_entries` ADD `reversal_journal_id` int(11) NOT NULL DEFAULT 0",
            "ALTER TABLE `epc_erp_purchases` ADD `voided_at` int(11) NOT NULL DEFAULT 0",
            "ALTER TABLE `epc_erp_purchases` ADD `void_reason` varchar(255) DEFAULT NULL",
            "ALTER TABLE `epc_erp_purchases` ADD `voided_by` int(11) NOT NULL DEFAULT 0",
            "ALTER TABLE `epc_erp_purchases` ADD `reversal_journal_id` int(11) NOT NULL DEFAULT 0",
            "ALTER TABLE `epc_erp_purchases` MODIFY `status`"
            + " enum('draft','confirmed','paid','partial','voided') NOT NULL DEFAULT 'confirmed'",
            "ALTER TABLE `epc_erp_gl_journals` ADD `reversed_by_journal_id` int(11) NOT NULL DEFAULT 0",
            "ALTER TABLE `epc_erp_settlement_allocations` ADD `active` tinyint(1) NOT NULL DEFAULT 1",
        })
        {
            await ErpDb.TryExecuteAsync(connection, statement, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record CashEntryRow(
        int Active,
        long VoidedAt,
        long TransferPairId,
        string VoucherNo,
        string Reference,
        string Note);

    private sealed record PurchaseRow(
        int Active,
        long VoidedAt,
        string Status,
        long GlJournalId,
        string InvoiceNumber,
        string Note,
        int SupplierId);
}
