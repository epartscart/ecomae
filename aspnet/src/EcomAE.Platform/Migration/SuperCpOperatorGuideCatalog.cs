namespace EcomAE.Platform.Migration;

/// <summary>
/// PHP twin of the <c>$modules</c> table in
/// <c>cp/content/control/portal/epc_super_cp_operator_guide.php</c>.
/// </summary>
public sealed record SuperCpOperatorGuideModule(
    string Icon,
    string Title,
    string Url,
    string Summary,
    string Who,
    IReadOnlyList<string> Workflow);

public static class SuperCpOperatorGuideCatalog
{
    private const string Be = "cp";

    public static IReadOnlyList<SuperCpOperatorGuideModule> Modules { get; } =
    [
        new(
            "fa-th-large",
            "Super CP Fleet Dashboard",
            $"/{Be}/control/portal/epc_super_cp_fleet_dashboard",
            "View all CP instances across all 28 industries. Search tenants, access any CP/ERP directly.",
            "Platform administrators needing fleet-wide visibility into all tenant CPs.",
            [
                "View all industry groups with their tenant counts.",
                "Click any tenant card to open its CP, ERP, or storefront.",
                "Use search to find specific tenants by name, domain, or industry.",
            ]),
        new(
            "fa-calculator",
            "Super ERP Fleet Dashboard",
            $"/{Be}/control/portal/epc_super_erp_fleet_dashboard",
            "All ERP instances with module status, BOS control, and fleet-wide operations.",
            "Platform operators managing ERP modules, monitoring tenant health, and BOS administration.",
            [
                "View all ERP instances (live + demo) across industries.",
                "Check module activation status for each tenant.",
                "Access BOS for full fleet control (tenants, billing, security, deployment).",
            ]),
        new(
            "fa-users",
            "Customer board",
            $"/{Be}/control/portal/epc_super_cp_customer_board",
            "Cross-tenant customer search across the platform registry and every live tenant MySQL database.",
            "Support, onboarding, and billing operators who need one search instead of opening each client CP.",
            [
                "Enter email, phone, name, or company in the search box.",
                "Filter by platform-only or a specific tenant from the registry.",
                "Open CRM, ERP, or tenant CP from Quick actions for the matching user row.",
            ]),
        new(
            "fa-tags",
            "Price configs",
            $"/{Be}/control/portal/epc_super_cp_price_configs",
            "Markup rules applied to built-in catalogue, price lists, and API or channel pricing.",
            "Commercial operators defining platform defaults or per-tenant overrides before go-live.",
            [
                "Create a platform default rule (scope = Platform default).",
                "Add tenant overrides with higher priority when a client needs different margins.",
                "Set client type (catalogue, API, channel, price list) and optional client ref.",
                "Verify live prices under Shop → Prices on the tenant or demo storefront.",
            ]),
        new(
            "fa-th-large",
            "Info blocks",
            $"/{Be}/control/portal/epc_super_cp_info_blocks",
            "CMS-style HTML blocks for marketing pages, storefront banners, checkout sidebars, and CP notices.",
            "Content and launch operators publishing promos, compliance notices, or maintenance banners.",
            [
                "Choose placement (homepage, footer, checkout, CP notice, etc.).",
                "Set scope to Platform or Tenant and pick site_key when tenant-specific.",
                "Use a stable block_key (lowercase, underscores) for theme hooks.",
                "Preview on the tenant storefront after DNS is live.",
            ]),
        new(
            "fa-envelope",
            "Communication",
            $"/{Be}/control/portal/epc_super_cp_communication",
            "Platform email notification policy, SMTP diagnostics, and internal operator tasks.",
            "Platform operators coordinating onboarding, DNS go-live, demos, and internal follow-ups.",
            [
                "Review SMTP row (host/from) — transport lives in config.epc-smtp.php / Modern auth.",
                "Toggle which events send mail (onboard, DNS live, demo expiry, task assigned, digest).",
                "Create internal tasks with assignee, tenant link, due date, and priority.",
                "Filter tasks by status for open onboarding or support queues.",
            ]),
    ];

    /// <summary>PHP “Typical operator day” flow strip.</summary>
    public static IReadOnlyList<string> OperatorDay { get; } =
    [
        "Tenant hub — onboard client, DNS, industry pack",
        "Customer board — verify admin user, open CRM/ERP",
        "Price configs — seed markup before catalogue import",
        "Info blocks — launch banner or CP notice",
        "Communication — tasks + notification toggles for go-live",
    ];

    public static string CustomerBoardUrl => $"/{Be}/control/portal/epc_super_cp_customer_board";

    public static string TenantHubUrl => $"/{Be}/shop/tenant_hub/tenant_hub";
}
