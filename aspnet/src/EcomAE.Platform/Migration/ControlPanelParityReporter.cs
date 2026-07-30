namespace EcomAE.Platform.Migration;

public sealed class ControlPanelParityReporter : IControlPanelParityReporter
{
    public ControlPanelParityReport BuildReport()
    {
        return new ControlPanelParityReport(
            "Control Panel / Super CP",
            "ecomae.com/CP and /cp",
            "/cp/parity plus /cp shell",
            "shell-parity-visible",
            [
                "Canonical CP route aliases are mapped to the ASP.NET shell.",
                "Legacy session probe and parity endpoints are available before CP cutover.",
                "Surface parity report tracks CP login, dashboard shell, tenant selector, and access-denial evidence."
            ],
            [
                "Replay PHP Super CP login and dashboard fixtures against ASP.NET shell output.",
                "Port tenant administration, user management, settings, and dashboard widgets.",
                "Validate permission-denied UX and audit logging with production role fixtures."
            ]);
    }
}
