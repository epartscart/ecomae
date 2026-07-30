using EcomAE.Platform.Modules;

namespace EcomAE.Platform.Migration;

public sealed record MigrationParityReport(
    string FinalState,
    string PhpRuntimeStatus,
    IReadOnlyCollection<SurfaceModuleDescriptor> Surfaces,
    string[] NextMilestones);
