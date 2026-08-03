namespace EcomAE.Platform.Auth;

public sealed class LegacySessionParityReporter : ILegacySessionParityReporter
{
    public LegacySessionParityReport BuildReport()
    {
        return new LegacySessionParityReport(
            "PHP CP/ERP/BOS session cookies and API authorization headers",
            "ASP.NET Core DbBackedLegacySessionValidator (admin+customer sessions + backend group claims + nested module ACL) + diagnostic probe + Batch 3 login bridge hardening",
            "login-bridge-hybrid-batch3-hardened",
            [
                "admin_session/admin_u_id cookies",
                "session/u_id cookies",
                "sessions.type=1",
                "users_groups_bind∩groups.for_backend",
                "modules_access with groups.parent ancestry",
                "X-API-Key header",
                "Bearer API key header",
                "POST /auth/login/admin (opt-in write)",
                "customer token md5(contact+userId+time+secret)",
                "login cookie dual-sample compare script"
            ],
            [
                "On CloudPanel: issue_smoke_credentials.sh with ECOMAE_CONFIRM_SYNC_ADMIN_SESSION=YES binds quoted cookie and syncs session into TenantRegistry; validate via /auth/session/probe (kind=Admin).",
                "Login bridge writes: set EcomAE__SecretSuccession (= PHP secret_succession), redeploy, use /cp/login|/erp/login|/bos/login|/storefront/login or POST /auth/login/admin. Verify with scripts/cloudpanel_verify_secret_succession_configured.sh (never prints the secret).",
                "Dual-sample cookie evidence: scripts/cloudpanel_capture_login_cookie_dual_samples.sh + scripts/compare_login_cookie_dual_samples.py → docs/migration/evidence/login-session-bridge/ (cutoverAllowed=false).",
                "Optional storefront digests: ECOMAE_CUSTOMER_COOKIE_HEADER=session=...; u_id=<digits> (not required for ReadyToRemovePhp).",
                "DECISION: keep /BOS/ PHP-authoritative for native $_SESSION modules. /bos/login mints MySQL admin cookies for digests//bos/app only — not epc_bos_context.",
                "Social/demo/shared-ERP picker, rate-limit, and password hash upgrade remain PHP-authoritative.",
                "Replace legacy cookie bridge with Enterprise BOS identity (OAuth 2.1 / OIDC / JWT) after parity evidence."
            ]);
    }
}
