namespace EcomAE.Platform.Api.Catalog;

public static class LegacyCatalogModificationsSql
{
    public const string SourceTable = "epc_umapi_modifications";

    public const string SelectBySectionAndMs = """
        SELECT `section`, `ms_id`, `modification_id`, `title`, `year_from`, `year_to`, `power_kw`, `capacity_lt`, `fuel_type`, `raw_json`, `updated_at`
        FROM `epc_umapi_modifications`
        WHERE `section` = @section AND `ms_id` = @msId
        ORDER BY `title` ASC
        """;
}
