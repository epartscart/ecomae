using System.Data.Common;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_cft_line_add</c> / ajax <c>cft_line_add</c> twin.
/// INSERT <c>epc_cft_line</c> when the forecast exists.
/// Forecast save, instruments, projection, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpCftLineAddWriteService
{
    Task<ErpSimpleWriteResult> AddAsync(
        ErpCftLineAddWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpCftLineAddWriteRequest(
    long ForecastId = 0,
    string? DueDate = null,
    string? Direction = null,
    decimal Amount = 0,
    string? Category = null,
    string? Source = null,
    string? Notes = null);

public sealed class ErpCftLineAddWriteService : IErpCftLineAddWriteService
{
    private static readonly HashSet<string> Directions = new(StringComparer.Ordinal)
    {
        "in", "out",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpCftLineAddWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddAsync(
        ErpCftLineAddWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ForecastId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Forecast not found");
        }

        var direction = request.Direction ?? "in";
        if (!Directions.Contains(direction))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Direction must be in or out");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var dueDate = request.DueDate ?? string.Empty;
        var category = request.Category ?? string.Empty;
        var source = request.Source ?? string.Empty;
        var notes = request.Notes ?? string.Empty;
        var amount = Math.Abs(request.Amount);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_cft_forecast", "name", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_cft_line", "due_date", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Cash forecast line table is not provisioned");
        }

        var nameObj = await ErpDb.ScalarAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `name` FROM `epc_cft_forecast` WHERE `id`=? LIMIT 1"),
            cancellationToken,
            request.ForecastId).ConfigureAwait(false);
        if (nameObj is null)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Forecast not found");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_cft_line` (`forecast_id`,`due_date`,`direction`,`amount`,`category`,`source`,`notes`) VALUES (?,?,?,?,?,?,?)"),
            cancellationToken,
            request.ForecastId,
            dueDate,
            direction,
            amount,
            category,
            source,
            notes).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Forecast line added", id);
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }
}
