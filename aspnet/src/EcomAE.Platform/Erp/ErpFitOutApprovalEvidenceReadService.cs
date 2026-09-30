namespace EcomAE.Platform.Erp;

public interface IErpFitOutApprovalEvidenceReadService
{
    Task<ErpFitOutApprovalEvidenceResult> ReadAsync(
        long projectId,
        CancellationToken cancellationToken = default);
}

public sealed record ErpFitOutApprovalEvidenceResult(
    long ProjectId,
    IReadOnlyList<ErpFitOutApprovalQueueRow> Pending,
    IReadOnlyList<ErpFitOutApprovalAuditRow> Decisions,
    decimal PendingAmount,
    string Source,
    string Message);

public sealed class ErpFitOutApprovalEvidenceReadService
    : IErpFitOutApprovalEvidenceReadService
{
    private readonly IErpFitOutApprovalQueueReadService _queue;
    private readonly IErpFitOutApprovalAuditReadService _audit;

    public ErpFitOutApprovalEvidenceReadService(
        IErpFitOutApprovalQueueReadService queue,
        IErpFitOutApprovalAuditReadService audit)
    {
        _queue = queue;
        _audit = audit;
    }

    public async Task<ErpFitOutApprovalEvidenceResult> ReadAsync(
        long projectId,
        CancellationToken cancellationToken = default)
    {
        if (projectId <= 0)
        {
            return Empty(projectId, "Project id is required.");
        }

        var pendingTask = _queue.ReadAsync(projectId, cancellationToken);
        var decisionsTask = _audit.ReadAsync(projectId, cancellationToken);
        await Task.WhenAll(pendingTask, decisionsTask).ConfigureAwait(false);
        var pending = await pendingTask.ConfigureAwait(false);
        var decisions = await decisionsTask.ConfigureAwait(false);
        var source = pending.Source == "database-error"
            ? "database-error"
            : pending.Source == "migration"
                ? "migration"
                : "database";
        return new(
            projectId,
            pending.Rows,
            decisions,
            pending.TotalAmount,
            source,
            pending.Message);
    }

    private static ErpFitOutApprovalEvidenceResult Empty(
        long projectId,
        string message)
        => new(projectId, [], [], 0m, "migration", message);
}
