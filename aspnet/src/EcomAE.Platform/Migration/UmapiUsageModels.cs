namespace EcomAE.Platform.Migration;

public sealed record UmapiUsageBucket(string Key, int Live, int Cache, int Blocked);

public sealed record UmapiUsageDay(string Date, int Live, int Cache, int Blocked);

public sealed record UmapiUsageRecentEvent(
    long CreatedAt,
    string Time,
    string Action,
    string Section,
    string Source,
    string Path,
    int HttpStatus,
    bool FromCache,
    bool QuotaBlocked,
    bool IsLive,
    string Message);

public sealed record UmapiUsageSummary(
    int DailyLimit,
    int TodayLive,
    int TodayCache,
    int TodayBlocked,
    int Remaining,
    double PctUsed,
    bool QuotaExceeded,
    IReadOnlyList<UmapiUsageBucket> ByActionToday,
    IReadOnlyList<UmapiUsageBucket> BySourceToday,
    IReadOnlyList<UmapiUsageDay> History,
    IReadOnlyList<UmapiUsageRecentEvent> RecentToday,
    string Source,
    string Message);
