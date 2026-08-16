using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpGlLine(long CoaId, decimal Debit, decimal Credit, string LineNote);

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

    Task<long> PostCashEntryAsync(
        DbConnection connection,
        long cashEntryId,
        int adminId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpGlPostingService : IErpGlPostingService
{
    private readonly IErpVoucherNumberService _vouchers;

    public ErpGlPostingService(IErpVoucherNumberService vouchers) => _vouchers = vouchers;

    public async Task<long> PostJournalAsync(
        DbConnection connection,
        ErpGlJournalHeader header,
        IReadOnlyList<ErpGlLine> lines,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(lines);

        Validate(lines);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        var journalDate = header.JournalDate > 0 ? header.JournalDate : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var lockDate = await FiscalLockDateAsync(connection, cancellationToken).ConfigureAwait(false);
        if (lockDate > 0 && journalDate <= lockDate)
        {
            var closed = DateTimeOffset.FromUnixTimeSeconds(lockDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            throw new ErpWriteException("Period is closed: cannot post on or before " + closed);
        }

        // Voucher numbering runs DDL, which MySQL implicitly commits — resolve it
        // before the journal transaction opens, exactly as PHP does.
        var journalNo = await _vouchers.NextAsync(connection, null, "GV", cancellationToken).ConfigureAwait(false);
        var companyId = header.CompanyId > 0
            ? header.CompanyId
            : await DefaultCompanyIdAsync(connection, cancellationToken).ConfigureAwait(false);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
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

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return journalId;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
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
        if (direction == 1)
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

            totalDebit += decimal.Round(line.Debit, 4, MidpointRounding.AwayFromZero);
            totalCredit += decimal.Round(line.Credit, 4, MidpointRounding.AwayFromZero);
        }

        if (Math.Abs(totalDebit - totalCredit) > 0.0001m)
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

    private static Task<long> CoaIdByCodeAsync(DbConnection connection, string code, CancellationToken cancellationToken)
        => ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = ? AND `active` = 1 LIMIT 1"),
            cancellationToken,
            code);

    private static async Task<long> FiscalLockDateAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            return await ErpDb.LongAsync(
                connection,
                null,
                "SELECT MAX(`lock_date`) FROM `epc_erp_fiscal_locks` WHERE `active` = 1",
                cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return 0L;
        }
    }

    /// <summary>PHP <c>epc_erp_gl_default_company_id</c>: lowest-id active legal entity.</summary>
    private static async Task<long> DefaultCompanyIdAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            return await ErpDb.LongAsync(
                connection,
                null,
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
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_gl_journals` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `journal_no` varchar(32) NOT NULL,"
            + " `journal_date` int(11) NOT NULL,"
            + " `reference` varchar(128) DEFAULT NULL,"
            + " `description` text,"
            + " `source_type` enum('manual','sales','purchase','payment','cash','opening','adjustment') NOT NULL DEFAULT 'manual',"
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
