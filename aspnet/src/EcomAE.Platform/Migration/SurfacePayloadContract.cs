namespace EcomAE.Platform.Migration;

public sealed record SurfacePayloadContract(
    string Surface,
    string AspNetRoute,
    string LegacyPhpAuthority,
    string AuthRequired,
    IReadOnlyList<string> RequiredEnvelopeFields,
    IReadOnlyList<string> RequiredSummaryOrItemFields,
    IReadOnlyList<string> FunctionsCovered,
    string PresentationChrome,
    string ParityStatus);
