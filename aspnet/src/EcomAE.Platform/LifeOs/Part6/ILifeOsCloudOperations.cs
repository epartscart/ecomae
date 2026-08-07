namespace EcomAE.Platform.LifeOs.Part6;

/// <summary>
/// Part 6 — Cloud Infrastructure, DevOps &amp; Production Operations (Ch.61–81).
/// Planet-scale ops scaffold registry (not a live multi-region mesh).
/// </summary>
public interface ILifeOsCloudOperations
{
    IReadOnlyList<LifeOsInfraCapability> InfrastructureCapabilities { get; }

    IReadOnlyList<LifeOsRegion> Regions { get; }

    IReadOnlyList<string> ClusterComponents { get; }

    IReadOnlyList<LifeOsNodePool> NodePools { get; }

    IReadOnlyList<LifeOsMeshResponsibility> MeshResponsibilities { get; }

    IReadOnlyList<LifeOsIacTool> IacTools { get; }

    IReadOnlyList<string> CiPipelineStages { get; }

    IReadOnlyList<LifeOsCiGate> QualityGates { get; }

    IReadOnlyList<string> CdPipelineStages { get; }

    IReadOnlyList<LifeOsDeployStrategy> DeployStrategies { get; }

    IReadOnlyList<string> ContainerImageRequirements { get; }

    IReadOnlyList<LifeOsScaleMetric> AutoscalerMetrics { get; }

    IReadOnlyList<LifeOsGpuCategory> GpuCategories { get; }

    IReadOnlyList<LifeOsModelServingCapability> ModelServingCapabilities { get; }

    IReadOnlyList<LifeOsStoragePrefix> ObjectStorageLayout { get; }

    IReadOnlyList<LifeOsBackupSchedule> BackupSchedule { get; }

    IReadOnlyList<LifeOsDrScenario> DisasterScenarios { get; }

    IReadOnlyList<string> ObservabilityStack { get; }

    IReadOnlyList<string> MonitoredMetrics { get; }

    IReadOnlyList<string> LogCategories { get; }

    IReadOnlyList<string> ManagedSecrets { get; }

    IReadOnlyList<string> EdgeWorkloads { get; }

    IReadOnlyList<LifeOsPerfTarget> PerformanceTargets { get; }

    IReadOnlyList<LifeOsSreObjective> SreObjectives { get; }

    IReadOnlyList<LifeOsReadinessItem> ProductionReadinessChecklist { get; }

    object GlobalArchitectureDigest();

    object KubernetesDigest();

    object CiCdDigest();

    object GpuAndModelServingDigest();

    object BackupAndDrDigest();

    object ObservabilityDigest();

    object EdgeAndPerformanceDigest();

    object SreDigest();

    object FullPart6Digest();
}
