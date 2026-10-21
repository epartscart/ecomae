namespace EcomAE.Platform.Erp;

/// <summary>
/// SHAM Phase-5 tail: UAE VAT return input — per-entity VAT summary over a period.
///
/// Reads posted GL journal lines joined to journals for the entity:
/// output VAT = credits on 2100 (sales), input VAT = debits on 1150 (purchases),
/// net = output − input (positive = payable to FTA). Optional BU narrows the
/// period to one business unit. Entity isolation is by the journal's
/// <c>company_id</c> with a line-level <c>legal_entity_id</c> fallback.
/// </summary>
public sealed record ErpShamVatSummary(
    bool Ok,
    string Message,
    string Entity,
    string Bu,
    long FromTs,
    long ToTs,
    decimal OutputVat,
    decimal InputVat,
    decimal NetPayable,
    int JournalCount);

public interface IErpShamVatSummaryReadService
{
    Task<ErpShamVatSummary> LoadAsync(
        string entityCode,
        string? buCode,
        long fromTs,
        long toTs,
        string userEmail,
        CancellationToken cancellationToken = default);
}

public sealed class ErpShamVatSummaryReadService : IErpShamVatSummaryReadService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpShamFoundationService _foundation;

    public ErpShamVatSummaryReadService(IErpWriteConnectionFactory connections, IErpShamFoundationService foundation)
    {
        _connections = connections;
        _foundation = foundation;
    }

    public async Task<ErpShamVatSummary> LoadAsync(
        string entityCode,
        string? buCode,
        long fromTs,
        long toTs,
        string userEmail,
        CancellationToken cancellationToken = default)
    {
        var entity = (entityCode ?? string.Empty).Trim().ToUpperInvariant();
        var bu = (buCode ?? string.Empty).Trim().ToUpperInvariant();
        if (entity.Length == 0)
        {
            return new(false, "Entity code is required (?cmp=).", entity, bu, fromTs, toTs, 0m, 0m, 0m, 0);
        }

        if (!_connections.IsConfigured)
        {
            return new(false, "TenantRegistry DB is not configured.", entity, bu, fromTs, toTs, 0m, 0m, 0m, 0);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var entityId = await ErpDb.LongAsync(connection, null,
                ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_legal_entities` WHERE `code`=? AND `active`=1 LIMIT 1"),
                cancellationToken, entity).ConfigureAwait(false);
            if (entityId == 0)
            {
                return new(false, "Unknown or inactive legal entity '" + entity + "'.", entity, bu, fromTs, toTs, 0m, 0m, 0m, 0);
            }

            // Resolve numeric selectors through the ORG BU table (id->code) so
            // the scoped totals and the access grant evaluate the same BU —
            // pm and org ids differ and matching pm.id directly could expose a
            // different BU than the one the grant was checked against.
            bu = await _foundation.NormalizeBuCodeAsync(entityId, bu, cancellationToken).ConfigureAwait(false);
            bu = bu.ToUpperInvariant();
            var buId = 0L;
            if (bu.Length > 0)
            {
                buId = await ErpDb.LongAsync(connection, null,
                    ErpDb.Positional("SELECT `id` FROM `epc_erp_pm_business_units` WHERE `code`=? AND `legal_entity_id`=? AND `active`=1 LIMIT 1"),
                    cancellationToken, bu, entityId).ConfigureAwait(false);
                if (buId == 0)
                {
                    return new(false, "Business unit '" + bu + "' does not exist under entity " + entity + ".", entity, bu, fromTs, toTs, 0m, 0m, 0m, 0);
                }
            }

            // Reads are scoped like writes — a 'vat-view' grant in the entity (and
            // BU when supplied) is required before tax totals are returned.
            if (!await _foundation.AccessAllowedAsync(userEmail, entity, bu.Length > 0 ? bu : null, "vat-view", cancellationToken).ConfigureAwait(false))
            {
                return new(false, "No active grant for '" + userEmail + "' to view VAT totals in " + entity + (bu.Length > 0 ? "/" + bu : "") + ".", entity, bu, fromTs, toTs, 0m, 0m, 0m, 0);
            }

            var output = 0m;
            var input = 0m;
            var journals = 0;
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = ErpDb.Positional(
                    "SELECT COALESCE(SUM(CASE WHEN a.`code`='2100' THEN l.`credit`-l.`debit` ELSE 0 END),0),"
                    + " COALESCE(SUM(CASE WHEN a.`code`='1150' THEN l.`debit`-l.`credit` ELSE 0 END),0),"
                    + " COUNT(DISTINCT j.`id`)"
                    + " FROM `epc_erp_gl_lines` l"
                    + " JOIN `epc_erp_gl_journals` j ON j.`id`=l.`journal_id` AND j.`active`=1 AND j.`company_id`=?"
                    + " JOIN `epc_erp_coa_accounts` a ON a.`id`=l.`coa_id`"
                    + " WHERE a.`code` IN ('2100','1150') AND (?=0 OR l.`legal_entity_id`=?)"
                    + " AND (?=0 OR l.`business_unit_id`=?)"
                    + " AND (?=0 OR (CASE WHEN j.`journal_date` < 100000000 THEN IFNULL(UNIX_TIMESTAMP(STR_TO_DATE(CAST(j.`journal_date` AS CHAR),'%Y%m%d')),0) ELSE j.`journal_date` END)>=?)"
                    + " AND (?=0 OR (CASE WHEN j.`journal_date` < 100000000 THEN IFNULL(UNIX_TIMESTAMP(STR_TO_DATE(CAST(j.`journal_date` AS CHAR),'%Y%m%d')),0) ELSE j.`journal_date` END)<=?)");
                ErpDb.AddParameters(cmd, entityId, entityId, entityId, buId, buId, fromTs, fromTs, toTs, toTs);
                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    output = reader.GetDecimal(0);
                    input = reader.GetDecimal(1);
                    journals = (int)reader.GetInt64(2);
                }
            }

            return new(true, "VAT summary for " + entity + (bu.Length > 0 ? "/" + bu : "") + ".", entity, bu, fromTs, toTs, output, input, output - input, journals);
        }
        catch (Exception)
        {
            return new(false, "VAT summary read failed.", entity, bu, fromTs, toTs, 0m, 0m, 0m, 0);
        }
    }
}
