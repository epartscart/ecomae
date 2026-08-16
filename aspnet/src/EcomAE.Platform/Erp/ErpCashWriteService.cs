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

    public string Reference { get; init; } = string.Empty;

    public string Note { get; init; } = string.Empty;

    public long Time { get; init; }
}

public sealed record ErpCashEntryResult(long CashEntryId, string VoucherNo, long GlJournalId, long LedgerId);

/// <summary>PHP <c>epc_erp_customer_settlement</c> payload (<c>income</c> = PHP <c>direction=credit</c>).</summary>
public sealed record ErpCustomerSettlementInput
{
    public int UserId { get; init; }

    public decimal Amount { get; init; }

    public bool Income { get; init; }

    public string EntryKind { get; init; } = "adjustment";

    public string Reference { get; init; } = string.Empty;

    public string Note { get; init; } = string.Empty;

    public long Time { get; init; }

    public bool PostGl { get; init; }
}

/// <summary>
/// Live ASP.NET port of PHP <c>epc_erp_cash_entry</c>, <c>epc_erp_receipt_voucher</c> and
/// <c>epc_erp_payment_voucher</c> / <c>epc_erp_supplier_payment</c>
/// (<c>content/shop/finance/epc_erp_helpers.php</c>, <c>epc_erp_vouchers.php</c>):
/// same validation, RV/PV numbering, counterparty ledger rows and GL posting.
/// Invoice-allocation (FIFO settlement) and VAT-on-advance registers stay PHP-only for now
/// and are refused rather than silently skipped.
/// </summary>
public interface IErpCashWriteService
{
    Task<ErpCashEntryResult> CashEntryAsync(ErpCashEntryInput input, int adminId, CancellationToken cancellationToken = default);

    Task<ErpCashEntryResult> ReceiptVoucherAsync(ErpReceiptVoucherInput input, int adminId, CancellationToken cancellationToken = default);

    Task<ErpCashEntryResult> PaymentVoucherAsync(ErpPaymentVoucherInput input, int adminId, CancellationToken cancellationToken = default);

    Task<long> CustomerSettlementAsync(
        DbConnection connection,
        ErpCustomerSettlementInput input,
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

    public ErpCashWriteService(
        IErpWriteConnectionFactory connections,
        IErpVoucherNumberService vouchers,
        IErpGlPostingService gl,
        IErpAuditLogWriter audit)
    {
        _connections = connections;
        _vouchers = vouchers;
        _gl = gl;
        _audit = audit;
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

        if (input.IsAdvance ?? true)
        {
            throw new ErpWriteException("Advance receipts (VAT on advance) remain PHP-only — post the receipt with is_advance = 0");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await AssertAccountAsync(connection, input.AccountId, cancellationToken).ConfigureAwait(false);
        await AssertCustomerAsync(connection, input.UserId, cancellationToken).ConfigureAwait(false);

        var voucherNo = await _vouchers.NextAsync(connection, null, "RV", cancellationToken).ConfigureAwait(false);
        var time = input.Time > 0 ? input.Time : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var reference = input.Reference.Trim().Length > 0 ? input.Reference.Trim() : voucherNo;
        var note = input.Note.Trim().Length > 0 ? input.Note.Trim() : "Customer receipt " + voucherNo;

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_cash_bank_entries` (`account_id`, `time`, `entry_type`, `direction`, `amount`,"
                + " `counterparty_type`, `counterparty_id`, `reference`, `note`, `voucher_no`, `sales_order_id`,"
                + " `sales_invoice_id`, `is_advance`, `admin_id`) VALUES (?,?,'receipt',1,?,'customer',?,?,?,?,?,?,0,?)"),
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
                PostGl = input.PostGl,
            },
            adminId,
            cancellationToken).ConfigureAwait(false);

        var journalId = await TryPostCashEntryAsync(connection, cashEntryId, adminId, cancellationToken).ConfigureAwait(false);
        await LogAsync(connection, adminId, "receipt_voucher", cashEntryId, "Customer receipt posted", voucherNo, amount, cancellationToken).ConfigureAwait(false);
        return new ErpCashEntryResult(cashEntryId, voucherNo, journalId, ledgerId);
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
        await AssertAccountAsync(connection, input.AccountId, cancellationToken).ConfigureAwait(false);
        await AssertSupplierAsync(connection, input.SupplierId, cancellationToken).ConfigureAwait(false);

        var voucherNo = input.Reference.Trim();
        if (!voucherNo.StartsWith("PV-", StringComparison.Ordinal))
        {
            voucherNo = await _vouchers.NextAsync(connection, null, "PV", cancellationToken).ConfigureAwait(false);
        }

        var time = input.Time > 0 ? input.Time : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var note = input.Note.Trim().Length > 0 ? input.Note.Trim() : "Supplier payment";
        long cashEntryId;

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_cash_bank_entries` (`account_id`, `time`, `entry_type`, `direction`, `amount`,"
                    + " `counterparty_type`, `counterparty_id`, `purchase_id`, `reference`, `note`, `voucher_no`, `is_advance`,"
                    + " `admin_id`) VALUES (?,?,'payment',0,?,'supplier',?,?,?,?,?,0,?)"),
                cancellationToken,
                input.AccountId,
                time,
                amount,
                input.SupplierId,
                input.PurchaseId,
                voucherNo,
                note,
                voucherNo,
                adminId).ConfigureAwait(false);
            cashEntryId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_supplier_accounting` (`supplier_id`, `time`, `is_credit`, `amount`, `purchase_id`,"
                    + " `cash_entry_id`, `reference`, `note`, `admin_id`, `entry_kind`) VALUES (?,?,0,?,?,?,?,?,?,'payment')"),
                cancellationToken,
                input.SupplierId,
                time,
                amount,
                input.PurchaseId,
                cashEntryId,
                voucherNo,
                note,
                adminId).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        var journalId = await TryPostCashEntryAsync(connection, cashEntryId, adminId, cancellationToken).ConfigureAwait(false);
        await LogAsync(connection, adminId, "payment_voucher", cashEntryId, "Supplier payment posted", voucherNo, amount, cancellationToken).ConfigureAwait(false);
        return new ErpCashEntryResult(cashEntryId, voucherNo, journalId, 0);
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
        var entryKind = input.EntryKind;
        var reference = input.Reference;
        var note = input.Note;
        var time = input.Time > 0 ? input.Time : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var postGl = input.PostGl;
        if (userId <= 0 || amount <= 0m)
        {
            throw new ErpWriteException("Customer and positive amount required");
        }

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
        var hasDetail = await HasColumnAsync(connection, "shop_users_accounting", "tech_value_text", cancellationToken).ConfigureAwait(false);
        if (hasDetail)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `shop_users_accounting` (`user_id`, `time`, `income`, `amount`, `operation_code`, `active`,"
                    + " `office_id`, `tech_value_text`) VALUES (?,?,?,?,?,1,0,?)"),
                cancellationToken,
                userId,
                time,
                income ? 1 : 0,
                amount,
                codeId,
                detail).ConfigureAwait(false);
        }
        else
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT INTO `shop_users_accounting` (`user_id`, `time`, `income`, `amount`, `operation_code`, `active`,"
                    + " `office_id`) VALUES (?,?,?,?,?,1,0)"),
                cancellationToken,
                userId,
                time,
                income ? 1 : 0,
                amount,
                codeId).ConfigureAwait(false);
        }

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
    }
}
