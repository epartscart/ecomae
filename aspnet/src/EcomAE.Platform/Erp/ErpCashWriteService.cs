using System.Data.Common;
using System.Text.Json;

namespace EcomAE.Platform.Erp;

public sealed record ErpCashEntryInput
{
    public int AccountId { get; init; }

    public decimal Amount { get; init; }

    /// <summary>True increases the account balance (PHP <c>direction</c> = 1 → receipt).</summary>
    public bool Direction { get; init; }

    public string EntryType { get; init; } = string.Empty;

    public string CounterpartyType { get; init; } = "none";

    public int CounterpartyId { get; init; }

    public long OrderId { get; init; }

    public string Reference { get; init; } = string.Empty;

    /// <summary>Explicit voucher number; empty lets PHP-equivalent RV/PV numbering assign one.</summary>
    public string VoucherNo { get; init; } = string.Empty;

    public string Note { get; init; } = string.Empty;

    public long Time { get; init; }
}

public sealed record ErpReceiptVoucherInput
{
    public int UserId { get; init; }

    public int AccountId { get; init; }

    public decimal Amount { get; init; }

    public long SalesOrderId { get; init; }

    public long SalesInvoiceId { get; init; }

    /// <summary>
    /// PHP treats an unallocated receipt as an advance unless <c>is_advance</c> is sent and falsy,
    /// so <c>null</c> (field omitted) means advance here too.
    /// </summary>
    public bool? IsAdvance { get; init; }

    public bool PostGl { get; init; }

    /// <summary>PHP <c>order_id</c> passed through to the customer ledger settlement row.</summary>
    public long OrderId { get; init; }

    /// <summary>PHP <c>auto_allocate</c>: FIFO the receipt across the customer's open invoices.</summary>
    public bool AutoAllocate { get; init; }

    /// <summary>PHP <c>alloc_invoice_id[]</c>.</summary>
    public IReadOnlyList<long>? AllocInvoiceIds { get; init; }

    /// <summary>PHP <c>alloc_amount[]</c>.</summary>
    public IReadOnlyList<decimal>? AllocAmounts { get; init; }

    public string Reference { get; init; } = string.Empty;

    public string Note { get; init; } = string.Empty;

    public long Time { get; init; }
}

public sealed record ErpPaymentVoucherInput
{
    public int SupplierId { get; init; }

    public int AccountId { get; init; }

    public decimal Amount { get; init; }

    public long PurchaseId { get; init; }

    public long PurchaseOrderId { get; init; }

    /// <summary>PHP <c>is_advance</c>: books a supplier prepayment instead of a bill payment.</summary>
    public bool IsAdvance { get; init; }

    /// <summary>PHP <c>auto_allocate</c>: FIFO the payment across the supplier's open bills.</summary>
    public bool AutoAllocate { get; init; }

    public IReadOnlyList<long>? AllocInvoiceIds { get; init; }

    public IReadOnlyList<decimal>? AllocAmounts { get; init; }

    public string Reference { get; init; } = string.Empty;

    public string Note { get; init; } = string.Empty;

    public long Time { get; init; }
}

/// <summary>PHP <c>epc_erp_transfer_voucher</c> payload.</summary>
public sealed record ErpTransferVoucherInput
{
    public int FromAccountId { get; init; }

    public int ToAccountId { get; init; }

    public decimal Amount { get; init; }

    public string Note { get; init; } = string.Empty;

    public long Time { get; init; }
}

public sealed record ErpTransferVoucherResult(string VoucherNo, long OutEntryId, long InEntryId);

public sealed record ErpCashEntryResult(
    long CashEntryId,
    string VoucherNo,
    long GlJournalId,
    long LedgerId,
    bool IsAdvance = false,
    decimal Allocated = 0m,
    decimal Unallocated = 0m);

/// <summary>PHP <c>epc_erp_customer_settlement</c> payload (<c>income</c> = PHP <c>direction=credit</c>).</summary>
public sealed record ErpCustomerSettlementInput
{
    public int UserId { get; init; }

    public decimal Amount { get; init; }

    public bool Income { get; init; }

    public string EntryKind { get; init; } = "adjustment";

    public long OrderId { get; init; }

    public string Reference { get; init; } = string.Empty;

    public string Note { get; init; } = string.Empty;

    public long Time { get; init; }

    public bool PostGl { get; init; }
}

/// <summary>PHP <c>epc_erp_supplier_settlement</c> payload (<c>increase</c> raises the payable).</summary>
public sealed record ErpSupplierSettlementInput
{
    public int SupplierId { get; init; }

    public decimal Amount { get; init; }

    /// <summary>PHP <c>direction</c>: <c>increase</c> or <c>decrease</c> payable.</summary>
    public string Direction { get; init; } = "decrease";

    public string EntryKind { get; init; } = "adjustment";

    public long PurchaseId { get; init; }

    public long OrderId { get; init; }

    public string Reference { get; init; } = string.Empty;

    public string Note { get; init; } = string.Empty;

    public long Time { get; init; }

    public bool PostGl { get; init; }
}

