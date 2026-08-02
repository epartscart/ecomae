namespace EcomAE.Platform.Api.Catalog;

public static class LegacyCatalogModelsSql
{
    public const string SourceTable = "epc_umapi_models";

    public const string SelectBySectionAndMfa = """
        SELECT `section`, `mfa_id`, `ms_id`, `model_series`, `year_from`, `year_to`, `raw_json`, `updated_at`
        FROM `epc_umapi_models`
        WHERE `section` = @section AND `mfa_id` = @mfaId
        ORDER BY `model_series` ASC
        """;
}
