namespace EcomAE.Platform.Migration;

public sealed record PythonSidecarCatalogReport(
    string TargetArchitecture,
    string PhpRetirementRule,
    PythonSidecarWorkload[] Workloads,
    string[] IntegrationRules,
    string[] NextImplementationSlices);
