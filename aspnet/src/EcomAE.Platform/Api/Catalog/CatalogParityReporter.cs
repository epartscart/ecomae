namespace EcomAE.Platform.Api.Catalog;

public sealed class CatalogParityReporter : ICatalogParityReporter
{
    public CatalogParityReport BuildReport()
    {
        return new CatalogParityReport(
            "PHP api/v1/catalog.php and Laximo/UMAPI integrations",
            "ASP.NET Core catalog status/manufacturers/models/modifications/brands/vin/engines/analogs/article-brands/categories/products/engine-search/article-links/article/articles/engine/suppliers/brand-parts DB/cache readers + API-key auth",
            "catalog-cache-routes-live-miss-fill-php",
            ReadyForShadowTraffic: true,
            [
                "On CloudPanel: ensure_epc_api_clients_table.sh → issue_smoke_credentials.sh (epc_catalog_ key) → validate_final_gate_env.sh.",
                "Envelope contracts cover all wired catalog routes; dual-sample via compare_catalog_*_parity.py before each shadow.",
                "Contract-only dry run: python3 scripts/compare_catalog_list_parity.py manufacturers sample.json sample.json --contract-only.",
                "Batch 5 miss harness: bash scripts/cloudpanel_probe_catalog_miss_path.sh then capture/compare_catalog_miss_dual_samples (cutoverAllowed=false).",
                "Batch 5 miss-fill dry-run: worker job catalog-miss-fill (CatalogMissFillDryRunExecutor) — outbound=0 writes=0 fills=0; confirm_* refused.",
                "Live UMAPI proxy fills for articles/engine and all cache write-on-miss remain PHP-authoritative.",
                "Do not ship ASP.NET outbound UMAPI miss-fill until miss dual-sample evidence + human approval; never invent RELEASE_OWNER_APPROVAL.md."
            ]);
    }
}
