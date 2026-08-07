using EcomAE.Platform.LifeOs;
using EcomAE.Platform.LifeOs.Part5;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LifeOsPart5PlatformTests
{
    private static ServiceProvider Build()
    {
        var sc = new ServiceCollection();
        sc.AddLifeOsPart2Scaffold();
        return sc.BuildServiceProvider();
    }

    [Fact]
    public void MicroservicesCatalogHasCorePlatformServices()
    {
        using var sp = Build();
        var platform = sp.GetRequiredService<ILifeOsPlatformEngineering>();
        Assert.True(platform.Microservices.Count >= 20);
        Assert.Contains(platform.Microservices, s => s.Key == "memory");
        Assert.Contains(platform.Microservices, s => s.Key == "ai-gateway");
        Assert.Contains(platform.Microservices, s => s.Key == "workflow");
    }

    [Fact]
    public void ApiEnvelopeMatchesSpecShape()
    {
        using var sp = Build();
        var platform = sp.GetRequiredService<ILifeOsPlatformEngineering>();
        var ok = platform.Ok(new { id = 1 }, new { page = 1 });
        Assert.True(ok.Success);
        Assert.NotNull(ok.Data);
        var fail = platform.Fail("TASK_NOT_FOUND", "Task not found");
        Assert.False(fail.Success);
        Assert.Equal("TASK_NOT_FOUND", fail.Error!.Code);
    }

    [Fact]
    public void EventTopicsAndDataStoresCoverPolyglotPersistence()
    {
        using var sp = Build();
        var platform = sp.GetRequiredService<ILifeOsPlatformEngineering>();
        Assert.Contains(platform.EventTopics, t => t.Topic == "planner.events");
        Assert.Contains(platform.DataStores, d => d.Key == "postgres");
        Assert.Contains(platform.DataStores, d => d.Key == "pgvector");
        Assert.Contains(platform.DataStores, d => d.Key == "redis");
        Assert.Equal(10, platform.MemoryLayers.Count);
    }

    [Fact]
    public void AgentSdkAndAiGatewayArePopulated()
    {
        using var sp = Build();
        var platform = sp.GetRequiredService<ILifeOsPlatformEngineering>();
        Assert.Contains(platform.AgentSdkContract, c => c.Field == "Safety Validation");
        Assert.Contains(platform.AiGatewayRoutes, r => r.Task == "Coding");
        Assert.Contains(platform.SamplePlugins, p => p.Name == "Sales Agent");
    }

    [Fact]
    public void KnowledgeGraphSampleHasWorksOnEdge()
    {
        using var sp = Build();
        var json = System.Text.Json.JsonSerializer.Serialize(
            sp.GetRequiredService<ILifeOsPlatformEngineering>().KnowledgeGraphSample());
        Assert.Contains("WorksOn", json);
        Assert.Contains("project:lifeos", json);
    }

    [Fact]
    public void Part5DigestCoversChapters42To60()
    {
        using var sp = Build();
        var json = System.Text.Json.JsonSerializer.Serialize(
            sp.GetRequiredService<ILifeOsPlatformEngineering>().FullPart5Digest());
        Assert.Contains("Microservices Architecture", json);
        Assert.Contains("API Gateway", json);
        Assert.Contains("Knowledge Graph", json);
        Assert.Contains("Engineering Standards", json);
        Assert.Contains("Zero Trust", json);
    }
}
