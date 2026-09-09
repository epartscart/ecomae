using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_crm.php</c> twin of <c>epc_crm_save_project</c> and <c>epc_crm_save_project_task</c>.
/// Quote email and send stay Classic. Schema-ensure stays Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpCrmProjectWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        CpCrmProjectSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveTaskAsync(
        CpCrmProjectTaskSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CpCrmProjectSaveRequest(
    long Id,
    string? Name,
    long OpportunityId,
    long OrderId,
    string? Status,
    int ProgressPct,
    string? StartDate,
    string? EndDate,
    long OwnerUserId,
    string? Notes);

public sealed record CpCrmProjectTaskSaveRequest(
    long ProjectId,
    string? Title,
    string? Status,
    int ProgressPct,
    decimal HoursEst,
    string? DueDate);

public sealed class CpCrmProjectWriteService : ICpCrmProjectWriteService
{
    public static readonly HashSet<string> Statuses = new(StringComparer.Ordinal)
    {
        "planned", "active", "on_hold", "done", "cancelled",
    };

    public static readonly HashSet<string> TaskStatuses = new(StringComparer.Ordinal)
    {
        "todo", "doing", "done",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public CpCrmProjectWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeStatus(string? status)
    {
        var raw = (status ?? string.Empty).Trim();
        return Statuses.Contains(raw) ? raw : "planned";
    }

    public static string NormalizeTaskStatus(string? status)
    {
        var raw = (status ?? string.Empty).Trim();
        return TaskStatuses.Contains(raw) ? raw : "todo";
    }

    public static string NormalizeName(string? name)
    {
        var raw = (name ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            raw = "Project";
        }

        return raw.Length > 255 ? raw[..255] : raw;
    }

    public static string NormalizeTaskTitle(string? title)
    {
        var raw = (title ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            raw = "Task";
        }

        return raw.Length > 255 ? raw[..255] : raw;
    }

    public static decimal NormalizeHours(decimal hours)
        => hours < 0 ? 0 : hours;

    public static int NormalizeProgress(int progress)
        => Math.Clamp(progress, 0, 100);

    public static long ParseDate(string? raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return 0;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix) && unix >= 0)
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

        return 0;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        CpCrmProjectSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Id < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Project id is invalid.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var name = NormalizeName(request.Name);
        var status = NormalizeStatus(request.Status);
        var progress = NormalizeProgress(request.ProgressPct);
        var start = ParseDate(request.StartDate);
        var end = ParseDate(request.EndDate);
        var oppId = request.OpportunityId < 0 ? 0 : request.OpportunityId;
        var orderId = request.OrderId < 0 ? 0 : request.OrderId;
        var owner = request.OwnerUserId > 0 ? request.OwnerUserId : 0;
        var notes = (request.Notes ?? string.Empty).Trim();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (request.Id > 0)
            {
                var exists = await ErpDb.LongAsync(
                    connection, null,
                    ErpDb.Positional("SELECT COUNT(*) FROM `epc_crm_projects` WHERE `id`=?"),
                    cancellationToken, request.Id).ConfigureAwait(false);
                if (exists <= 0)
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Project was not found.");
                }

                if (owner <= 0)
                {
                    owner = await ErpDb.LongAsync(
                        connection, null,
                        ErpDb.Positional("SELECT `owner_user_id` FROM `epc_crm_projects` WHERE `id`=?"),
                        cancellationToken, request.Id).ConfigureAwait(false);
                }

                if (notes.Length == 0)
                {
                    notes = (await ErpDb.StringAsync(
                        connection, null,
                        ErpDb.Positional("SELECT `notes` FROM `epc_crm_projects` WHERE `id`=?"),
                        cancellationToken, request.Id) ?? string.Empty).Trim();
                }

                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional(
                        """
                        UPDATE `epc_crm_projects`
                        SET `name`=?, `opportunity_id`=?, `order_id`=?, `status`=?, `progress_pct`=?,
                            `start_date`=?, `end_date`=?, `owner_user_id`=?, `notes`=?, `time_updated`=?
                        WHERE `id`=?
                        """),
                    cancellationToken,
                    name, oppId, orderId, status, progress, start, end, owner, notes, now, request.Id).ConfigureAwait(false);
                return ErpSimpleWriteResult.Ok("Project saved", request.Id);
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_crm_projects`
                    (`name`, `opportunity_id`, `order_id`, `status`, `progress_pct`, `start_date`, `end_date`, `owner_user_id`, `notes`, `time_created`, `time_updated`)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                    """),
                cancellationToken,
                name, oppId, orderId, status, progress, start, end, owner, notes, now, now).ConfigureAwait(false);
            var created = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Project saved", created);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "CRM project table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> SaveTaskAsync(
        CpCrmProjectTaskSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ProjectId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Project id is invalid.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var title = NormalizeTaskTitle(request.Title);
        var status = NormalizeTaskStatus(request.Status);
        var progress = NormalizeProgress(request.ProgressPct);
        var hours = NormalizeHours(request.HoursEst);
        var due = ParseDate(request.DueDate);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var exists = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_crm_projects` WHERE `id`=?"),
                cancellationToken, request.ProjectId).ConfigureAwait(false);
            if (exists <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Project was not found.");
            }

            var sort = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT IFNULL(MAX(`sort_order`), 0) + 1 FROM `epc_crm_project_tasks` WHERE `project_id`=?"),
                cancellationToken, request.ProjectId).ConfigureAwait(false);

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional(
                    """
                    INSERT INTO `epc_crm_project_tasks`
                    (`project_id`, `title`, `status`, `progress_pct`, `hours_est`, `due_date`, `sort_order`, `time_updated`)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                    """),
                cancellationToken,
                request.ProjectId, title, status, progress, hours, due, sort, now).ConfigureAwait(false);
            var created = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);

            var avg = await ErpDb.DecimalAsync(
                connection, null,
                ErpDb.Positional("SELECT AVG(`progress_pct`) FROM `epc_crm_project_tasks` WHERE `project_id`=?"),
                cancellationToken, request.ProjectId).ConfigureAwait(false);
            var pct = (int)Math.Round(avg, MidpointRounding.AwayFromZero);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_crm_projects` SET `progress_pct`=?, `time_updated`=? WHERE `id`=?"),
                cancellationToken, pct, now, request.ProjectId).ConfigureAwait(false);

            return ErpSimpleWriteResult.Ok("Task added", created);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "CRM project-task table is missing — schema-ensure stays Classic.");
        }
    }
}
