namespace EcomAE.Platform.Auth;

public sealed class LegacyApiClientParityReporter : ILegacyApiClientParityReporter
{
    public LegacyApiClientParityReport BuildReport()
    {
        return new LegacyApiClientParityReport(
            "PHP api key checks backed by epc_api_clients and epc_umapi_usage_log",
            "ASP.NET Core authenticator on /api/v1/price/lookup + catalog routes with DbLegacyApiClientStore + usage logger",
            "auth-wired-awaiting-staging-keys",
            ["epc_catalog_", "epc_pricepro_"],
            ["product scope", "allowed actions", "daily quota", "usage log shape", "price lookup exact-route gate", "catalog exact-route gate"],
            [
                "On CloudPanel: diagnose_smoke_db.sh → apply_epc_api_clients_ddl.sh or align_tenant_registry_to_php_db.sh → ensure_epc_api_clients_table.sh → issue_smoke_credentials.sh.",
                "Configure ConnectionStrings__TenantRegistry so DbLegacyApiClientStore can read epc_api_clients (PHP db≠TenantRegistry mismatch blocks issuer).",
                "Replay issued epc_pricepro_/epc_catalog_ staging keys against ASP.NET and PHP before exact-route shadow.",
                "Confirm epc_umapi_usage_log rows are written for allowed and quota-blocked requests in staging."
            ]);
    }
}
