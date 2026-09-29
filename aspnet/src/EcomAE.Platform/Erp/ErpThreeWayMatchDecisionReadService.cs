using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

public interface IErpThreeWayMatchDecisionReadService
{
    Task<IReadOnlyDictionary<long, ErpThreeWayMatchDecision>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<ErpThreeWayMatchDecision?> GetAsync(
        long purchaseOrderId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpThreeWayMatchDecision(
    string Status,
    string ExceptionReason,
    decimal VarianceAmount,
    decimal ToleranceAmount,
    long AdminId,
    DateTimeOffset UpdatedAtUtc);

public sealed class ErpThreeWayMatchDecisionReadService : IErpThreeWayMatchDecisionReadService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpThreeWayMatchDecisionReadService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<IReadOnlyDictionary<long, ErpThreeWayMatchDecision>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return new Dictionary<long, ErpThreeWayMatchDecision>();
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, cancellationToken).ConfigureAwait(false))
            {
                return new Dictionary<long, ErpThreeWayMatchDecision>();
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT `purchase_order_id`,`match_status`,`exception_reason`,
                       `variance_amount`,`tolerance_amount`,`admin_id`,`updated_at_utc`
                FROM `ecomae_erp_three_way_matches`
                """;
            return await ReadAsync(command, cancellationToken).ConfigureAwait(false);
        }
        catch (DbException)
        {
            return new Dictionary<long, ErpThreeWayMatchDecision>();
        }
    }

    public async Task<ErpThreeWayMatchDecision?> GetAsync(
        long purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured || purchaseOrderId <= 0)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (!await TableExistsAsync(connection, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT `purchase_order_id`,`match_status`,`exception_reason`,
                       `variance_amount`,`tolerance_amount`,`admin_id`,`updated_at_utc`
                FROM `ecomae_erp_three_way_matches`
                WHERE `purchase_order_id`=?
                LIMIT 1
                """;
            AddParameter(command, purchaseOrderId);
            var rows = await ReadAsync(command, cancellationToken).ConfigureAwait(false);
            return rows.TryGetValue(purchaseOrderId, out var decision) ? decision : null;
        }
        catch (DbException)
        {
            return null;
        }
    }

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema=DATABASE() AND table_name='ecomae_erp_three_way_matches'
            """;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0;
    }

    private static async Task<IReadOnlyDictionary<long, ErpThreeWayMatchDecision>> ReadAsync(
        DbCommand command,
        CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var rows = new Dictionary<long, ErpThreeWayMatchDecision>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var purchaseOrderId = Convert.ToInt64(reader["purchase_order_id"], CultureInfo.InvariantCulture);
            rows[purchaseOrderId] = new ErpThreeWayMatchDecision(
                Convert.ToString(reader["match_status"] is DBNull ? string.Empty : reader["match_status"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToString(reader["exception_reason"] is DBNull ? string.Empty : reader["exception_reason"], CultureInfo.InvariantCulture) ?? string.Empty,
                Convert.ToDecimal(reader["variance_amount"] is DBNull ? 0m : reader["variance_amount"], CultureInfo.InvariantCulture),
                Convert.ToDecimal(reader["tolerance_amount"] is DBNull ? 0m : reader["tolerance_amount"], CultureInfo.InvariantCulture),
                Convert.ToInt64(reader["admin_id"] is DBNull ? 0 : reader["admin_id"], CultureInfo.InvariantCulture),
                ReadUtc(reader["updated_at_utc"]));
        }

        return rows;
    }

    private static DateTimeOffset ReadUtc(object value)
    {
        if (value is DateTime dateTime)
        {
            return new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
        }

        return DateTimeOffset.TryParse(
            Convert.ToString(value, CultureInfo.InvariantCulture),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;
    }

    private static void AddParameter(DbCommand command, object value)
    {
        var parameter = command.CreateParameter();
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
