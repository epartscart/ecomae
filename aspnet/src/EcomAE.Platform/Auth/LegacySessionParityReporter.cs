namespace EcomAE.Platform.Auth;

public sealed class LegacySessionParityReporter : ILegacySessionParityReporter
{
    public LegacySessionParityReport BuildReport()
    {
        return new LegacySessionParityReport(
            "PHP CP/ERP/BOS session cookies and API authorization headers",
            "ASP.NET Core DbBackedLegacySessionValidator (admin sessions table check) + diagnostic probe",
            "admin-db-check-wired-awaiting-staging",
            ["admin_session/admin_u_id cookies", "sessions.type=1", "X-API-Key header", "Bearer API key header"],
            [
                "Map PHP user roles and permissions into ASP.NET Core authorization claims.",
                "Validate customer storefront sessions against the sessions table.",
                "Replay CP, ERP, and BOS login flows in staging before traffic cutover."
            ]);
    }
}
