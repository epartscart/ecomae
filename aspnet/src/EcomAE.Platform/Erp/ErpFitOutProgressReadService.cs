using System.Data.Common;

namespace EcomAE.Platform.Erp;

public interface IErpFitOutProgressReadService
{
    Task<ErpFitOutProgressResult> ReadAsync(
        long projectId, string recordType = "", int limit = 200,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutProgressRow(
    long Id, long ProjectId, long CostCodeId, long ParentId, string RecordType,
    string Reference, string Title, string Description, decimal Quantity,
    decimal Amount, decimal CompletionPercent, string EventDate, string Status);

public sealed record ErpFitOutProgressResult(
    IReadOnlyList<ErpFitOutProgressRow> Rows, string Source, string Message);

public sealed class ErpFitOutProgressReadService : IErpFitOutProgressReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpFitOutProgressReadService(IErpWriteConnectionFactory connections)
        => _connections = connections;

    public async Task<ErpFitOutProgressResult> ReadAsync(
        long projectId, string recordType = "", int limit = 200,
        CancellationToken cancellationToken = default)
    {
        if (projectId <= 0)
            return Empty("A project id is required.", "invalid");
        if (!_connections.IsConfigured)
            return Empty("TenantRegistry DB is not configured.", "migration");

        var boundedLimit = Math.Clamp(limit, 1, 500);
        var hasType = !string.IsNullOrWhiteSpace(recordType);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional($"""
            SELECT `id`,`project_id`,`cost_code_id`,`parent_id`,`record_type`,
                   `reference`,`title`,`description`,`quantity`,`amount`,
                   `completion_percent`,`event_date`,`status`
            FROM `ecomae_fitout_delivery_records`
            WHERE `project_id`=? AND `record_type` IN
                  ('progress_claim','client_progress_claim','weighted_progress')
            {(hasType ? "AND `record_type`=?" : string.Empty)}
            ORDER BY `event_date` DESC, `id` DESC
            LIMIT {boundedLimit}
            """);
        if (hasType)
            ErpDb.AddParameters(command, projectId, recordType.Trim());
        else
            ErpDb.AddParameters(command, projectId);

        var rows = new List<ErpFitOutProgressRow>();
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new(
                    reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3),
                    Text(reader, 4), Text(reader, 5), Text(reader, 6), Text(reader, 7),
                    reader.GetDecimal(8), reader.GetDecimal(9), reader.GetDecimal(10),
                    Text(reader, 11), Text(reader, 12)));
            }
        }
        catch (DbException exception)
        {
            return Empty(exception.Message, "database-error");
        }

        return new(rows, "database", string.Empty);
    }

    private static ErpFitOutProgressResult Empty(string message, string source)
        => new([], source, message);

    private static string Text(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
}
