using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// SHAM Phase-4: entity × BU scoped sales-invoice GL posting that closes the
/// reserve → invoice chain.
///
/// One transaction, in order:
/// 1. Entity active in <c>epc_erp_pm_legal_entities</c> (<c>company_id</c>) and —
///    when a BU is supplied — BU inside that entity with its operational
///    <c>epc_erp_pm_business_units</c> id (the id GL dimensions join on).
/// 2. <c>AccessAllowedAsync</c> grant for entity × BU × <c>invoice-post</c>.
/// 3. COA per-entity activation for the accounts the engine debits/credits
///    (1100 receivable, 4000 revenue, 2100 VAT output when tax is present).
/// 4. Optional <c>consumeSourceId</c>: the sales order's open holds are consumed
///    (<c>ConsumeBySourceAsync</c>) inside the same transaction — a shipment that
///    invoices releases its reservation atomically with posting.
/// 5. <c>PostSalesInvoiceAsync</c> stamps <c>company_id</c>/<c>business_unit_id</c>/
///    <c>legal_entity_id</c> on journal + lines; idempotent per invoice.
/// </summary>
public sealed record ErpShamInvoicePostResult(bool Ok, string Message, long JournalId);

public interface IErpShamInvoicePostService
{
    Task<ErpShamInvoicePostResult> PostAsync(
        string entityCode,
        string? buCode,
        ErpGlSalesInvoicePosting invoice,
        string consumeSourceType,
        long consumeSourceId,
        string userEmail,
        int adminId,
        CancellationToken cancellationToken = default);
}

public sealed class ErpShamInvoicePostService : IErpShamInvoicePostService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpShamFoundationService _foundation;
    private readonly IErpGlPostingService _posting;
    private readonly IErpInventoryReservationWriteService _reservations;
    private readonly IErpVoucherNumberService _vouchers;

    public ErpShamInvoicePostService(
        IErpWriteConnectionFactory connections,
        IErpShamFoundationService foundation,
        IErpGlPostingService posting,
        IErpInventoryReservationWriteService reservations,
        IErpVoucherNumberService vouchers)
    {
        _connections = connections;
        _foundation = foundation;
        _posting = posting;
        _reservations = reservations;
        _vouchers = vouchers;
    }

    public async Task<ErpShamInvoicePostResult> PostAsync(
        string entityCode,
        string? buCode,
        ErpGlSalesInvoicePosting invoice,
        string consumeSourceType,
        long consumeSourceId,
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

        if (invoice is null || invoice.InvoiceId <= 0 || string.IsNullOrWhiteSpace(invoice.InvoiceNumber) || invoice.TotalIncl <= 0m)
        {
            return new(false, "An issued invoice is required: invoiceId, number and a positive total.", 0);
        }

        if (!_connections.IsConfigured)
        {
            return new(false, "TenantRegistry DB is not configured.", 0);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            // All schema DDL must run before BeginTransactionAsync — DDL implicitly
            // commits, and the posting/voucher/reservation writers skip EnsureSchema
            // when an ambient transaction exists.
            await ErpShamFoundationService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            await _posting.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            await _vouchers.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            await ErpInventoryReservationWriteService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

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
                var orgBuId = await ErpDb.LongAsync(connection, null,
                    ErpDb.Positional("SELECT `id` FROM `epc_erp_org_business_units` WHERE `code`=? AND `entity_id`=? AND `status`='active' LIMIT 1"),
                    cancellationToken, bu, entityId).ConfigureAwait(false);
                if (orgBuId == 0)
                {
                    return new(false, "Business unit '" + bu + "' does not exist under entity " + entity + " (or is disabled).", 0);
                }

                buId = await ErpDb.LongAsync(connection, null,
                    ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_business_units` WHERE `code`=? AND `legal_entity_id`=? AND `active`=1 LIMIT 1"),
                    cancellationToken, bu, entityId).ConfigureAwait(false);
                if (buId == 0)
                {
                    return new(false, "Business unit '" + bu + "' has no active operational record (epc_erp_pm_business_units) — re-run the foundation seed.", 0);
                }
            }

            var allowed = await _foundation.AccessAllowedAsync(
                userEmail, entity, bu.Length > 0 ? bu : null, "invoice-post", cancellationToken).ConfigureAwait(false);
            if (!allowed)
            {
                return new(false, "No active grant for '" + userEmail + "' to post invoices in " + entity + (bu.Length > 0 ? "/" + bu : "") + ".", 0);
            }

            // Shared-taxonomy gate: the accounts this posting touches must be
            // activated for the entity before the journal is allowed through.
            foreach (var code in invoice.TotalVat > 0m ? new[] { "1100", "4000", "2100" } : new[] { "1100", "4000" })
            {
                if (!await _foundation.AccountActiveForEntityAsync(entity, code, cancellationToken).ConfigureAwait(false))
                {
                    return new(false, "Account " + code + " is not activated for entity " + entity + " (org_entity_accounts).", 0);
                }
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (consumeSourceId > 0)
                {
                    var consumeType = string.IsNullOrWhiteSpace(consumeSourceType)
                        ? ErpInventoryReservationWriteService.SourceSalesOrder
                        : consumeSourceType.Trim();

                    // Scope check: refuse to consume when another legal entity holds
                    // reservations on this document — ConsumeBySourceAsync is
                    // unscoped, so the caller's grant must never clear foreign holds.
                    var foreign = await ErpDb.LongAsync(connection, transaction,
                        ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_inv_reservations` WHERE `source_type`=? AND `source_id`=? AND `status` IN ('held','confirmed') AND `owner_company_id`<>?"),
                        cancellationToken, consumeType, consumeSourceId, entityId).ConfigureAwait(false);
                    if (foreign > 0)
                    {
                        return new(false, "Source document " + consumeSourceId + " has open holds owned by another legal entity — cannot consume across scopes.", 0);
                    }

                    await _reservations.ConsumeBySourceAsync(
                        connection,
                        transaction,
                        consumeType,
                        consumeSourceId,
                        adminId,
                        cancellationToken).ConfigureAwait(false);
                }

                var journalId = await _posting.PostSalesInvoiceAsync(
                    connection,
                    transaction,
                    invoice with { CompanyId = entityId, BusinessUnitId = buId, LegalEntityId = entityId },
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new(true, "Sales invoice " + invoice.InvoiceNumber + " posted to " + entity + (bu.Length > 0 ? "/" + bu : "") + " as journal " + journalId + ".", journalId);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }
        catch (Exception)
        {
            // Never echo DB exception text — it can carry table/SQL detail to callers.
            return new(false, "Invoice posting failed.", 0);
        }
    }
}
