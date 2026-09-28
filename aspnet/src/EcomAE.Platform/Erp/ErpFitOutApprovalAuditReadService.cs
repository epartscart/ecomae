using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpFitOutApprovalAuditReadService
{
    Task<IReadOnlyList<ErpFitOutApprovalAuditRow>> ReadAsync(
        long projectId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutApprovalAuditRow(
    long Id,
    long DeliveryRecordId,
    long ProjectId,
    string RecordType,
    string Reference,
    string Decision,
    long AdminId,
    DateTime DecidedAtUtc);

public sealed class ErpFitOutApprovalAuditReadService
    : IErpFitOutApprovalAuditReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutApprovalAuditReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<IReadOnlyList<ErpFitOutApprovalAuditRow>> ReadAsync(
        long projectId,
        CancellationToken cancellationToken = default)
    {
        if (projectId <= 0 || !_connections.IsConfigured)
        {
            return [];
        }

        await using var connection = await _connections
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("""
                SELECT audit.`id`,audit.`delivery_record_id`,records.`project_id`,
                       records.`record_type`,records.`reference`,audit.`decision`,
                       audit.`admin_id`,audit.`decided_at_utc`
                FROM `ecomae_fitout_approval_audit` audit
                INNER JOIN `ecomae_fitout_delivery_records` records
                    ON records.`id`=audit.`delivery_record_id`
                WHERE records.`project_id`=?
                ORDER BY audit.`decided_at_utc` DESC,audit.`id` DESC
                """);
            ErpDb.AddParameters(command, projectId);
            var rows = new List<ErpFitOutApprovalAuditRow>();
            await using var reader = await command
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(
                    reader.GetInt64(0),
                    reader.GetInt64(1),
                    reader.GetInt64(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetInt64(6),
                    reader.GetDateTime(7)));
            }

            return rows;
        }
        catch (DbException)
        {
            return [];
        }
    }
}
