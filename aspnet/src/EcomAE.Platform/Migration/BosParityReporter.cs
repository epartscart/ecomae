namespace EcomAE.Platform.Migration;

public sealed class BosParityReporter : IBosParityReporter
{
    public BosParityReport BuildReport()
    {
        return new BosParityReport(
            "Super BOS / BOC",
            "ecomae.com/BOS, /bos, and cp/content/control/portal/epc_boc_*",
            "/bos/parity plus /bos shell",
            "operations-shell-parity-visible",
            [
                "Canonical BOS route aliases are mapped to the ASP.NET shell.",
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