/// <summary>
/// Live ASP.NET port of PHP <c>epc_erp_cash_entry</c>, <c>epc_erp_receipt_voucher</c> and
/// <c>epc_erp_payment_voucher</c> / <c>epc_erp_supplier_payment</c>
/// (<c>content/shop/finance/epc_erp_helpers.php</c>, <c>epc_erp_vouchers.php</c>):
/// same validation, RV/PV/TV numbering, counterparty ledger rows, invoice/bill allocation
/// (<c>epc_erp_settlement.php</c>), VAT-on-advance registers (<c>epc_erp_advances.php</c>)
/// and GL posting.
/// </summary>
public interface IErpCashWriteService
{
    Task<ErpCashEntryResult> CashEntryAsync(ErpCashEntryInput input, int adminId, CancellationToken cancellationToken = default);

    Task<ErpCashEntryResult> ReceiptVoucherAsync(ErpReceiptVoucherInput input, int adminId, CancellationToken cancellationToken = default);

    Task<ErpCashEntryResult> PaymentVoucherAsync(ErpPaymentVoucherInput input, int adminId, CancellationToken cancellationToken = default);

    Task<ErpTransferVoucherResult> TransferVoucherAsync(ErpTransferVoucherInput input, int adminId, CancellationToken cancellationToken = default);

    Task<long> CustomerSettlementAsync(
        DbConnection connection,
        ErpCustomerSettlementInput input,
        int adminId,
        CancellationToken cancellationToken = default);

