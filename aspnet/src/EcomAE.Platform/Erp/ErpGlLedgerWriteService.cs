using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>PHP <c>epc_erp_gl_manual_entry</c> payload (<c>lines_json</c> decoded by the caller).</summary>
public sealed record ErpManualJournalInput
{
    public IReadOnlyList<ErpGlLine> Lines { get; init; } = [];

    public string Reference { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public long JournalDate { get; init; }
}

/// <summary>PHP <c>epc_erp_gl_create_coa</c> payload.</summary>
public sealed record ErpCoaAccountInput
{
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string AccountType { get; init; } = "expense";

    /// <summary>Empty derives the side from the account type, exactly as PHP does.</summary>
    public string NormalSide { get; init; } = string.Empty;

    public long ParentId { get; init; }

    public decimal OpeningBalance { get; init; }

    public string Description { get; init; } = string.Empty;
}

/// <summary>PHP <c>epc_erp_create_cash_account</c> payload.</summary>
public sealed record ErpCashAccountInput
{
    public string Name { get; init; } = string.Empty;

    public string AccountType { get; init; } = "cash";

    public string BankName { get; init; } = string.Empty;

    public string AccountNumber { get; init; } = string.Empty;

    public string CurrencyCode { get; init; } = "AED";

    public decimal OpeningBalance { get; init; }

    public long OfficeId { get; init; }

    public long LegalEntityId { get; init; }

    public long BusinessUnitId { get; init; }

    public long GlAccountId { get; init; }

    public string Iban { get; init; } = string.Empty;

    public string SwiftBic { get; init; } = string.Empty;

    public string BankBranch { get; init; } = string.Empty;

    public string RoutingCode { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public string ContactName { get; init; } = string.Empty;

    public string ContactPhone { get; init; } = string.Empty;

    public string ContactEmail { get; init; } = string.Empty;

    public string Status { get; init; } = "active";

    public string Notes { get; init; } = string.Empty;
}

public sealed record ErpJournalResult(long JournalId, string JournalNo);

public sealed record ErpReversedJournalResult(
    long JournalId,
    string JournalNo,
    long SourceJournalId,
    string SourceJournalNo);

/// <summary>
/// Live ASP.NET port of the PHP ledger master-data and manual-posting writes
/// (<c>content/shop/finance/epc_erp_gl.php</c>, <c>epc_erp_helpers.php</c>):
/// <c>epc_erp_gl_manual_entry</c>, <c>epc_erp_gl_reverse_journal</c>,
/// <c>epc_erp_gl_create_coa</c> and <c>epc_erp_create_cash_account</c>.
/// </summary>
public interface IErpGlLedgerWriteService
{
    Task<ErpJournalResult> ManualJournalAsync(
        ErpManualJournalInput input,
        int adminId,
        CancellationToken cancellationToken = default);

