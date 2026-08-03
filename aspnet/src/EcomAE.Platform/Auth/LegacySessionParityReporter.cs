namespace EcomAE.Platform.Auth;

public sealed class LegacySessionParityReporter : ILegacySessionParityReporter
{
    public LegacySessionParityReport BuildReport()
    {
        return new LegacySessionParityReport(
            "PHP CP/ERP/BOS session cookies and API authorization headers",
            "ASP.NET Core DbBackedLegacySessionValidator (admin+customer sessions + backend group claims + nested module ACL) + diagnostic probe",
            "nested-module-acl-wired-awaiting-staging",
            ["admin_session/admin_u_id cookies", "session/u_id cookies", "sessions.type=1", "users_groups_bind∩groups.for_backend", "modules_access with groups.parent ancestry", "X-API-Key header", "Bearer API key header"],
            [
                "On CloudPanel: issue_smoke_credentials.sh binds quoted admin_session+admin_u_id cookie; validate via /auth/session/probe (kind=Admin).",
                "Optional storefront digests: ECOMAE_CUSTOMER_COOKIE_HEADER=session=...; u_id=<digits> (not required for ReadyToRemovePhp).",
                "Replay CP, ERP, BOS, and storefront login flows in staging before traffic cutover.",
                "Replace legacy cookie bridge with Enterprise BOS identity (OAuth 2.1 / OIDC / JWT) after parity evidence."
            ]);
    }
}
