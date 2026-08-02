namespace EcomAE.Platform.Auth;

public sealed class LegacyApiClientParityReporter : ILegacyApiClientParityReporter
{
    public LegacyApiClientParityReport BuildReport()
    {
        return new LegacyApiClientParityReport(
            "PHP api key checks backed by epc_api_clients and epc_umapi_usage_log",
            "ASP.NET Core authenticator on /api/v1/price/lookup with DbLegacyApiClientStore + usage logger",
            "auth-wired-awaiting-staging-keys",
            ["epc_catalog_", "epc_pricepro_"],
            ["product scope", "allowed actions", "daily quota", "usage log shape", "price lookup exact-route gate"],
            [
                "Configure ConnectionStrings__TenantRegistry so DbLegacyApiClientStore can read epc_api_clients.",
                "Replay issued epc_pricepro_ staging keys against ASP.NET and PHP before exact-route shadow.",
                "Confirm epc_umapi_usage_log rows are written for allowed and quota-blocked requests in staging."
            ]);
    }
}
