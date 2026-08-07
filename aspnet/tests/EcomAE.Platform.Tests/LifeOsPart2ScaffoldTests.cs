using EcomAE.Platform.LifeOs;
using EcomAE.Platform.LifeOs.Engines;
using EcomAE.Platform.LifeOs.EventBus;
using EcomAE.Platform.LifeOs.Models;
using EcomAE.Platform.LifeOs.Orchestrator;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class LifeOsPart2ScaffoldTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLifeOsPart2Scaffold();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void DiRegistersOrchestratorAndEngines()
    {
        using var sp = BuildProvider();
        Assert.NotNull(sp.GetRequiredService<ILifeOsOrchestrator>());
        Assert.NotNull(sp.GetRequiredService<ILifeOsEventBus>());
        Assert.NotNull(sp.GetRequiredService<ILifeOsContextEngine>());
        Assert.NotNull(sp.GetRequiredService<ILifeOsMemorySystem>());
        Assert.NotNull(sp.GetRequiredService<ILifeOsAgentFramework>());
        Assert.NotNull(sp.GetRequiredService<ILifeOsPlanningEngine>());
    }

    [Fact]
    public async Task EventBusPublishesAndRetainsRecent()
    {
        var bus = new InMemoryLifeOsEventBus();
        var evt = LifeOsEventFactory.SampleVoice();
        await bus.PublishAsync(evt);
        Assert.Contains(bus.Recent(), e => e.EventId == evt.EventId);
        Assert.Equal(LifeOsEventType.VoiceEvent, evt.Type);
    }

    [Fact]
    public void ContextEngineScoresSources()
    {
        var engine = new LifeOsContextEngine();
        var ctx = engine.Build(LifeOsEventFactory.SampleVoice());
        Assert.True(ctx.Sources.Count >= 5);
        Assert.InRange(ctx.AggregateConfidence, 0.3, 1.0);
        Assert.Contains("Voice", engine.KnownSourceNames);
    }

    [Fact]
    public void MemoryLayersSeedAndSnapshot()
    {
        var memory = new LifeOsMemorySystem();
        memory.SeedDemoProject();
        var snap = memory.Snapshot();
        Assert.True(snap.CountsByLayer["Strategic"] >= 1);
        Assert.True(snap.CountsByLayer["Project"] >= 1);
        Assert.NotEmpty(snap.Recent);
    }

    [Fact]
    public void AgentCatalogHasCoreSpecialists()
    {
        var agents = new LifeOsAgentFramework();
        Assert.True(agents.Catalog.Count >= 25);
        Assert.Contains(agents.Catalog, a => a.Key == "calendar");
        Assert.Contains(agents.Catalog, a => a.Key == "memory");
        var selected = agents.SelectAgents("Schedule tomorrow's meeting.",
            new LifeOsContextEngine().Build(LifeOsEventFactory.SampleVoice()));
        Assert.Contains("calendar", selected);
        Assert.Contains("security", selected);
    }

    [Fact]
    public void PlanningEngineDecomposesLifeOsMvp()
    {
        var plan = new LifeOsPlanningEngine().SampleLifeOsMvp();
        Assert.Equal("Launch LifeOS MVP", plan.Goal);
        Assert.Equal(10, plan.Tasks.Count);
        Assert.Contains(plan.Tasks, t => t.Title == "Implement Memory");
        Assert.Contains(plan.Tasks, t => t.DependsOn.Contains("t9") || t.Title == "Deployment");
    }

    [Fact]
    public async Task OrchestratorRunsFullPipeline()
    {
        using var sp = BuildProvider();
        var orch = sp.GetRequiredService<ILifeOsOrchestrator>();
        var result = await orch.ProcessAsync(LifeOsEventFactory.SampleVoice());

        Assert.StartsWith("TR-", result.TraceId);
        Assert.Equal("Schedule tomorrow's meeting.", result.Intent);
        Assert.NotEmpty(result.SelectedAgents);
        Assert.NotNull(result.Plan);
        Assert.Contains("EventNormalization", result.Pipeline);
        Assert.Contains("LearningFeedback", result.Pipeline);
        Assert.True(result.PermissionOk);
        Assert.Contains("orchestrator", result.AggregatedResponse, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArchitectureDigestIncludesPart2Chapters()
    {
        using var sp = BuildProvider();
        var digest = sp.GetRequiredService<ILifeOsOrchestrator>().ArchitectureDigest();
        var json = System.Text.Json.JsonSerializer.Serialize(digest);
        Assert.Contains("Orchestrator", json);
        Assert.Contains("Event Bus", json);
        Assert.Contains("Memory System", json);
        Assert.Contains("scaffold", json);
    }
}
