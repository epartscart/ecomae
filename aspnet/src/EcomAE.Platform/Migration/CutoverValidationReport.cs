namespace EcomAE.Platform.Migration;

public sealed record CutoverValidationReport(
    string Status,
    IReadOnlyCollection<string> RequiredSignals,
    IReadOnlyCollection<string> RollbackControls,
    IReadOnlyCollection<string> ApprovalGates);
