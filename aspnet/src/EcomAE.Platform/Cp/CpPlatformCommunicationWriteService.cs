using System.Data.Common;
using System.Text.RegularExpressions;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>epc_super_cp_communication.php</c> twin of <c>epc_scp_task_save</c>
/// and <c>epc_scp_task_delete</c>. Notification policy, SMTP, and schema-ensure stay Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpPlatformCommunicationWriteService
{
    Task<ErpSimpleWriteResult> SaveTaskAsync(
        CpPlatformCommunicationSaveTaskRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteTaskAsync(
        long id,
        CancellationToken cancellationToken = default);
}

public sealed record CpPlatformCommunicationSaveTaskRequest(
    long Id,
    string? Title,
    string? Description,
    long AssignedTo,
    string? AssignedEmail,
    string? SiteKey,
    string? Category,
    string? Status,
    string? Priority,
    long DueAt,
    long CreatedBy);

public sealed class CpPlatformCommunicationWriteService : ICpPlatformCommunicationWriteService
{
    private static readonly Regex SiteKeySafe = new("[^a-z0-9_]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static readonly IReadOnlyDictionary<string, string> Categories = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["onboarding"] = "Onboarding",
        ["support"] = "Support",
        ["billing"] = "Billing",
        ["pricing"] = "Pricing",
        ["content"] = "Content",
        ["other"] = "Other",
    };

    public static readonly IReadOnlyDictionary<string, string> Statuses = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["open"] = "Open",
        ["in_progress"] = "In progress",
        ["done"] = "Done",
        ["cancelled"] = "Cancelled",
    };

    public static readonly IReadOnlyDictionary<string, string> Priorities = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["low"] = "Low",
        ["normal"] = "Normal",
        ["high"] = "High",
        ["urgent"] = "Urgent",
    };

    private readonly IErpWriteConnectionFactory _connections;

    public CpPlatformCommunicationWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string NormalizeSiteKey(string? raw)
        => SiteKeySafe.Replace((raw ?? string.Empty).Trim().ToLowerInvariant(), string.Empty);

    public static string NormalizeCategory(string? raw)
    {
        var category = (raw ?? string.Empty).Trim();
        return Categories.ContainsKey(category) ? category : "support";
    }

    public static string NormalizeStatus(string? raw)
    {
        var status = (raw ?? string.Empty).Trim();
        return Statuses.ContainsKey(status) ? status : "open";
    }

    public static string NormalizePriority(string? raw)
    {
        var priority = (raw ?? string.Empty).Trim();
        return Priorities.ContainsKey(priority) ? priority : "normal";
    }

    public async Task<ErpSimpleWriteResult> SaveTaskAsync(
        CpPlatformCommunicationSaveTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        var title = (request.Title ?? string.Empty).Trim();
        if (title.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Title is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var description = request.Description ?? string.Empty;
        var assignedTo = request.AssignedTo < 0 ? 0 : request.AssignedTo;
        var assignedEmail = (request.AssignedEmail ?? string.Empty).Trim().ToLowerInvariant();
        var siteKey = NormalizeSiteKey(request.SiteKey);
        var category = NormalizeCategory(request.Category);
        var status = NormalizeStatus(request.Status);
        var priority = NormalizePriority(request.Priority);
        var dueAt = request.DueAt < 0 ? 0 : request.DueAt;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var createdBy = request.CreatedBy < 0 ? 0 : request.CreatedBy;

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            if (request.Id > 0)
            {
                var existing = await ErpDb.LongAsync(
                    connection, null,
                    ErpDb.Positional("SELECT `id` FROM `epc_platform_internal_tasks` WHERE `id`=? LIMIT 1"),
                    cancellationToken, request.Id).ConfigureAwait(false);
                if (existing <= 0)
                {
                    return ErpSimpleWriteResult.Fail("not_found", "Task not found");
                }

                if (description.Length == 0)
                {
                    description = await ErpDb.StringAsync(
                        connection, null,
                        "SELECT IFNULL(`description`, '') FROM `epc_platform_internal_tasks` WHERE `id` = @p0 LIMIT 1",
                        cancellationToken, request.Id).ConfigureAwait(false) ?? string.Empty;
                }

                await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("UPDATE `epc_platform_internal_tasks` SET `title`=?, `description`=?, `assigned_to`=?, `assigned_email`=?, `site_key`=?, `category`=?, `status`=?, `priority`=?, `due_at`=?, `updated_at`=? WHERE `id`=?"),
                    cancellationToken, title, description, assignedTo, assignedEmail, siteKey, category, status, priority, dueAt, now, request.Id).ConfigureAwait(false);
                return ErpSimpleWriteResult.Ok("Task saved.", request.Id);
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `epc_platform_internal_tasks` (`title`, `description`, `assigned_to`, `assigned_email`, `site_key`, `category`, `status`, `priority`, `due_at`, `updated_at`, `created_by`, `created_at`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
                cancellationToken, title, description, assignedTo, assignedEmail, siteKey, category, status, priority, dueAt, now, createdBy, now).ConfigureAwait(false);
            var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Task saved.", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Platform-communication table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> DeleteTaskAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Task id is required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("DELETE FROM `epc_platform_internal_tasks` WHERE `id`=?"),
                cancellationToken, id).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Task deleted.", id);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Platform-communication table is missing — schema-ensure stays Classic.");
        }
    }
}