    Task<ErpCashEntryResult> SupplierSettlementAsync(
        ErpSupplierSettlementInput input,
        int adminId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpCashWriteService : IErpCashWriteService
{
    private static readonly string[] EntryTypes = ["receipt", "payment", "transfer_in", "transfer_out", "adjustment"];

    private static readonly string[] CounterpartyTypes = ["none", "customer", "supplier", "internal"];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpVoucherNumberService _vouchers;
    private readonly IErpGlPostingService _gl;
    private readonly IErpAuditLogWriter _audit;
    private readonly IErpSettlementAllocationService _allocations;
    private readonly IErpAdvanceVatService _advances;

    public ErpCashWriteService(
        IErpWriteConnectionFactory connections,
        IErpVoucherNumberService vouchers,
        IErpGlPostingService gl,
        IErpAuditLogWriter audit,
        IErpSettlementAllocationService allocations,
        IErpAdvanceVatService advances)
    {
        _connections = connections;
        _vouchers = vouchers;
        _gl = gl;
        _audit = audit;
        _allocations = allocations;
        _advances = advances;
    }

    public async Task<ErpCashEntryResult> CashEntryAsync(ErpCashEntryInput input, int adminId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureConfigured();

        var amount = ErpTaxAmountCalculator.Round2(input.Amount);
        if (amount <= 0m || input.AccountId <= 0)
        {
            throw new ErpWriteException("Invalid entry");
        }

        var entryType = ResolveEntryType(input.EntryType, input.Direction);
        var counterpartyType = CounterpartyTypes.Contains(input.CounterpartyType, StringComparer.Ordinal)
            ? input.CounterpartyType
            : "none";
        var time = input.Time > 0 ? input.Time : DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await AssertAccountAsync(connection, input.AccountId, cancellationToken).ConfigureAwait(false);

        var voucherNo = input.VoucherNo.Trim();
        if (voucherNo.Length == 0 && input.Direction && string.Equals(counterpartyType, "customer", StringComparison.Ordinal))
        {
            voucherNo = await _vouchers.NextAsync(connection, null, "RV", cancellationToken).ConfigureAwait(false);
        }
        else if (voucherNo.Length == 0 && !input.Direction && string.Equals(counterpartyType, "supplier", StringComparison.Ordinal))
        {
            voucherNo = await _vouchers.NextAsync(connection, null, "PV", cancellationToken).ConfigureAwait(false);
        }

        var reference = input.Reference.Trim();
        if (reference.Length == 0 && voucherNo.Length > 0)
        {
            reference = voucherNo;
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_cash_bank_entries` (`account_id`, `time`, `entry_type`, `direction`, `amount`,"
                + " `counterparty_type`, `counterparty_id`, `order_id`, `reference`, `note`, `voucher_no`, `admin_id`)"
                + " VALUES (?,?,?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            input.AccountId,
            time,
            entryType,
            input.Direction ? 1 : 0,
            amount,
            counterpartyType,
            input.CounterpartyId,
            input.OrderId,
            reference,
            input.Note.Trim(),
            voucherNo.Length > 0 ? voucherNo : null,
            adminId).ConfigureAwait(false);
        var cashEntryId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);

        var journalId = await TryPostCashEntryAsync(connection, cashEntryId, adminId, cancellationToken).ConfigureAwait(false);
        await LogAsync(connection, adminId, "cash_entry", cashEntryId, "Cash/bank entry saved", voucherNo, amount, cancellationToken).ConfigureAwait(false);
        return new ErpCashEntryResult(cashEntryId, voucherNo, journalId, 0);
    }

    public async Task<ErpCashEntryResult> ReceiptVoucherAsync(ErpReceiptVoucherInput input, int adminId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureConfigured();

        var amount = ErpTaxAmountCalculator.Round2(input.Amount);
        if (input.UserId <= 0 || input.AccountId <= 0 || amount <= 0m)
        {
            throw new ErpWriteException("Customer, bank account, and amount required");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await _advances.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await _allocations.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await AssertAccountAsync(connection, input.AccountId, cancellationToken).ConfigureAwait(false);
        await AssertCustomerAsync(connection, input.UserId, cancellationToken).ConfigureAwait(false);

        // Which open invoices this receipt settles: explicit lines, else FIFO when asked.
        var allocation = ErpSettlementAllocationService.ParseAllocations(input.AllocInvoiceIds, input.AllocAmounts);
        if (allocation.Count == 0 && input.AutoAllocate)
        {
            allocation = ErpSettlementAllocationService.Fifo(
                await _allocations.OpenCustomerInvoicesAsync(connection, input.UserId, cancellationToken).ConfigureAwait(false),
                amount);
        }

        var hasAllocation = allocation.Count > 0;

        // A receipt that settles invoices is never an advance — it knocks off AR.
        var isAdvance = !hasAllocation && (input.IsAdvance ?? true);
        var voucherNo = await _vouchers.NextAsync(connection, null, "RV", cancellationToken).ConfigureAwait(false);
        var time = input.Time > 0 ? input.Time : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var reference = input.Reference.Trim().Length > 0 ? input.Reference.Trim() : voucherNo;
        var note = input.Note.Trim().Length > 0
            ? input.Note.Trim()
            : (isAdvance ? "Customer advance receipt " : "Customer receipt ") + voucherNo;

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_cash_bank_entries` (`account_id`, `time`, `entry_type`, `direction`, `amount`,"
                + " `counterparty_type`, `counterparty_id`, `reference`, `note`, `voucher_no`, `sales_order_id`,"
                + " `sales_invoice_id`, `is_advance`, `admin_id`) VALUES (?,?,'receipt',1,?,'customer',?,?,?,?,?,?,?,?)"),
            cancellationToken,
            input.AccountId,
            time,
            amount,
            input.UserId,
            reference,
            note,
            voucherNo,
            input.SalesOrderId,
            input.SalesInvoiceId,
            isAdvance ? 1 : 0,
            adminId).ConfigureAwait(false);
        var cashEntryId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);

        var ledgerId = await CustomerSettlementAsync(
            connection,
            new ErpCustomerSettlementInput
            {
                UserId = input.UserId,
                Amount = amount,
                Income = false,
                EntryKind = "settlement",
                Reference = voucherNo,
                Note = note,
                Time = time,
                OrderId = input.OrderId,

                // When invoices are knocked off, the cash-entry journal below posts the real
                // Dr Bank / Cr AR movement, so the AR settlement journal must not double-touch AR.
                PostGl = !hasAllocation && input.PostGl && !isAdvance,
            },
            adminId,
            cancellationToken).ConfigureAwait(false);

        var allocated = hasAllocation
            ? await _allocations.ApplyReceiptAllocationsAsync(
                connection,
                cashEntryId,
                voucherNo,
                input.UserId,
                allocation,
                time,
                amount,
                adminId,
                cancellationToken).ConfigureAwait(false)
            : 0m;

        if (isAdvance)
        {
            await _advances.RecordCustomerAdvanceAsync(
                connection,
                cashEntryId,
                input.UserId,
                amount,
                input.SalesOrderId,
                time,
                cancellationToken).ConfigureAwait(false);
            var advanceJournalId = await TryPostAdvanceAsync(connection, cashEntryId, true, adminId, cancellationToken).ConfigureAwait(false);
            await LogAsync(connection, adminId, "receipt_voucher", cashEntryId, "Customer advance receipt posted", voucherNo, amount, cancellationToken).ConfigureAwait(false);
            return new ErpCashEntryResult(cashEntryId, voucherNo, advanceJournalId, ledgerId, true, 0m, amount);
        }

        var journalId = await TryPostCashEntryAsync(connection, cashEntryId, adminId, cancellationToken).ConfigureAwait(false);
        await LogAsync(connection, adminId, "receipt_voucher", cashEntryId, "Customer receipt posted", voucherNo, amount, cancellationToken).ConfigureAwait(false);
        return new ErpCashEntryResult(
            cashEntryId,
            voucherNo,
            journalId,
            ledgerId,
            false,
            allocated,
            ErpTaxAmountCalculator.Round2(amount - allocated));
    }

    public async Task<ErpCashEntryResult> PaymentVoucherAsync(ErpPaymentVoucherInput input, int adminId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureConfigured();

        var amount = ErpTaxAmountCalculator.Round2(input.Amount);
        if (amount <= 0m || input.SupplierId <= 0 || input.AccountId <= 0)
        {
            throw new ErpWriteException("Invalid payment data");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await _advances.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await _allocations.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await AssertAccountAsync(connection, input.AccountId, cancellationToken).ConfigureAwait(false);
        await AssertSupplierAsync(connection, input.SupplierId, cancellationToken).ConfigureAwait(false);

        var voucherNo = input.Reference.Trim();
        if (!voucherNo.StartsWith("PV-", StringComparison.Ordinal))
        {
            voucherNo = await _vouchers.NextAsync(connection, null, "PV", cancellationToken).ConfigureAwait(false);
        }

        // Which open bills this payment settles: explicit lines, else FIFO when asked.
        var allocation = ErpSettlementAllocationService.ParseAllocations(input.AllocInvoiceIds, input.AllocAmounts);
        if (allocation.Count == 0 && input.AutoAllocate)
        {
            allocation = ErpSettlementAllocationService.Fifo(
                await _allocations.OpenSupplierBillsAsync(connection, input.SupplierId, cancellationToken).ConfigureAwait(false),
                amount);
        }

        var time = input.Time > 0 ? input.Time : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var isAdvance = allocation.Count == 0 && input.IsAdvance;
        var note = input.Note.Trim().Length > 0
            ? input.Note.Trim()
            : allocation.Count > 0
                ? "Supplier payment " + voucherNo
                : isAdvance ? "Supplier advance payment" : "Supplier payment";
        long cashEntryId;
        var allocated = 0m;

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_cash_bank_entries` (`account_id`, `time`, `entry_type`, `direction`, `amount`,"
                    + " `counterparty_type`, `counterparty_id`, `purchase_id`, `purchase_order_id`, `reference`, `note`,"
                    + " `voucher_no`, `is_advance`, `admin_id`) VALUES (?,?,'payment',0,?,'supplier',?,?,?,?,?,?,?,?)"),
                cancellationToken,
                input.AccountId,
                time,
                amount,
                input.SupplierId,
                allocation.Count > 0 ? 0L : input.PurchaseId,
                input.PurchaseOrderId,
                voucherNo,
                note,
                voucherNo,
                isAdvance ? 1 : 0,
                adminId).ConfigureAwait(false);
            cashEntryId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

            if (allocation.Count > 0)
            {
                // Allocation path: per-bill payable knock-off, remainder kept on account so the
                // net payable still moves by the full amount.
                allocated = await _allocations.ApplyPaymentAllocationsAsync(
                    connection,
                    transaction,
                    cashEntryId,
                    voucherNo,
                    input.SupplierId,
                    allocation,
                    time,
                    amount,
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                var remainder = ErpTaxAmountCalculator.Round2(amount - allocated);
                if (remainder > 0.005m)
                {
                    await ErpDb.ExecuteAsync(
                        connection,
                        transaction,
                        ErpDb.Positional(
                            "INSERT INTO `epc_erp_supplier_accounting` (`supplier_id`, `time`, `is_credit`, `amount`,"
                            + " `purchase_id`, `cash_entry_id`, `reference`, `note`, `admin_id`, `entry_kind`)"
                            + " VALUES (?,?,0,?,0,?,?,?,?,'settlement')"),
                        cancellationToken,
                        input.SupplierId,
                        time,
                        remainder,
                        cashEntryId,
                        voucherNo,
                        "On-account payment " + voucherNo,
                        adminId).ConfigureAwait(false);
                }
            }
            else
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_erp_supplier_accounting` (`supplier_id`, `time`, `is_credit`, `amount`, `purchase_id`,"
                        + " `cash_entry_id`, `reference`, `note`, `admin_id`, `entry_kind`) VALUES (?,?,0,?,?,?,?,?,?,?)"),
                    cancellationToken,
                    input.SupplierId,
                    time,
                    amount,
                    input.PurchaseId,
                    cashEntryId,
                    voucherNo,
                    note,
                    adminId,
                    isAdvance ? "settlement" : "payment").ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        if (isAdvance)
        {
            await _advances.RecordSupplierAdvanceAsync(
                connection,
                cashEntryId,
                input.SupplierId,
                amount,
                input.PurchaseOrderId,
                time,
                cancellationToken).ConfigureAwait(false);
            var advanceJournalId = await TryPostAdvanceAsync(connection, cashEntryId, false, adminId, cancellationToken).ConfigureAwait(false);
            await LogAsync(connection, adminId, "payment_voucher", cashEntryId, "Supplier advance payment posted", voucherNo, amount, cancellationToken).ConfigureAwait(false);
            return new ErpCashEntryResult(cashEntryId, voucherNo, advanceJournalId, 0, true, 0m, amount);
        }

        var journalId = await TryPostCashEntryAsync(connection, cashEntryId, adminId, cancellationToken).ConfigureAwait(false);
        await LogAsync(connection, adminId, "payment_voucher", cashEntryId, "Supplier payment posted", voucherNo, amount, cancellationToken).ConfigureAwait(false);
        return new ErpCashEntryResult(
            cashEntryId,
            voucherNo,
            journalId,
            0,
            false,
            allocated,
            ErpTaxAmountCalculator.Round2(amount - allocated));
    }

    /// <summary>
    /// PHP <c>epc_erp_transfer_voucher</c>: TV voucher, paired <c>transfer_out</c>/<c>transfer_in</c>
    /// cash entries linked through <c>transfer_pair_id</c>, then best-effort GL on both legs.
    /// </summary>
    public async Task<ErpTransferVoucherResult> TransferVoucherAsync(
        ErpTransferVoucherInput input,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureConfigured();

        var amount = ErpTaxAmountCalculator.Round2(input.Amount);
        if (input.FromAccountId <= 0 || input.ToAccountId <= 0 || input.FromAccountId == input.ToAccountId || amount <= 0m)
        {
            throw new ErpWriteException("Two distinct accounts and a positive amount required");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await AssertAccountAsync(connection, input.FromAccountId, cancellationToken).ConfigureAwait(false);
        await AssertAccountAsync(connection, input.ToAccountId, cancellationToken).ConfigureAwait(false);

        var voucherNo = await _vouchers.NextAsync(connection, null, "TV", cancellationToken).ConfigureAwait(false);
        var time = input.Time > 0 ? input.Time : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var note = input.Note.Trim().Length > 0 ? input.Note.Trim() : "Transfer " + voucherNo;
        long outEntryId;
        long inEntryId;

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_cash_bank_entries` (`account_id`, `time`, `entry_type`, `direction`, `amount`,"
                    + " `counterparty_type`, `counterparty_id`, `reference`, `note`, `voucher_no`, `admin_id`)"
                    + " VALUES (?,?,'transfer_out',0,?,'internal',?,?,?,?,?)"),
                cancellationToken,
                input.FromAccountId,
                time,
                amount,
                input.ToAccountId,
                voucherNo,
                note,
                voucherNo,
                adminId).ConfigureAwait(false);
            outEntryId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_cash_bank_entries` (`account_id`, `time`, `entry_type`, `direction`, `amount`,"
                    + " `counterparty_type`, `counterparty_id`, `transfer_pair_id`, `reference`, `note`, `voucher_no`, `admin_id`)"
                    + " VALUES (?,?,'transfer_in',1,?,'internal',?,?,?,?,?,?)"),
                cancellationToken,
                input.ToAccountId,
                time,
                amount,
                input.FromAccountId,
                outEntryId,
                voucherNo,
                note,
                voucherNo,
                adminId).ConfigureAwait(false);
            inEntryId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `epc_erp_cash_bank_entries` SET `transfer_pair_id` = ? WHERE `id` = ?"),
                cancellationToken,
                inEntryId,
                outEntryId).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        await TryPostCashEntryAsync(connection, outEntryId, adminId, cancellationToken).ConfigureAwait(false);
        await TryPostCashEntryAsync(connection, inEntryId, adminId, cancellationToken).ConfigureAwait(false);
        await LogAsync(connection, adminId, "transfer_voucher", outEntryId, "Cash transfer posted", voucherNo, amount, cancellationToken).ConfigureAwait(false);
        return new ErpTransferVoucherResult(voucherNo, outEntryId, inEntryId);
    }

