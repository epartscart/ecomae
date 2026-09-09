using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_crm.php</c> twins of <c>epc_crm_toggle_activity_done</c> and <c>epc_crm_save_activity</c>
/// (finance helper: type default <c>task</c>, related default <c>lead</c>).
/// Quote email and send stay Classic. Schema-ensure stays Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpCrmActivityWriteService
{
    Task<ErpSimpleWriteResult> ToggleDoneAsync(
        long id,
        bool done,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveAsync(
        CpCrmActivitySaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CpCrmActivitySaveRequest(
    long Id,
    string? ActivityType,
    string? RelatedType,
    long RelatedId,
    string? DueDate,
    bool Done,
    long OwnerUserId,
    string? Notes);

public sealed class CpCrmActivityWriteService : ICpCrmActivityWriteService
{
    public static readonly HashSet<string> ActivityTypes = new(StringComparer.Ordinal)
    {
        "call", "email", "meeting", "note", "task",
    };

    public static readonly HashSet<string> RelatedTypes = new(StringComparer.Ordinal)
    {
        "lead", "opportunity", "user",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public CpCrmActivityWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeActivityType(string? activityType)
    {
        var raw = (activityType ?? string.Empty).Trim();
        return ActivityTypes.Contains(raw) ? raw : "task";
    }

    public static string NormalizeRelatedType(string? relatedType)
    {
        var raw = (relatedType ?? string.Empty).Trim();
        return RelatedTypes.Contains(raw) ? raw : "lead";
    }

    public static long ParseDueDate(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix) && unix > 0)
        {
            return unix;
        }

        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            if (parsed.TimeOfDay == TimeSpan.Zero)
            {
                parsed = parsed.AddHours(12);
            }

            return parsed.ToUnixTimeSeconds();
        }

        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public async Task<ErpSimpleWriteResult> ToggleDoneAsync(
        long id,
        bool done,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Activity id is invalid.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    UPDATE `epc_crm_activities`
                    SET `done`=?, `time_updated`=?
                    WHERE `id`=?
                    """),
                cancellationToken,
                done ? 1 : 0, now, id);
            return ErpSimpleWriteResult.Ok("Activity updated", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "CRM activity table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        CpCrmActivitySaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Id < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Activity id is invalid.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var activityType = NormalizeActivityType(request.ActivityType);
        var relatedType = NormalizeRelatedType(request.RelatedType);
        var relatedId = request.RelatedId < 0 ? 0 : request.RelatedId;
        var due = ParseDueDate(request.DueDate);
        var done = request.Done ? 1 : 0;
        var owner = request.OwnerUserId > 0 ? request.OwnerUserId : 0;
        var notes = (request.Notes ?? string.Empty).Trim();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (request.Id > 0)
            {
                var exists = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `epc_crm_activities` WHERE `id`=?"),
                    cancellationToken,
                    request.Id).ConfigureAwait(false);
                if (exists <= 0)
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Activity was not found.");
                }

                if (owner <= 0)
                {
                    owner = await ErpDb.LongAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT `owner_user_id` FROM `epc_crm_activities` WHERE `id`=?"),
                        cancellationToken,
                        request.Id).ConfigureAwait(false);
                }

                if (notes.Length == 0)
                {
                    notes = (await ErpDb.StringAsync(
                        connection,
                        null,
                        ErpDb.Positional("SELECT `notes` FROM `epc_crm_activities` WHERE `id`=?"),
                        cancellationToken,
                        request.Id).ConfigureAwait(false) ?? string.Empty).Trim();
                }

                await ErpDb.ExecuteAsync(
                    connection,
                    null,
                    ErpDb.Positional(
                        """
                        UPDATE `epc_crm_activities`
                        SET `activity_type`=?, `related_type`=?, `related_id`=?, `due_date`=?, `done`=?,
                            `owner_user_id`=?, `notes`=?, `time_updated`=?
                        WHERE `id`=?
                        """),
                    cancellationToken,
                    activityType, relatedType, relatedId, due, done,
                    owner, notes, now, request.Id).ConfigureAwait(false);
                return ErpSimpleWriteResult.Ok("Activity saved", request.Id);
            }

            await ErpDb.ExecuteAsync(
                connection,
                null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_crm_activities`
                    (`activity_type`, `related_type`, `related_id`, `due_date`, `done`, `owner_user_id`, `notes`, `time_created`, `time_updated`)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
                    """),
                cancellationToken,
                activityType, relatedType, relatedId, due, done,
                owner, notes, now, now).ConfigureAwait(false);
            var created = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Activity saved", created);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "CRM activity table is missing — schema-ensure stays Classic.");
        }
    }
}
