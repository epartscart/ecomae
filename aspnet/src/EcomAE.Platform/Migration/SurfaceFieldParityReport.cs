namespace EcomAE.Platform.Migration;

public sealed record SurfaceFieldParityReport(
    string Status,
    bool CutoverAllowed,
    int ContractCount,
    int PresentationAssetsChecked,
    int PresentationAssetsOk,
    IReadOnlyCollection<SurfacePayloadContract> Contracts,
    IReadOnlyCollection<SurfaceFunctionParityItem> Functions,
    IReadOnlyCollection<string> Guarantees,
    IReadOnlyCollection<string> RemainingGaps,
    IReadOnlyCollection<string> NextActions,
    bool ReadyForPhpRemoval = false);

public sealed record SurfaceFunctionParityItem(
    string Surface,
    string PhpFunctionOrEntry,
    string AspNetRouteOrCapability,
    string Status,
    string Notes);
