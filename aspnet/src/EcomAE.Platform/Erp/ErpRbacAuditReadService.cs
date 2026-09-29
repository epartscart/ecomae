using System.Data.Common;

namespace EcomAE.Platform.Erp;

public sealed record ErpRbacAuditEntry(
    long Id,
    long Time,
    int AdminId,
    string Action,
    string EntityType,
    long EntityId,
    string Summary);

public interface IErpRbacAuditReadService
{
    Task<IReadOnlyList<ErpRbacAuditEntry>> ListAsync(
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed class ErpRbacAuditReadService : IErpRbacAuditReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpRbacAuditReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<IReadOnlyList<ErpRbacAuditEntry>> ListAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return [];
        }

        var safeLimit = Math.Clamp(limit, 1, 100);
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT `id`,`time`,`admin_id`,`action`,`entity_type`,`entity_id`,`summary` "
                + "FROM `epc_erp_audit_log` "
                + "WHERE `entity_type` LIKE 'rbac_%' "
                + "ORDER BY `time` DESC,`id` DESC LIMIT ?";
            var parameter = command.CreateParameter();
            parameter.Value = safeLimit;
            command.Parameters.Add(parameter);

            var entries = new List<ErpRbacAuditEntry>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                entries.Add(new ErpRbacAuditEntry(
                    Convert.ToInt64(reader["id"]),
                    Convert.ToInt64(reader["time"]),
                    Convert.ToInt32(reader["admin_id"]),
                    Convert.ToString(reader["action"]) ?? string.Empty,
                    Convert.ToString(reader["entity_type"]) ?? string.Empty,
                    Convert.ToInt64(reader["entity_id"]),
                    Convert.ToString(reader["summary"]) ?? string.Empty));
            }

            return entries;
        }
        catch (DbException)
        {
            return [];
        }
    }
}
