namespace EcomAE.Platform.Auth;

public sealed class LegacySessionParityReporter : ILegacySessionParityReporter
{
    public LegacySessionParityReport BuildReport()
    {
        return new LegacySessionParityReport(
            "PHP CP/ERP/BOS session cookies and API authorization headers",
            "ASP.NET Core DbBackedLegacySessionValidator (admin+customer sessions table checks) + diagnostic probe",
            "session-db-checks-wired-awaiting-staging",
            ["admin_session/admin_u_id cookies", "session/u_id cookies", "sessions.type=1", "X-API-Key header", "Bearer API key header"],
            [
                "Map PHP user roles and permissions into ASP.NET Core authorization claims.",
                "Replay CP, ERP, BOS, and storefront login flows in staging before traffic cutover."
            ]);
    }
}
