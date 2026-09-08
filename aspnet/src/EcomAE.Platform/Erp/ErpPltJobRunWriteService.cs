using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_plt_batch_run</c> / ajax <c>plt_job_run</c> twin.
/// INSERT <c>epc_plt_batch_run</c> and UPDATE <c>epc_plt_batch_job</c>
/// last/next/status. Job save, feature save, and schema ensure stay PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpPltJobRunWriteService
{
    Task<ErpSimpleWriteResult> RunAsync(
        ErpPltJobRunWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpPltJobRunWriteRequest(
    long JobId = 0,
    string? Status = null,
    string? Message = null);

public sealed class ErpPltJobRunWriteService : IErpPltJobRunWriteService
{
    public static readonly HashSet<string> Statuses = new(StringComparer.Ordinal)
    {
        "waiting", "executing", "ended", "error", "canceled",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpPltJobRunWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> RunAsync(
        ErpPltJobRunWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var status = request.Status ?? "ended";
        var invalid = Validate(status);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var message = Clip(request.Message ?? string.Empty, 255);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_plt_batch_job", "last_run", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_plt_batch_job", "status", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Batch job table is not provisioned");
        }

        if (!await ColumnExistsAsync(connection, "epc_plt_batch_run", "started", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_plt_batch_run", "status", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Batch run table is not provisioned");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = ErpDb.Positional("SELECT `recurrence_min`,`next_run` FROM `epc_plt_batch_job` WHERE `id`=?");
        ErpDb.AddParameters(command, request.JobId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Job not found");
        }

        var recurrenceMin = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture);
        var existingNext = reader.IsDBNull(1) ? 0L : Convert.ToInt64(reader.GetValue(1), CultureInfo.InvariantCulture);
        await reader.DisposeAsync().ConfigureAwait(false);

        var next = string.Equals(status, "ended", StringComparison.Ordinal)
            ? NextRun(now, recurrenceMin)
            : existingNext;

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_plt_batch_run` (`job_id`,`started`,`ended`,`status`,`message`) VALUES (?,?,?,?,?)"),
            cancellationToken,
            request.JobId,
            now,
            now,
            status,
            message).ConfigureAwait(false);
        var runId = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_plt_batch_job` SET `last_run`=?, `next_run`=?, `status`=? WHERE `id`=?"),
            cancellationToken,
            now,
            next,
            status,
            request.JobId).ConfigureAwait(false);

        return new ErpSimpleWriteResult(
            true,
            "ok",
            "Batch job executed (" + status + ")",
            runId,
            2);
    }

    public static string? Validate(string status)
        => Statuses.Contains(status) ? null : "Invalid batch status";

    /// <summary>PHP <c>epc_plt_next_run</c>: from + recurrence minutes, or 0 when recurrence &lt;= 0.</summary>
    public static long NextRun(long from, int recurrenceMin)
        => recurrenceMin <= 0 ? 0 : from + (recurrenceMin * 60L);

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];

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
