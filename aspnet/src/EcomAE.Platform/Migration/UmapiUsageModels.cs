namespace EcomAE.Platform.Migration;

public sealed record UmapiUsageBucket(string Key, int Live, int Cache, int Blocked);

public sealed record UmapiUsageDay(string Date, int Live, int Cache, int Blocked);

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
    string Source,
    string Message);
