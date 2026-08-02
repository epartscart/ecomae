namespace EcomAE.Platform.Migration;

public sealed class BosParityReporter : IBosParityReporter
{
    public BosParityReport BuildReport()
    {
        return new BosParityReport(
            "Super BOS / BOC",
            "ecomae.com/BOS, /bos, and cp/content/control/portal/epc_boc_*",
            "/bos/parity plus admin-session-gated /bos shell",
            "audit-log-fleet-readiness-session-gated-awaiting-staging",
            [
                "Canonical BOS route aliases are mapped to the ASP.NET Core shell.",
                "BOS shell requires admin session via DbBackedLegacySessionValidator (401 when anonymous).",
                "Read-only /bos fleet-summary, fleet-health, tenants, fleet-readiness, and audit-log digests are wired.",
                "Fleet readiness is platform-DB-only scoring (no per-tenant PDO connects).",
                "Apache BOS rewrites are case-insensitive for operator-entered URLs.",
                "Surface parity report tracks privileged operations, tenant fleet health, audit trails, and rollback safety."
            ],
            [
                "Replay PHP BOS command-center and tenant fleet health fixtures.",
                "Port privileged operations, emergency rollback controls, and audit evidence.",
                "Validate super-admin access denial and approval workflows before cutover."
            ]);
    }
}
