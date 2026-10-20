using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Posts auto-parts trading journals through the shared GL so parts sales,
/// COGS, stock purchases and receipts appear on the income statement and
/// balance sheet. Mirrors <see cref="ErpFitOutGl"/>.
/// </summary>
public static class ErpAutoPartsGl
{
    public static async Task EnsureCoaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var row in ErpAutoPartsIfrs.Accounts)
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

    /// <summary>Journal id already posted for this unique AP-* reference — the idempotency key that makes re-posting safe.</summary>
    public static async Task<long> PostedJournalIdAsync(
        DbConnection connection,
        string reference,
        CancellationToken cancellationToken)
    {
        if (reference.Length == 0)
        {
            return 0;
        }

        return await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT `id` FROM `epc_erp_gl_journals` WHERE `source_type`='adjustment' AND `reference`=? AND `active`=1 ORDER BY `id` LIMIT 1"),
            cancellationToken,
            reference).ConfigureAwait(false);
    }

    public static async Task<long> PostAsync(
        IErpGlPostingService gl,
        DbConnection connection,
        DbTransaction? transaction,
        int actorId,
        long companyId,
        long sourceId,
        string reference,
        string description,
        IReadOnlyList<ErpAutoPartsIfrs.JournalLine> recipe,
        long journalDate,
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

        // source_type is a fixed enum shared with PHP documents; like fit-out
        // postings, auto-parts journals carry 'adjustment' and a unique AP-* reference.
        return await gl.PostJournalAsync(
            connection,
            transaction,
            new ErpGlJournalHeader
            {
                JournalDate = journalDate > 0 ? journalDate : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Reference = reference,
                Description = description,
                SourceType = "adjustment",
                SourceId = sourceId,
                CompanyId = companyId,
                LegislationRef = "IFRS15",
            },
            lines,
            actorId,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> CoaIdAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string code,
        CancellationToken cancellationToken)
        => await ErpDb.LongAsync(
            connection,
            transaction,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code`=? ORDER BY `id` LIMIT 1"),
            cancellationToken,
            code).ConfigureAwait(false);
}
