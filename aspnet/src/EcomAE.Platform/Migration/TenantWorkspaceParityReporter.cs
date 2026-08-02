namespace EcomAE.Platform.Migration;

public sealed class TenantWorkspaceParityReporter : ITenantWorkspaceParityReporter
{
    public TenantWorkspaceParityReport BuildReport()
    {
        return new TenantWorkspaceParityReport(
            "Tenant CP and tenant ERP workspaces",
            "tenant.com/CP, tenant.com/ERP, and ERP-only tenant finance routes",
            "/tenant/workspace/parity plus /cp and /erp shells under tenant context",
            "tenant-routing-parity-visible",
            [
                "Route tenant resolver differentiates platform, live-tenant, and ERP-only tenant modes.",
                "CP and ERP shell catalogs include tenant mode in diagnostic payloads.",
                "Surface parity report tracks tenant CP and tenant ERP separately from Super CP and Platform ERP."
            ],
            [
                "On CloudPanel: ensure→issue→validate→capture surface digests under tenant hosts after platform smoke.",
                "Replay tenant admin menus, tenant user scopes, settings, orders, pricing, and finance fixtures.",
                "Validate ERP-only tenant navigation, access denial, and fallback behavior.",
                "Bind tenant registry to production MySQL source instead of static seed configuration.",
                "Keep AdminAspNetEnabled=false / RequirePhpFallback=true until exact-route shadows have dual-sample match."
            ]);
    }
}
