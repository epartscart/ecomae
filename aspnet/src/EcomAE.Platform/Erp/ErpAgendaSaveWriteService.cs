using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_agenda_save</c> / ajax <c>agenda_save</c> twin.
/// INSERT <c>epc_erp_agenda_events</c>. Schema ensure stays PHP.
/// Does not CREATE tables.
/// </summary>
public interface IErpAgendaSaveWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        ErpAgendaSaveWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpAgendaSaveWriteRequest(
    string? Title = null,
    string? EventType = null,
    string? StartAt = null,
    string? EndAt = null,
    string? EntityType = null,
    long EntityId = 0,
    long AssignedUserId = 0,
    string? Location = null,
    string? Notes = null,
    long AdminId = 0);

public sealed class ErpAgendaSaveWriteService : IErpAgendaSaveWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public ErpAgendaSaveWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        ErpAgendaSaveWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var title = (request.Title ?? string.Empty).Trim();
        var invalid = Validate(title);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        title = Clip(title, 255);
        var eventType = Clip((request.EventType ?? "meeting").Trim(), 32);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var start = ResolveStart(request.StartAt, now);
        var end = ResolveEnd(request.EndAt, start);
        var entityType = Clip((request.EntityType ?? string.Empty).Trim(), 32);
        var entityId = request.EntityId < 0 ? 0 : request.EntityId;
        var assigned = request.AssignedUserId < 0 ? 0 : request.AssignedUserId;
        var location = Clip((request.Location ?? string.Empty).Trim(), 255);
        var notes = request.Notes ?? string.Empty;
        notes = notes.Trim();
        var adminId = request.AdminId < 0 ? 0 : request.AdminId;

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await ColumnExistsAsync(connection, "epc_erp_agenda_events", "title", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_erp_agenda_events", "start_at", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Agenda event table is not provisioned");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_agenda_events` (`title`, `event_type`, `start_at`, `end_at`, `entity_type`, `entity_id`, `assigned_user_id`, `location`, `notes`, `admin_id`, `time_created`) VALUES (?,?,?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            title,
            eventType,
            start,
            end,
            entityType,
            entityId,
            assigned,
            location,
            notes,
            adminId,
            now).ConfigureAwait(false);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Agenda event added", id);
    }

    public static string? Validate(string title)
        => title.Length == 0 ? "Event title required" : null;

    public static long ResolveStart(string? raw, long now)
        => IsPhpEmpty(raw) ? now : ParseWhen(raw!);

    public static long ResolveEnd(string? raw, long start)
        => IsPhpEmpty(raw) ? start + 3600 : ParseWhen(raw!);

    public static long ParseWhen(string raw)
    {
        raw = raw.Trim();
        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix)
            && raw.Length >= 9)
        {
            return unix;
        }

        var normalized = raw.Replace("T", " ", StringComparison.Ordinal);
        if (DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt)
            || DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out dt)
            || DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out dt))
        {
            return new DateTimeOffset(dt).ToUnixTimeSeconds();
        }

        return 0;
    }

    private static bool IsPhpEmpty(string? raw)
        => string.IsNullOrWhiteSpace(raw) || raw.Trim() == "0";

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
