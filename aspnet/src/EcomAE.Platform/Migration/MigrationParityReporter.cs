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
                "Replace placeholder CP login and tenant hub",
                "Replace ERP shell and finance dashboard",
                "Replace BOS command center",
                "Port catalog/price APIs",
                "Port background jobs and remove PHP cron scripts"
            ]);
    }
}
