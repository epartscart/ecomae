namespace EcomAE.Platform.Api.Catalog;

public sealed class CatalogParityReporter : ICatalogParityReporter
{
    public CatalogParityReport BuildReport()
    {
        return new CatalogParityReport(
            "PHP api/v1/catalog.php and Laximo/UMAPI integrations",
            "ASP.NET Core catalog status/manufacturers/models/modifications/brands/vin/engines/analogs/article-brands/categories/products/engine-search/article-links/article/articles/engine/suppliers/brand-parts DB/cache readers + API-key auth",
            "catalog-cache-routes-wired-awaiting-staging",
            ReadyForShadowTraffic: true,
            [
                "On CloudPanel: ensure_epc_api_clients_table.sh → issue_smoke_credentials.sh (epc_catalog_ key) → validate_final_gate_env.sh.",
                "Envelope contracts cover all wired catalog routes; dual-sample via compare_catalog_*_parity.py before each shadow.",
                "Contract-only dry run: python3 scripts/compare_catalog_list_parity.py manufacturers sample.json sample.json --contract-only.",
                "Promote one nginx-catalog-*-shadow-example.conf location = path after authenticated smoke (never broad /api).",
                "Wire live UMAPI proxy fills for articles/engine on cache miss still remain PHP-authoritative.",
                "Enforce staging smoke with epc_catalog_ API keys before enabling exact-route catalog shadows."
            ]);
    }
}
