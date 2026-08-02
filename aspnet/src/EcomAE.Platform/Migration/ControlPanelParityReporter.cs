namespace EcomAE.Platform.Migration;

public sealed class ControlPanelParityReporter : IControlPanelParityReporter
{
    public ControlPanelParityReport BuildReport()
    {
        return new ControlPanelParityReport(
            "Control Panel / Super CP",
            "ecomae.com/CP and /cp",
            "/cp/parity plus admin-session-gated /cp shell",
            "menus-pages-sessions-storages-session-gated-awaiting-staging",
            [
                "Canonical CP route aliases are mapped to the ASP.NET Core shell.",
                "CP shell requires admin backend-group session via DbBackedLegacySessionValidator (401 when anonymous).",
                "Read-only /cp dashboard, tenants, users, groups, modules, config-items, menus, pages, admin-sessions, and storages digests are wired.",
                "Legacy session probe exposes capabilities and module ACL before CP cutover.",
                "Surface parity report tracks CP login, dashboard shell, tenant selector, and access-denial evidence."
            ],
            [
                "Replay PHP Super CP login and dashboard fixtures against ASP.NET Core shell output.",
                "Port tenant administration, user management writes, settings, and dashboard widgets.",
                "Validate permission-denied UX and audit logging with production role fixtures."
            ]);
    }
}
