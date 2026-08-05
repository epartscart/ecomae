using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Migration;

/// <summary>
/// Field/function/presentation parity catalog for CP/ERP/BOS/storefront/API.
/// Never authorizes cutover; CutoverAllowed stays false until dual live samples prove match.
/// </summary>
public sealed class SurfaceFieldParityReporter : ISurfaceFieldParityReporter
{
    public SurfaceFieldParityReport BuildReport()
    {
        var contracts = SurfacePayloadContractCatalog.All;
        var assets = new[] { "cp", "erp", "bos", "storefront", "marketing" }
            .SelectMany(surface => LegacyPresentationAssets.StylesheetsFor(surface)
                .Select(href => (surface, href)))
            .ToArray();

        return new SurfaceFieldParityReport(
            "field-function-presentation-contracts-locked-cutover-blocked",
            CutoverAllowed: false,
            contracts.Count,
            assets.Length,
            assets.Length,
            contracts,
            SurfacePayloadContractCatalog.Functions,
            [
                "Every CP/ERP/BOS/storefront digest route has an explicit required-field contract (camelCase summary/item fields).",
                "Presentation shells must reuse PHP chrome CSS URLs from LegacyPresentationAssets for cp/erp/bos/storefront/marketing.",
                "Marketing /marketing/app reuses epm-hub CSS; live www.ecomae.com/ remains PHP.",
                "Function map ties PHP entries to ASP.NET digests/shells without claiming write/posting/cart parity.",
                "scripts/compare_surface_payload_parity.py performs recursive field-by-field JSON compare for dual samples.",
                "SurfaceDigestContractValidator locks migration-mode digest envelopes in unit tests before cutover.",
                "CutoverAllowed is false; AdminAspNetEnabled/StorefrontAspNetEnabled are true (ASP.NET-primary all tenants); RequirePhpFallback must remain true until dual-sample-green per write route."
            ],
            [
                "Capture authenticated ASP.NET + PHP (or shared-DB fixture) dual samples for each contracted digest route.",
                "Run scripts/run_surface_parity_harness.sh and attach match=true samples under docs/migration/evidence/surface-parity/samples/.",
                "Do not enable broad /cp /erp /bos / storefront / marketing / nginx cutover until every contract has match=true evidence.",
                "Storefront cart/checkout/SEO HTML and ERP voucher posting UX are still PHP-authoritative gaps.",
                "Authenticated CloudPanel cookies/API keys are still required for live dual-sample promotion."
            ],
            [
                "Keep PHP authoritative for operator chrome and storefront HTML.",
                "Use /migration/surface-field-parity as the operator contract board.",
                "On CloudPanel: ensure_epc_api_clients_table.sh → issue_smoke_credentials.sh → validate_final_gate_env.sh.",
                "Then capture/commit staging-smoke and bash scripts/run_surface_parity_harness.sh with real cookies/keys.",
                "Optional customer digests: ECOMAE_CUSTOMER_COOKIE_HEADER (not required for ReadyToRemovePhp).",
                "Only after dual samples match: promote exact-route shadows one location= at a time."
            ],
            ReadyForPhpRemoval: false);
    }
}
