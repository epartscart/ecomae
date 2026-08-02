namespace EcomAE.Platform.Api.Catalog;

public static class LegacyCatalogVinSql
{
    public const string SourceTable = "epc_umapi_vin_cache";

    public const string SelectByVinLanguageRegion = """
        SELECT `vin`, `language`, `region`, `response_json`, `vehicle_count`, `manufacturer`, `model_label`, `http_status`, `updated_at`
        FROM `epc_umapi_vin_cache`
        WHERE `vin` = @vin AND `language` = @language AND `region` = @region
        LIMIT 1
        """;
}
