namespace EcomAE.Platform.Auth;

public static class LegacyApiUsageLogSql
{
    public const string InsertUsage = """
        INSERT INTO `epc_umapi_usage_log`
        (`usage_date`, `created_at`, `action`, `section`, `source`, `client_id`, `request_path`, `http_status`, `from_cache`, `quota_blocked`, `is_live`, `message`, `ip`)
        VALUES (CURDATE(), @createdAt, @action, @section, @source, @clientId, @requestPath, @httpStatus, 0, @quotaBlocked, 1, @message, @ip)
        """;
}
