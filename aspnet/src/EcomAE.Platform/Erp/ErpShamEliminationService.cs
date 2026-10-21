namespace EcomAE.Platform.Erp;

/// <summary>
/// SHAM blueprint §10/§12: consolidation eliminations. Scans the period's
/// intercompany journals (reference 'IC-%') posted on the books of the
/// consolidation group's 'full' members (<c>epc_entity_members</c>) and
/// posts one reversing journal per account pair into the group's parent
/// entity, tagged <c>ELIM-&lt;period&gt;</c>. Idempotent per period: a
/// second run refuses rather than double-eliminating.
/// Grant: <c>elim-run</c>.
/// </summary>
public interface IErpShamEliminationService
{
    Task<ErpShamElimResult> RunAsync(string groupEntityCode, int period, string userEmail, CancellationToken cancellationToken = default);
}

public sealed record ErpShamElimResult(bool Ok, string Message, long JournalId, int Lines, decimal Amount);

public sealed class ErpShamEliminationService : IErpShamEliminationService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpShamFoundationService _foundation;
    private readonly IErpGlPostingService _posting;
    private readonly IErpVoucherNumberService _vouchers;

    public ErpShamEliminationService(IErpWriteConnectionFactory connections, IErpShamFoundationService foundation, IErpGlPostingService posting, IErpVoucherNumberService vouchers)
    {
        _connections = connections;
        _foundation = foundation;
        _posting = posting;
        _vouchers = vouchers;
    }

    public async Task<ErpShamElimResult> RunAsync(string groupEntityCode, int period, string userEmail, CancellationToken cancellationToken = default)
    {
        if (period < 200001 || period > 209912)
        {
            return new(false, "period must be YYYYMM.", 0, 0, 0m);
        }
        var parentCode = (groupEntityCode ?? string.Empty).Trim();

        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpShamFoundationService.EnsureSchemaAsync(conn, cancellationToken).ConfigureAwait(false);

        // Default parent = the consolidation group's own parent_entity
        // (epc_entity_groups), not a hardcoded code.
        if (parentCode.Length == 0)
        {
            // Default = the first group whose parent the caller actually has
            // elim-run on — never blindly the oldest group.
            var candidates = new List<string>();
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT `parent_entity` FROM `epc_entity_groups` WHERE `status`='active' ORDER BY `id`";
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var s = reader.GetString(0);
                    if (s.Length > 0) candidates.Add(s);
                }
            }
            foreach (var candidate in candidates)
            {
                if (await _foundation.AccessAllowedAsync(userEmail, candidate, null, "elim-run", cancellationToken).ConfigureAwait(false))
                {
                    parentCode = candidate;
                    break;
                }
            }
            if (parentCode.Length == 0 && candidates.Count > 0) parentCode = candidates[0];
            if (parentCode.Length == 0) parentCode = "MAIN";
        }
        if (!await _foundation.AccessAllowedAsync(userEmail, parentCode, null, "elim-run", cancellationToken).ConfigureAwait(false))
        {
            return new(false, "elim-run grant required for " + parentCode + ".", 0, 0, 0m);
        }

        var parentId = (int)await ErpDb.LongAsync(conn, null,
            "SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code`=? AND `active`=1 LIMIT 1",
            cancellationToken, parentCode).ConfigureAwait(false);
        if (parentId <= 0)
        {
            return new(false, "Unknown parent entity " + parentCode + ".", 0, 0, 0m);
        }

        // Only the group(s) this entity parents — never every group's members.
        var memberIds = new List<int>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = ErpDb.Positional(
                "SELECT DISTINCT e.`id` FROM `epc_entity_members` m"
                + " JOIN `epc_erp_pm_legal_entities` e ON e.`code` = m.`site_key` AND e.`active` = 1"
                + " JOIN `epc_entity_groups` g ON g.`id` = m.`group_id` AND g.`status` = 'active'"
                + " WHERE m.`consolidation` = 'full' AND g.`parent_entity` = ?");
            ErpDb.AddParameters(cmd, parentCode);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                memberIds.Add(reader.GetInt32(0));
            }
        }
        if (memberIds.Count == 0)
        {
            return new(false, "No 'full' consolidation members — nothing to eliminate.", 0, 0, 0m);
        }

        var fromTs = new DateTimeOffset(period / 100, period % 100, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        var toTs = new DateTimeOffset(period / 100, period % 100, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1).ToUnixTimeSeconds() - 1;

        var elimRef = "ELIM-" + period;
        var existing = await ErpDb.LongAsync(conn, null,
            "SELECT `id` FROM `epc_erp_gl_journals` WHERE `source_type`='manual' AND `reference`=? AND `company_id`=? AND `active`=1 LIMIT 1",
            cancellationToken, elimRef, parentId).ConfigureAwait(false);
        if (existing > 0)
        {
            return new(false, $"Elimination {elimRef} already posted (journal {existing}) — reverse it manually to re-run.", 0, 0, 0m);
        }
        if (await ErpShamFoundationService.PeriodLockedAsync(conn, parentId, fromTs, cancellationToken).ConfigureAwait(false))
        {
            return new(false, $"Period {period} is locked for {parentCode}.", 0, 0, 0m);
        }

        var inClause = string.Join(",", memberIds);
        var balances = new Dictionary<int, (decimal Dr, decimal Cr)>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = ErpDb.Positional(
                "SELECT l.`coa_id`, SUM(l.`debit`), SUM(l.`credit`) FROM `epc_erp_gl_lines` l"
                + " JOIN `epc_erp_gl_journals` j ON j.`id` = l.`journal_id`"
                + " WHERE j.`active`=1 AND j.`reference` LIKE 'IC-%'"
                + " AND j.`company_id` IN (" + inClause + ")"
                + " AND j.`journal_date` BETWEEN ? AND ?"
                + " GROUP BY l.`coa_id` HAVING SUM(l.`debit`) <> SUM(l.`credit`) OR SUM(l.`debit`) > 0");
            ErpDb.AddParameters(cmd, fromTs, toTs);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                balances[reader.GetInt32(0)] = (reader.GetDecimal(1), reader.GetDecimal(2));
            }
        }
        if (balances.Count == 0)
        {
            return new(false, $"No intercompany postings in period {period} across full members.", 0, 0, 0m);
        }

        // Voucher sequence schema must exist BEFORE the ambient transaction
        // (PostJournalAsync skips its own ensure when a tx is supplied).
        await ErpGlPostingService.EnsureSchemaAsync(conn, cancellationToken).ConfigureAwait(false);
        await _vouchers.EnsureSchemaAsync(conn, cancellationToken).ConfigureAwait(false);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var lines = new List<ErpGlLine>();
        decimal total = 0m;
        foreach (var (accountId, b) in balances)
        {
            // Reverse the member-book IC balances onto the group parent.
            lines.Add(new ErpGlLine(accountId, b.Cr, b.Dr, "ELIM " + period, LegalEntityId: parentId));
            total += b.Cr;
        }
        var journalId = await _posting.PostJournalAsync(
            conn, tx,
            new ErpGlJournalHeader
            {
                JournalDate = toTs,
                Reference = elimRef,
                Description = $"Consolidation elimination for IC postings in {period} ({memberIds.Count} full members)",
                SourceType = "manual",
                SourceId = 0,
                CompanyId = parentId,
            },
            lines, 0, cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(true, $"Elimination {elimRef} posted (journal {journalId}) across {balances.Count} account(s).", journalId, lines.Count, total);
    }
}