    Task<ErpReversedJournalResult> ReverseJournalAsync(
        long journalId,
        long reverseDate,
        string note,
        int adminId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverses on a caller-owned connection so document-lifecycle voids can reverse the
    /// journals they own without opening a second tenant connection.
    /// </summary>
    Task<ErpReversedJournalResult> ReverseJournalAsync(
        DbConnection connection,
        long journalId,
        long reverseDate,
        string note,
        int adminId,
        CancellationToken cancellationToken = default);

    Task<long> CreateCoaAccountAsync(
        ErpCoaAccountInput input,
        int adminId,
        CancellationToken cancellationToken = default);

    Task<long> CreateCashAccountAsync(
        ErpCashAccountInput input,
        int adminId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpGlLedgerWriteService : IErpGlLedgerWriteService
{
    private static readonly string[] AccountTypes = ["asset", "liability", "equity", "revenue", "expense"];

    private static readonly string[] CreditSideTypes = ["liability", "equity", "revenue"];

    private static readonly string[] CashAccountStatuses = ["active", "inactive", "closed"];

    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpGlPostingService _gl;
    private readonly IErpAuditLogWriter _audit;

    public ErpGlLedgerWriteService(
        IErpWriteConnectionFactory connections,
        IErpGlPostingService gl,
        IErpAuditLogWriter audit)
    {
        _connections = connections;
        _gl = gl;
        _audit = audit;
    }

    public async Task<ErpJournalResult> ManualJournalAsync(
        ErpManualJournalInput input,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureConfigured();

        var lines = input.Lines ?? [];
        if (lines.Count == 0)
        {
            throw new ErpWriteException("Add at least two GL lines");
        }

        ErpGlPostingService.Validate(lines);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureCoaSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await AssertCoaLinesAsync(connection, lines, cancellationToken).ConfigureAwait(false);

        var journalId = await _gl.PostJournalAsync(
            connection,
            new ErpGlJournalHeader
            {
                JournalDate = input.JournalDate,
                Reference = input.Reference,
                Description = input.Description.Trim().Length > 0 ? input.Description : "Manual journal entry",
                SourceType = "manual",
            },
            lines,
            adminId,
            cancellationToken).ConfigureAwait(false);

        var journalNo = await JournalNoAsync(connection, journalId, cancellationToken).ConfigureAwait(false);
        await LogAsync(
            connection,
            adminId,
            "gl_manual_entry",
            journalId,
            "GL journal posted",
            new Dictionary<string, string?>
            {
                ["journal_no"] = journalNo,
                ["lines"] = lines.Count.ToString(CultureInfo.InvariantCulture),
            },
            cancellationToken).ConfigureAwait(false);
        return new ErpJournalResult(journalId, journalNo);
    }

    public async Task<ErpReversedJournalResult> ReverseJournalAsync(
        long journalId,
        long reverseDate,
        string note,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();
        if (journalId <= 0)
        {
            throw new ErpWriteException("Journal not found or already reversed");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ReverseJournalAsync(connection, journalId, reverseDate, note, adminId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ErpReversedJournalResult> ReverseJournalAsync(
        DbConnection connection,
        long journalId,
        long reverseDate,
        string note,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (journalId <= 0)
        {
            throw new ErpWriteException("Journal not found or already reversed");
        }

        await EnsureCoaSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            "ALTER TABLE `epc_erp_gl_journals` ADD `reversed_by_journal_id` int(11) NOT NULL DEFAULT 0",
            cancellationToken).ConfigureAwait(false);

        var journal = await JournalHeadAsync(connection, journalId, cancellationToken).ConfigureAwait(false)
            ?? throw new ErpWriteException("Journal not found or already reversed");

        // PHP's message promises this guard but never queries for it, so a journal can be
        // reversed repeatedly and each pass double-counts the mirrored side.
        var existingReversal = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT `id` FROM `epc_erp_gl_journals` WHERE `source_type` = 'adjustment' AND `source_id` = ?"
                + " AND `active` = 1 LIMIT 1"),
            cancellationToken,
            journalId).ConfigureAwait(false);
        if (existingReversal <= 0)
        {
            existingReversal = journal.ReversedByJournalId;
        }

        if (existingReversal > 0)
        {
            throw new ErpWriteException("Journal not found or already reversed");
        }

        var lines = await JournalLinesAsync(connection, journalId, cancellationToken).ConfigureAwait(false);
        if (lines.Count == 0)
        {
            throw new ErpWriteException("Journal has no lines to reverse");
        }

        // PHP swaps debit and credit on every line; the mirror journal is the audited
        // correction, posted journals are never edited or deleted.
        var reversed = lines
            .Select(line => new ErpGlLine(
                line.CoaId,
                ErpTaxAmountCalculator.Round2(line.Credit),
                ErpTaxAmountCalculator.Round2(line.Debit),
                "Reversal — " + line.LineNote))
            .ToList();

        var trimmedNote = (note ?? string.Empty).Trim();
        var newJournalId = await _gl.PostJournalAsync(
            connection,
            new ErpGlJournalHeader
            {
                JournalDate = reverseDate > 0 ? reverseDate : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Reference = "REV of " + journal.JournalNo,
                Description = trimmedNote.Length > 0 ? trimmedNote : "Reversal of " + journal.JournalNo,
                SourceType = "adjustment",
                SourceId = journalId,
                // A reversal stays booked in the original entry's company, whichever
                // company is active in the session now.
                CompanyId = journal.CompanyId,
            },
            reversed,
            adminId,
            cancellationToken).ConfigureAwait(false);

        // PHP epc_erp_doc_reverse_journals stamps the back-reference so later voids of the
        // same document reuse the mirror instead of posting another one.
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_gl_journals` SET `reversed_by_journal_id` = ? WHERE `id` = ?"),
            cancellationToken,
            newJournalId,
            journalId).ConfigureAwait(false);

        var newJournalNo = await JournalNoAsync(connection, newJournalId, cancellationToken).ConfigureAwait(false);
        await LogAsync(
            connection,
            adminId,
            "gl_reverse",
            journalId,
            "Reversed by journal #" + newJournalId.ToString(CultureInfo.InvariantCulture),
            new Dictionary<string, string?>
            {
                ["journal_no"] = journal.JournalNo,
                ["reversal_journal_no"] = newJournalNo,
            },
            cancellationToken).ConfigureAwait(false);
        return new ErpReversedJournalResult(newJournalId, newJournalNo, journalId, journal.JournalNo);
    }

    public async Task<long> CreateCoaAccountAsync(
        ErpCoaAccountInput input,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureConfigured();

        var type = input.AccountType.Trim().Length > 0 ? input.AccountType.Trim() : "expense";
        if (!AccountTypes.Contains(type, StringComparer.Ordinal))
        {
            throw new ErpWriteException("Invalid account type");
        }

        var code = input.Code.Trim();
        if (code.Length == 0)
        {
            throw new ErpWriteException("Account code required");
        }

        var normalSide = CreditSideTypes.Contains(type, StringComparer.Ordinal) ? "credit" : "debit";
        var requestedSide = input.NormalSide.Trim();
        if (requestedSide is "debit" or "credit")
        {
            normalSide = requestedSide;
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureCoaSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        // `x_code` is unique, so PHP surfaces the driver error as a failed action —
        // the pre-check turns it into the same failed response with a usable message.
        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = ? LIMIT 1"),
            cancellationToken,
            code).ConfigureAwait(false);
        if (existing > 0)
        {
            throw new ErpWriteException("Account code " + code + " already exists");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_coa_accounts` (`code`, `name`, `account_type`, `normal_side`, `parent_id`,"
                + " `opening_balance`, `description`, `time_created`) VALUES (?,?,?,?,?,?,?,?)"),
            cancellationToken,
            code,
            input.Name.Trim(),
            type,
            normalSide,
            input.ParentId,
            ErpTaxAmountCalculator.Round2(input.OpeningBalance),
            input.Description.Trim(),
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);

        var accountId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        await LogAsync(
            connection,
            adminId,
            "coa_create",
            accountId,
            "COA account created",
            new Dictionary<string, string?>
            {
                ["code"] = code,
                ["account_type"] = type,
                ["normal_side"] = normalSide,
            },
            cancellationToken).ConfigureAwait(false);
        return accountId;
    }

    public async Task<long> CreateCashAccountAsync(
        ErpCashAccountInput input,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureConfigured();

        var name = input.Name.Trim();
        if (name.Length == 0)
        {
            throw new ErpWriteException("Account name required");
        }

        var type = string.Equals(input.AccountType.Trim(), "bank", StringComparison.Ordinal) ? "bank" : "cash";
        var status = input.Status.Trim();
        if (!CashAccountStatuses.Contains(status, StringComparer.Ordinal))
        {
            status = "active";
        }

        var currency = input.CurrencyCode.Trim();
        if (currency.Length == 0)
        {
            currency = "AED";
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureCashAccountSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_cash_bank_accounts` (`name`, `account_type`, `bank_name`, `account_number`,"
                + " `currency_code`, `opening_balance`, `office_id`, `legal_entity_id`, `business_unit_id`,"
                + " `gl_account_id`, `iban`, `swift_bic`, `bank_branch`, `routing_code`, `address`, `contact_name`,"
                + " `contact_phone`, `contact_email`, `status`, `notes`, `time_created`)"
                + " VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            name,
            type,
            input.BankName.Trim(),
            input.AccountNumber.Trim(),
            currency,
            ErpTaxAmountCalculator.Round2(input.OpeningBalance),
            input.OfficeId,
            input.LegalEntityId,
            input.BusinessUnitId,
            input.GlAccountId,
            input.Iban.Trim(),
            input.SwiftBic.Trim(),
            input.BankBranch.Trim(),
            input.RoutingCode.Trim(),
            input.Address.Trim(),
            input.ContactName.Trim(),
            input.ContactPhone.Trim(),
            input.ContactEmail.Trim(),
            status,
            input.Notes.Trim(),
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);

        var accountId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        await LinkCashCoaAsync(connection, accountId, type, cancellationToken).ConfigureAwait(false);
        await LogAsync(
            connection,
            adminId,
            "cash_account_create",
            accountId,
            "Account created",
            new Dictionary<string, string?>
            {
                ["name"] = name,
                ["account_type"] = type,
                ["currency_code"] = currency,
            },
            cancellationToken).ConfigureAwait(false);
        return accountId;
    }

    /// <summary>PHP posts manual lines straight through; an unknown COA id would fail on the FK-less insert.</summary>
    private static async Task AssertCoaLinesAsync(
        DbConnection connection,
        IReadOnlyList<ErpGlLine> lines,
        CancellationToken cancellationToken)
    {
        foreach (var line in lines)
        {
            var exists = line.CoaId > 0
                && await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `id` = ? AND `active` = 1 LIMIT 1"),
                    cancellationToken,
                    line.CoaId).ConfigureAwait(false) > 0;
            if (!exists)
            {
                throw new ErpWriteException(
                    "GL account " + line.CoaId.ToString(CultureInfo.InvariantCulture) + " not found");
            }
        }
    }

    private static async Task<(string JournalNo, long CompanyId, long ReversedByJournalId)?> JournalHeadAsync(
        DbConnection connection,
        long journalId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `journal_no`, `company_id`, COALESCE(`reversed_by_journal_id`, 0)"
            + " FROM `epc_erp_gl_journals` WHERE `id` = ? AND `active` = 1 LIMIT 1");
        ErpDb.AddParameters(command, journalId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return (
            reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
            reader.IsDBNull(1) ? 0L : reader.GetInt64(1),
            reader.IsDBNull(2) ? 0L : reader.GetInt64(2));
    }

    private static async Task<List<ErpGlLine>> JournalLinesAsync(
        DbConnection connection,
        long journalId,
        CancellationToken cancellationToken)
    {
        var lines = new List<ErpGlLine>();
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional(
            "SELECT `coa_id`, `debit`, `credit`, `line_note` FROM `epc_erp_gl_lines` WHERE `journal_id` = ? ORDER BY `id` ASC");
        ErpDb.AddParameters(command, journalId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            lines.Add(new ErpGlLine(
                reader.IsDBNull(0) ? 0L : reader.GetInt64(0),
                reader.IsDBNull(1) ? 0m : reader.GetDecimal(1),
                reader.IsDBNull(2) ? 0m : reader.GetDecimal(2),
                reader.IsDBNull(3) ? string.Empty : reader.GetString(3)));
        }

        return lines;
    }

    private static async Task<string> JournalNoAsync(DbConnection connection, long journalId, CancellationToken cancellationToken)
        => await ErpDb.StringAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `journal_no` FROM `epc_erp_gl_journals` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            journalId).ConfigureAwait(false) ?? string.Empty;

    /// <summary>PHP <c>epc_erp_gl_link_cash_coa</c>, applied to the account just created.</summary>
    private static async Task LinkCashCoaAsync(
        DbConnection connection,
        long accountId,
        string accountType,
        CancellationToken cancellationToken)
    {
        try
        {
            var coaId = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = ? LIMIT 1"),
                cancellationToken,
                string.Equals(accountType, "bank", StringComparison.Ordinal) ? "1010" : "1000").ConfigureAwait(false);
            if (coaId <= 0)
            {
                return;
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional("UPDATE `epc_erp_cash_bank_accounts` SET `coa_id` = ? WHERE `id` = ? AND `coa_id` = 0"),
                cancellationToken,
                coaId,
                accountId).ConfigureAwait(false);
        }
        catch (DbException)
        {
            // Chart of accounts not installed yet — PHP's link pass is equally best-effort.
        }
    }

    private Task LogAsync(
        DbConnection connection,
        int adminId,
        string action,
        long entityId,
        string summary,
        IReadOnlyDictionary<string, string?> detail,
        CancellationToken cancellationToken)
        => _audit.LogAsync(
            connection,
            null,
            adminId,
            action,
            action.StartsWith("gl_", StringComparison.Ordinal) ? "gl_journal" : "account",
            entityId,
            summary,
            detail,
            cancellationToken);

    private void EnsureConfigured()
    {
        if (!_connections.IsConfigured)
        {
            throw new ErpWriteException("No database");
        }
    }

    /// <summary>Subset of PHP <c>epc_erp_gl_ensure_schema</c> for the chart of accounts.</summary>
    private static Task EnsureCoaSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
        => ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_coa_accounts` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `code` varchar(16) NOT NULL,"
            + " `name` varchar(255) NOT NULL,"
            + " `account_type` enum('asset','liability','equity','revenue','expense') NOT NULL,"
            + " `normal_side` enum('debit','credit') NOT NULL DEFAULT 'debit',"
            + " `parent_id` int(11) NOT NULL DEFAULT 0,"
            + " `opening_balance` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `description` varchar(512) DEFAULT NULL,"
            + " `system_flag` tinyint(1) NOT NULL DEFAULT 0,"
            + " `active` tinyint(1) NOT NULL DEFAULT 1,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`),"
            + " UNIQUE KEY `x_code` (`code`),"
            + " KEY `x_type` (`account_type`,`active`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP chart of accounts'",
            cancellationToken);

    /// <summary>Subset of PHP <c>epc_erp_ensure_schema</c> for the cash/bank account master.</summary>
    private static async Task EnsureCashAccountSchemaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_cash_bank_accounts` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `name` varchar(255) NOT NULL,"
            + " `account_type` enum('cash','bank') NOT NULL DEFAULT 'cash',"
            + " `bank_name` varchar(255) DEFAULT NULL,"
            + " `account_number` varchar(64) DEFAULT NULL,"
            + " `currency_code` varchar(8) NOT NULL DEFAULT 'AED',"
            + " `opening_balance` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `office_id` int(11) NOT NULL DEFAULT 0,"
            + " `active` tinyint(1) NOT NULL DEFAULT 1,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`),"
            + " KEY `x_active` (`active`),"
            + " KEY `x_office` (`office_id`)"
            + ") ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='ERP cash and bank accounts'",
            cancellationToken).ConfigureAwait(false);

        // PHP adds these through epc_erp_schema_add_column_if_missing for tenant
        // databases created before treasury/multi-entity fields existed.
        foreach (var column in new[]
        {
            "`coa_id` int(11) NOT NULL DEFAULT 0",
            "`legal_entity_id` int(11) NOT NULL DEFAULT 0",
            "`business_unit_id` int(11) NOT NULL DEFAULT 0",
            "`gl_account_id` int(11) NOT NULL DEFAULT 0",
            "`iban` varchar(64) DEFAULT NULL",
            "`swift_bic` varchar(32) DEFAULT NULL",
            "`bank_branch` varchar(255) DEFAULT NULL",
            "`routing_code` varchar(64) DEFAULT NULL",
            "`address` varchar(512) DEFAULT NULL",
            "`contact_name` varchar(255) DEFAULT NULL",
            "`contact_phone` varchar(64) DEFAULT NULL",
            "`contact_email` varchar(128) DEFAULT NULL",
            "`status` varchar(16) NOT NULL DEFAULT 'active'",
            "`notes` varchar(1000) DEFAULT NULL",
        })
        {
            await ErpDb.TryExecuteAsync(
                connection,
                "ALTER TABLE `epc_erp_cash_bank_accounts` ADD " + column,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
