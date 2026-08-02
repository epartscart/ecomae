namespace EcomAE.Platform.Api.Catalog;

public static class LegacyCatalogManufacturersSql
{
    public const string SourceTable = "epc_umapi_manufacturers";

    public const string SelectBySection = """
        SELECT `section`, `mfa_id`, `manufacturer`, `manufacturer_ru`, `type`, `country`, `popular`, `is_logo`, `raw_json`, `updated_at`
        FROM `epc_umapi_manufacturers`
        WHERE `section` = @section
        ORDER BY `manufacturer` ASC
        """;
}
