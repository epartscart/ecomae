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

public sealed record ErpShamInvoiceIssueResult(bool Ok, string Message, long InvoiceId);

public interface IErpShamInvoicePostService
{
    Task<ErpShamInvoiceIssueResult> IssueAsync(
        string entityCode,
        string? buCode,
        string invoiceNumber,
        string customer,
        decimal subtotal,
        decimal totalVat,
        decimal totalIncl,
        long orderId,
        string userEmail,
        int adminId,
        CancellationToken cancellationToken = default);

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

        if (invoice is null || invoice.InvoiceId <= 0)
        {
            return new(false, "An issued invoice document id is required (invoiceId).", 0);
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
            await EnsureInvoiceStoreAsync(connection, cancellationToken).ConfigureAwait(false);

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

            // The invoice must exist as an ISSUED document in this entity's store —
            // a grant alone cannot fabricate receivable/revenue entries.
            var docVat = 0m;
            var docTotal = 0m;
            var docSubtotal = 0m;
            var docOrderId = 0L;
            string? docStatus = null;
            string? docNumber = null;
            await using (var docCmd = connection.CreateCommand())
            {
                docCmd.CommandText = ErpDb.Positional(
                    "SELECT `invoice_number`,`subtotal`,`vat`,`total`,`order_id`,`status` FROM `epc_erp_sham_sales_invoices` WHERE `id`=? AND `entity_id`=? LIMIT 1");
                ErpDb.AddParameters(docCmd, invoice.InvoiceId, entityId);
                await using var docReader = await docCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await docReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    docNumber = docReader.GetString(0);
                    docSubtotal = docReader.GetDecimal(1);
                    docVat = docReader.GetDecimal(2);
                    docTotal = docReader.GetDecimal(3);
                    docOrderId = docReader.GetInt64(4);
                    docStatus = docReader.GetString(5);
                }
            }

            if (docStatus is null)
            {
                return new(false, "Invoice " + invoice.InvoiceId + " is not an issued document in entity " + entity + " — issue it via /erp/sham/sales-invoice-issue first.", 0);
            }

            if (!string.Equals(docStatus, "issued", StringComparison.Ordinal))
            {
                return new(false, "Invoice " + docNumber + " is '" + docStatus + "' — only issued documents post.", 0);
            }

