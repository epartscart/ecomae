namespace EcomAE.Platform.Migration;

public sealed class ControlPanelParityReporter : IControlPanelParityReporter
{
    public ControlPanelParityReport BuildReport()
    {
        return new ControlPanelParityReport(
            "Control Panel / Super CP",
            "ecomae.com/CP and /cp",
            "/cp/parity plus admin-session-gated /cp shell",
            "presentation-shell-scaffolded-awaiting-staging",
            [
                "Canonical CP route aliases are mapped to the ASP.NET Core shell.",
                "CP shell requires admin backend-group session via DbBackedLegacySessionValidator (401 when anonymous).",
                "CP shell negotiates presentation-preserving HTML (PHP bootstrap_admin CSS) while defaulting to JSON for tooling.",
                "Read-only /cp digests include menus, pages, admin-sessions, storages, currencies, and api-clients metadata.",
                "Legacy session probe exposes capabilities and module ACL before CP cutover.",
                "Surface parity report tracks CP login, dashboard shell, tenant selector, and access-denial evidence."
            ],
            [
                "On CloudPanel: ensure_epc_api_clients_table.sh → issue_smoke_credentials.sh → validate_final_gate_env.sh → capture surface digests.",
                "Replay PHP Super CP login and dashboard fixtures against ASP.NET Core HTML/JSON shell output.",
                "Port tenant administration, user management writes, settings, and dashboard widgets.",
                "Validate permission-denied UX and audit logging with production role fixtures.",
                "Promote only location = digests via nginx-surface-digests-shadow-example.conf (never broad /cp)."
            ]);
    }
}
