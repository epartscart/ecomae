namespace EcomAE.Platform.Migration;

public sealed record MigrationCutoverStep(
    int Order,
    string RoutePattern,
    string CurrentRuntime,
    string TargetRuntime,
    string RequiredGate,
    bool EnabledByDefault);
