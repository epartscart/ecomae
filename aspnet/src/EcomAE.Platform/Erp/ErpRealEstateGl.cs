using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Posts IAS 40 / IFRS 15 real-estate journals through the existing GL so they
/// appear on the balance sheet and income statement.
/// </summary>
public static class ErpRealEstateGl
{
    public static async Task EnsureCoaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var row in ErpRealEstateIfrs.Accounts)
        {
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    "INSERT IGNORE INTO `epc_erp_coa_accounts` (`code`, `name`, `account_type`, `normal_side`, `description`, `system_flag`, `time_created`)"
                    + " VALUES (?, ?, ?, ?, ?, 1, ?)"),
                cancellationToken,
                row.Code,
                row.Name,
                row.Type,
                row.Side,
                row.Description,
                now).ConfigureAwait(false);
        }
    }

    public static async Task<long> PostAsync(
        IErpGlPostingService gl,
        DbConnection connection,
        DbTransaction transaction,
        int actorId,
        long companyId,
        long sourceId,
        string reference,
        string description,
        IReadOnlyList<ErpRealEstateIfrs.JournalLine> recipe,
        CancellationToken cancellationToken)
    {
        if (gl is null || recipe.Count == 0)
        {
            return 0;
        }

        var lines = new List<ErpGlLine>(recipe.Count);
        foreach (var line in recipe)
        {
            var coa = await CoaIdAsync(connection, transaction, line.Code, cancellationToken).ConfigureAwait(false);
            if (coa <= 0)
            {
                return 0;
            }

            lines.Add(new ErpGlLine(coa, line.Debit, line.Credit, line.Note));
        }

        return await gl.PostJournalAsync(
            connection,
            transaction,
            new ErpGlJournalHeader
            {
                JournalDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Reference = reference,
                Description = description,
                SourceType = "adjustment",
                SourceId = sourceId,
                CompanyId = companyId,
                LegislationRef = "IAS40/IFRS15/IFRS18",
            },
            lines,
            actorId,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task StampAsync(
        DbConnection connection,
        DbTransaction transaction,
        string table,
        long id,
        long journalId,
        CancellationToken cancellationToken,
        string column = "gl_journal_id")
    {
        if (journalId <= 0 || id <= 0 || !SafeIdent(table) || !SafeIdent(column))
        {
            return;
        }

        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `" + table + "` SET `" + column + "`=? WHERE `id`=?"),
                cancellationToken,
                journalId,
                id).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    public static async Task ReverseAsync(
        IErpGlPostingService gl,
        DbConnection connection,
        DbTransaction transaction,
        int actorId,
        long journalId,
        CancellationToken cancellationToken)
    {
        if (gl is null || journalId <= 0)
        {
            return;
        }

        try
        {
            var already = await ErpDb.LongAsync(
                connection,
                transaction,
                ErpDb.Positional("SELECT IFNULL(`reversed_by_journal_id`,0) FROM `epc_erp_gl_journals` WHERE `id`=? LIMIT 1"),
                cancellationToken,
                journalId).ConfigureAwait(false);
            if (already > 0)
            {
                return;
            }
        }
        catch (DbException)
        {
        }

        var companyId = await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT IFNULL(`company_id`,0) FROM `epc_erp_gl_journals` WHERE `id`=? AND `active`=1 LIMIT 1"),
            cancellationToken,
            journalId).ConfigureAwait(false);
        if (companyId < 0)
        {
            return;
        }

        var lines = new List<ErpGlLine>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = ErpDb.Positional(
                "SELECT `coa_id`,`debit`,`credit`,IFNULL(`line_note`,'') FROM `epc_erp_gl_lines` WHERE `journal_id`=?");
            ErpDb.AddParameters(command, journalId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var debit = reader.IsDBNull(1) ? 0m : reader.GetDecimal(1);
                var credit = reader.IsDBNull(2) ? 0m : reader.GetDecimal(2);
                lines.Add(new ErpGlLine(
                    Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture),
                    credit,
                    debit,
                    "Reversal — " + Convert.ToString(reader.GetValue(3), CultureInfo.InvariantCulture)));
            }
        }

        if (lines.Count == 0)
        {
            return;
        }

        var reverseId = await gl.PostJournalAsync(
            connection,
            transaction,
            new ErpGlJournalHeader
            {
                JournalDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Reference = "REV-RE-" + journalId.ToString(CultureInfo.InvariantCulture),
                Description = "IAS 40 / IFRS 15 reversal of real-estate journal " + journalId.ToString(CultureInfo.InvariantCulture),
                SourceType = "adjustment",
                SourceId = journalId,
                CompanyId = companyId,
                LegislationRef = "IAS40/IFRS15/IFRS18",
            },
            lines,
            actorId,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await ErpDb.ExecuteAsync(
                connection,
                transaction,
                ErpDb.Positional("UPDATE `epc_erp_gl_journals` SET `reversed_by_journal_id`=? WHERE `id`=?"),
                cancellationToken,
                reverseId,
                journalId).ConfigureAwait(false);
        }
        catch (DbException)
        {
        }
    }

    private static bool SafeIdent(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && value.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_');

    private static Task<long> CoaIdAsync(
        DbConnection connection,
        DbTransaction transaction,
        string code,
        CancellationToken cancellationToken)
        => ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code` = ? AND `active` = 1 LIMIT 1"),
            cancellationToken,
            code);
}
