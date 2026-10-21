using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// SHAM Phase-2: entity × BU scoped manual journal posting.
///
/// Chains the foundation gates in order before calling the shared GL engine
/// (<see cref="IErpGlPostingService.PostJournalAsync"/>):
///
/// 1. Entity resolves in <c>epc_erp_pm_legal_entities</c> and is active.
/// 2. BU resolves inside that entity in <c>epc_erp_org_business_units</c> — a BU code
///    that exists under a different entity is rejected (no cross-entity pairing).
/// 3. <see cref="IErpShamFoundationService.AccessAllowedAsync"/> proves the caller's
///    grant for entity × BU × action <c>journal-post</c> — group-wide
///    (<c>entity_id=0</c>) and entity-wide (<c>bu_id=0</c>) grants widen correctly.
/// 4. Every line's account resolves in <c>epc_erp_coa_accounts</c> and is activated
///    for the entity via <see cref="IErpShamFoundationService.AccountActiveForEntityAsync"/>
///    (shared COA taxonomy, per-entity activation — blueprint §6).
///
/// The GL engine itself still runs balance validation, fiscal-period lock, GV
/// numbering and the posting transaction — this service only adds the SHAM scope
/// checks and stamps <c>company_id</c>/<c>legal_entity_id</c>/<c>business_unit_id</c>.
/// </summary>
public sealed record ErpShamJournalLine(string Account, decimal Debit, decimal Credit, string Note);

public sealed record ErpShamJournalPostResult(bool Ok, string Message, long JournalId);

public interface IErpShamJournalPostService
{
    Task<ErpShamJournalPostResult> PostAsync(
        string entityCode,
        string? buCode,
        long journalDate,
        string reference,
        string description,
        string currencyCode,
        decimal exchangeRate,
        IReadOnlyList<ErpShamJournalLine> lines,
        string userEmail,
        int adminId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpShamJournalPostService : IErpShamJournalPostService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpShamFoundationService _foundation;
    private readonly IErpGlPostingService _posting;

    public ErpShamJournalPostService(
        IErpWriteConnectionFactory connections,
        IErpShamFoundationService foundation,
        IErpGlPostingService posting)
    {
        _connections = connections;
        _foundation = foundation;
        _posting = posting;
    }

    public async Task<ErpShamJournalPostResult> PostAsync(
        string entityCode,
        string? buCode,
        long journalDate,
        string reference,
        string description,
        string currencyCode,
        decimal exchangeRate,
        IReadOnlyList<ErpShamJournalLine> lines,
        string userEmail,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        var entity = (entityCode ?? string.Empty).Trim().ToUpperInvariant();
        var bu = (buCode ?? string.Empty).Trim().ToUpperInvariant();
        if (entity.Length == 0)
        {
            return new(false, "Entity code is required (?cmp= or the entity field).", 0);
        }

        if (lines is null || lines.Count == 0)
        {
            return new(false, "Journal requires at least one line.", 0);
        }

        if (!_connections.IsConfigured)
        {
            return new(false, "TenantRegistry DB is not configured.", 0);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpShamFoundationService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            await _posting.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

            var entityId = await ErpDb.LongAsync(connection, null,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code`=? AND `active`=1 LIMIT 1"),
                cancellationToken, entity).ConfigureAwait(false);
            if (entityId == 0)
            {
                return new(false, "Unknown or inactive legal entity '" + entity + "'.", 0);
            }

            var buId = 0L;
            if (bu.Length > 0)
            {
                buId = await ErpDb.LongAsync(connection, null,
                    ErpDb.Positional("SELECT `id` FROM `epc_erp_org_business_units` WHERE `code`=? AND `entity_id`=? AND `status`='active' LIMIT 1"),
                    cancellationToken, bu, entityId).ConfigureAwait(false);
                if (buId == 0)
                {
                    return new(false, "Business unit '" + bu + "' does not exist under entity " + entity + " (or is disabled).", 0);
                }
            }

            // Access matrix: user × entity × BU × action 'journal-post'. Fail-closed.
            var allowed = await _foundation.AccessAllowedAsync(
                userEmail, entity, bu.Length > 0 ? bu : null, "journal-post", cancellationToken).ConfigureAwait(false);
            if (!allowed)
            {
                return new(false, "No active grant for '" + userEmail + "' to post journals in " + entity + (bu.Length > 0 ? "/" + bu : "") + ".", 0);
            }

            // Resolve + activate each account for this entity (shared taxonomy gate).
            var glLines = new List<ErpGlLine>(lines.Count);
            foreach (var line in lines)
            {
                var account = (line.Account ?? string.Empty).Trim();
                var coaId = await ErpDb.LongAsync(connection, null,
                    ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code`=? LIMIT 1"),
                    cancellationToken, account).ConfigureAwait(false);
                if (coaId == 0)
                {
                    return new(false, "Account '" + account + "' is not in the chart of accounts.", 0);
                }

                var active = await _foundation.AccountActiveForEntityAsync(entity, account, cancellationToken).ConfigureAwait(false);
                if (!active)
                {
                    return new(false, "Account " + account + " is not activated for entity " + entity + " (org_entity_accounts).", 0);
                }

                glLines.Add(new ErpGlLine(coaId, line.Debit, line.Credit, line.Note ?? string.Empty, BusinessUnitId: buId, LegalEntityId: entityId));
            }

            var header = new ErpGlJournalHeader
            {
                JournalDate = journalDate,
                Reference = reference ?? string.Empty,
                Description = description ?? string.Empty,
                SourceType = "sham_manual",
                CompanyId = entityId,
                BusinessUnitId = buId,
                CurrencyCode = currencyCode ?? string.Empty,
                ExchangeRate = exchangeRate,
            };

            var journalId = await _posting.PostJournalAsync(connection, header, glLines, adminId, cancellationToken).ConfigureAwait(false);
            return new(true, "Journal GV posted for " + entity + (bu.Length > 0 ? " / BU " + bu : "") + " (journal id " + journalId + ").", journalId);
        }
        catch (DbException ex)
        {
            return new(false, "Journal post failed: " + ex.Message, 0);
        }
        catch (InvalidOperationException ex)
        {
            return new(false, "Journal post refused: " + ex.Message, 0);
        }
    }
}
