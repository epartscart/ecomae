namespace EcomAE.Platform.Migration;

public sealed record PlatformJobsStatusBucket(string Status, int Count);

public sealed record PlatformJobsTypeBucket(string JobType, int Count);

public sealed record PlatformJobsRecentRow(
    long Id,
    string JobType,
    string TenantKey,
    string Status,
    int Priority,
    int Attempts,
    int MaxAttempts,
    string? AvailableAt,
    string? StartedAt,
    string? FinishedAt,
    string LastError,
    string? CreatedAt,
    string? UpdatedAt);

public sealed record PlatformJobsSummary(
    int Total,
    int Queued,
    int Running,
    int Done,
    int Failed,
    IReadOnlyList<PlatformJobsStatusBucket> ByStatus,
    IReadOnlyList<PlatformJobsTypeBucket> ByType,
    IReadOnlyList<PlatformJobsRecentRow> Recent,
    string Source,
    string Message);
