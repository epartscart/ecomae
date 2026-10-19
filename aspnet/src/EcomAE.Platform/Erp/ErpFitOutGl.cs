using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Posts IFRS 15 over-time fit-out journals through the shared GL so project
/// cost, revenue, advances, retention and certification appear on the income
/// statement and balance sheet. Mirrors <see cref="ErpRealEstateGl"/>.
/// </summary>
public static class ErpFitOutGl
{
    public static async Task EnsureCoaAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var row in ErpFitOutIfrs.Accounts)
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

    /// <summary>Journal id already posted for this source row — the idempotency key that makes re-posting safe.</summary>
    public static async Task<long> PostedJournalIdAsync(
        DbConnection connection,
        string sourceType,
        long sourceId,
        CancellationToken cancellationToken)
    {
        if (sourceId <= 0)
        {
            return 0;
        }

        return await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional(
                "SELECT `id` FROM `epc_erp_gl_journals` WHERE `source_type`=? AND `source_id`=? AND `active`=1 ORDER BY `id` LIMIT 1"),
            cancellationToken,
            sourceType,
            sourceId).ConfigureAwait(false);
    }

    public static async Task<long> PostAsync(
        IErpGlPostingService gl,
        DbConnection connection,
        DbTransaction? transaction,
        int actorId,
        long companyId,
        long sourceId,
        string sourceType,
        string reference,
        string description,
        IReadOnlyList<ErpFitOutIfrs.JournalLine> recipe,
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

        return await gl.PostJournalAsync(
            connection,
            transaction,
            new ErpGlJournalHeader
            {
                JournalDate = journalDate > 0 ? journalDate : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Reference = reference,
                Description = description,
                SourceType = sourceType,
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
