using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpFitOutApprovalQueueReadService
{
    Task<ErpFitOutApprovalQueueResult> ReadAsync(
        long? projectId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutApprovalQueueRow(
    long Id,
    long ProjectId,
    string RecordType,
    string Reference,
    string Title,
    decimal Amount,
    string Status,
    DateOnly EventDate);

public sealed record ErpFitOutApprovalQueueResult(
    IReadOnlyList<ErpFitOutApprovalQueueRow> Rows,
    decimal TotalAmount,
    string Source,
    string Message);

public sealed class ErpFitOutApprovalQueueReadService
    : IErpFitOutApprovalQueueReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutApprovalQueueReadService(
        IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpFitOutApprovalQueueResult> ReadAsync(
        long? projectId,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return Empty("TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        var projectFilter = projectId is > 0 ? " AND `project_id`=?" : string.Empty;
        command.CommandText = ErpDb.Positional($"""
            SELECT `id`,`project_id`,`record_type`,`reference`,`title`,
                   `amount`,`status`,`event_date`
            FROM `ecomae_fitout_delivery_records`
            WHERE `status`='pending'
              AND `record_type` IN (
                  'approval_request','site_engineer_approval','project_manager_approval',
                  'variation_approval','final_settlement','work_completion_certificate',
                  'subcontract_payment_certificate','client_payment_certificate',
                  'retention_release','vendor_bill','payment_voucher'
              ){projectFilter}
            ORDER BY `event_date`,`id`
            """);
        if (projectId is > 0)
        {
            ErpDb.AddParameters(command, projectId.Value);
        }

        var rows = new List<ErpFitOutApprovalQueueRow>();
        try
        {
            await using var reader = await command
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(
                    reader.GetInt64(0),
                    reader.GetInt64(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetDecimal(5),
                    reader.GetString(6),
                    DateOnly.FromDateTime(reader.GetDateTime(7))));
            }
        }
        catch (DbException exception)
        {
            return new([], 0m, "database-error", exception.Message);
        }

        return new(rows, rows.Sum(row => row.Amount), "database", string.Empty);
    }

    private static ErpFitOutApprovalQueueResult Empty(string message) =>
        new([], 0m, "migration", message);
}
