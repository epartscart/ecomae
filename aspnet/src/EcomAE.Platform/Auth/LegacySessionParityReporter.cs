namespace EcomAE.Platform.Auth;

public sealed class LegacySessionParityReporter : ILegacySessionParityReporter
{
    public LegacySessionParityReport BuildReport()
    {
        return new LegacySessionParityReport(
            "PHP CP/ERP/BOS session cookies and API authorization headers",
            "ASP.NET Core DbBackedLegacySessionValidator (admin+customer sessions + backend group claims + module ACL probe) + diagnostic probe",
            "module-acl-probe-wired-awaiting-staging",
            ["admin_session/admin_u_id cookies", "session/u_id cookies", "sessions.type=1", "users_groups_bind∩groups.for_backend", "modules_access/open modules", "X-API-Key header", "Bearer API key header"],
            [
                "Expand nested PHP group inheritance for modules_access beyond direct group grants.",
                "Replay CP, ERP, BOS, and storefront login flows in staging before traffic cutover."
            ]);
    }
}
