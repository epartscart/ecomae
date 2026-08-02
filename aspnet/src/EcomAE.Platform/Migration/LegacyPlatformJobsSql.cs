namespace EcomAE.Platform.Migration;

public static class LegacyPlatformJobsSql
{
    public const string SourceTable = "epc_platform_jobs";

    public const string CountByStatus = """
        SELECT `status`, COUNT(*) AS `count`
        FROM `epc_platform_jobs`
        GROUP BY `status`
        ORDER BY `count` DESC, `status` ASC
        """;

    public const string CountByType = """
        SELECT `job_type`, COUNT(*) AS `count`
        FROM `epc_platform_jobs`
        GROUP BY `job_type`
        ORDER BY `count` DESC, `job_type` ASC
        LIMIT 50
        """;

    public const string Recent = """
        SELECT `id`, `job_type`, `tenant_key`, `status`, `priority`, `attempts`, `max_attempts`,
               `available_at`, `started_at`, `finished_at`, `last_error`, `created_at`, `updated_at`
        FROM `epc_platform_jobs`
        ORDER BY `id` DESC
        LIMIT @limit
        """;
}
