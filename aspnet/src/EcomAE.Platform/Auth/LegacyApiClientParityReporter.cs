namespace EcomAE.Platform.Auth;

public sealed class LegacyApiClientParityReporter : ILegacyApiClientParityReporter
{
    public LegacyApiClientParityReport BuildReport()
    {
        return new LegacyApiClientParityReport(
            "PHP api key checks backed by epc_api_clients and epc_umapi_usage_log",
            "ASP.NET parser, policy evaluator, and usage-log contract",
            "contract-ready-db-pending",
            ["epc_catalog_", "epc_pricepro_"],
            ["product scope", "allowed actions", "daily quota", "usage log shape"],
            [
                "Execute key hash lookup against epc_api_clients.",
                "Persist quota usage rows to epc_umapi_usage_log on every allowed/blocked API request.",
                "Replay production API keys in a secure staging environment before public cutover."
            ]);
    }
}
