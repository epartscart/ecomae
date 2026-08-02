namespace EcomAE.Platform.Migration;

public static class LegacyUmapiUsageSql
{
    public const string SourceTable = "epc_umapi_usage_log";

    public const string CountTodayLive = """
        SELECT COUNT(*) FROM `epc_umapi_usage_log`
        WHERE `usage_date` = CURDATE() AND `is_live` = 1
        """;

    public const string CountTodayCache = """
        SELECT COUNT(*) FROM `epc_umapi_usage_log`
        WHERE `usage_date` = CURDATE() AND `from_cache` = 1
        """;

    public const string CountTodayBlocked = """
        SELECT COUNT(*) FROM `epc_umapi_usage_log`
        WHERE `usage_date` = CURDATE() AND `quota_blocked` = 1
        """;

    public const string ByActionToday = """
        SELECT `action`, SUM(`is_live`) AS `live`, SUM(`from_cache`) AS `cache`, SUM(`quota_blocked`) AS `blocked`
        FROM `epc_umapi_usage_log`
        WHERE `usage_date` = CURDATE()
        GROUP BY `action`
        ORDER BY `live` DESC, `action` ASC
        """;

    public const string BySourceToday = """
        SELECT `source`, SUM(`is_live`) AS `live`, SUM(`from_cache`) AS `cache`, SUM(`quota_blocked`) AS `blocked`
        FROM `epc_umapi_usage_log`
        WHERE `usage_date` = CURDATE()
        GROUP BY `source`
        ORDER BY `live` DESC, `source` ASC
        """;

    public const string History = """
        SELECT `usage_date`, SUM(`is_live`) AS `live`, SUM(`from_cache`) AS `cache`, SUM(`quota_blocked`) AS `blocked`
        FROM `epc_umapi_usage_log`
        WHERE `usage_date` >= DATE_SUB(CURDATE(), INTERVAL @daysMinusOne DAY)
        GROUP BY `usage_date`
        ORDER BY `usage_date` DESC
        """;

    public const string RecentToday = """
        SELECT `created_at`, `action`, `section`, `source`, `request_path`, `http_status`, `from_cache`, `quota_blocked`, `is_live`, `message`
        FROM `epc_umapi_usage_log`
        WHERE `usage_date` = CURDATE()
        ORDER BY `id` DESC
        LIMIT @limit
        """;
}
