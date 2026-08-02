namespace EcomAE.Platform.Api.Catalog;

public static class LegacyUmapiActionCacheSql
{
    public const string SourceTable = "epc_umapi_cache";

    public const string SelectByCacheKey = """
        SELECT `cache_key`, `action`, `section`, `language`, `region`, `response_json`, `rows_count`, `http_status`, `last_sync`
        FROM `epc_umapi_cache`
        WHERE `cache_key` = @cacheKey
        LIMIT 1
        """;
}
