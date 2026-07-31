namespace EcomAE.Platform.Migration;

public sealed record MigrationCutoverPlan(
    string Strategy,
    IReadOnlyCollection<MigrationCutoverStep> Steps,
    string[] RollbackActions);
