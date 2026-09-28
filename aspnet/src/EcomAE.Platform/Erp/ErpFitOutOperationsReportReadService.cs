using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpFitOutOperationsReportReadService
{
    Task<ErpFitOutOperationsReportResult> ReadAsync(
        long? projectId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutOperationsReportRow(
    string Category,
    string Metric,
    int Count,
    decimal Amount);

public sealed record ErpFitOutOperationsReportResult(
    IReadOnlyList<ErpFitOutOperationsReportRow> Rows,
    string Source,
    string Message);

public sealed class ErpFitOutOperationsReportReadService
    : IErpFitOutOperationsReportReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutOperationsReportReadService(
        IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpFitOutOperationsReportResult> ReadAsync(
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
            SELECT 'estimation', 'estimates', COUNT(*),
                   COALESCE(SUM(`markup_percent`),0)
            FROM `ecomae_fitout_estimates` {filter}
            UNION ALL
            SELECT 'estimation', 'boq_lines', COUNT(*),
                   COALESCE(SUM(`quantity`*`unit_rate`),0)
            FROM `ecomae_fitout_boq_lines`
            WHERE `estimate_id` IN (
                SELECT `id` FROM `ecomae_fitout_estimates` {filter})
            UNION ALL
            SELECT 'procurement', 'linked_documents', COUNT(*),
                   COALESCE(SUM(`committed_amount`),0)
            FROM `ecomae_fitout_procurement_links` {filter}
            UNION ALL
            SELECT 'sales', 'quotations', COUNT(*),
                   COALESCE(SUM(`revision`),0)
            FROM `ecomae_fitout_quotations`
            WHERE `estimate_id` IN (
                SELECT `id` FROM `ecomae_fitout_estimates` {filter})
            UNION ALL
            SELECT 'delivery', 'approved_records', COUNT(*),
                   COALESCE(SUM(`amount`),0)
            FROM `ecomae_fitout_delivery_records`
            WHERE `status`='approved' {andFilter}
            UNION ALL
            SELECT 'delivery', 'pending_approvals', COUNT(*),
                   COALESCE(SUM(`amount`),0)
            FROM `ecomae_fitout_delivery_records`
            WHERE `status`='pending'
              AND `record_type` IN (
                  'approval_request','site_engineer_approval','project_manager_approval',
                  'variation_approval','final_settlement','work_completion_certificate',
                  'subcontract_payment_certificate','client_payment_certificate',
                  'retention_release','vendor_bill','payment_voucher'
              ) {andFilter}
            UNION ALL
            SELECT 'delivery', 'retention_ledger', COUNT(*),
                   COALESCE(SUM(`amount`),0)
            FROM `ecomae_fitout_retention_ledger`
            {filter}
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
                projectId.Value);
        }

        var rows = new List<ErpFitOutOperationsReportRow>();
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
