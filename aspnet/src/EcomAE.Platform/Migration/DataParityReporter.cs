namespace EcomAE.Platform.Migration;

public sealed class DataParityReporter : IDataParityReporter
{
    public DataParityReport BuildReport()
    {
        return new DataParityReport(
            "contracts-ready-production-data-pending",
            [
                "Catalog and price lookup contracts expose repository-backed services and legacy SQL mapping.",
                "Legacy API client contracts expose parser, policy, SQL, and usage-log boundaries.",
                "Tenant registry contracts expose route classification, seed configuration, and portal tenant row mapping.",
                "Legacy session bridge exposes PHPSESSID, X-API-Key, and Bearer-key diagnostic paths."
            ],
            [
                "shop_docpart_prices_data for price lookup parity.",
                "epc_api_clients and epc_umapi_usage_log for API key and quota parity.",
                "epc_portal_tenants for platform, live-tenant, and ERP-only tenant resolution.",
                "PHP session store and CP/ERP/BOS permission tables for login parity."
            ],
            [
                "On CloudPanel: ensure_epc_api_clients_table.sh → issue_smoke_credentials.sh → capture staging-smoke JSON (never invent).",
                "Run read-only shadow queries against production-like MySQL fixtures.",
                "Compare PHP and ASP.NET Core JSON payloads via compare_*_parity.py for catalog, price, tenant, auth, and session probes.",
                "Record approved fixtures and latency budgets before enabling route cutover flags.",
                "Keep PHP authoritative until every production data source has replay evidence and rollback coverage."
            ]);
    }
}
