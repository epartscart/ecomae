namespace EcomAE.Platform.Auth;

public static class LegacyApiClientSql
{
    public const string ClientsTable = "epc_api_clients";

    public const string UsageLogTable = "epc_umapi_usage_log";

    public const string FetchActiveClientByHash = """
        SELECT *
        FROM `epc_api_clients`
        WHERE `client_key_hash` = @hash AND `active` = 1
        LIMIT 1
        """;

    public const string ConsumeDailyQuota = """
        UPDATE `epc_api_clients`
        SET `calls_today` = `calls_today` + 1, `time_updated` = @now
        WHERE `id` = @id AND `calls_today` < @dailyLimit
        """;

    public const string ResetDailyQuota = """
        UPDATE `epc_api_clients`
        SET `calls_today` = 0, `calls_reset_date` = @today, `time_updated` = @now
        WHERE `id` = @id
        """;
}
