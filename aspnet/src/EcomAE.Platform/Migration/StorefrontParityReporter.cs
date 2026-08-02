namespace EcomAE.Platform.Migration;

public sealed class StorefrontParityReporter : IStorefrontParityReporter
{
    public StorefrontParityReport BuildReport()
    {
        return new StorefrontParityReport(
            "Storefront / customer commerce",
            "tenant storefront root, content/shop/, content/general_pages/, and templates/",
            "/storefront/parity plus customer-gated account, account-summary, orders, garage, and profile",
            "account-profile-garage-session-gated-awaiting-staging",
            [
                "Storefront shell lists home, CMS, catalog, cart, checkout, and customer account sections.",
                "Customer session gate protects /storefront/account, account-summary, orders, garage, and profile digests.",
                "Tenant resolver keeps live storefront traffic classified before cutover.",
                "Live smoke script can validate storefront reachability when explicitly enabled."
            ],
            [
                "Replay PHP storefront HTML, SEO metadata, catalog, cart, checkout, and account fixtures.",
                "Validate cart/session compatibility and sandbox payment handoff.",
                "Compare asset rendering, cache headers, and localized catalog output before traffic cutover."
            ]);
    }
}
