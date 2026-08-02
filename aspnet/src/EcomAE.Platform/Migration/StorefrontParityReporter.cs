namespace EcomAE.Platform.Migration;

public sealed class StorefrontParityReporter : IStorefrontParityReporter
{
    public StorefrontParityReport BuildReport()
    {
        return new StorefrontParityReport(
            "Storefront / customer commerce",
            "tenant storefront root, content/shop/, content/general_pages/, and templates/",
            "/storefront/parity plus customer-gated account, account-summary, orders, garage, and profile",
            "presentation-shell-scaffolded-awaiting-staging",
            [
                "Storefront shell lists home, CMS, catalog, cart, checkout, and customer account sections.",
                "Storefront account/placeholder shells negotiate presentation-preserving HTML (templates/modex CSS) while defaulting to JSON for tooling.",
                "Customer session gate protects /storefront/account, account-summary, orders, garage, and profile digests.",
                "Tenant resolver keeps live storefront traffic classified before cutover.",
                "Optional customer smoke: ECOMAE_CUSTOMER_COOKIE_HEADER (session=...; u_id=<digits>) via run_storefront_digest_exact_route_smoke.sh / capture artifacts.",
                "Storefront digests are optional promotion evidence — not required for ReadyToRemovePhp."
            ],
            [
                "Replay PHP storefront HTML, SEO metadata, catalog, cart, checkout, and account fixtures.",
                "Validate cart/session compatibility and sandbox payment handoff.",
                "Compare asset rendering, cache headers, and localized catalog output before traffic cutover.",
                "Keep StorefrontAspNetEnabled=false until exact-route storefront digest shadows have dual-sample match."
            ]);
    }
}