            // The stored document is authoritative — caller-supplied amounts never
            // override what the entity issued.
            invoice = invoice with { InvoiceNumber = docNumber!, Subtotal = docSubtotal, TotalVat = docVat, TotalIncl = docTotal };
            if (consumeSourceId <= 0 && docOrderId > 0)
            {
                consumeSourceId = docOrderId;
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
                    // Normalize the source type exactly like the engine: anything that is
                    // not 'quote' is 'sales_order', so the guard and the scoped consume
                    // can never disagree on which holds they inspect.
                    var consumeType = string.Equals(consumeSourceType?.Trim(), ErpInventoryReservationWriteService.SourceQuote, StringComparison.OrdinalIgnoreCase)
                        ? ErpInventoryReservationWriteService.SourceQuote
                        : ErpInventoryReservationWriteService.SourceSalesOrder;

                    // Scope check: refuse to consume when another legal entity holds
                    // reservations on this document — the caller's grant must never
                    // clear foreign holds.
                    var foreign = await ErpDb.LongAsync(connection, transaction,
                        ErpDb.Positional("SELECT COUNT(*) FROM `epc_erp_inv_reservations` WHERE `source_type`=? AND `source_id`=? AND `status` IN ('held','confirmed') AND `owner_company_id`<>?"),
                        cancellationToken, consumeType, consumeSourceId, entityId).ConfigureAwait(false);
                    if (foreign > 0)
                    {
                        return new(false, "Source document " + consumeSourceId + " has open holds owned by another legal entity — cannot consume across scopes.", 0);
                    }

                    // Owner-scoped consume inside the transaction: only this entity's
                    // holds are marked consumed — a foreign hold inserted after the
                    // check cannot be swept up.
                    await ErpDb.ExecuteAsync(connection, transaction,
                        ErpDb.Positional("UPDATE `epc_erp_inv_reservations` SET `status`='consumed', `consumed_at`=?, `expires_at`=0, `time_updated`=? WHERE `source_type`=? AND `source_id`=? AND `status` IN ('held','confirmed') AND `owner_company_id`=?"),
                        cancellationToken, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), DateTimeOffset.UtcNow.ToUnixTimeSeconds(), consumeType, consumeSourceId, entityId).ConfigureAwait(false);
                }

                var journalId = await _posting.PostSalesInvoiceAsync(
                    connection,
                    transaction,
                    invoice with { CompanyId = entityId, BusinessUnitId = buId, LegalEntityId = entityId },
                    adminId,
                    cancellationToken).ConfigureAwait(false);

                await ErpDb.ExecuteAsync(connection, transaction,
                    ErpDb.Positional("UPDATE `epc_erp_sham_sales_invoices` SET `status`='posted', `journal_id`=? WHERE `id`=? AND `entity_id`=? AND `status`='issued'"),
                    cancellationToken, journalId, invoice.InvoiceId, entityId).ConfigureAwait(false);

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

    /// <summary>
    /// Issues a sales-invoice document into the entity's store — the document
    /// <c>PostAsync</c> then requires before a journal can be booked. Grant:
    /// <c>invoice-issue</c>. Idempotent on (entity, invoice_number).
    /// </summary>
    public async Task<ErpShamInvoiceIssueResult> IssueAsync(
        string entityCode,
        string? buCode,
        string invoiceNumber,
        string customer,
        decimal subtotal,
        decimal totalVat,
        decimal totalIncl,
        long orderId,
        string userEmail,
        int adminId,
        CancellationToken cancellationToken = default)
    {
        var entity = (entityCode ?? string.Empty).Trim().ToUpperInvariant();
        var bu = (buCode ?? string.Empty).Trim().ToUpperInvariant();
        var number = (invoiceNumber ?? string.Empty).Trim();
        if (entity.Length == 0 || number.Length == 0)
        {
            return new(false, "Entity code and invoice number are required.", 0);
        }

        if (subtotal < 0m || totalVat < 0m || totalIncl <= 0m)
        {
            return new(false, "Amounts must be non-negative and total positive.", 0);
        }

        if (!_connections.IsConfigured)
        {
            return new(false, "TenantRegistry DB is not configured.", 0);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpShamFoundationService.EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
            await EnsureInvoiceStoreAsync(connection, cancellationToken).ConfigureAwait(false);

            var entityId = await ErpDb.LongAsync(connection, null,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code`=? AND `active`=1 LIMIT 1"),
                cancellationToken, entity).ConfigureAwait(false);
            if (entityId == 0)
            {
                return new(false, "Unknown or inactive legal entity '" + entity + "'.", 0);
            }

            if (bu.Length > 0)
            {
                var orgBuId = await ErpDb.LongAsync(connection, null,
                    ErpDb.Positional("SELECT `id` FROM `epc_erp_org_business_units` WHERE `code`=? AND `entity_id`=? AND `status`='active' LIMIT 1"),
                    cancellationToken, bu, entityId).ConfigureAwait(false);
                if (orgBuId == 0)
                {
                    return new(false, "Business unit '" + bu + "' does not exist under entity " + entity + " (or is disabled).", 0);
                }
            }

            if (!await _foundation.AccessAllowedAsync(userEmail, entity, bu.Length > 0 ? bu : null, "invoice-issue", cancellationToken).ConfigureAwait(false))
            {
                return new(false, "No active grant for '" + userEmail + "' to issue invoices in " + entity + (bu.Length > 0 ? "/" + bu : "") + ".", 0);
            }

            var existing = await ErpDb.LongAsync(connection, null,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_sham_sales_invoices` WHERE `entity_id`=? AND `invoice_number`=? LIMIT 1"),
                cancellationToken, entityId, number).ConfigureAwait(false);
            if (existing > 0)
            {
                return new(true, "Invoice " + number + " already issued in " + entity + " (document " + existing + ").", existing);
            }

            await ErpDb.ExecuteAsync(connection, null,
                ErpDb.Positional(
                    "INSERT INTO `epc_erp_sham_sales_invoices` (`entity_id`,`bu_code`,`invoice_number`,`customer`,`subtotal`,`vat`,`total`,`order_id`,`status`,`issued_by`,`time_created`) VALUES (?,?,?,?,?,?,?,?,'issued',?,?)"),
                cancellationToken, entityId, bu, number, (customer ?? string.Empty).Trim(), subtotal, totalVat, totalIncl, orderId, adminId, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
            var docId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return new(true, "Invoice " + number + " issued in " + entity + (bu.Length > 0 ? "/" + bu : "") + " (document " + docId + ").", docId);
        }
        catch (Exception)
        {
            return new(false, "Invoice issue failed.", 0);
        }
    }

    private static async Task EnsureInvoiceStoreAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(connection,
            "CREATE TABLE IF NOT EXISTS `epc_erp_sham_sales_invoices` ("
            + " `id` int(11) NOT NULL AUTO_INCREMENT,"
            + " `entity_id` int(11) NOT NULL DEFAULT 0,"
            + " `bu_code` varchar(40) NOT NULL DEFAULT '',"
            + " `invoice_number` varchar(64) NOT NULL DEFAULT '',"
            + " `customer` varchar(255) NOT NULL DEFAULT '',"
            + " `subtotal` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `vat` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `total` decimal(14,2) NOT NULL DEFAULT 0.00,"
            + " `order_id` int(11) NOT NULL DEFAULT 0,"
            + " `journal_id` int(11) NOT NULL DEFAULT 0,"
            + " `status` varchar(12) NOT NULL DEFAULT 'issued',"
            + " `issued_by` int(11) NOT NULL DEFAULT 0,"
            + " `time_created` int(11) NOT NULL DEFAULT 0,"
            + " PRIMARY KEY (`id`),"
            + " UNIQUE KEY `ux_entity_invoice` (`entity_id`,`invoice_number`),"
            + " KEY `x_status` (`status`)) ENGINE=InnoDB DEFAULT CHARSET=utf8 COMMENT='SHAM issued sales invoices (Phase-4 doc store)'",
            cancellationToken).ConfigureAwait(false);
    }
}
