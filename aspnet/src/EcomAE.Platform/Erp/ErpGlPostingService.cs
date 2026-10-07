using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpGlLine(long CoaId, decimal Debit, decimal Credit, string LineNote);

public sealed record ErpGlSalesInvoicePosting(
    long InvoiceId,
    string InvoiceNumber,
    decimal Subtotal,
    decimal TotalVat,
    decimal TotalIncl,
    long IssueDate,
    string LegislationRef = "");

public sealed record ErpGlJournalHeader
{
    public long JournalDate { get; init; }

    public string Reference { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string SourceType { get; init; } = "manual";

    public long SourceId { get; init; }

    /// <summary>Zero resolves the tenant's default legal entity (PHP <c>epc_erp_gl_resolve_company_id</c>).</summary>
    public long CompanyId { get; init; }

    /// <summary>
    /// Country-specific legislation reference stamped on the journal. PHP leaves it empty
    /// unless the caller supplies one, so tenant-country compliance stays with the caller.
    /// </summary>
    public string LegislationRef { get; init; } = string.Empty;
}

/// <summary>
/// Live ASP.NET port of PHP <c>epc_erp_gl_post_journal</c> / <c>epc_erp_gl_post_cash_entry</c>
/// (<c>content/shop/finance/epc_erp_gl.php</c>): balanced double entry, fiscal-period lock,
/// GV voucher numbering, company scoping and cash-entry journal derivation.
/// </summary>
public interface IErpGlPostingService
{
    Task<long> PostJournalAsync(
        DbConnection connection,
        ErpGlJournalHeader header,
        IReadOnlyList<ErpGlLine> lines,
        int adminId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts inside the caller's ambient transaction (no own commit/rollback). The caller must run
    /// <see cref="EnsureSchemaAsync"/> before opening the transaction because schema DDL implicitly commits.
    /// </summary>
    Task<long> PostJournalAsync(
        DbConnection connection,
        DbTransaction? transaction,
        ErpGlJournalHeader header,
        IReadOnlyList<ErpGlLine> lines,
        int adminId,
        CancellationToken cancellationToken = default);

    /// <summary>GL tables, COA seed and voucher sequence schema — DDL that must precede any ambient transaction.</summary>
    Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken = default);

