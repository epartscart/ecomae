using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// SHAM Phase-5: paired intercompany document — posts two journals in one
/// transaction, one per legal entity, so both sides of an IC trade/recharge land
/// on the right books atomically.
///
/// Entity A journal: Dr <c>accountA</c> / Cr <c>accountACr</c> on entity A's books.
/// Entity B journal: Dr <c>accountB</c> / Cr <c>accountBCr</c> on entity B's books.
/// Both journals share the caller reference and a common <c>source_id</c> (the
/// pair id) so they trace as one document.
///
/// Gates: both entities active → BU resolved inside its entity when supplied →
/// <c>AccessAllowedAsync('ic-post')</c> grant in <b>both</b> entities → each
/// account activated for its own entity — fail-closed everywhere.
/// </summary>
public sealed record ErpShamIcPostResult(bool Ok, string Message, long JournalAId, long JournalBId);

public interface IErpShamIntercompanyPostService
{
    Task<ErpShamIcPostResult> PostAsync(
        string entityACode,
        string? buACode,
        string entityBCode,
        string? buBCode,
        long journalDate,
        string reference,
        string description,
        string accountADebit,
        string accountACredit,
        string accountBDebit,
        string accountBCredit,
        decimal amount,
        string currencyCode,
        decimal exchangeRate,
        string userEmail,
        int adminId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpShamIntercompanyPostService : IErpShamIntercompanyPostService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpShamFoundationService _foundation;
    private readonly IErpGlPostingService _posting;
    private readonly IErpVoucherNumberService _vouchers;

    public ErpShamIntercompanyPostService(
        IErpWriteConnectionFactory connections,
        IErpShamFoundationService foundation,
        IErpGlPostingService posting,
        IErpVoucherNumberService vouchers)
    {
        _connections = connections;
        _foundation = foundation;
        _posting = posting;
        _vouchers = vouchers;
    }


    public async Task<ErpShamIcPostResult> PostAsync(
        string entityACode,
        string? buACode,
        string entityBCode,
        string? buBCode,
        long journalDate,
        string reference,
        string description,
        string accountADebit,
        string accountACredit,
        string accountBDebit,
        string accountBCredit,
        decimal amount,
        string currencyCode,
        decimal exchangeRate,
        string userEmail,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        var entityA = (entityACode ?? string.Empty).Trim().ToUpperInvariant();
        var entityB = (entityBCode ?? string.Empty).Trim().ToUpperInvariant();
        if (entityA.Length == 0 || entityB.Length == 0)
        {
            return new(false, "Both entity codes are required (entityA and entityB).", 0, 0);
        }

        if (string.Equals(entityA, entityB, StringComparison.Ordinal))
        {
            return new(false, "Intercompany requires two different legal entities.", 0, 0);
        }

        if (amount <= 0m)
        {
            return new(false, "Amount must be positive.", 0, 0);
        }

        var accounts = new[] { accountADebit, accountACredit, accountBDebit, accountBCredit };
        if (accounts.Any(a => string.IsNullOrWhiteSpace(a)))
        {
            return new(false, "All four accounts are required (drA, crA, drB, crB).", 0, 0);
        }

        if (!_connections.IsConfigured)
        {
            return new(false, "TenantRegistry DB is not configured.", 0, 0);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            // All schema DDL before the transaction — ambient-tx callers skip it.
            await ErpShamFoundationService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            await _posting.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            await _vouchers.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);


            var entityAId = await ErpDb.LongAsync(connection, null,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code`=? AND `active`=1 LIMIT 1"),
                cancellationToken, entityA).ConfigureAwait(false);
            var entityBId = await ErpDb.LongAsync(connection, null,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code`=? AND `active`=1 LIMIT 1"),
                cancellationToken, entityB).ConfigureAwait(false);
            if (entityAId == 0 || entityBId == 0)
            {
                return new(false, "Unknown or inactive entity: " + (entityAId == 0 ? entityA : entityB) + ".", 0, 0);
            }

            var buAId = await ResolvePmBuAsync(connection, entityAId, entityA, buACode, cancellationToken).ConfigureAwait(false);
            var buBId = await ResolvePmBuAsync(connection, entityBId, entityB, buBCode, cancellationToken).ConfigureAwait(false);
            if (buAId < 0 || buBId < 0)
            {
                return new(false, "Business unit resolution failed — see entity/BU requirements.", 0, 0);
            }

            if (!await _foundation.AccessAllowedAsync(userEmail, entityA, buACode, "ic-post", cancellationToken).ConfigureAwait(false)
                || !await _foundation.AccessAllowedAsync(userEmail, entityB, buBCode, "ic-post", cancellationToken).ConfigureAwait(false))
            {
                return new(false, "No active 'ic-post' grant for '" + userEmail + "' in both " + entityA + " and " + entityB + ".", 0, 0);
            }

            var coaA = await ResolveCoaAsync(connection, entityA, accountADebit!, accountACredit!, cancellationToken).ConfigureAwait(false);
            var coaB = await ResolveCoaAsync(connection, entityB, accountBDebit!, accountBCredit!, cancellationToken).ConfigureAwait(false);
            if (coaA is null || coaB is null)
            {
                return new(false, "An account is missing from the chart or not activated for its entity.", 0, 0);
            }

            // Phase-6 close: neither entity may post the trade into a locked month.
            if (await ErpShamFoundationService.PeriodLockedAsync(connection, entityAId, journalDate, cancellationToken).ConfigureAwait(false)
                || await ErpShamFoundationService.PeriodLockedAsync(connection, entityBId, journalDate, cancellationToken).ConfigureAwait(false))
            {
                return new(false, "Period is locked for " + entityA + " or " + entityB + " — no postings into closed months.", 0, 0);
            }

            var icRef = "IC-" + (reference ?? string.Empty).Trim();

            // Serialize concurrent claims on this pair reference: a named lock makes
            // the check-then-post below atomic across connections (released when the
            // connection closes; held for the transaction's duration).
            var lockHeld = await ErpDb.LongAsync(connection, null,
                ErpDb.Positional("SELECT COALESCE(GET_LOCK(?, 10), 0)"),
                cancellationToken, "sham_ic_" + icRef).ConfigureAwait(false);
            if (lockHeld != 1)
            {
                return new(false, "Another request is posting intercompany pair " + icRef + " — retry in a moment.", 0, 0);
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Idempotent on the pair reference: a retried request finds the
                // already-posted journals instead of double-booking both entities.
                var existing = await ErpDb.LongAsync(connection, transaction,
                    ErpDb.Positional("SELECT MIN(`id`) FROM `epc_erp_gl_journals` WHERE `source_type`='manual' AND `reference`=? AND `company_id` IN (?,?) AND `active`=1"),
                    cancellationToken, icRef, entityAId, entityBId).ConfigureAwait(false);
                if (existing > 0)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return new(false, "Intercompany pair " + icRef + " is already posted (journal " + existing + ") — use a different reference.", 0, 0);
                }

                var journalA = await _posting.PostJournalAsync(
                    connection,
                    transaction,
                    new ErpGlJournalHeader
                    {
                        JournalDate = journalDate,
                        Reference = icRef,
                        Description = (description ?? string.Empty) + " [IC " + entityA + "->" + entityB + "]",
                        SourceType = "manual",
                        SourceId = 0,
                        CompanyId = entityAId,
                        BusinessUnitId = buAId,
                        CurrencyCode = currencyCode ?? string.Empty,
                        ExchangeRate = exchangeRate,
                    },
                    new List<ErpGlLine>
                    {
                        new(coaA.Value.Dr, amount, 0m, "IC " + entityA + "->" + entityB, BusinessUnitId: buAId, LegalEntityId: entityAId),
                        new(coaA.Value.Cr, 0m, amount, "IC " + entityA + "->" + entityB, BusinessUnitId: buAId, LegalEntityId: entityAId),
                    },
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                // Pair id = journal A's id — unique across calls, and both rows carry it
                // (A self-references) so the two sides trace as one document.
                await ErpDb.ExecuteAsync(connection, transaction,
                    ErpDb.Positional("UPDATE `epc_erp_gl_journals` SET `source_id`=? WHERE `id`=?"),
                    cancellationToken, journalA, journalA).ConfigureAwait(false);

                var journalB = await _posting.PostJournalAsync(
                    connection,
                    transaction,
                    new ErpGlJournalHeader
                    {
                        JournalDate = journalDate,
                        Reference = icRef,
                        Description = (description ?? string.Empty) + " [IC " + entityB + "<-" + entityA + "]",
                        SourceType = "manual",
                        SourceId = journalA,
                        CompanyId = entityBId,
                        BusinessUnitId = buBId,
                        CurrencyCode = currencyCode ?? string.Empty,
                        ExchangeRate = exchangeRate,
                    },
                    new List<ErpGlLine>
                    {
                        new(coaB.Value.Dr, amount, 0m, "IC " + entityB + "<-" + entityA, BusinessUnitId: buBId, LegalEntityId: entityBId),
                        new(coaB.Value.Cr, 0m, amount, "IC " + entityB + "<-" + entityA, BusinessUnitId: buBId, LegalEntityId: entityBId),
                    },
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new(true, "Intercompany pair posted: " + entityA + " journal " + journalA + ", " + entityB + " journal " + journalB + " (ref " + icRef + ").", journalA, journalB);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }
        catch (Exception)
        {
            return new(false, "Intercompany posting failed.", 0, 0);
        }
    }

    private static async Task<long> ResolvePmBuAsync(
        DbConnection connection, long entityId, string entity, string? buCode, CancellationToken cancellationToken)
    {
        var bu = (buCode ?? string.Empty).Trim().ToUpperInvariant();
        if (bu.Length == 0)
        {
            return 0;
        }

        var orgBuId = await ErpDb.LongAsync(connection, null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_business_units` WHERE `code`=? AND `legal_entity_id`=? AND `active`=1 LIMIT 1"),
            cancellationToken, bu, entityId).ConfigureAwait(false);
        if (orgBuId == 0)
        {
            return -1;
        }

        var pmId = await ErpDb.LongAsync(connection, null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_business_units` WHERE `code`=? AND `legal_entity_id`=? AND `active`=1 LIMIT 1"),
            cancellationToken, bu, entityId).ConfigureAwait(false);
        return pmId == 0 ? -1 : pmId;
    }

    private async Task<(long Dr, long Cr)?> ResolveCoaAsync(
        DbConnection connection, string entity, string drAccount, string crAccount, CancellationToken cancellationToken)
    {
        var dr = (drAccount ?? string.Empty).Trim();
        var cr = (crAccount ?? string.Empty).Trim();
        var drId = await ErpDb.LongAsync(connection, null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code`=? AND `active`=1 LIMIT 1"),
            cancellationToken, dr).ConfigureAwait(false);
        var crId = await ErpDb.LongAsync(connection, null,
            ErpDb.Positional("SELECT `id` FROM `epc_erp_coa_accounts` WHERE `code`=? AND `active`=1 LIMIT 1"),
            cancellationToken, cr).ConfigureAwait(false);
        if (drId == 0 || crId == 0)
        {
            return null;
        }

        if (!await _foundation.AccountActiveForEntityAsync(entity, dr, cancellationToken).ConfigureAwait(false)
            || !await _foundation.AccountActiveForEntityAsync(entity, cr, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return (drId, crId);
    }
}
