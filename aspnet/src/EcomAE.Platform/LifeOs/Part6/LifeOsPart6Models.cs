namespace EcomAE.Platform.LifeOs.Part6;

public sealed record LifeOsInfraCapability(string Key, string Title, string Status);

public sealed record LifeOsRegion(string Key, string Title, string Role);

public sealed record LifeOsNodePool(
    string Key,
    string Title,
    IReadOnlyList<string> Workloads,
    string Hardware);

public sealed record LifeOsMeshResponsibility(string Key, string Title);

public sealed record LifeOsIacTool(string Key, string Title, string Role);

public sealed record LifeOsCiGate(string Key, string Title, string Threshold);

public sealed record LifeOsDeployStrategy(string Key, string Title, string Notes);

public sealed record LifeOsScaleMetric(string Key, string Title);

public sealed record LifeOsGpuCategory(string Key, string Title);

public sealed record LifeOsModelServingCapability(string Key, string Title);

public sealed record LifeOsStoragePrefix(string Path, string Purpose);

public sealed record LifeOsBackupSchedule(string Cadence, string Kind);

public sealed record LifeOsDrScenario(string Key, string Title);

public sealed record LifeOsPerfTarget(string Component, string Target);

public sealed record LifeOsSreObjective(string Key, string Title, string Target);

public sealed record LifeOsReadinessItem(string Category, string Item, string Status);
