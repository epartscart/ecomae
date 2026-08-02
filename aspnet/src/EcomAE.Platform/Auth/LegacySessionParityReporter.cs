namespace EcomAE.Platform.Auth;

public sealed class LegacySessionParityReporter : ILegacySessionParityReporter
{
    public LegacySessionParityReport BuildReport()
    {
        return new LegacySessionParityReport(
            "PHP CP/ERP/BOS session cookies and API authorization headers",
            "ASP.NET Core DbBackedLegacySessionValidator (admin+customer sessions + backend group claims) + diagnostic probe",
            "session-db-claims-wired-awaiting-staging",
            ["admin_session/admin_u_id cookies", "session/u_id cookies", "sessions.type=1", "users_groups_bind∩groups.for_backend", "X-API-Key header", "Bearer API key header"],
            [
                "Map fine-grained PHP roles into ASP.NET Core authorization claims beyond backend-group access.",
                "Replay CP, ERP, BOS, and storefront login flows in staging before traffic cutover."
            ]);
    }
}
