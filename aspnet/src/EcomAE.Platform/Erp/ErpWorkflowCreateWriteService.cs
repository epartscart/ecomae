using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_workflow_create</c> twin. Staff schema ensure and sample seed stay PHP.
/// </summary>
public interface IErpWorkflowCreateWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        string? title,
        string? departmentCode,
        string? priority,
        long orderId,
        string? description,
        string? workflowStep,
        int assignedUserId,
        string? dueAt,
        int createdBy,
        CancellationToken cancellationToken = default);
}

public sealed class ErpWorkflowCreateWriteService : IErpWorkflowCreateWriteService
{
    public static readonly string[] AllowedPriorities = ["low", "normal", "high"];
    public static readonly string[] AllowedStatuses = ["pending", "in_progress", "done", "cancelled"];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpWorkflowCreateWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        string? title,
        string? departmentCode,
        string? priority,
        long orderId,
        string? description,
        string? workflowStep,
        int assignedUserId,
        string? dueAt,
        int createdBy,
        CancellationToken cancellationToken = default)
    {
        var task = Clip(title, 255);
        if (task.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Title is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var dept = Clip(departmentCode, 32);
        if (dept.Length == 0)
        {
            dept = "admin";
        }

        var pri = (priority ?? string.Empty).Trim().ToLowerInvariant();
        if (!AllowedPriorities.Contains(pri, StringComparer.Ordinal))
        {
            pri = "normal";
        }

        if (orderId < 0)
        {
            orderId = 0;
        }

        if (assignedUserId < 0)
        {
            assignedUserId = 0;
        }

        if (createdBy < 0)
        {
            createdBy = 0;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var due = ResolveDueUnix(dueAt, now);
        var step = Clip(workflowStep, 64);
        var notes = (description ?? string.Empty).Trim();

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "INSERT INTO `epc_erp_workflow_tasks` (`department_code`, `workflow_step`, `title`, `description`, `order_id`, `status`, `priority`, `assigned_user_id`, `created_by`, `due_at`, `time_created`) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)"),
            cancellationToken,
            dept, step, task, notes, orderId, "pending", pri, assignedUserId, createdBy, due, now);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Workflow task created.", id);
    }

    public static long ResolveDueUnix(string? raw, long now)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return now + 86400L * 3;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix) && unix > 1_000_000_000)
        {
            return unix;
        }

        if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
        }

        return now + 86400L * 3;
    }

    private static string Clip(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max];
    }
}
