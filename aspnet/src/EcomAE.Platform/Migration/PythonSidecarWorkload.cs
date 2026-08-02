namespace EcomAE.Platform.Migration;

public sealed record PythonSidecarWorkload(
    string Key,
    string DisplayName,
    string RuntimeOwner,
    string CurrentStatus,
    string AspNetCoreContract,
    string PythonAdvantage,
    string[] RequiredEvidence);
