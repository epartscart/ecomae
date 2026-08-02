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

    /// <summary>
    /// PHP caches action=article with empty params (path carries id). Prefer matching response JSON by id.
    /// </summary>
    public const string SelectArticleById = """
        SELECT `cache_key`, `action`, `section`, `language`, `region`, `response_json`, `rows_count`, `http_status`, `last_sync`
        FROM `epc_umapi_cache`
        WHERE `action` = 'article'
          AND `section` = @section
          AND `language` = @language
          AND `region` = @region
          AND (
            `response_json` LIKE CONCAT('%"ART_ID":', @articleId, '%')
            OR `response_json` LIKE CONCAT('%"ART_ID": "', @articleId, '"%')
            OR `response_json` LIKE CONCAT('%"id":', @articleId, '%')
            OR `response_json` LIKE CONCAT('%"id": "', @articleId, '"%')
          )
        ORDER BY `last_sync` DESC
        LIMIT 1
        """;
}