    /// <summary>
    /// PHP <c>epc_erp_supplier_settlement</c>: AP ledger row on <c>epc_erp_supplier_accounting</c> plus optional GL.
    /// </summary>
    public async Task<ErpCashEntryResult> SupplierSettlementAsync(
        ErpSupplierSettlementInput input,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureConfigured();

        var amount = ErpTaxAmountCalculator.Round2(input.Amount);
        if (input.SupplierId <= 0 || amount <= 0m)
        {
            throw new ErpWriteException("Supplier and positive amount required");
        }

        var direction = input.Direction.Trim();
        if (direction is not ("increase" or "decrease"))
        {
            throw new ErpWriteException("Direction must be increase or decrease payable");
        }

        var entryKind = NormalizeSettlementKind(input.EntryKind);
        if (entryKind == "write_off" && direction == "increase")
        {
            throw new ErpWriteException("Write-off must decrease payable");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        if (input.OrderId > 0)
        {
            await ErpOrderCompletionGuard.AssertCompleteAsync(
                connection,
                input.OrderId,
                "Supplier settlement linked to order",
                cancellationToken).ConfigureAwait(false);
        }

        if (input.PurchaseId > 0)
        {
            var linkedOrder = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `order_id` FROM `epc_erp_purchases` WHERE `id` = ? AND `active` = 1 LIMIT 1"),
                cancellationToken,
                input.PurchaseId).ConfigureAwait(false);
            if (linkedOrder > 0)
            {
                await ErpOrderCompletionGuard.AssertCompleteAsync(
                    connection,
                    linkedOrder,
                    "Supplier settlement linked to purchase order",
                    cancellationToken).ConfigureAwait(false);
            }
        }

        await AssertSupplierAsync(connection, input.SupplierId, cancellationToken).ConfigureAwait(false);

        var isCredit = direction == "increase";
        var reference = input.Reference.Trim();
        var note = input.Note.Trim();
        if (note.Length == 0)
        {
            note = SettlementKindLabel(entryKind) + (reference.Length > 0 ? " — " + reference : string.Empty);
        }

        var time = input.Time > 0 ? input.Time : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_supplier_accounting` (`supplier_id`, `time`, `is_credit`, `amount`, `purchase_id`,"
                + " `order_id`, `reference`, `note`, `admin_id`, `entry_kind`) VALUES (?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            input.SupplierId,
            time,
            isCredit ? 1 : 0,
            amount,
            input.PurchaseId,
            input.OrderId,
            reference,
            note,
            adminId,
            entryKind).ConfigureAwait(false);
        var ledgerId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);

        var journalId = 0L;
        if (input.PostGl)
        {
            journalId = await TryPostApSettlementAsync(
                connection,
                ledgerId,
                amount,
                isCredit,
                entryKind,
                reference,
                note,
                time,
                adminId,
                cancellationToken).ConfigureAwait(false);
            if (journalId > 0)
            {
                await ErpDb.TryExecuteAsync(
                    connection,
                    "ALTER TABLE `epc_erp_supplier_accounting` ADD COLUMN `gl_journal_id` int(11) NOT NULL DEFAULT 0",
                    cancellationToken).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional("UPDATE `epc_erp_supplier_accounting` SET `gl_journal_id` = ? WHERE `id` = ?"),
                    cancellationToken,
                    journalId,
                    ledgerId).ConfigureAwait(false);
            }
        }

        await LogAsync(connection, adminId, "supplier_settlement", ledgerId, note, reference, amount, cancellationToken).ConfigureAwait(false);
        return new ErpCashEntryResult(0, reference, journalId, ledgerId);
    }

    /// <summary>PHP <c>epc_erp_settlement_kinds</c> keys; anything else degrades to <c>adjustment</c>.</summary>
    public static string NormalizeSettlementKind(string? entryKind)
    {
        var kind = (entryKind ?? string.Empty).Trim();
        return kind is "settlement" or "write_off" ? kind : "adjustment";
    }

    /// <summary>PHP <c>epc_erp_settlement_kinds</c> labels, used for the default ledger note.</summary>
    public static string SettlementKindLabel(string entryKind) => entryKind switch
    {
        "settlement" => "Settlement (non-cash close-off)",
        "write_off" => "Write-off",
        _ => "Adjustment / correction",
    };

    /// <summary>AP side of PHP <c>epc_erp_gl_post_ap_settlement</c>: 2000 payable vs 6100 expense.</summary>
    private async Task<long> TryPostApSettlementAsync(
        DbConnection connection,
        long ledgerId,
        decimal amount,
        bool isCredit,
        string entryKind,
        string reference,
        string note,
        long time,
        int adminId,
        CancellationToken cancellationToken)
    {
        var payable = await CoaIdAsync(connection, "2000", cancellationToken).ConfigureAwait(false);
        var expense = await CoaIdAsync(connection, "6100", cancellationToken).ConfigureAwait(false);
        if (payable <= 0 || expense <= 0)
        {
            return 0L;
        }

        var lines = isCredit
            ? new List<ErpGlLine>
            {
                new(expense, amount, 0m, "Supplier charge — " + entryKind),
                new(payable, 0m, amount, "Accounts payable increase"),
            }
            : new List<ErpGlLine>
            {
                new(payable, amount, 0m, "Accounts payable decrease"),
                new(expense, 0m, amount, "Supplier credit — " + entryKind),
            };

        try
        {
            return await _gl.PostJournalAsync(
                connection,
                new ErpGlJournalHeader
                {
                    JournalDate = time,
                    Reference = reference,
                    Description = note.Length > 0 ? note : "Supplier AP settlement",
                    SourceType = "adjustment",
                    SourceId = ledgerId,
                },
                lines,
                adminId,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ErpWriteException)
        {
            // PHP swallows settlement GL failures — the ledger row still stands.
            return 0L;
        }
        catch (DbException)
        {
            return 0L;
        }
    }

    /// <summary>PHP <c>epc_erp_cash_entry</c> entry-type resolution (direction decides unless overridden).</summary>
    public static string ResolveEntryType(string? entryType, bool direction)
    {
        var requested = (entryType ?? string.Empty).Trim();
        return EntryTypes.Contains(requested, StringComparer.Ordinal)
            ? requested
            : direction ? "receipt" : "payment";
    }

    /// <summary>PHP <c>epc_erp_customer_settlement</c>: AR ledger row on <c>shop_users_accounting</c> plus optional GL.</summary>
    public async Task<long> CustomerSettlementAsync(
        DbConnection connection,
        ErpCustomerSettlementInput input,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(input);

        var userId = input.UserId;
        var amount = ErpTaxAmountCalculator.Round2(input.Amount);
        var income = input.Income;
        var entryKind = NormalizeSettlementKind(input.EntryKind);
        var reference = input.Reference.Trim();
        var note = input.Note.Trim();
        var time = input.Time > 0 ? input.Time : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var postGl = input.PostGl;
        if (userId <= 0 || amount <= 0m)
        {
            throw new ErpWriteException("Customer and positive amount required");
        }

        if (input.OrderId > 0)
        {
            await ErpOrderCompletionGuard.AssertCompleteAsync(
                connection,
                input.OrderId,
                "Customer settlement linked to order",
                cancellationToken).ConfigureAwait(false);
        }

        if (entryKind == "write_off" && income)
        {
            throw new ErpWriteException("Write-off must reduce customer balance (debit direction)");
        }

        if (note.Length == 0)
        {
            note = SettlementKindLabel(entryKind) + (reference.Length > 0 ? " — " + reference : string.Empty);
        }

        await EnsureAccountingCodesAsync(connection, cancellationToken).ConfigureAwait(false);
        var codeId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `shop_accounting_codes` WHERE `key` = ? LIMIT 1"),
            cancellationToken,
            income ? "epc_erp_ar_credit" : "epc_erp_ar_debit").ConfigureAwait(false);
        if (codeId <= 0)
        {
            throw new ErpWriteException("ERP accounting code missing — run ERP setup");
        }

        var detail = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["entry_kind"] = entryKind,
            ["reference"] = reference,
            ["erp"] = 1,
        });

        // The shop ledger predates ERP: older tenant databases have neither the
        // tech_value_text detail column nor order_id (PHP probes SHOW COLUMNS too).
        var columns = new List<string> { "user_id", "time", "income", "amount", "operation_code", "active", "office_id" };
        var values = new List<object?> { userId, time, income ? 1 : 0, amount, codeId, 1, 0 };
        if (await HasColumnAsync(connection, "shop_users_accounting", "order_id", cancellationToken).ConfigureAwait(false))
        {
            columns.Add("order_id");
            values.Add(input.OrderId);
        }

        if (await HasColumnAsync(connection, "shop_users_accounting", "tech_value_text", cancellationToken).ConfigureAwait(false))
        {
            columns.Add("tech_value_text");
            values.Add(detail);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `shop_users_accounting` (`" + string.Join("`,`", columns) + "`) VALUES ("
                + string.Join(',', Enumerable.Repeat("?", columns.Count)) + ")"),
            cancellationToken,
            [.. values]).ConfigureAwait(false);

        var ledgerId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);

        if (!postGl)
        {
            return ledgerId;
        }

        var receivable = await CoaIdAsync(connection, "1100", cancellationToken).ConfigureAwait(false);
        var expense = await CoaIdAsync(connection, "6100", cancellationToken).ConfigureAwait(false);
        if (receivable <= 0 || expense <= 0)
        {
            return ledgerId;
        }

        var lines = income
            ? new List<ErpGlLine>
            {
                new(expense, amount, 0m, "Customer credit — " + entryKind),
                new(receivable, 0m, amount, "Customer ledger credit"),
            }
            : new List<ErpGlLine>
            {
                new(receivable, amount, 0m, "Customer ledger debit"),
                new(expense, 0m, amount, "Customer debit — " + entryKind),
            };

        try
        {
            await _gl.PostJournalAsync(
                connection,
                new ErpGlJournalHeader
                {
                    JournalDate = time,
                    Reference = reference,
                    Description = note.Length > 0 ? note : "Customer AR settlement",
                    SourceType = "adjustment",
                    SourceId = ledgerId,
                },
                lines,
                adminId,
                cancellationToken).ConfigureAwait(false);
        }
        catch (ErpWriteException)
        {
            // PHP swallows settlement GL failures — the ledger row still stands.
        }
        catch (DbException)
        {
        }

        return ledgerId;
    }

    private async Task<long> TryPostCashEntryAsync(DbConnection connection, long cashEntryId, int adminId, CancellationToken cancellationToken)
    {
        try
        {
            return await _gl.PostCashEntryAsync(connection, cashEntryId, adminId, cancellationToken).ConfigureAwait(false);
        }
        catch (ErpWriteException)
        {
            // PHP posts the cash entry then swallows GL failures (missing COA, closed period).
            return 0L;
        }
        catch (DbException)
        {
            return 0L;
        }
    }

    /// <summary>PHP wraps the advance journals in try/catch as well — the cash entry stands either way.</summary>
    private async Task<long> TryPostAdvanceAsync(
        DbConnection connection,
        long cashEntryId,
        bool receipt,
        int adminId,
        CancellationToken cancellationToken)
    {
        try
        {
            return receipt
                ? await _advances.PostAdvanceReceiptGlAsync(connection, cashEntryId, adminId, cancellationToken).ConfigureAwait(false)
                : await _advances.PostAdvancePaymentGlAsync(connection, cashEntryId, adminId, cancellationToken).ConfigureAwait(false);
        }
        catch (ErpWriteException)
        {
            return 0L;
        }
        catch (DbException)
        {
            return 0L;
        }
    }

    private Task LogAsync(
        DbConnection connection,
        int adminId,
        string action,
        long cashEntryId,
        string summary,
        string voucherNo,
        decimal amount,
        CancellationToken cancellationToken)
        => _audit.LogAsync(
            connection,
            null,
            adminId,
            action,
            "cash_entry",
            cashEntryId,
            summary,
            new Dictionary<string, string?>
            {
                ["voucher_no"] = voucherNo,
                ["amount"] = amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            },
            cancellationToken);

    /// <summary>Port of PHP <c>epc_erp_ensure_accounting_codes</c>: seed the AR settlement codes on first use.</summary>
    private static async Task EnsureAccountingCodesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        foreach (var (key, income, name) in new[]
        {
            ("epc_erp_ar_credit", 1, "ERP — customer credit (settlement/adjustment)"),
            ("epc_erp_ar_debit", 0, "ERP — customer debit (settlement/adjustment)"),
        })
        {
            var existing = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `shop_accounting_codes` WHERE `key` = ? LIMIT 1"),
                cancellationToken,
                key).ConfigureAwait(false);
            if (existing > 0)
            {
                continue;
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `shop_accounting_codes` (`income`, `name`, `manual_available`, `key`) VALUES (?, ?, 1, ?)"),
                cancellationToken,
                income,
                name,
                key).ConfigureAwait(false);
        }
    }

    private static async Task<bool> HasColumnAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        try
        {
            var found = await ErpDb.StringAsync(
                connection,
                null,
                ErpDb.Positional("SHOW COLUMNS FROM `" + table + "` LIKE ?"),
                cancellationToken,
                column).ConfigureAwait(false);
            return found is not null;
        }
        catch (DbException)
        {
            return false;
        }
    }

    private static Task<long> CoaIdAsync(DbConnection connection, string code, CancellationToken cancellationToken)
        => ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = ? AND `active` = 1 LIMIT 1"),
            cancellationToken,
            code);

    private static async Task AssertAccountAsync(DbConnection connection, int accountId, CancellationToken cancellationToken)
    {
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_cash_bank_accounts` WHERE `id` = ? AND `active` = 1 LIMIT 1"),
            cancellationToken,
            accountId).ConfigureAwait(false);
        if (exists <= 0)
        {
            throw new ErpWriteException("Cash/bank account not found");
        }
    }

    private static async Task AssertCustomerAsync(DbConnection connection, int userId, CancellationToken cancellationToken)
    {
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `user_id` FROM `users` WHERE `user_id` = ? LIMIT 1"),
            cancellationToken,
            userId).ConfigureAwait(false);
        if (exists <= 0)
        {
            throw new ErpWriteException("Customer not found");
        }
    }

    private static async Task AssertSupplierAsync(DbConnection connection, int supplierId, CancellationToken cancellationToken)
    {
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_suppliers` WHERE `id` = ? AND `active` = 1 LIMIT 1"),
            cancellationToken,
            supplierId).ConfigureAwait(false);
        if (exists <= 0)
        {
            throw new ErpWriteException("Supplier not found");
        }
    }

    private void EnsureConfigured()
    {
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("No database");
        }
    }

    /// <summary>Subset of PHP <c>epc_erp_ensure_schema</c> / <c>epc_erp_vouchers_ensure_schema</c> for the cash tables.</summary>
    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_cash_bank_entries` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `account_id` int(11) NOT NULL,"
            + " `time` int(11) NOT NULL,"
            + " `entry_type` enum('receipt','payment','transfer_in','transfer_out','adjustment') NOT NULL DEFAULT 'receipt',"
            + " `direction` tinyint(1) NOT NULL COMMENT '1=increase balance, 0=decrease',"
            + " `amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `counterparty_type` enum('none','customer','supplier','internal') NOT NULL DEFAULT 'none',"
            + " `counterparty_id` int(11) NOT NULL DEFAULT 0,"
            + " `order_id` int(11) NOT NULL DEFAULT 0,"
            + " `purchase_id` int(11) NOT NULL DEFAULT 0,"
            + " `transfer_pair_id` int(11) NOT NULL DEFAULT 0,"
            + " `reference` varchar(128) DEFAULT NULL,"
            + " `note` text,"
            + " `admin_id` int(11) NOT NULL DEFAULT 0,"
            + " `active` tinyint(1) NOT NULL DEFAULT 1,"
            + " PRIMARY KEY (`id`),"
            + " KEY `x_account` (`account_id`,`active`),"
            + " KEY `x_time` (`time`),"
            + " KEY `x_order` (`order_id`),"
            + " KEY `x_purchase` (`purchase_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP cash/bank journal entries'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_supplier_accounting` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `supplier_id` int(11) NOT NULL,"
            + " `time` int(11) NOT NULL,"
            + " `is_credit` tinyint(1) NOT NULL COMMENT '1=invoice/payable up, 0=payment down',"
            + " `amount` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `purchase_id` int(11) NOT NULL DEFAULT 0,"
            + " `cash_entry_id` int(11) NOT NULL DEFAULT 0,"
            + " `order_id` int(11) NOT NULL DEFAULT 0,"
            + " `reference` varchar(128) DEFAULT NULL,"
            + " `note` text,"
            + " `admin_id` int(11) NOT NULL DEFAULT 0,"
            + " `active` tinyint(1) NOT NULL DEFAULT 1,"
            + " PRIMARY KEY (`id`),"
            + " KEY `x_supplier` (`supplier_id`,`active`,`is_credit`),"
            + " KEY `x_purchase` (`purchase_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP supplier payable ledger'",
            cancellationToken).ConfigureAwait(false);

        // Additive columns PHP adds through epc_erp_schema_add_column_if_missing.
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_erp_supplier_accounting` ADD `entry_kind` varchar(32) NOT NULL DEFAULT 'invoice'",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_erp_cash_bank_entries` ADD `voucher_no` varchar(32) DEFAULT NULL",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_erp_cash_bank_entries` ADD `sales_order_id` int(11) NOT NULL DEFAULT 0",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_erp_cash_bank_entries` ADD `sales_invoice_id` int(11) NOT NULL DEFAULT 0",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_erp_cash_bank_entries` ADD `is_advance` tinyint(1) NOT NULL DEFAULT 0",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_erp_cash_bank_entries` ADD `purchase_order_id` int(11) NOT NULL DEFAULT 0",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_erp_cash_bank_entries` ADD `gl_journal_id` int(11) NOT NULL DEFAULT 0",
            cancellationToken).ConfigureAwait(false);
    }
}
