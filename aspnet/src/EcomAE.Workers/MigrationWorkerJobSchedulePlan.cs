namespace EcomAE.Workers;

public sealed record MigrationWorkerJobSchedulePlan(
    string JobKey,
    string Schedule,
    string LockKey,
    string RetryPolicy,
    bool RequiresDistributedLock,
    bool ReadyForExecution,
    string ReadinessReason);
