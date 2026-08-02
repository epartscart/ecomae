namespace EcomAE.Platform.Api.Catalog;

public static class LegacyCatalogBrandsSql
{
    public const string SourceTable = "epc_umapi_brands";

    public const string SelectAll = """
        SELECT `sup_id`, `brand`, `full_name`, `raw_json`, `updated_at`
        FROM `epc_umapi_brands`
        ORDER BY `brand` ASC
        """;
}