    Task<long> PostCashEntryAsync(
        DbConnection connection,
        long cashEntryId,
        int adminId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sales-invoice recognition (Dr 1100 AR / Cr 4000 revenue / Cr 2100 VAT output), the same
    /// account mapping PHP <c>epc_erp_gl_post_sales_orders</c> applies to storefront orders.
    /// Idempotent per <c>source_type = 'sales_invoice'</c> / document id.
    /// </summary>
    Task<long> PostSalesInvoiceAsync(
        DbConnection connection,
        ErpGlSalesInvoicePosting invoice,
        int adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Same posting inside the caller's ambient transaction (SO→tax-invoice conversion).</summary>
    Task<long> PostSalesInvoiceAsync(
        DbConnection connection,
        DbTransaction transaction,
        ErpGlSalesInvoicePosting invoice,
        int adminId,
        CancellationToken cancellationToken = default);

    /// <summary>Port of PHP <c>epc_erp_gl_post_purchase</c>: COGS (+ input VAT) debit against accounts payable.</summary>
    Task<long> PostPurchaseAsync(
        DbConnection connection,
        long purchaseId,
        int adminId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpGlPostingService : IErpGlPostingService
{
    private readonly IErpVoucherNumberService _vouchers;

    public ErpGlPostingService(IErpVoucherNumberService vouchers) => _vouchers = vouchers;

    public Task<long> PostJournalAsync(
        DbConnection connection,
        ErpGlJournalHeader header,
        IReadOnlyList<ErpGlLine> lines,
        int adminId,
        CancellationToken cancellationToken = default)
        => PostJournalAsync(connection, null, header, lines, adminId, cancellationToken);

    Task IErpGlPostingService.EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
        => EnsureSchemaAsync(connection, cancellationToken);

    public async Task<long> PostJournalAsync(
        DbConnection connection,
        DbTransaction? ambient,
        ErpGlJournalHeader header,
        IReadOnlyList<ErpGlLine> lines,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(lines);

        Validate(lines);
        if (ambient is null)
        {
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        }

        var journalDate = header.JournalDate > 0 ? header.JournalDate : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await AssertPostingPeriodOpenAsync(connection, ambient, journalDate, cancellationToken).ConfigureAwait(false);

        // Voucher numbering runs DDL, which MySQL implicitly commits — resolve it
        // before the journal transaction opens, exactly as PHP does.
        var journalNo = await _vouchers.NextAsync(connection, ambient, "GV", cancellationToken).ConfigureAwait(false);
        var companyId = header.CompanyId > 0
            ? header.CompanyId
            : await DefaultCompanyIdAsync(connection, ambient, cancellationToken).ConfigureAwait(false);

        var transaction = ambient ?? await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var owned = ambient is null;
        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_gl_journals` (`journal_no`, `journal_date`, `reference`, `description`, `source_type`,"
                    + " `source_id`, `company_id`, `uae_tax_legislation_ref`, `admin_id`, `time_created`) VALUES (?,?,?,?,?,?,?,?,?,?)"),
                cancellationToken,
                journalNo,
                journalDate,
                header.Reference.Trim(),
                header.Description.Trim(),
                header.SourceType,
                header.SourceId,
                companyId,
                header.LegislationRef.Trim(),
                adminId,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);

            var journalId = await ErpDb.LastInsertIdAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            foreach (var line in lines)
            {
                await ErpDb.ExecuteAsync(
                    connection,
                    transaction,
                    ErpDb.Positional(
                        "INSERT INTO `epc_erp_gl_lines` (`journal_id`, `coa_id`, `debit`, `credit`, `line_note`) VALUES (?,?,?,?,?)"),
                    cancellationToken,
                    journalId,
                    line.CoaId,
                    ErpTaxAmountCalculator.Round2(line.Debit),
                    ErpTaxAmountCalculator.Round2(line.Credit),
                    (line.LineNote ?? string.Empty).Trim()).ConfigureAwait(false);
            }

            if (owned)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            return journalId;
        }
        catch
        {
            if (owned)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            if (owned)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task<long> PostCashEntryAsync(
        DbConnection connection,
        long cashEntryId,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT e.`time`, e.`direction`, e.`amount`, e.`counterparty_type`, e.`reference`, e.`gl_journal_id`,"
            + " a.`coa_id`, a.`account_type`"
            + " FROM `epc_erp_cash_bank_entries` e"
            + " INNER JOIN `epc_erp_cash_bank_accounts` a ON a.`id` = e.`account_id`"
            + " WHERE e.`id` = ? AND e.`active` = 1 LIMIT 1");
        var idParameter = command.CreateParameter();
        idParameter.ParameterName = "@p0";
        idParameter.Value = cashEntryId;
        command.Parameters.Add(idParameter);

        long entryTime;
        int direction;
        decimal amount;
        string counterpartyType;
        string reference;
        long existingJournalId;
        long cashCoaId;
        string accountType;

        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new ErpWriteException("Cash entry not found");
            }

            entryTime = reader.GetInt64(0);
            direction = reader.GetInt32(1);
            amount = ErpTaxAmountCalculator.Round2(reader.GetDecimal(2));
            counterpartyType = reader.GetString(3);
            reference = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
            existingJournalId = reader.IsDBNull(5) ? 0L : reader.GetInt64(5);
            cashCoaId = reader.IsDBNull(6) ? 0L : reader.GetInt64(6);
            accountType = reader.IsDBNull(7) ? "cash" : reader.GetString(7);
        }

        if (existingJournalId > 0)
        {
            return existingJournalId;
        }

        if (cashCoaId <= 0)
        {
            cashCoaId = await CoaIdByCodeAsync(
                connection,
                string.Equals(accountType, "bank", StringComparison.Ordinal) ? "1010" : "1000",
                cancellationToken).ConfigureAwait(false);
        }

        if (cashCoaId <= 0)
        {
            throw new ErpWriteException("Cash COA not linked");
        }

        var lines = new List<ErpGlLine>(2);
        if (string.Equals(counterpartyType, "internal", StringComparison.Ordinal))
        {
            // Transfer legs net to zero on the clearing account and leave revenue and expense untouched.
            var transit = await TransitCoaIdAsync(connection, cancellationToken).ConfigureAwait(false);
            lines.Add(direction == 1
                ? new ErpGlLine(cashCoaId, amount, 0m, "Transfer in")
                : new ErpGlLine(cashCoaId, 0m, amount, "Transfer out"));
            lines.Add(direction == 1
                ? new ErpGlLine(transit, 0m, amount, "Cash in transit")
                : new ErpGlLine(transit, amount, 0m, "Cash in transit"));
        }
        else if (direction == 1)
        {
            lines.Add(new ErpGlLine(cashCoaId, amount, 0m, "Receipt"));
            var receivable = string.Equals(counterpartyType, "customer", StringComparison.Ordinal)
                ? await CoaIdByCodeAsync(connection, "1100", cancellationToken).ConfigureAwait(false)
                : 0L;
            if (receivable > 0)
            {
                lines.Add(new ErpGlLine(receivable, 0m, amount, "Customer receipt"));
            }
            else
            {
                var revenue = await CoaIdByCodeAsync(connection, "4000", cancellationToken).ConfigureAwait(false);
                if (revenue <= 0)
                {
                    throw new ErpWriteException("Revenue COA missing");
                }

                lines.Add(new ErpGlLine(revenue, 0m, amount, "Other income"));
            }
        }
        else
        {
            lines.Add(new ErpGlLine(cashCoaId, 0m, amount, "Payment"));
            var payable = string.Equals(counterpartyType, "supplier", StringComparison.Ordinal)
                ? await CoaIdByCodeAsync(connection, "2000", cancellationToken).ConfigureAwait(false)
                : 0L;
            if (payable > 0)
            {
                lines.Add(new ErpGlLine(payable, amount, 0m, "Supplier payment"));
            }
            else
            {
                var expense = await CoaIdByCodeAsync(connection, "6100", cancellationToken).ConfigureAwait(false);
                if (expense <= 0)
                {
                    throw new ErpWriteException("Expense COA missing");
                }

                lines.Add(new ErpGlLine(expense, amount, 0m, "Expense"));
            }
        }

        var journalId = await PostJournalAsync(
            connection,
            new ErpGlJournalHeader
            {
                JournalDate = entryTime,
                Reference = reference,
                Description = "Cash/bank entry #" + cashEntryId.ToString(CultureInfo.InvariantCulture),
                SourceType = "cash",
                SourceId = cashEntryId,
            },
            lines,
            adminId,
            cancellationToken).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_cash_bank_entries` SET `gl_journal_id` = ? WHERE `id` = ?"),
            cancellationToken,
            journalId,
            cashEntryId).ConfigureAwait(false);
        return journalId;
    }

    /// <summary>PHP <c>epc_erp_gl_post_journal</c> pre-flight checks (double entry, sign, balance, amount).</summary>
    public static void Validate(IReadOnlyList<ErpGlLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
        {
            throw new ErpWriteException("Journal must have lines");
        }

        if (lines.Count < 2)
        {
            throw new ErpWriteException("Double-entry bookkeeping requires at least two lines");
        }

        var totalDebit = 0m;
        var totalCredit = 0m;
        foreach (var line in lines)
        {
            if (line.Debit < 0m || line.Credit < 0m)
            {
                throw new ErpWriteException("Ledger posting values must be greater than or equal to zero (no double-negatives allowed)");
            }

            // Lines are stored as decimal(14,2), so balance must hold on the stored values.
            totalDebit += ErpTaxAmountCalculator.Round2(line.Debit);
            totalCredit += ErpTaxAmountCalculator.Round2(line.Credit);
        }

        if (totalDebit != totalCredit)
        {
            throw new ErpWriteException(
                "Journal not balanced: debit "
                + totalDebit.ToString(CultureInfo.InvariantCulture)
                + " vs credit "
                + totalCredit.ToString(CultureInfo.InvariantCulture));
        }

        if (totalDebit <= 0m)
        {
            throw new ErpWriteException("Journal amount must be greater than zero");
        }
    }

    public Task<long> PostSalesInvoiceAsync(
        DbConnection connection,
        ErpGlSalesInvoicePosting invoice,
        int adminId,
        CancellationToken cancellationToken = default)
        => PostSalesInvoiceCoreAsync(connection, null, invoice, adminId, cancellationToken);

    public Task<long> PostSalesInvoiceAsync(
        DbConnection connection,
        DbTransaction transaction,
        ErpGlSalesInvoicePosting invoice,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return PostSalesInvoiceCoreAsync(connection, transaction, invoice, adminId, cancellationToken);
    }

    private async Task<long> PostSalesInvoiceCoreAsync(
        DbConnection connection,
        DbTransaction? transaction,
        ErpGlSalesInvoicePosting invoice,
        int adminId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(invoice);
        if (transaction is null)
        {
            await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        }

        var existing = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional(
                "SELECT `id` FROM `epc_erp_gl_journals` WHERE `source_type` = 'sales_invoice' AND `source_id` = ? AND `active` = 1 LIMIT 1"),
            cancellationToken,
            invoice.InvoiceId).ConfigureAwait(false);
        if (existing > 0)
        {
            return existing;
        }

        var receivableId = await CoaIdByCodeAsync(connection, transaction, "1100", cancellationToken).ConfigureAwait(false);
        var revenueId = await CoaIdByCodeAsync(connection, transaction, "4000", cancellationToken).ConfigureAwait(false);
        if (receivableId <= 0 || revenueId <= 0)
        {
            throw new ErpWriteException("Sales COA accounts missing");
        }

        var lines = SalesInvoiceLines(
            receivableId,
            revenueId,
            invoice.TotalVat > 0m ? await CoaIdByCodeAsync(connection, transaction, "2100", cancellationToken).ConfigureAwait(false) : 0L,
            invoice);

        return await PostJournalAsync(
            connection,
            transaction,
            new ErpGlJournalHeader
            {
                JournalDate = invoice.IssueDate,
                Reference = invoice.InvoiceNumber,
                Description = "Sales invoice " + invoice.InvoiceNumber,
                SourceType = "sales_invoice",
                SourceId = invoice.InvoiceId,
                LegislationRef = invoice.LegislationRef,
            },
            lines,
            adminId,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Dr AR gross; Cr revenue net; Cr VAT output when the tenant charges VAT and the account exists.</summary>
    public static List<ErpGlLine> SalesInvoiceLines(long receivableId, long revenueId, long vatOutputId, ErpGlSalesInvoicePosting invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        var vat = vatOutputId > 0 ? invoice.TotalVat : 0m;
        var lines = new List<ErpGlLine>(3)
        {
            new(receivableId, invoice.TotalIncl, 0m, "Sales invoice " + invoice.InvoiceNumber),
            new(revenueId, 0m, invoice.TotalIncl - vat, "Sales revenue (ex VAT)"),
        };
        if (vat > 0m)
        {
            lines.Add(new ErpGlLine(vatOutputId, 0m, vat, "VAT output"));
        }

        return lines;
    }

    public async Task<long> PostPurchaseAsync(
        DbConnection connection,
        long purchaseId,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var purchase = await LoadPurchaseAsync(connection, purchaseId, cancellationToken).ConfigureAwait(false)
            ?? throw new ErpWriteException("Purchase not found");
        if (purchase.GlJournalId > 0)
        {
            return purchase.GlJournalId;
        }

        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT `id` FROM `epc_erp_gl_journals` WHERE `source_type` = 'purchase' AND `source_id` = ? AND `active` = 1 LIMIT 1"),
            cancellationToken,
            purchaseId).ConfigureAwait(false);
        if (existing > 0)
        {
            await LinkPurchaseJournalAsync(connection, purchaseId, existing, cancellationToken).ConfigureAwait(false);
            return existing;
        }

        var cogsId = await CoaIdByCodeAsync(connection, "5000", cancellationToken).ConfigureAwait(false);
        var payableId = await CoaIdByCodeAsync(connection, "2000", cancellationToken).ConfigureAwait(false);
        if (cogsId <= 0 || payableId <= 0)
        {
            throw new ErpWriteException("COA accounts missing — run ERP setup");
        }

        var vatInputId = await CoaIdByCodeAsync(connection, "1150", cancellationToken).ConfigureAwait(false);
        var lines = new List<ErpGlLine> { new(cogsId, purchase.AmountExVat, 0m, "Purchase ex VAT") };
        if (purchase.VatAmount > 0m && vatInputId > 0)
        {
            lines.Add(new ErpGlLine(vatInputId, purchase.VatAmount, 0m, "VAT input"));
        }

        lines.Add(new ErpGlLine(payableId, 0m, purchase.TotalAmount, "Accounts payable"));

        var journalId = await PostJournalAsync(
            connection,
            new ErpGlJournalHeader
            {
                JournalDate = purchase.PurchaseDate,
                Reference = purchase.InvoiceNumber,
                Description = "Purchase invoice #" + purchaseId.ToString(CultureInfo.InvariantCulture),
                SourceType = "purchase",
                SourceId = purchaseId,
                LegislationRef = purchase.LegislationRef,
            },
            lines,
            adminId,
            cancellationToken).ConfigureAwait(false);

        await LinkPurchaseJournalAsync(connection, purchaseId, journalId, cancellationToken).ConfigureAwait(false);
        return journalId;
    }

    private static Task LinkPurchaseJournalAsync(
        DbConnection connection,
        long purchaseId,
        long journalId,
        CancellationToken cancellationToken)
        => ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_purchases` SET `gl_journal_id` = ? WHERE `id` = ?"),
            cancellationToken,
            journalId,
            purchaseId);

    private static async Task<(decimal AmountExVat, decimal VatAmount, decimal TotalAmount, long PurchaseDate, string InvoiceNumber, string LegislationRef, long GlJournalId)?> LoadPurchaseAsync(
        DbConnection connection,
        long purchaseId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `amount_ex_vat`, `vat_amount`, `total_amount`, `purchase_date`, `invoice_number`,"
            + " `uae_tax_legislation_ref`, `gl_journal_id` FROM `epc_erp_purchases` WHERE `id` = ? AND `active` = 1 LIMIT 1");
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
            reader.IsDBNull(0) ? 0m : reader.GetDecimal(0),
            reader.IsDBNull(1) ? 0m : reader.GetDecimal(1),
            reader.IsDBNull(2) ? 0m : reader.GetDecimal(2),
            reader.IsDBNull(3) ? 0L : reader.GetInt64(3),
            reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            reader.IsDBNull(6) ? 0L : reader.GetInt64(6));
    }

    private static Task<long> CoaIdByCodeAsync(DbConnection connection, string code, CancellationToken cancellationToken)
        => CoaIdByCodeAsync(connection, null, code, cancellationToken);

    private static async Task<long> TransitCoaIdAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var transit = ErpGlChartOfAccountsSeeder.SystemAccounts.First(a => a.Code == ErpGlChartOfAccountsSeeder.CashInTransitCode);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT IGNORE INTO `epc_erp_coa_accounts` (`code`, `name`, `account_type`, `normal_side`, `description`, `system_flag`, `time_created`)"
                + " VALUES (?, ?, ?, ?, ?, 1, ?)"),
            cancellationToken,
            transit.Code,
            transit.Name,
            transit.Type,
            transit.Side,
            transit.Description,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);

        var type = await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `account_type` FROM `epc_erp_coa_accounts` WHERE `code` = ? AND `active` = 1 LIMIT 1"),
            cancellationToken,
            transit.Code).ConfigureAwait(false);
        if (!string.Equals(type, "asset", StringComparison.Ordinal))
        {
            throw new ErpWriteException("Account " + transit.Code + " must be an active asset account to post cash transfers");
        }

        return await CoaIdByCodeAsync(connection, transit.Code, cancellationToken).ConfigureAwait(false);
    }

    private static Task<long> CoaIdByCodeAsync(DbConnection connection, DbTransaction? transaction, string code, CancellationToken cancellationToken)
        => ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = ? AND `active` = 1 LIMIT 1"),
            cancellationToken,
            code);

    /// <summary>`epc_erp_periods.status` of <c>soft_close</c> or <c>locked</c> blocks every journal source,
    /// not only manual journals (sub-ledger postings share this path).</summary>
    public static bool PeriodBlocksPosting(string status)
        => status.Equals("locked", StringComparison.OrdinalIgnoreCase)
            || status.Equals("soft_close", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Fiscal-lock date and <c>epc_erp_periods</c> status guard shared by every sub-ledger write
    /// (manual journals, customer receipts, supplier payments) so a locked month refuses the
    /// document itself, not only its GL journal.
    /// </summary>
    public static async Task AssertPostingPeriodOpenAsync(
        DbConnection connection,
        DbTransaction? transaction,
        long postingDate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var lockDate = await FiscalLockDateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
        if (lockDate > 0 && postingDate <= lockDate)
        {
            var closed = DateTimeOffset.FromUnixTimeSeconds(lockDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            throw new ErpWriteException("Period is closed: cannot post on or before " + closed);
        }

        var periodStatus = await PeriodStatusAsync(connection, transaction, postingDate, cancellationToken).ConfigureAwait(false);
        if (PeriodBlocksPosting(periodStatus))
        {
            throw new ErpWriteException(PeriodBlockedMessage(periodStatus));
        }
    }

    public static string PeriodBlockedMessage(string status)
        => "Journal posting is blocked because the accounting period is " + status.Replace('_', ' ');

    public static string PeriodKey(long journalDate)
        => DateTimeOffset.FromUnixTimeSeconds(journalDate).ToUniversalTime().ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static async Task<string> PeriodStatusAsync(DbConnection connection, DbTransaction? transaction, long journalDate, CancellationToken cancellationToken)
    {
        try
        {
            var status = await ErpDb.StringAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT COALESCE(`status`, 'open') FROM `epc_erp_periods` WHERE `year_month` = ? LIMIT 1"),
                cancellationToken,
                PeriodKey(journalDate)).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(status) ? "open" : status;
        }
        catch (DbException)
        {
            return "open";
        }
    }

    private static async Task<long> FiscalLockDateAsync(DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        try
        {
            return await ErpDb.LongAsync(
                connection,
                transaction,
                "SELECT MAX(`lock_date`) FROM `epc_erp_fiscal_locks` WHERE `active` = 1",
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return 0L;
        }
    }

    /// <summary>PHP <c>epc_erp_gl_default_company_id</c>: lowest-id active legal entity.</summary>
    private static async Task<long> DefaultCompanyIdAsync(DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        try
        {
            return await ErpDb.LongAsync(
                connection,
                transaction,
                "SELECT MIN(`id`) FROM `epc_erp_pm_legal_entities` WHERE `active` = 1",
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return 0L;
        }
    }

    /// <summary>Subset of PHP <c>epc_erp_gl_ensure_schema</c> for the tables this service writes.</summary>
    private static async Task EnsureSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpGlChartOfAccountsSeeder.EnsureAsync(connection, cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_gl_journals` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `journal_no` varchar(32) NOT NULL,"
            + " `journal_date` int(11) NOT NULL,"
            + " `reference` varchar(128) DEFAULT NULL,"
            + " `description` text,"
            + " `source_type` enum('manual','sales','purchase','payment','cash','opening','adjustment','sales_invoice') NOT NULL DEFAULT 'manual',"
            + " `source_id` int(11) NOT NULL DEFAULT 0,"
            + " `admin_id` int(11) NOT NULL DEFAULT 0,"
            + " `active` tinyint(1) NOT NULL DEFAULT 1,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`),"
            + " UNIQUE KEY `x_journal_no` (`journal_no`),"
            + " KEY `x_date` (`journal_date`),"
            + " KEY `x_source` (`source_type`,`source_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP GL journal headers'",
            cancellationToken).ConfigureAwait(false);

        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_gl_lines` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `journal_id` int(11) NOT NULL,"
            + " `coa_id` int(11) NOT NULL,"
            + " `debit` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `credit` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `line_note` varchar(255) DEFAULT NULL,"
            + " PRIMARY KEY (`id`),"
            + " KEY `x_journal` (`journal_id`),"
            + " KEY `x_coa` (`coa_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP GL journal lines'",
            cancellationToken).ConfigureAwait(false);

        // Tenant databases created by PHP lack the 'sales_invoice' source type; appending an
        // enum member is a metadata-only change and PHP readers keep working.
        var sourceType = await ErpDb.StringAsync(
            connection,
            null,
            "SELECT `COLUMN_TYPE` FROM `information_schema`.`COLUMNS` WHERE `TABLE_SCHEMA` = DATABASE() AND `TABLE_NAME` = 'epc_erp_gl_journals' AND `COLUMN_NAME` = 'source_type'",
            cancellationToken).ConfigureAwait(false);
        if (sourceType is null || !sourceType.Contains("'sales_invoice'", StringComparison.Ordinal))
        {
            await ErpDb.TryExecuteAsync(
                connection,
                "ALTER TABLE `epc_erp_gl_journals` MODIFY `source_type` enum('manual','sales','purchase','payment','cash','opening','adjustment','sales_invoice') NOT NULL DEFAULT 'manual'",
                cancellationToken).ConfigureAwait(false);
        }

        // PHP adds these through epc_erp_schema_add_column_if_missing on tenant
        // databases created before multi-entity / VAT columns existed.
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_erp_gl_journals` ADD `company_id` int(11) NOT NULL DEFAULT 0",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_erp_gl_journals` ADD `uae_tax_legislation_ref` varchar(64) NOT NULL DEFAULT ''",
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_erp_cash_bank_entries` ADD `gl_journal_id` int(11) NOT NULL DEFAULT 0",
            cancellationToken).ConfigureAwait(false);
    }
}
