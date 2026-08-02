using EcomAE.Platform.Modules;

namespace EcomAE.Platform.Migration;

public sealed class MigrationParityReporter : IMigrationParityReporter
{
    private readonly IEnumerable<ISurfaceModule> _surfaceModules;

    public MigrationParityReporter(IEnumerable<ISurfaceModule> surfaceModules)
    {
        _surfaceModules = surfaceModules;
    }

    public MigrationParityReport BuildReport()
    {
        return new MigrationParityReport(
            "zero PHP files and no PHP runtime after parity cutover",
            "kept during migration only",
            _surfaceModules.Select(module => module.Descriptor).ToArray(),
            [
                "CloudPanel ensure→issue→validate→capture staging-smoke (never invent keys/cookies).",
                "Dual-sample compare_*_parity.py for catalog/price/surface digests before each location= shadow.",
                "Promote exact-route nginx shadows one path at a time; keep broad /api /cp /erp /bos /storefront blocked.",
                "Port remaining interactive UX (CP login forms, ERP voucher posting, storefront cart/checkout HTML).",
                "Worker dry-run layer is complete; live schedule cutover still requires job parity + PHP cron removal approval.",
                "Release-owner APPROVED_TO_REMOVE_PHP_FALLBACK before ReadyToRemovePhp / PHP-FPM decommission."
            ]);
    }
}
