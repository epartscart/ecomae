namespace EcomAE.Platform.Api.Catalog;

public static class LegacyCatalogStatusSql
{
    public const string SyncStatusTable = "epc_umapi_sync_status";

    public const string SelectSyncStatus = """
        SELECT `connected`, `status_code`, `message`, `last_checked`, `last_success`, `last_error`
        FROM `epc_umapi_sync_status`
        WHERE `id` = 1
        LIMIT 1
        """;

    public const string CountManufacturers = "SELECT COUNT(*) FROM `epc_umapi_manufacturers`";
    public const string CountModels = "SELECT COUNT(*) FROM `epc_umapi_models`";
    public const string CountModifications = "SELECT COUNT(*) FROM `epc_umapi_modifications`";
    public const string CountBrands = "SELECT COUNT(*) FROM `epc_umapi_brands`";
    public const string CountVinCache = "SELECT COUNT(*) FROM `epc_umapi_vin_cache` WHERE `vehicle_count` > 0";
    public const string CountCacheRows = "SELECT COUNT(*) FROM `epc_umapi_cache`";
    public const string CountManufacturersBySection = """
        SELECT `section`, COUNT(*) AS `cnt`
        FROM `epc_umapi_manufacturers`
        GROUP BY `section`
        """;
}
