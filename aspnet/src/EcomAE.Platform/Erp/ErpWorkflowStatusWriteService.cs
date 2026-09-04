namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_erp_workflow_update_status</c> twin. Task create stays PHP.
/// </summary>
public interface IErpWorkflowStatusWriteService
{
    Task<ErpSimpleWriteResult> SetStatusAsync(long taskId, string? status, CancellationToken cancellationToken = default);
}

public sealed class ErpWorkflowStatusWriteService : IErpWorkflowStatusWriteService
{
    internal static readonly string[] Allowed = ["pending", "in_progress", "done", "cancelled"];

    private readonly IErpWriteConnectionFactory _connections;

    public ErpWorkflowStatusWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetStatusAsync(
        long taskId,
        string? status,
        CancellationToken cancellationToken = default)
    {
        if (taskId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A workflow task id is required.");
        }

        var next = (status ?? string.Empty).Trim().ToLowerInvariant();
        if (!Allowed.Contains(next, StringComparer.Ordinal))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid status");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var completedAt = next == "done" ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : 0L;
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `epc_erp_workflow_tasks` SET `status` = ?, `completed_at` = ? WHERE `id` = ?"),
            cancellationToken,
            next, completedAt, taskId);
        return ErpSimpleWriteResult.Ok("Workflow task updated", taskId);
    }
}
