using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpFitOutFinanceOperationsReportReadService
{
    Task<ErpFitOutFinanceOperationsReportResult> ReadAsync(
        long? projectId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutFinanceOperationsReportRow(
    string Category,
    string Metric,
    int Count,
    decimal Amount);

public sealed record ErpFitOutFinanceOperationsReportResult(
    IReadOnlyList<ErpFitOutFinanceOperationsReportRow> Rows,
    string Source,
    string Message);

public sealed class ErpFitOutFinanceOperationsReportReadService
    : IErpFitOutFinanceOperationsReportReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutFinanceOperationsReportReadService(
        IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpFitOutFinanceOperationsReportResult> ReadAsync(
        long? projectId,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new([], "migration", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        var filter = projectId is > 0 ? "WHERE `project_id`=?" : string.Empty;
        var andFilter = projectId is > 0 ? "AND `project_id`=?" : string.Empty;
        command.CommandText = ErpDb.Positional($"""
            SELECT 'inventory', 'material_movements', COUNT(*),
                   COALESCE(SUM(
                       CASE
                           WHEN `movement`='return' THEN -(`quantity`*`unit_cost`)
                           ELSE `quantity`*`unit_cost`
                       END),0)
            FROM `ecomae_fitout_material_movements` {filter}
            UNION ALL
            SELECT 'subcontract', 'certifications', COUNT(*),
                   COALESCE(SUM(`amount`),0)
            FROM `ecomae_fitout_delivery_records`
            WHERE `record_type`='subcontract_certification'
              {andFilter}
            UNION ALL
            SELECT 'finance', 'progress_claims', COUNT(*),
                   COALESCE(SUM(`amount`),0)
            FROM `ecomae_fitout_delivery_records`
            WHERE `record_type`='progress_claim'
              {andFilter}
            UNION ALL
            SELECT 'finance', 'recoveries', COUNT(*),
                   COALESCE(SUM(`amount`),0)
            FROM `ecomae_fitout_delivery_records`
            WHERE `record_type` IN ('advance_recovery','retention_recovery')
              {andFilter}
            UNION ALL
            SELECT 'finance', 'client_certifications', COUNT(*),
                   COALESCE(SUM(`amount`),0)
            FROM `ecomae_fitout_delivery_records`
            WHERE `record_type` IN ('progress_claim','client_progress_claim','client_payment_certificate')
              AND `status`='approved'
              {andFilter}
            UNION ALL
            SELECT 'finance', 'subcontract_certifications', COUNT(*),
                   COALESCE(SUM(`amount`),0)
            FROM `ecomae_fitout_delivery_records`
            WHERE `record_type` IN ('subcontract_certification','subcontractor_progress_claim')
              AND `status`='approved'
              {andFilter}
            UNION ALL
            SELECT 'finance', 'retention_releases', COUNT(*),
                   COALESCE(SUM(`amount`),0)
            FROM `ecomae_fitout_delivery_records`
            WHERE `record_type`='retention_release'
              AND `status`='approved'
              {andFilter}
            UNION ALL
            SELECT 'finance', 'approved_payment_vouchers', COUNT(*),
                   COALESCE(SUM(`amount`),0)
            FROM `ecomae_fitout_delivery_records`
            WHERE `record_type`='payment_voucher'
              AND `status`='approved'
              {andFilter}
            ORDER BY 1,2
            """);
        if (projectId is > 0)
        {
            ErpDb.AddParameters(
                command,
                projectId.Value,
                projectId.Value,
                projectId.Value,
                projectId.Value,
                projectId.Value,
                projectId.Value,
                projectId.Value,
                projectId.Value);
        }

        var rows = new List<ErpFitOutFinanceOperationsReportRow>();
        try
        {
            await using var reader = await command
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.GetDecimal(3)));
            }
        }
        catch (DbException exception)
        {
            return new([], "database-error", exception.Message);
        }

        return new(rows, "database", string.Empty);
    }
}
