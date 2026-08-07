using EcomAE.Platform.LifeOs;
using EcomAE.Platform.LifeOs.Part6;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LifeOsPart6CloudOpsTests
{
    private static ServiceProvider Build()
    {
        var sc = new ServiceCollection();
        sc.AddLifeOsPart2Scaffold();
        return sc.BuildServiceProvider();
    }

    [Fact]
    public void InfrastructureCapabilitiesCoverHaAndMultiRegion()
    {
        using var sp = Build();
        var ops = sp.GetRequiredService<ILifeOsCloudOperations>();
        Assert.True(ops.InfrastructureCapabilities.Count >= 10);
        Assert.Contains(ops.InfrastructureCapabilities, c => c.Key == "ha");
        Assert.Contains(ops.InfrastructureCapabilities, c => c.Key == "multi-region");
        Assert.Equal(3, ops.Regions.Count);
    }

    [Fact]
    public void KubernetesNodePoolsIncludeGpuAiAndVoice()
    {
        using var sp = Build();
        var ops = sp.GetRequiredService<ILifeOsCloudOperations>();
        Assert.Contains(ops.NodePools, p => p.Key == "ai");
        Assert.Contains(ops.NodePools, p => p.Key == "vision");
        Assert.Contains(ops.NodePools, p => p.Key == "voice");
        Assert.Contains(ops.MeshResponsibilities, m => m.Key == "mtls");
    }

    [Fact]
    public void CiCdQualityGatesRequireCoverageAndSecurity()
    {
        using var sp = Build();
        var ops = sp.GetRequiredService<ILifeOsCloudOperations>();
        Assert.Contains(ops.QualityGates, g => g.Key == "coverage" && g.Threshold.Contains("85"));
        Assert.Contains(ops.QualityGates, g => g.Key == "security");
        Assert.Contains(ops.DeployStrategies, d => d.Key == "canary");
        Assert.Contains(ops.ContainerImageRequirements, r => r == "Signed");
    }

    [Fact]
    public void BackupDrHasRpoRtoAndGpuCategories()
    {
        using var sp = Build();
        var ops = sp.GetRequiredService<ILifeOsCloudOperations>();
        var json = System.Text.Json.JsonSerializer.Serialize(ops.BackupAndDrDigest());
        Assert.Contains("15 minutes", json);
        Assert.Contains("1 hour", json);
        Assert.Contains(ops.GpuCategories, g => g.Key == "large-llm");
        Assert.Contains(ops.ModelServingCapabilities, c => c.Key == "hybrid");
    }

    [Fact]
    public void SreAndReadinessCoverAvailabilityAndChecklist()
    {
        using var sp = Build();
        var ops = sp.GetRequiredService<ILifeOsCloudOperations>();
        Assert.Contains(ops.SreObjectives, o => o.Key == "availability" && o.Target.Contains("99.99"));
        Assert.True(ops.ProductionReadinessChecklist.Count >= 16);
        Assert.Contains(ops.PerformanceTargets, t => t.Component == "API Gateway" && t.Target.Contains("50"));
    }

    [Fact]
    public void Part6DigestCoversChapters61To81()
    {
        using var sp = Build();
        var json = System.Text.Json.JsonSerializer.Serialize(
            sp.GetRequiredService<ILifeOsCloudOperations>().FullPart6Digest());
        Assert.Contains("Kubernetes Platform", json);
        Assert.Contains("Service Mesh", json);
        Assert.Contains("Site Reliability Engineering", json);
        Assert.Contains("Production Readiness Checklist", json);
        Assert.Contains("FORCE_LIVE", json);
    }
}
